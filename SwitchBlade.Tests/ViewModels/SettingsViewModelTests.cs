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

        [Fact]
        public void Constructor_InitializesBrowserProcesses()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var plugins = Enumerable.Empty<PluginInfo>();

            var vm = new SettingsViewModel(settingsService, themeService, plugins);

            Assert.NotNull(vm.BrowserProcesses);
            Assert.NotEmpty(vm.BrowserProcesses);
        }

        [Fact]
        public void Constructor_InitializesExcludedProcesses()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var plugins = Enumerable.Empty<PluginInfo>();

            var vm = new SettingsViewModel(settingsService, themeService, plugins);

            Assert.NotNull(vm.ExcludedProcesses);
        }

        [Fact]
        public void Constructor_InitializesAvailableThemes()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var plugins = Enumerable.Empty<PluginInfo>();

            var vm = new SettingsViewModel(settingsService, themeService, plugins);

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

            var vm = new SettingsViewModel(settingsService, themeService, plugins);

            Assert.Single(vm.LoadedPlugins);
            Assert.Equal("TestPlugin", vm.LoadedPlugins.First().Name);
        }

        [Fact]
        public void NewProcessName_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            vm.NewProcessName = "firefox";

            Assert.Equal("firefox", vm.NewProcessName);
        }

        [Fact]
        public void NewProcessName_Change_RaisesPropertyChanged()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.NewProcessName))
                    propertyChangedRaised = true;
            };

            vm.NewProcessName = "firefox";

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void SelectedProcess_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            vm.SelectedProcess = "chrome";

            Assert.Equal("chrome", vm.SelectedProcess);
        }

        [Fact]
        public void NewExcludedProcessName_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            vm.NewExcludedProcessName = "notepad";

            Assert.Equal("notepad", vm.NewExcludedProcessName);
        }

        [Fact]
        public void SelectedExcludedProcess_SetValue_UpdatesProperty()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            vm.SelectedExcludedProcess = "TextInputHost";

            Assert.Equal("TextInputHost", vm.SelectedExcludedProcess);
        }

        [Fact]
        public void AddProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            Assert.NotNull(vm.AddProcessCommand);
        }

        [Fact]
        public void RemoveProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            Assert.NotNull(vm.RemoveProcessCommand);
        }

        [Fact]
        public void AddExcludedProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            Assert.NotNull(vm.AddExcludedProcessCommand);
        }

        [Fact]
        public void RemoveExcludedProcessCommand_IsNotNull()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            Assert.NotNull(vm.RemoveExcludedProcessCommand);
        }

        [Fact]
        public void UpdateHotKey_UpdatesSettingsAndRaisesPropertyChanged()
        {
            var settingsService = new SettingsService();
            var themeService = CreateThemeService(settingsService);
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());
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
            var vm = new SettingsViewModel(settingsService, themeService, Enumerable.Empty<PluginInfo>());

            Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        }
    }
}
