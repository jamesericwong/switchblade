using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests
{
    [TestFixture]
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
        }

        private TestableWindowProvider _provider;
        private Mock<IPluginContext> _mockContext;
        private Mock<ILogger> _mockLogger;

        [SetUp]
        public void Setup()
        {
            _provider = new TestableWindowProvider();
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _provider.Initialize(_mockContext.Object);
        }

        [Test]
        public void GetWindows_ShouldCacheCompleteResults()
        {
            // Arrange
            var itemM = new WindowItem { Hwnd = (IntPtr)100, Title = "Main", IsFallback = true };
            var itemT1 = new WindowItem { Hwnd = (IntPtr)100, Title = "Tab 1", IsFallback = false };
            
            _provider.NextScanResults = new List<WindowItem> { itemT1 };

            // Act
            var results = _provider.GetWindows().ToList();

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("Tab 1"));
        }

        [Test]
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
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("Detailed Tab"), "Should return LKG item");
            Assert.That(results[0].IsFallback, Is.False);
        }

        [Test]
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
            Assert.That(results.Count, Is.EqualTo(0), "Should not return anything if process is gone");
        }
    }
}
