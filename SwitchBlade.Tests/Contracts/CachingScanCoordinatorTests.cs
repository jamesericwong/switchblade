using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    public class CachingScanCoordinatorTests
    {
        private static (CachingScanCoordinator Coordinator, Mock<ILogger> Logger) CreateCoordinator()
        {
            var logger = new Mock<ILogger>();
            var coordinator = new CachingScanCoordinator(() => "TestProvider", () => logger.Object);
            return (coordinator, logger);
        }

        [Fact]
        public void Run_FirstCall_ExecutesScanAndCachesResult()
        {
            // Arrange
            var (coordinator, _) = CreateCoordinator();
            int scanCount = 0;
            var items = new List<WindowItem>
            {
                new() { Title = "Window 1" },
                new() { Title = "Window 2" }
            };

            // Act
            var result = coordinator.Run(() =>
            {
                Interlocked.Increment(ref scanCount);
                return items;
            }).ToList();

            // Assert
            Assert.Equal(1, scanCount);
            Assert.Equal(2, result.Count);
            Assert.Equal(2, coordinator.CachedResults.Count);
            Assert.False(coordinator.IsRunning);
        }

        [Fact]
        public void Run_SecondCallAfterFirstCompletes_ExecutesScanAgain()
        {
            // Arrange
            var (coordinator, _) = CreateCoordinator();
            int scanCount = 0;

            // Act - each call runs the scan since the previous one completed
            coordinator.Run(() => { Interlocked.Increment(ref scanCount); return new List<WindowItem> { new() { Title = "A" } }; });
            coordinator.Run(() => { Interlocked.Increment(ref scanCount); return new List<WindowItem> { new() { Title = "B" } }; });

            // Assert
            Assert.Equal(2, scanCount);
        }

        [Fact]
        public void IsRunning_InitiallyFalse()
        {
            var (coordinator, _) = CreateCoordinator();

            Assert.False(coordinator.IsRunning);
        }

        [Fact]
        public void CachedResults_InitiallyEmpty()
        {
            var (coordinator, _) = CreateCoordinator();

            Assert.Empty(coordinator.CachedResults);
        }

        [Fact]
        public async Task Run_ConcurrentCalls_ReturnsCachedWhileScanInProgress()
        {
            // Arrange - coordinate the test flow deterministically with manual reset events
            using var scanStartedHandle = new ManualResetEventSlim(false);
            using var continueScanHandle = new ManualResetEventSlim(false);
            int scanCount = 0;

            var (coordinator, _) = CreateCoordinator();

            // Act - start first scan in background; it blocks until released
            var firstTask = Task.Run(() => coordinator.Run(() =>
            {
                Interlocked.Increment(ref scanCount);
                scanStartedHandle.Set();
                continueScanHandle.Wait(TimeSpan.FromSeconds(10));
                return new List<WindowItem> { new() { Title = "Slow Result" } };
            }).ToList());

            Assert.True(scanStartedHandle.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for scan to start");

            // Second call while first is still running should return cached (empty initially) and NOT run the scan
            var secondResult = coordinator.Run(() => throw new InvalidOperationException("Second scan must not execute")).ToList();

            // Let the first scan finish
            continueScanHandle.Set();
            var firstResult = await firstTask;

            // Assert
            Assert.Equal(1, scanCount); // Only one actual scan
            Assert.Single(firstResult); // First call got the real results
            Assert.Empty(secondResult); // Second call got cached (empty initially)
        }

        [Fact]
        public void Run_ScanThrows_ReturnsPreviousCacheAndAllowsRetry()
        {
            // Arrange - first successful scan populates the cache
            var (coordinator, logger) = CreateCoordinator();
            var goodItems = new List<WindowItem> { new() { Title = "Good" } };

            coordinator.Run(() => goodItems);

            // Act - failing scan should fall back to the previous cache
            var onError = coordinator.Run(() => throw new InvalidOperationException("boom")).ToList();

            // Assert - cached results returned, error logged, and the next call retries (flag reset)
            logger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Error during scan")), It.IsAny<Exception>()), Times.Once);
            Assert.Single(onError);
            Assert.Equal("Good", onError[0].Title);

            int retryCount = 0;
            var retryResult = coordinator.Run(() =>
            {
                Interlocked.Increment(ref retryCount);
                return new List<WindowItem> { new() { Title = "Retry" } };
            }).ToList();

            Assert.Equal(1, retryCount);
            Assert.Single(retryResult);
            Assert.Equal("Retry", retryResult[0].Title);
        }

        [Fact]
        public void Run_ScanThrowsOnFirstCall_ReturnsEmptyCache()
        {
            // Arrange
            var (coordinator, _) = CreateCoordinator();

            // Act & Assert - no exception escapes; empty cache is returned
            var result = coordinator.Run(() => throw new InvalidOperationException("boom")).ToList();

            Assert.Empty(result);
            Assert.False(coordinator.IsRunning);
        }

        [Fact]
        public void ClearCache_EmptiesCachedResults()
        {
            // Arrange - populate the cache with a scan
            var (coordinator, _) = CreateCoordinator();
            coordinator.Run(() => new List<WindowItem> { new() { Title = "Cached" } });
            Assert.Single(coordinator.CachedResults);

            // Act
            coordinator.ClearCache();

            // Assert
            Assert.Empty(coordinator.CachedResults);
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CachingScanCoordinator(null!, () => null));
        }

        [Fact]
        public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CachingScanCoordinator(() => "TestProvider", null!));
        }

        [Fact]
        public void Run_NullScan_ThrowsArgumentNullException()
        {
            var (coordinator, _) = CreateCoordinator();

            Assert.Throws<ArgumentNullException>(() => coordinator.Run(null!));
        }

        [Fact]
        public void Run_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var (coordinator, _) = CreateCoordinator();
            coordinator.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => coordinator.Run(() => new List<WindowItem>()));
        }

        [Fact]
        public void CachedResults_AfterDispose_ThrowsObjectDisposedException()
        {
            var (coordinator, _) = CreateCoordinator();
            coordinator.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = coordinator.CachedResults);
        }
    }
}
