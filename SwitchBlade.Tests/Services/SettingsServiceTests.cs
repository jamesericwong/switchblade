using System;
using System.Collections.Generic;
using Moq;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class SettingsServiceTests
    {
        private readonly Mock<ISettingsStorage> _mockStorage;
        private readonly Mock<IWindowsStartupManager> _mockStartupManager;

        public SettingsServiceTests()
        {
            _mockStorage = new Mock<ISettingsStorage>();
            _mockStartupManager = new Mock<IWindowsStartupManager>();

            // Default setups to avoid nulls/missing keys
            _mockStorage.Setup(s => s.HasKey(It.IsAny<string>())).Returns(true);
            _mockStorage.Setup(s => s.GetStringList(It.IsAny<string>())).Returns(new List<string>());
        }

        [Fact]
        public void LoadSettings_RetrievesValuesFromStorage()
        {
            // Arrange
            _mockStorage.Setup(s => s.GetValue("CurrentTheme", It.IsAny<string>())).Returns("Dark");
            _mockStorage.Setup(s => s.GetValue("WindowWidth", It.IsAny<double>())).Returns(1024.0);
            _mockStorage.Setup(s => s.GetValue("EnableNumberShortcuts", It.IsAny<bool>())).Returns(false);

            // Act
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            // Assert
            Assert.Equal("Dark", service.Settings.CurrentTheme);
            Assert.Equal(1024.0, service.Settings.WindowWidth);
            Assert.False(service.Settings.EnableNumberShortcuts);
        }

        [Fact]
        public void LoadSettings_RecreatesMissingKeys()
        {
            // Arrange
            _mockStorage.Setup(s => s.HasKey("HotKeyKey")).Returns(false);
            _mockStorage.Setup(s => s.GetValue("HotKeyKey", 0x51u)).Returns(0x51u);

            // Act
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            // Assert
            // Verification: SaveSettings should have been called (which calls Flush and writes to registry)
            _mockStorage.Verify(s => s.SetValue("HotKeyKey", 0x51u), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.Flush(), Times.AtLeastOnce);
        }

        [Fact]
        public void LoadSettings_UsesDefaultWhenStorageReturnsDefault()
        {
            // Arrange
            _mockStorage.Setup(s => s.GetValue("CurrentTheme", "Super Light")).Returns("Super Light");

            // Act
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            // Assert
            Assert.Equal("Super Light", service.Settings.CurrentTheme);
        }

        [Fact]
        public void SaveSettings_PersistsValuesToStorage()
        {
            // Arrange
            _mockStorage.Setup(s => s.HasKey(It.IsAny<string>())).Returns(true);
            _mockStorage.Setup(s => s.GetStringList("ExcludedProcesses")).Returns(new List<string> { "notepad" });
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            service.Settings.CurrentTheme = "CustomTheme";
            service.Settings.WindowWidth = 1200.0;
            service.Settings.EnableFuzzySearch = false;

            // Act
            service.SaveSettings();

            // Assert
            _mockStorage.Verify(s => s.SetValue("CurrentTheme", "CustomTheme"), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.SetValue("WindowWidth", 1200.0), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.SetValue("EnableFuzzySearch", false), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.Flush(), Times.AtLeastOnce);
        }

        [Fact]
        public void SaveSettings_TriggersSettingsChangedEvent()
        {
            // Arrange
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);
            bool eventRaised = false;
            service.SettingsChanged += () => eventRaised = true;

            // Act
            service.SaveSettings();

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void LoadSettings_SyncsStartupWithActualState()
        {
            // Arrange
            _mockStorage.Setup(s => s.GetValue("LaunchOnStartup", It.IsAny<bool>())).Returns(true);
            _mockStartupManager.Setup(m => m.IsStartupEnabled()).Returns(false); // Out of sync

            // Act
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            // Assert
            Assert.False(service.Settings.LaunchOnStartup); // Should be synced to actual state
            _mockStorage.Verify(s => s.SetStringList(It.IsAny<string>(), It.IsAny<List<string>>()), Times.AtLeastOnce); // Should trigger a save (dirty)
        }

        [Fact]
        public void LoadSettings_LoadsUiaWorkerTimeoutSeconds()
        {
            // Arrange
            _mockStorage.Setup(s => s.GetValue("UiaWorkerTimeoutSeconds", It.IsAny<int>())).Returns(120);

            // Act
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);

            // Assert
            Assert.Equal(120, service.Settings.UiaWorkerTimeoutSeconds);
        }
    }
}
