using Xunit;
using Moq;
using SwitchBlade.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchBlade.Tests.Contracts
{
    /// <summary>
    /// Test implementation of CachingWindowProviderBase for testing purposes.
    /// </summary>
    public class TestCachingWindowProvider : CachingWindowProviderBase
    {
        public override string PluginName => "TestProvider";
        public override bool HasSettings => false;

        private readonly List<WindowItem> _testResults;
        private readonly int _scanDelayMs;
        public int ScanCallCount { get; private set; } = 0;

        public TestCachingWindowProvider(List<WindowItem>? testResults = null, int scanDelayMs = 0)
        {
            _testResults = testResults ?? new List<WindowItem>();
            _scanDelayMs = scanDelayMs;
        }

        public override void ActivateWindow(WindowItem item) { }

        protected override IEnumerable<WindowItem> ScanWindowsCore()
        {
            ScanCallCount++;

            if (_scanDelayMs > 0)
            {
                Thread.Sleep(_scanDelayMs);
            }

            return _testResults;
        }

        protected override int GetPid(IntPtr hwnd)
        {
            return 1234;
        }

        protected override (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
            return ("TestProcess", "test.exe");
        }
    }

    public class CachingWindowProviderBaseTests
    {
        [Fact]
        public void GetWindows_FirstCall_RunsScanWindowsCore()
        {
            // Arrange
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Test Window 1" },
                new WindowItem { Title = "Test Window 2" }
            };
            var provider = new TestCachingWindowProvider(expectedItems);

            // Act
            var result = provider.GetWindows();

            // Assert
            Assert.Equal(1, provider.ScanCallCount);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetWindows_SecondCallAfterFirstCompletes_RunsScanAgain()
        {
            // Arrange
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Test Window" }
            };
            var provider = new TestCachingWindowProvider(expectedItems);

            // Act
            var result1 = provider.GetWindows();
            var result2 = provider.GetWindows();

            // Assert - each call runs scan since previous scan completed
            Assert.Equal(2, provider.ScanCallCount);
        }

        [Fact]
        public void IsScanRunning_InitiallyFalse()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Assert
            Assert.False(provider.IsScanRunning);
        }

        [Fact]
        public void CachedWindows_InitiallyEmpty()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Assert
            Assert.Empty(provider.CachedWindows);
        }

        [Fact]
        public void CachedWindows_PopulatedAfterScan()
        {
            // Arrange
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Cached Window" }
            };
            var provider = new TestCachingWindowProvider(expectedItems);

            // Act
            provider.GetWindows();

            // Assert
            Assert.Single(provider.CachedWindows);
            Assert.Equal("Cached Window", provider.CachedWindows[0].Title);
        }

        [Fact]
        public async Task GetWindows_ConcurrentCall_ReturnsCachedWhileScanInProgress()
        {
            // Arrange - create a provider with slow scan
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Slow Result" }
            };
            var provider = new TestCachingWindowProvider(expectedItems, scanDelayMs: 200);

            // Act - start first scan in background
            var firstScanTask = Task.Run(() => provider.GetWindows());

            // Wait a bit for the scan to start
            await Task.Delay(50);

            // Second call while first is still running should return cached (empty initially)
            var secondResult = provider.GetWindows().ToList();

            // Wait for first scan to complete
            var firstResult = (await firstScanTask).ToList();

            // Assert
            Assert.Equal(1, provider.ScanCallCount); // Only one actual scan
            Assert.Single(firstResult); // First call got the real results
            Assert.Empty(secondResult); // Second call got cached (empty initially)
        }

        [Fact]
        public void PluginName_ReturnsExpectedValue()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Assert
            Assert.Equal("TestProvider", provider.PluginName);
        }

        [Fact]
        public void HasSettings_ReturnsExpectedValue()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Assert
            Assert.False(provider.HasSettings);
        }

        [Fact]
        public void Initialize_DoesNotThrow()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();
            var mockLogger = new Mock<ILogger>();
            var context = new PluginContext(mockLogger.Object);

            // Act & Assert - should not throw
            provider.Initialize(context);
        }

        [Fact]
        public void ReloadSettings_DoesNotThrow()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Act & Assert - should not throw
            provider.ReloadSettings();
        }

        [Fact]
        public void GetHandledProcesses_ReturnsEmptyByDefault()
        {
            // Arrange
            var provider = new TestCachingWindowProvider();

            // Act
            var result = provider.GetHandledProcesses();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task CachedWindows_CanBeReadConcurrently()
        {
            // Arrange - Tests that ReaderWriterLockSlim allows concurrent reads
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Window 1" },
                new WindowItem { Title = "Window 2" },
                new WindowItem { Title = "Window 3" }
            };
            var provider = new TestCachingWindowProvider(expectedItems);

            // Initial scan to populate cache
            provider.GetWindows();

            // Act - Multiple concurrent reads should not block each other
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                var cached = provider.CachedWindows;
                return cached.Count;
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert - All concurrent reads should succeed with same count
            Assert.All(results, count => Assert.Equal(3, count));
        }

        [Fact]
        public void GetWindows_ConcurrentReads_AllReturnValidResults()
        {
            // Arrange - Tests that multiple GetWindows calls when scan is NOT running all trigger scans
            var expectedItems = new List<WindowItem>
            {
                new WindowItem { Title = "Test Window" }
            };
            var provider = new TestCachingWindowProvider(expectedItems);

            // Act - Multiple rapid GetWindows calls (sequential, not during scan)
            var results = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                var windows = provider.GetWindows().ToList();
                results.Add(windows.Count);
            }

            // Assert - Each call should return valid results
            Assert.All(results, count => Assert.Equal(1, count));
            Assert.Equal(5, provider.ScanCallCount); // Each call runs scan since previous completed
        }
    }
}

