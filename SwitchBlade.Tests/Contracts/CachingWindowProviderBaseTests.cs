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

        public override void ShowSettingsDialog(nint ownerHwnd) { }
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

            // Act & Assert - should not throw
            provider.Initialize(new object(), mockLogger.Object);
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
    }
}
