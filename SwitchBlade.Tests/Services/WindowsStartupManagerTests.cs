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
        private readonly Mock<ILogger> _mockLogger;
        private readonly WindowsStartupManager _manager;
        private const string APP_KEY = @"Software\SwitchBlade";
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string VALUE_NAME = "SwitchBlade";

        public WindowsStartupManagerTests()
        {
            _mockRegistry = new Mock<IRegistryService>();
            _mockLogger = new Mock<ILogger>();
            _manager = new WindowsStartupManager(_mockRegistry.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_NullRegistryService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new WindowsStartupManager(null!, _mockLogger.Object));
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
        public void IsStartupEnabled_ReturnsFalse_OnException()
        {
             _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("Fail"));
             Assert.False(_manager.IsStartupEnabled());
             // IsStartupEnabled swallows exception without logging
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
            Assert.Throws<ArgumentException>(() => _manager.EnableStartup(null!));
        }

        [Fact]
        public void EnableStartup_LogsError_OnException()
        {
            _mockRegistry.Setup(r => r.SetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Throws(new Exception("Fail"));
            
            _manager.EnableStartup(@"C:\app.exe");
            
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("enable startup")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void EnableStartup_NoLogger_Exception_BranchCoverage()
        {
            var managerNoLogger = new WindowsStartupManager(_mockRegistry.Object, null);
            _mockRegistry.Setup(r => r.SetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Throws(new Exception("Fail"));
            
            // Should not throw or crash
            managerNoLogger.EnableStartup(@"C:\app.exe");
            
            _mockRegistry.Verify(r => r.SetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_ReturnsFalse_WhenMarkerMissing()
        {
             _mockRegistry.Setup(r => r.GetCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun")).Returns((object?)null);
             Assert.False(_manager.CheckAndApplyStartupMarker());
        }

        [Fact]
        public void DisableStartup_DeletesRegistryValue()
        {
            _manager.DisableStartup();
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(RUN_KEY, VALUE_NAME, false), Times.Once);
        }

        [Fact]
        public void DisableStartup_LogsError_OnException()
        {
            _mockRegistry.Setup(r => r.DeleteCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Throws(new Exception("Fail"));
            
            _manager.DisableStartup();

            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("disable startup")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void DisableStartup_NoLogger_Exception_BranchCoverage()
        {
            var managerNoLogger = new WindowsStartupManager(_mockRegistry.Object, null);
            _mockRegistry.Setup(r => r.DeleteCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Throws(new Exception("Fail"));
            
            // Should not throw or crash
            managerNoLogger.DisableStartup();
            
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
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
        public void CheckAndApplyStartupMarker_ToStringReturnsNull_BranchCoverage()
        {
            // Mock object that returns null for ToString()
            var mockObj = new Mock<object>();
            mockObj.Setup(o => o.ToString()).Returns((string?)null);
            
            _mockRegistry.Setup(r => r.GetCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun")).Returns(mockObj.Object);
            
            var result = _manager.CheckAndApplyStartupMarker();
            
            Assert.False(result); // Should default to "0" via ?? "0"
            _mockRegistry.Verify(r => r.DeleteCurrentUserValue(APP_KEY, "EnableStartupOnFirstRun", false), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_LogsError_OnException()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("Fail"));

            Assert.False(_manager.CheckAndApplyStartupMarker());
            
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("EnableStartupOnFirstRun")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_NoLogger_Exception_BranchCoverage()
        {
            var managerNoLogger = new WindowsStartupManager(_mockRegistry.Object, null);
            _mockRegistry.Setup(r => r.GetCurrentUserValue(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("Fail"));

            Assert.False(managerNoLogger.CheckAndApplyStartupMarker());
        }
    }
}
