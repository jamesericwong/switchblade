using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Tests.ViewModels
{
    public class SettingsViewModelTests
    {
        private Mock<SettingsService> CreateMockSettingsService()
        {
            // SettingsService doesn't have a virtual/interface, so we test with real instance
            // For more isolated tests, consider extracting an interface
            return new Mock<SettingsService>();
        }

        private ThemeService CreateThemeService(SettingsService settingsService)
        {
            return new ThemeService(settingsService);
        }



        private Mock<IPluginService> CreateMockPluginService(IEnumerable<PluginInfo>? plugins = null)
        {
            var mock = new Mock<IPluginService>();
            mock.Setup(p => p.GetPluginInfos()).Returns(plugins ?? Enumerable.Empty<PluginInfo>());
            return mock;
        }

        [Fact]
        public void Constructor_InitializesExcludedProcesses()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();

            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.NotNull(vm.ExcludedProcesses);
        }

        [Fact]
        public void Constructor_InitializesAvailableThemes()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();

            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.NotNull(vm.AvailableThemes);
            Assert.NotEmpty(vm.AvailableThemes);
        }

        [Fact]
        public void Constructor_InitializesLoadedPlugins()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var plugins = new List<PluginInfo>
            {
                new PluginInfo { Name = "TestPlugin" }
            };
            var pluginService = CreateMockPluginService(plugins);

            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.Single(vm.LoadedPlugins);
            Assert.Equal("TestPlugin", vm.LoadedPlugins.First().Name);
        }

        [Fact]
        public void NewExcludedProcessName_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            vm.NewExcludedProcessName = "notepad";

            Assert.Equal("notepad", vm.NewExcludedProcessName);
        }

        [Fact]
        public void SelectedExcludedProcess_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            vm.SelectedExcludedProcess = "TextInputHost";

            Assert.Equal("TextInputHost", vm.SelectedExcludedProcess);
        }

        [Fact]
        public void AddExcludedProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.NotNull(vm.AddExcludedProcessCommand);
        }

        [Fact]
        public void RemoveExcludedProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.NotNull(vm.RemoveExcludedProcessCommand);
        }

        [Fact]
        public void UpdateHotKey_UpdatesSettingsAndRaisesPropertyChanged()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.HotKeyString))
                    propertyChangedRaised = true;
            };

            vm.UpdateHotKey(0x0002, 0x41); // Ctrl + A

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void ViewModel_ImplementsINotifyPropertyChanged()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var pluginService = CreateMockPluginService();
            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        }

        [Fact]
        public void TogglePluginCommand_DisablingPlugin_AddsToSettings()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var plugin = new PluginInfo { Name = "TestPlugin" };
            var plugins = new List<PluginInfo> { plugin };
            var pluginService = CreateMockPluginService(plugins);

            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            // Simulate unchecking the box (IsEnabled goes false)
            plugin.IsEnabled = false;

            // Execute command
            vm.TogglePluginCommand.Execute(plugin);

            Assert.Contains("TestPlugin", settingsService.Settings.DisabledPlugins);
        }

        [Fact]
        public void TogglePluginCommand_EnablingPlugin_RemovesFromSettings()
        {
            var settingsService = new SettingsService();
            // Clear any existing settings from registry to avoid duplicates
            settingsService.Settings.DisabledPlugins.Clear();
            settingsService.Settings.DisabledPlugins.Add("TestPlugin");

            var themeService = CreateThemeService(settingsService);
            var plugin = new PluginInfo { Name = "TestPlugin" };
            var plugins = new List<PluginInfo> { plugin };
            var pluginService = CreateMockPluginService(plugins);

            var vm = new SettingsViewModel(settingsService, themeService, pluginService.Object);

            // Verify initial state
            Assert.False(plugin.IsEnabled);

            // Simulate checking the box
            plugin.IsEnabled = true;

            // Execute command
            vm.TogglePluginCommand.Execute(plugin);

            Assert.DoesNotContain("TestPlugin", settingsService.Settings.DisabledPlugins);
        }
    }
}
