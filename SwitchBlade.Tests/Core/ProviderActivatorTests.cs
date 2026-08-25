using System;
using System.Runtime.InteropServices;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class ProviderActivatorTests
    {
        private readonly Mock<ILogger> _mockLogger = new();

        [Fact]
        public void TryActivate_SourceThrows_ReturnsFalseAndLogs()
        {
            var provider = new Mock<IWindowProvider>();
            provider.Setup(p => p.ActivateWindow(It.IsAny<WindowItem>()))
                    .Throws(new COMException("simulated UIA activation failure"));
            var item = CreateItem(provider.Object);

            bool result = ProviderActivator.TryActivate(item, _mockLogger.Object);

            Assert.False(result);
            _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void TryActivate_SourceSucceeds_ReturnsTrue()
        {
            var provider = new Mock<IWindowProvider>();
            var item = CreateItem(provider.Object);

            bool result = ProviderActivator.TryActivate(item, _mockLogger.Object);

            Assert.True(result);
            provider.Verify(p => p.ActivateWindow(item), Times.Once);
            _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void TryActivate_NoSource_ReturnsFalse()
        {
            var item = new WindowItem { Hwnd = new IntPtr(42), Title = "orphan", Source = null };

            bool result = ProviderActivator.TryActivate(item, _mockLogger.Object);

            Assert.False(result);
            _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        private static WindowItem CreateItem(IWindowProvider provider) => new()
        {
            Hwnd = new IntPtr(42),
            Title = "Test Window",
            Source = provider
        };
    }
}
