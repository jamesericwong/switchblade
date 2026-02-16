using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests
{
    public class CachingWindowProviderBaseLKGTests
    {
        private class TestableWindowProvider : CachingWindowProviderBase
        {
            public override string PluginName => "TestProvider";
            public override bool HasSettings => false;
            public override void ActivateWindow(WindowItem item) { }

            public List<WindowItem> NextScanResults { get; set; } = new();
            public Dictionary<IntPtr, bool> WindowValidityMock { get; set; } = new();

            protected override IEnumerable<WindowItem> ScanWindowsCore()
            {
                return NextScanResults;
            }

            protected override int GetPid(IntPtr hwnd)
            {
                return (int)hwnd; // Simple PID mapping for test
            }

            protected override (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
            {
                return ("TestProcess", "test.exe");
            }

            protected override bool IsWindowValid(IntPtr hwnd)
            {
                if (WindowValidityMock.TryGetValue(hwnd, out bool isValid))
                {
                    return isValid;
                }
                return false;
            }
        }

        private readonly TestableWindowProvider _provider;
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;

        public CachingWindowProviderBaseLKGTests()
        {
            _provider = new TestableWindowProvider();
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _provider.Initialize(_mockContext.Object);
        }

        [Fact]
        public void GetWindows_ShouldPreserveLKG_WhenScanMissesWindow_ButWindowIsVisible()
        {
            // Arrange
            var hwnd = (IntPtr)1234;
            var goodItem = new WindowItem { Hwnd = hwnd, Title = "Good Item", IsFallback = false };
            
            // 1. Initial success
            _provider.NextScanResults = new List<WindowItem> { goodItem };
            _provider.GetWindows(); // Populate LKG

            // 2. Transient failure (scan returns empty) but window still valid
            _provider.NextScanResults = new List<WindowItem>();
            _provider.WindowValidityMock[hwnd] = true; // Window is still visible!

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.Single(results);
            Assert.Equal("Good Item", results[0].Title);
        }

        [Fact]
        public void GetWindows_ShouldRemoveLKG_WhenWindowIsInvalid()
        {
             // Arrange
            var hwnd = (IntPtr)1234;
            var goodItem = new WindowItem { Hwnd = hwnd, Title = "Good Item", IsFallback = false };
            
            // 1. Initial success
            _provider.NextScanResults = new List<WindowItem> { goodItem };
            _provider.GetWindows(); // Populate LKG

            // 2. Failure: Scan returns empty AND window is invalid (closed)
            _provider.NextScanResults = new List<WindowItem>();
            _provider.WindowValidityMock[hwnd] = false;

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.Empty(results);
        }
    }
}
