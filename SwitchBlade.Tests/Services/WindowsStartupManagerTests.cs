using Xunit;
using Moq;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Microsoft.Win32;

namespace SwitchBlade.Tests.Services
{
    public class WindowsStartupManagerTests
    {
        private readonly Mock<IRegistryService> _mockRegistry;
        private readonly WindowsStartupManager _manager;

        public WindowsStartupManagerTests()
        {
            _mockRegistry = new Mock<IRegistryService>();
            _manager = new WindowsStartupManager(_mockRegistry.Object);
        }

        [Fact]
        public void IsStartupEnabled_ReturnsTrue_WhenRegistryValueExists()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), "SwitchBlade")).Returns("somePath");

            // Act
            var result = _manager.IsStartupEnabled();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsStartupEnabled_ReturnsFalse_WhenRegistryValueIsNull()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), "SwitchBlade")).Returns((object?)null);

            // Act
            var result = _manager.IsStartupEnabled();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EnableStartup_NullPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _manager.EnableStartup(null!));
        }

        [Fact]
        public void EnableStartup_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _manager.EnableStartup(""));
        }

        [Fact]
        public void EnableStartup_SetsRegistryValue()
        {
            // Act
            _manager.EnableStartup("C:\\MyApp.exe");

            // Assert
            _mockRegistry.Verify(r => r.SetCurrentUserValue(
                @"Software\Microsoft\Windows\CurrentVersion\Run", 
                "SwitchBlade", 
                "\"C:\\MyApp.exe\" /minimized", 
                RegistryValueKind.String), Times.Once);
        }

        [Fact]
        public void DisableStartup_DeletesRegistryValue()
        {
            // Act
            _manager.DisableStartup();

            // Assert
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(
                @"Software\Microsoft\Windows\CurrentVersion\Run", 
                "SwitchBlade", 
                false), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_NoMarker_ReturnsFalse()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetCurrentUserValue(@"Software\SwitchBlade", "EnableStartupOnFirstRun")).Returns((object?)null);

            // Act
            var result = _manager.CheckAndApplyStartupMarker();

            // Assert
            Assert.False(result);
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_WithMarker1_ReturnsTrueAndDeletes()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetCurrentUserValue(@"Software\SwitchBlade", "EnableStartupOnFirstRun")).Returns("1");

            // Act
            var result = _manager.CheckAndApplyStartupMarker();

            // Assert
            Assert.True(result);
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(@"Software\SwitchBlade", "EnableStartupOnFirstRun", false), Times.Once);
        }
    }
}
