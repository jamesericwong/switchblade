using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using Xunit;

namespace SwitchBlade.Tests.ViewModels
{
    public class SettingsViewModelTests
    {
        private readonly Mock<ISettingsService> _settingsServiceMock;
        private readonly Mock<ThemeService> _themeServiceMock;
        private readonly Mock<IPluginService> _pluginServiceMock;
        private readonly Mock<IUIService> _uiServiceMock;
        private readonly Mock<IApplicationResourceHandler> _resourceHandlerMock;
        private readonly SettingsViewModel _viewModel;

        public SettingsViewModelTests()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            var userSettings = new UserSettings();
            _settingsServiceMock.Setup(s => s.Settings).Returns(userSettings);

            _resourceHandlerMock = new Mock<IApplicationResourceHandler>();
            _themeServiceMock = new Mock<ThemeService>(_settingsServiceMock.Object, _resourceHandlerMock.Object);

            _pluginServiceMock = new Mock<IPluginService>();
            _pluginServiceMock.Setup(p => p.GetPluginInfos()).Returns(new List<PluginInfo>());

            _uiServiceMock = new Mock<IUIService>();

            _viewModel = new SettingsViewModel(_settingsServiceMock.Object, _themeServiceMock.Object, _pluginServiceMock.Object, _uiServiceMock.Object);
        }

        [Theory]
        [InlineData(nameof(SettingsViewModel.EnablePreviews), true)]
        [InlineData(nameof(SettingsViewModel.HideTaskbarIcon), true)]
        [InlineData(nameof(SettingsViewModel.ShowIcons), true)]
        [InlineData(nameof(SettingsViewModel.LaunchOnStartup), true)]
        [InlineData(nameof(SettingsViewModel.EnableBackgroundPolling), true)]
        [InlineData(nameof(SettingsViewModel.EnableNumberShortcuts), true)]
        [InlineData(nameof(SettingsViewModel.EnableBadgeAnimations), true)]
        [InlineData(nameof(SettingsViewModel.EnableFuzzySearch), true)]
        [InlineData(nameof(SettingsViewModel.EnableSearchHighlighting), true)]
        [InlineData(nameof(SettingsViewModel.NewExcludedProcessName), "test.exe")]
        [InlineData(nameof(SettingsViewModel.SelectedExcludedProcess), "test.exe")]
        public void BooleanAndStringProperties_NotifyAndSave(string propertyName, object value)
        {
            // Arrange
            bool eventFired = false;
            _viewModel.PropertyChanged += (s, e) => { if (e.PropertyName == propertyName) eventFired = true; };

            // Act
            var prop = _viewModel.GetType().GetProperty(propertyName);
            Assert.NotNull(prop);
            prop.SetValue(_viewModel, value);

            // Assert
            Assert.True(eventFired, $"PropertyChanged event did not fire for {propertyName}");
            if (propertyName != nameof(SettingsViewModel.NewExcludedProcessName) &&
                propertyName != nameof(SettingsViewModel.SelectedExcludedProcess) &&
                propertyName != nameof(SettingsViewModel.LaunchOnStartup))
            {
                _viewModel.FlushPendingSave();
                _settingsServiceMock.Verify(s => s.SaveSettings(), Times.AtLeastOnce());
            }
        }

