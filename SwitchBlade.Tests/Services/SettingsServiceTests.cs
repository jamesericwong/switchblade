using System;
using System.Collections.Generic;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class SettingsServiceTests
    {
        private readonly Mock<ISettingsStorage> _mockStorage;
        private readonly Mock<IWindowsStartupManager> _mockStartupManager;
        private readonly Mock<IProcessFactory> _mockProcessFactory;
        private readonly Mock<IProcess> _mockProcess;

        public SettingsServiceTests()
        {
            _mockStorage = new Mock<ISettingsStorage>();
            _mockStartupManager = new Mock<IWindowsStartupManager>();
            _mockProcessFactory = new Mock<IProcessFactory>();
            _mockProcess = new Mock<IProcess>();

            // Default setups
            _mockStorage.Setup(s => s.HasKey(It.IsAny<string>())).Returns(true);
            _mockStorage.Setup(s => s.GetStringList(It.IsAny<string>())).Returns(new List<string>());
            
            // Process setup
            _mockProcessFactory.Setup(f => f.GetCurrentProcess()).Returns(_mockProcess.Object);
            // Default valid path
            _mockProcess.Setup(p => p.MainModuleFileName).Returns(@"C:\Test\SwitchBlade.exe");
        }

        [Fact]
        public void LoadSettings_RetrievesValuesFromStorage()
        {
            _mockStorage.Setup(s => s.GetValue("CurrentTheme", It.IsAny<string>())).Returns("Dark");
            _mockStorage.Setup(s => s.GetValue("WindowWidth", It.IsAny<double>())).Returns(1024.0);
            _mockStorage.Setup(s => s.GetValue("EnableNumberShortcuts", It.IsAny<bool>())).Returns(false);
            _mockStorage.Setup(s => s.GetValue("UiaWorkerTimeoutSeconds", It.IsAny<int>())).Returns(99);

            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);

            Assert.Equal("Dark", service.Settings.CurrentTheme);
            Assert.Equal(1024.0, service.Settings.WindowWidth);
            Assert.False(service.Settings.EnableNumberShortcuts);
            Assert.Equal(99, service.Settings.UiaWorkerTimeoutSeconds);
        }

        [Fact]
        public void LoadSettings_RecreatesMissingKeys()
        {
            _mockStorage.Setup(s => s.HasKey("HotKeyKey")).Returns(false);
            _mockStorage.Setup(s => s.GetValue("HotKeyKey", 0x51u)).Returns(0x51u);

            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);

            _mockStorage.Verify(s => s.SetValue("HotKeyKey", 0x51u), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.Flush(), Times.AtLeastOnce);
        }

        [Fact]
        public void LoadSettings_UsesDefaultWhenStorageReturnsDefault()
        {
            _mockStorage.Setup(s => s.GetValue("CurrentTheme", "Super Light")).Returns("Super Light");

            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);

            Assert.Equal("Super Light", service.Settings.CurrentTheme);
        }

        [Fact]
        public void LoadSettings_Lists_WhenMissing_SetsDirty()
        {
            _mockStorage.Setup(s => s.HasKey("ExcludedProcesses")).Returns(false);
            
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            
            // Should save empty list
            _mockStorage.Verify(s => s.SetStringList("ExcludedProcesses", It.IsAny<List<string>>()), Times.Once);
            _mockStorage.Verify(s => s.Flush(), Times.Once); // SaveSettings called
        }

        [Fact]
        public void LoadSettings_Lists_WhenPresent_LoadsThem()
        {
            _mockStorage.Setup(s => s.HasKey("ExcludedProcesses")).Returns(true);
            _mockStorage.Setup(s => s.GetStringList("ExcludedProcesses")).Returns(new List<string> { "foo.exe" });

            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            
            Assert.Contains("foo.exe", service.Settings.ExcludedProcesses);
        }

        [Fact]
        public void SaveSettings_PersistsValuesToStorage()
        {
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);

            service.Settings.CurrentTheme = "CustomTheme";
            service.Settings.WindowWidth = 1200.0;
            service.Settings.EnableFuzzySearch = false;

            service.SaveSettings();

            _mockStorage.Verify(s => s.SetValue("CurrentTheme", "CustomTheme"), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.SetValue("WindowWidth", 1200.0), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.SetValue("EnableFuzzySearch", false), Times.AtLeastOnce);
            _mockStorage.Verify(s => s.Flush(), Times.AtLeastOnce);
        }

        [Fact]
        public void SaveSettings_TriggersSettingsChangedEvent()
        {
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            bool eventRaised = false;
            service.SettingsChanged += () => eventRaised = true;

            service.SaveSettings();

            Assert.True(eventRaised);
        }

        [Fact]
        public void LoadSettings_SyncsStartupWithActualState()
        {
            _mockStorage.Setup(s => s.GetValue("LaunchOnStartup", It.IsAny<bool>())).Returns(true);
            _mockStartupManager.Setup(m => m.IsStartupEnabled()).Returns(false); // Out of sync

            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);

            Assert.False(service.Settings.LaunchOnStartup); // Should be synced to actual state
            _mockStorage.Verify(s => s.SetStringList(It.IsAny<string>(), It.IsAny<List<string>>()), Times.AtLeastOnce); // Should trigger a save (dirty)
        }

        [Fact]
        public void LoadSettings_WhenStartupMarkerExists_EnablesStartup()
        {
            _mockStartupManager.Setup(m => m.CheckAndApplyStartupMarker()).Returns(true);
            
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            
            Assert.True(service.Settings.LaunchOnStartup);
            _mockStorage.Verify(s => s.Flush(), Times.AtLeastOnce);
        }

        [Fact]
        public void SaveSettings_WhenErrorOccurs_LogsError()
        {
            var mockLogger = new Mock<ILogger>();
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, mockLogger.Object, _mockProcessFactory.Object);
            
            _mockStorage.Setup(s => s.Flush()).Throws(new Exception("Fail"));
            
            service.SaveSettings();
            
            mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void SaveSettings_WhenLaunchOnStartupTrue_EnablesStartup()
        {
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            service.Settings.LaunchOnStartup = true;

            // Setup process return
            _mockProcess.Setup(p => p.MainModuleFileName).Returns(@"C:\MyApp\SwitchBlade.exe");

            service.SaveSettings();

            _mockStartupManager.Verify(m => m.EnableStartup(@"C:\MyApp\SwitchBlade.exe"), Times.Once);
        }

        [Fact]
        public void SaveSettings_WhenLaunchOnStartupTrue_ButExePathEmpty_DoesNotEnable()
        {
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            service.Settings.LaunchOnStartup = true;

            _mockProcess.Setup(p => p.MainModuleFileName).Returns((string?)null);

            service.SaveSettings();

            _mockStartupManager.Verify(m => m.EnableStartup(It.IsAny<string>()), Times.Never);
            _mockStartupManager.Verify(m => m.DisableStartup(), Times.Never);
        }

        [Fact]
        public void SaveSettings_WhenLaunchOnStartupFalse_DisablesStartup()
        {
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            service.Settings.LaunchOnStartup = false;

            service.SaveSettings();

            _mockStartupManager.Verify(m => m.DisableStartup(), Times.Once);
        }

        [Fact]
        public void IsStartupEnabled_DelegatesToStartupManager()
        {
            _mockStartupManager.Setup(m => m.IsStartupEnabled()).Returns(true);
            var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object, null, _mockProcessFactory.Object);
            
            Assert.True(service.IsStartupEnabled());
        }
        
        [Fact]
        public void Constructor_NullArguments_Throws()
        {
             Assert.Throws<ArgumentNullException>(() => new SettingsService(null!, _mockStartupManager.Object));
             Assert.Throws<ArgumentNullException>(() => new SettingsService(_mockStorage.Object, null!));
        }

        [Fact]
        public void Constructor_DefaultOptionalArguments_Works()
        {
             var service = new SettingsService(_mockStorage.Object, _mockStartupManager.Object);
             Assert.NotNull(service);
        }
    }
}
