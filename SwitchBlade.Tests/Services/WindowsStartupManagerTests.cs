using System;
using Microsoft.Win32;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class WindowsStartupManagerTests
    {
        private readonly Mock<IRegistryService> _mockRegistry;
        private readonly WindowsStartupManager _manager;
        private const string APP_KEY = @"Software\SwitchBlade";
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string VALUE_NAME = "SwitchBlade";

        public WindowsStartupManagerTests()
        {
            _mockRegistry = new Mock<IRegistryService>();
            _manager = new WindowsStartupManager(_mockRegistry.Object);
        }

        [Fact]
        public void IsStartupEnabled_ReturnsCorrectValue()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(RUN_KEY, VALUE_NAME)).Returns("path");
            Assert.True(_manager.IsStartupEnabled());

            _mockRegistry.Setup(r => r.GetCurrentUserValue(RUN_KEY, VALUE_NAME)).Returns(null!);
            Assert.False(_manager.IsStartupEnabled());
        }

        [Fact]
        public void EnableStartup_SetsRegistryValue()
        {
            _manager.EnableStartup(@"C:\app.exe");
            _mockRegistry.Verify(r => r.SetCurrentUserValue(RUN_KEY, VALUE_NAME, "\"C:\\app.exe\" /minimized", RegistryValueKind.String), Times.Once);
        }

        [Fact]
        public void EnableStartup_NullPath_Throws()
        {
            Assert.Throws<ArgumentException>(() => _manager.EnableStartup(""));
        }

        [Fact]
        public void DisableStartup_DeletesRegistryValue()
        {
            _manager.DisableStartup();
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(RUN_KEY, VALUE_NAME, false), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_WorksForEnabled()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun")).Returns("1");
            
            var result = _manager.CheckAndApplyStartupMarker();
            
            Assert.True(result);
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun", false), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_WorksForDisabled()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun")).Returns("0");
            
            var result = _manager.CheckAndApplyStartupMarker();
            
            Assert.False(result);
        }

        [Fact]
        public void Methods_HandleExceptionsGracefully()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("Fail"));
            _mockRegistry.Setup(r => r.SetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Throws(new Exception("Fail"));
            _mockRegistry.Setup(r => r.DeleteCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Throws(new Exception("Fail"));

            Assert.False(_manager.IsStartupEnabled());
            _manager.EnableStartup("path"); // Should log and continue
            _manager.DisableStartup(); // Should log and continue
            Assert.False(_manager.CheckAndApplyStartupMarker());
        }
    }
}
