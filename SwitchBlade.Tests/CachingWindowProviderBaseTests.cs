using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests
{
    public class CachingWindowProviderBaseTests
    {
        // Concrete implementation for testing abstract class
        private class TestableWindowProvider : CachingWindowProviderBase
        {
            public override string PluginName => "TestProvider";
            public override bool HasSettings => false;
            public override void ActivateWindow(WindowItem item) { }

            // Expose for testing
            public List<WindowItem> NextScanResults { get; set; } = new();

            protected override IEnumerable<WindowItem> ScanWindowsCore()
            {
                return NextScanResults;
            }

            protected override int GetPid(IntPtr hwnd)
            {
                // Return a non-zero PID so that CachingWindowProviderBase doesn't skip it
                return 1234;
            }

            protected override (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
            {
                return ("TestProcess", "test.exe");
            }
        }

        private readonly TestableWindowProvider _provider;
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;

        public CachingWindowProviderBaseTests()
        {
            _provider = new TestableWindowProvider();
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _provider.Initialize(_mockContext.Object);
        }

        [Fact]
        public void GetWindows_ShouldCacheCompleteResults()
        {
            // Arrange
            var itemT1 = new WindowItem { Hwnd = (IntPtr)100, Title = "Tab 1", IsFallback = false };
            
            _provider.NextScanResults = new List<WindowItem> { itemT1 };

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.Single(results);
            Assert.Equal("Tab 1", results[0].Title);
        }

        [Fact]
        public void GetWindows_ShouldRetainLastKnownGood_WhenFallbackOccurs()
        {
            // Arrange
            // 1. Initial successful scan
            var goodItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Detailed Tab", IsFallback = false };
            _provider.NextScanResults = new List<WindowItem> { goodItem };
            
            // Run first scan to populate LKG
            _provider.GetWindows();

            // 2. Transiet failure - only returns fallback
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };
            _provider.NextScanResults = new List<WindowItem> { fallbackItem };

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.Single(results);
            Assert.Equal("Detailed Tab", results[0].Title);
            Assert.False(results[0].IsFallback, "Should return LKG item");
        }

        [Fact]
        public void GetWindows_ShouldClearLKG_WhenProcessDisappears()
        {
            // Arrange
            // 1. Initial successful scan
            var goodItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Detailed Tab", IsFallback = false };
            _provider.NextScanResults = new List<WindowItem> { goodItem };
            _provider.GetWindows();

            // 2. Process disappears (Scan returns empty)
            _provider.NextScanResults = new List<WindowItem>();

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.Empty(results);
        }
        [Fact]
        public void GetWindows_ShouldUpdateLKG_WhenGoodItemsFollowFallback()
        {
            // Arrange — first scan returns only fallback
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };
            _provider.NextScanResults = new List<WindowItem> { fallbackItem };
            _provider.GetWindows();

            // Second scan returns good items
            var goodItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Real Tab", IsFallback = false };
            _provider.NextScanResults = new List<WindowItem> { goodItem };
            var results = _provider.GetWindows().ToList();

            // Assert — good items replace fallback
            Assert.Single(results);
            Assert.Equal("Real Tab", results[0].Title);
            Assert.False(results[0].IsFallback);

            // Third scan returns fallback again — LKG should restore the good item
            var fallbackItem2 = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };
            _provider.NextScanResults = new List<WindowItem> { fallbackItem2 };
            var resultsAfterFallback = _provider.GetWindows().ToList();

            Assert.Single(resultsAfterFallback);
            Assert.Equal("Real Tab", resultsAfterFallback[0].Title);
            Assert.False(resultsAfterFallback[0].IsFallback, "LKG should restore the good item");
        }

        [Fact]
        public void GetWindows_ShouldPreserveLKG_WhenPidMissedButWindowStillValid()
        {
            // Arrange — use a provider that can simulate window validity
            var validatingProvider = new ValidatingWindowProvider();
            var mockContext = new Mock<IPluginContext>();
            var mockLogger = new Mock<ILogger>();
            mockContext.Setup(c => c.Logger).Returns(mockLogger.Object);
            validatingProvider.Initialize(mockContext.Object);

            // First scan returns good items
            var goodItem = new WindowItem { Hwnd = (IntPtr)5678, Title = "Tab 1", IsFallback = false };
            validatingProvider.NextScanResults = new List<WindowItem> { goodItem };
            validatingProvider.GetWindows();

            // Second scan returns nothing (PID completely missed) but window is still valid
            validatingProvider.NextScanResults = new List<WindowItem>();
            validatingProvider.ValidWindows.Add((IntPtr)5678);

            var results = validatingProvider.GetWindows().ToList();

            // Assert — LKG items should be preserved
            Assert.Single(results);
            Assert.Equal("Tab 1", results[0].Title);
        }
    }

    /// <summary>
    /// Test provider that supports configurable window validity for LKG testing.
    /// </summary>
    internal class ValidatingWindowProvider : CachingWindowProviderBase
    {
        public override string PluginName => "ValidatingProvider";
        public override bool HasSettings => false;
        public override void ActivateWindow(WindowItem item) { }

        public List<WindowItem> NextScanResults { get; set; } = new();
        public HashSet<IntPtr> ValidWindows { get; } = new();

        protected override IEnumerable<WindowItem> ScanWindowsCore() => NextScanResults;

        protected override int GetPid(IntPtr hwnd) => 5678;

        protected override (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
            => ("TestProcess", "test.exe");

        protected override bool IsWindowValid(IntPtr hwnd) => ValidWindows.Contains(hwnd);
    }
}
