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

            _viewModel = new SettingsViewModel(_settingsServiceMock.Object, _themeServiceMock.Object, _pluginServiceMock.Object);
        }

        [Fact]
        public void SelectedTheme_Set_AppliesTheme()
        {
            // Arrange
            var themeName = "Dark";
            
            // Act
            _viewModel.SelectedTheme = themeName;
            
            // Assert
            Assert.Equal(themeName, _viewModel.SelectedTheme);
        }

        [Fact]
        public void EnablePreviews_GetSet_Works()
        {
            _viewModel.EnablePreviews = true;
            Assert.True(_viewModel.EnablePreviews);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void HideTaskbarIcon_GetSet_Works()
        {
            _viewModel.HideTaskbarIcon = true;
            Assert.True(_viewModel.HideTaskbarIcon);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void ShowIcons_GetSet_Works()
        {
            _viewModel.ShowIcons = true;
            Assert.True(_viewModel.ShowIcons);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void FadeDurationMs_GetSet_Works()
        {
            _viewModel.FadeDurationMs = 500;
            Assert.Equal(500, _viewModel.FadeDurationMs);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void WindowOpacity_GetSet_Works()
        {
            _viewModel.WindowOpacity = 0.8;
            Assert.Equal(0.8, _viewModel.WindowOpacity);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void ItemHeight_GetSet_Works()
        {
            _viewModel.ItemHeight = 45;
            Assert.Equal(45, _viewModel.ItemHeight);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void EnableBackgroundPolling_GetSet_Works()
        {
            _viewModel.EnableBackgroundPolling = true;
            Assert.True(_viewModel.EnableBackgroundPolling);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void BackgroundPollingIntervalSeconds_GetSet_Works()
        {
            _viewModel.BackgroundPollingIntervalSeconds = 30;
            Assert.Equal(30, _viewModel.BackgroundPollingIntervalSeconds);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void EnableNumberShortcuts_GetSet_Works()
        {
            _viewModel.EnableNumberShortcuts = true;
            Assert.True(_viewModel.EnableNumberShortcuts);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void EnableBadgeAnimations_GetSet_Works()
        {
            _viewModel.EnableBadgeAnimations = true;
            Assert.True(_viewModel.EnableBadgeAnimations);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void RegexCacheSize_GetSet_Works()
        {
            _viewModel.RegexCacheSize = 200;
            Assert.Equal(200, _viewModel.RegexCacheSize);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void EnableFuzzySearch_GetSet_Works()
        {
            _viewModel.EnableFuzzySearch = true;
            Assert.True(_viewModel.EnableFuzzySearch);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void IconCacheSize_GetSet_Works()
        {
            _viewModel.IconCacheSize = 1000;
            Assert.Equal(1000, _viewModel.IconCacheSize);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void UiaWorkerTimeoutSeconds_GetSet_Works()
        {
            _viewModel.UiaWorkerTimeoutSeconds = 20;
            Assert.Equal(20, _viewModel.UiaWorkerTimeoutSeconds);
            _settingsServiceMock.Verify(s => s.SaveSettings(), Times.Once());
        }

        [Fact]
        public void RefreshBehavior_Properties_Work()
        {
            _viewModel.IsPreserveScrollSelected = true;
            Assert.True(_viewModel.IsPreserveScrollSelected);
            Assert.Equal(RefreshBehavior.PreserveScroll, _settingsServiceMock.Object.Settings.RefreshBehavior);

            _viewModel.IsPreserveIdentitySelected = true;
            Assert.True(_viewModel.IsPreserveIdentitySelected);
            Assert.Equal(RefreshBehavior.PreserveIdentity, _settingsServiceMock.Object.Settings.RefreshBehavior);

            _viewModel.IsPreserveIndexSelected = true;
            Assert.True(_viewModel.IsPreserveIndexSelected);
            Assert.Equal(RefreshBehavior.PreserveIndex, _settingsServiceMock.Object.Settings.RefreshBehavior);
        }

        [Fact]
        public void SelectedShortcutModifier_GetSet_Works()
        {
            _viewModel.SelectedShortcutModifier = "Ctrl";
            Assert.Equal("Ctrl", _viewModel.SelectedShortcutModifier);
            Assert.Equal(2u, _settingsServiceMock.Object.Settings.NumberShortcutModifier); // Ctrl = 2
        }

        [Fact]
        public void ExcludedProcesses_Commands_Work()
        {
            _viewModel.NewExcludedProcessName = "chrome";
            Assert.True(_viewModel.AddExcludedProcessCommand.CanExecute(null));
            
            _viewModel.AddExcludedProcessCommand.Execute(null);
            
            Assert.Contains("chrome", _viewModel.ExcludedProcesses);
            Assert.Equal("", _viewModel.NewExcludedProcessName);

            _viewModel.SelectedExcludedProcess = "chrome";
            Assert.True(_viewModel.RemoveExcludedProcessCommand.CanExecute(null));
            
            _viewModel.RemoveExcludedProcessCommand.Execute(null);
            Assert.DoesNotContain("chrome", _viewModel.ExcludedProcesses);
        }

        [Fact]
        public void TogglePlugin_Command_Works()
        {
            var plugin = new PluginInfo { Name = "TestPlugin", IsEnabled = true };
            _viewModel.TogglePluginCommand.Execute(plugin);
            Assert.DoesNotContain("TestPlugin", _settingsServiceMock.Object.Settings.DisabledPlugins);

            plugin.IsEnabled = false;
            _viewModel.TogglePluginCommand.Execute(plugin);
            Assert.Contains("TestPlugin", _settingsServiceMock.Object.Settings.DisabledPlugins);
        }

        [Fact]
        public void HotKeyString_ReturnsCorrectFormat()
        {
            _viewModel.UpdateHotKey(1 | 2, (uint)System.Windows.Forms.Keys.A); // Alt + Ctrl + A
            Assert.Contains("Alt", _viewModel.HotKeyString);
            Assert.Contains("Ctrl", _viewModel.HotKeyString);
            Assert.Contains("A", _viewModel.HotKeyString);
        }
    }
}