        [Theory]
        [InlineData(nameof(SettingsViewModel.FadeDurationMs), 100)]
        [InlineData(nameof(SettingsViewModel.WindowOpacity), 0.5)]
        [InlineData(nameof(SettingsViewModel.ItemHeight), 50.0)]
        [InlineData(nameof(SettingsViewModel.BackgroundPollingIntervalSeconds), 60)]
        [InlineData(nameof(SettingsViewModel.RegexCacheSize), 500)]
        [InlineData(nameof(SettingsViewModel.IconCacheSize), 2000)]
        [InlineData(nameof(SettingsViewModel.UiaWorkerTimeoutSeconds), 30)]
        public void NumericProperties_NotifyAndSave(string propertyName, object value)
        {
            // Arrange
            bool eventFired = false;
            _viewModel.PropertyChanged += (s, e) => { if (e.PropertyName == propertyName) eventFired = true; };

            // Act
            var prop = _viewModel.GetType().GetProperty(propertyName);
            Assert.NotNull(prop);
            prop.SetValue(_viewModel, value);

            // Assert
            Assert.True(eventFired, $"PropertyChanged event did not fire for {propertyName}");
            _viewModel.FlushPendingSave();
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.AtLeastOnce());
        }

        [Fact]
        public void RunAsAdministrator_SetsValue_WhenNoRestartNeeded()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.Settings).Returns(new UserSettings { RunAsAdministrator = false });
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(false);

            // Act
            _viewModel.RunAsAdministrator = false;

            // Assert
            _uiServiceMock.Verify(u => u.ShowMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Windows.MessageBoxButton>(), It.IsAny<System.Windows.MessageBoxImage>()), Times.Never());
        }

        [Fact]
        public void RunAsAdministrator_Restarts_WhenUserConfirms()
        {
            // Arrange
            _settingsServiceMock.Object.Settings.RunAsAdministrator = false;
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(false);
            _uiServiceMock.Setup(u => u.ShowMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Windows.MessageBoxButton>(), It.IsAny<System.Windows.MessageBoxImage>()))
                .Returns(System.Windows.MessageBoxResult.Yes);

            // Act
            _viewModel.RunAsAdministrator = true;

            // Assert
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
            _uiServiceMock.Verify(u => u.RestartApplication(), Times.Once());
        }

        [Fact]
        public void RunAsAdministrator_DoesNothing_WhenUserCancels()
        {
            // Arrange
            _settingsServiceMock.Object.Settings.RunAsAdministrator = false;
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(false);
            _uiServiceMock.Setup(u => u.ShowMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Windows.MessageBoxButton>(), It.IsAny<System.Windows.MessageBoxImage>()))
                .Returns(System.Windows.MessageBoxResult.No);

            // Act
            _viewModel.RunAsAdministrator = true;

            // Assert
            _uiServiceMock.Verify(u => u.RestartApplication(), Times.Never());
            Assert.False(_settingsServiceMock.Object.Settings.RunAsAdministrator);
        }

        [Fact]
        public void RunAsAdministrator_NoRestartNeeded_ComplexBranches()
        {
            // Case 1: Value changes to True, and already Admin
            _settingsServiceMock.Object.Settings.RunAsAdministrator = false;
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(true);
            _viewModel.RunAsAdministrator = true;
            _uiServiceMock.Verify(u => u.RestartApplication(), Times.Never());
            Assert.True(_settingsServiceMock.Object.Settings.RunAsAdministrator);

            // Case 2: Value changes to False, and already NOT Admin
            _settingsServiceMock.Object.Settings.RunAsAdministrator = true;
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(false);
            _viewModel.RunAsAdministrator = false;
            _uiServiceMock.Verify(u => u.RestartApplication(), Times.Never());
            Assert.False(_settingsServiceMock.Object.Settings.RunAsAdministrator);
        }

        [Fact]
        public void RunAsAdministrator_Restart_WhenDeElevating()
        {
            // Case: Currently Admin, set to False -> needs restart
            _settingsServiceMock.Object.Settings.RunAsAdministrator = true;
            _uiServiceMock.Setup(u => u.IsRunningAsAdmin()).Returns(true);
            _uiServiceMock.Setup(u => u.ShowMessageBox(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Windows.MessageBoxButton>(), It.IsAny<System.Windows.MessageBoxImage>()))
                .Returns(System.Windows.MessageBoxResult.Yes);

            _viewModel.RunAsAdministrator = false;

            _uiServiceMock.Verify(u => u.RestartApplication(), Times.Once());
        }

        [Fact]
        public void SelectedTheme_OnlyNotifiesIfChanged()
        {
            _viewModel.SelectedTheme = "Dark";
            bool notified = false;
            _viewModel.PropertyChanged += (s, e) => notified = true;

            _viewModel.SelectedTheme = "Dark";

            Assert.False(notified);
        }

        [Fact]
        public void AddExcludedProcess_Logic_DuplicatesAndEmpty()
        {
            // Duplicate
            _viewModel.NewExcludedProcessName = "chrome";
            _viewModel.AddExcludedProcessCommand.Execute(null);
            _settingsServiceMock.Invocations.Clear();
            _viewModel.NewExcludedProcessName = "chrome";
            _viewModel.AddExcludedProcessCommand.Execute(null);
            Assert.Single(_viewModel.ExcludedProcesses, p => p == "chrome");
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Never());

            // Empty
            _viewModel.ExcludedProcesses.Clear();
            _viewModel.NewExcludedProcessName = "  ";
            _viewModel.AddExcludedProcessCommand.Execute(null);
            Assert.Empty(_viewModel.ExcludedProcesses);
        }

        [Fact]
        public void TogglePlugin_SynchronizesWithSettings()
        {
            var plugin = new PluginInfo { Name = "TestPlugin", IsEnabled = false };
            _settingsServiceMock.Object.Settings.DisabledPlugins.Clear();

            // Enable
            plugin.IsEnabled = true;
            _viewModel.TogglePluginCommand.Execute(plugin);
            Assert.DoesNotContain("TestPlugin", _settingsServiceMock.Object.Settings.DisabledPlugins);

            // Disable
            plugin.IsEnabled = false;
            _viewModel.TogglePluginCommand.Execute(plugin);
            Assert.Contains("TestPlugin", _settingsServiceMock.Object.Settings.DisabledPlugins);

            // Disable again (should not duplicate)
            _viewModel.TogglePluginCommand.Execute(plugin);
            Assert.Single(_settingsServiceMock.Object.Settings.DisabledPlugins, p => p == "TestPlugin");
        }

        [Fact]
        public void RefreshBehavior_Properties_SyncWithSettings()
        {
            _viewModel.IsPreserveScrollSelected = true;
            Assert.Equal(RefreshBehavior.PreserveScroll, _settingsServiceMock.Object.Settings.RefreshBehavior);
            Assert.True(_viewModel.IsPreserveScrollSelected);
            Assert.False(_viewModel.IsPreserveIdentitySelected);
            Assert.False(_viewModel.IsPreserveIndexSelected);

            _viewModel.IsPreserveIdentitySelected = true;
            Assert.Equal(RefreshBehavior.PreserveIdentity, _settingsServiceMock.Object.Settings.RefreshBehavior);
            Assert.True(_viewModel.IsPreserveIdentitySelected);
            Assert.False(_viewModel.IsPreserveScrollSelected);

            _viewModel.IsPreserveIndexSelected = true;
            Assert.Equal(RefreshBehavior.PreserveIndex, _settingsServiceMock.Object.Settings.RefreshBehavior);
            Assert.True(_viewModel.IsPreserveIndexSelected);
            Assert.False(_viewModel.IsPreserveIdentitySelected);
        }

        [Fact]
        public void HotKeyString_ReflectsModifiersAndKey()
        {
            _settingsServiceMock.Object.Settings.HotKeyModifiers = 1 | 2 | 4 | 8;
            _settingsServiceMock.Object.Settings.HotKeyKey = (uint)System.Windows.Forms.Keys.A;

            var result = _viewModel.HotKeyString;

            Assert.Contains("Alt", result);
            Assert.Contains("Ctrl", result);
            Assert.Contains("Shift", result);
            Assert.Contains("Win", result);
            Assert.Contains("A", result);
        }

        [Fact]
        public void UpdateHotKey_UpdatesSettingsAndNotifies()
        {
            bool notified = false;
            _viewModel.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SettingsViewModel.HotKeyString)) notified = true; };

            _viewModel.UpdateHotKey(2, (uint)System.Windows.Forms.Keys.B);

            Assert.Equal(2u, _settingsServiceMock.Object.Settings.HotKeyModifiers);
            Assert.Equal((uint)System.Windows.Forms.Keys.B, _settingsServiceMock.Object.Settings.HotKeyKey);
            Assert.True(notified);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void RelayCommands_CanExecute_Logic()
        {
            // Add
            _viewModel.NewExcludedProcessName = "";
            Assert.False(_viewModel.AddExcludedProcessCommand.CanExecute(null));
            _viewModel.NewExcludedProcessName = "chrome";
            Assert.True(_viewModel.AddExcludedProcessCommand.CanExecute(null));

            // Remove
            _viewModel.SelectedExcludedProcess = "";
            Assert.False(_viewModel.RemoveExcludedProcessCommand.CanExecute(null));
            _viewModel.SelectedExcludedProcess = "chrome";
            Assert.True(_viewModel.RemoveExcludedProcessCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveExcludedProcess_Logic_Branches()
        {
            // Actual removal
            _viewModel.ExcludedProcesses.Add("chrome");
            _settingsServiceMock.Object.Settings.ExcludedProcesses.Add("chrome");
            _viewModel.SelectedExcludedProcess = "chrome";
            _viewModel.RemoveExcludedProcessCommand.Execute(null);
            Assert.DoesNotContain("chrome", _viewModel.ExcludedProcesses);
            Assert.DoesNotContain("chrome", _settingsServiceMock.Object.Settings.ExcludedProcesses);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());

            // Empty
            _viewModel.SelectedExcludedProcess = "";
            _viewModel.RemoveExcludedProcessCommand.Execute(null);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once()); // Only the first one called it

            // Not found
            _viewModel.ExcludedProcesses.Clear();
            _viewModel.SelectedExcludedProcess = "missing";
            _viewModel.RemoveExcludedProcessCommand.Execute(null);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once()); // Still once
        }

        [Fact]
        public void AllPropertyGetters_AreExercised()
        {
            _ = _viewModel.SelectedTheme;
            _ = _viewModel.EnablePreviews;
            _ = _viewModel.HideTaskbarIcon;
            _ = _viewModel.ShowIcons;
            _ = _viewModel.LaunchOnStartup;
            _ = _viewModel.RunAsAdministrator;
            _ = _viewModel.FadeDurationMs;
            _ = _viewModel.WindowOpacity;
            _ = _viewModel.ItemHeight;
            _ = _viewModel.EnableBackgroundPolling;
            _ = _viewModel.BackgroundPollingIntervalSeconds;
            _ = _viewModel.EnableNumberShortcuts;
            _ = _viewModel.EnableBadgeAnimations;
            _ = _viewModel.RegexCacheSize;
            _ = _viewModel.EnableFuzzySearch;
            _ = _viewModel.EnableSearchHighlighting;
            _ = _viewModel.IconCacheSize;
            _ = _viewModel.UiaWorkerTimeoutSeconds;
            _ = _viewModel.SelectedShortcutModifier;
            _ = _viewModel.HotKeyString;
            _ = _viewModel.NewExcludedProcessName;
            _ = _viewModel.SelectedExcludedProcess;
            _ = _viewModel.AddExcludedProcessCommand;
            _ = _viewModel.RemoveExcludedProcessCommand;
            _ = _viewModel.TogglePluginCommand;
            _ = _viewModel.AvailableThemes;
            _ = _viewModel.LoadedPlugins;
            _ = _viewModel.ExcludedProcesses;
            _ = _viewModel.AvailableShortcutModifiers;

            _viewModel.AddExcludedProcessCommand.CanExecuteChanged += (s, e) => { };
            _viewModel.RemoveExcludedProcessCommand.CanExecuteChanged += (s, e) => { };
        }

        [Fact]
        public void Constructor_HandlesPreDisabledPlugins()
        {
            var plugins = new List<PluginInfo> { new PluginInfo { Name = "DisabledPlugin", IsEnabled = true } };
            _pluginServiceMock.Setup(p => p.GetPluginInfos()).Returns(plugins);
            _settingsServiceMock.Object.Settings.DisabledPlugins.Add("DisabledPlugin");

            var vm = new SettingsViewModel(_settingsServiceMock.Object, _themeServiceMock.Object, _pluginServiceMock.Object, _uiServiceMock.Object);

            var plugin = vm.LoadedPlugins.First(p => p.Name == "DisabledPlugin");
            Assert.False(plugin.IsEnabled);
        }

        [Fact]
        public void SelectedShortcutModifier_Set_UpdatesSettings()
        {
            _viewModel.SelectedShortcutModifier = "Ctrl";
            Assert.Equal(2u, _settingsServiceMock.Object.Settings.NumberShortcutModifier);
            _viewModel.FlushPendingSave();
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void LaunchOnStartup_Getter_ReadsFromService()
        {
            _settingsServiceMock.Setup(s => s.IsStartupEnabled()).Returns(true);
            Assert.True(_viewModel.LaunchOnStartup);
        }

        [Fact]
        public void AvailableThemes_Property_Works()
        {
            Assert.NotNull(_viewModel.AvailableThemes);
        }

        [Fact]
        public void AvailableShortcutModifiers_ReturnsExpected()
        {
            Assert.Contains("Alt", _viewModel.AvailableShortcutModifiers);
            Assert.Contains("None", _viewModel.AvailableShortcutModifiers);
        }

        [Fact]
        public void SearchHighlightColor_Property_NotifyAndSave()
        {
            _viewModel.SearchHighlightColor = "#FF0000"; // Initial
            _settingsServiceMock.Invocations.Clear();

            bool notified = false;
            _viewModel.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SettingsViewModel.SearchHighlightColor)) notified = true; };

            _viewModel.SearchHighlightColor = "#00FF00";

            Assert.True(notified);
            Assert.Equal("#00FF00", _settingsServiceMock.Object.Settings.SearchHighlightColor);
            _viewModel.FlushPendingSave();
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void SetHighlightColorCommand_Executes_Correctly()
        {
            _viewModel.SetHighlightColorCommand.Execute("#123456");
            Assert.Equal("#123456", _viewModel.SearchHighlightColor);
        }

        [Fact]
        public void FlushPendingSave_HandlesNullTimer()
        {
            // Act & Assert
            var exception = Record.Exception(() => _viewModel.FlushPendingSave());
            Assert.Null(exception);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.AtLeastOnce());
        }
    }
}
