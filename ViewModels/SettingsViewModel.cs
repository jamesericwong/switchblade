using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SwitchBlade.Services;
using SwitchBlade.Core;

namespace SwitchBlade.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private readonly ThemeService _themeService;
        private string _selectedTheme;

        public ObservableCollection<string> AvailableThemes { get; set; }
        public ObservableCollection<PluginInfo> LoadedPlugins { get; private set; }



        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    _themeService.ApplyTheme(_selectedTheme);
                }
            }
        }

        public bool EnablePreviews
        {
            get => _settingsService.Settings.EnablePreviews;
            set { _settingsService.Settings.EnablePreviews = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool HideTaskbarIcon
        {
            get => _settingsService.Settings.HideTaskbarIcon;
            set { _settingsService.Settings.HideTaskbarIcon = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool LaunchOnStartup
        {
            // Read directly from Windows Run registry - single source of truth
            get => _settingsService.IsStartupEnabled();
            set { _settingsService.Settings.LaunchOnStartup = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool RunAsAdministrator
        {
            get => _settingsService.Settings.RunAsAdministrator;
            set
            {
                if (_settingsService.Settings.RunAsAdministrator != value)
                {
                    _settingsService.Settings.RunAsAdministrator = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();

                    // Offer to restart the app for the change to take effect
                    if (value && !Program.IsRunningAsAdmin())
                    {
                        var result = System.Windows.MessageBox.Show(
                            "This setting requires restarting SwitchBlade. Restart now?",
                            "Restart Required",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            RestartApplication();
                        }
                    }
                    else if (!value && Program.IsRunningAsAdmin())
                    {
                        var result = System.Windows.MessageBox.Show(
                            "To run without Administrator privileges, SwitchBlade needs to restart. Restart now?",
                            "Restart Required",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            RestartApplication();
                        }
                    }
                }
            }
        }

        private static void RestartApplication()
        {
            // Note: Settings have already been saved by the time this is called
            // The new process will read RunAsAdministrator from registry and handle elevation in Program.Main()

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "",
                UseShellExecute = true
                // Do NOT set Verb = "runas" here - let Program.Main() handle elevation based on registry
            };

            System.Diagnostics.Process.Start(startInfo);

            // Small delay to ensure registry writes are flushed and new process starts
            System.Threading.Thread.Sleep(100);

            System.Windows.Application.Current.Shutdown();
        }

        public int FadeDurationMs
        {
            get => _settingsService.Settings.FadeDurationMs;
            set { _settingsService.Settings.FadeDurationMs = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public double WindowOpacity
        {
            get => _settingsService.Settings.WindowOpacity;
            set { _settingsService.Settings.WindowOpacity = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public double ItemHeight
        {
            get => _settingsService.Settings.ItemHeight;
            set { _settingsService.Settings.ItemHeight = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool EnableBackgroundPolling
        {
            get => _settingsService.Settings.EnableBackgroundPolling;
            set { _settingsService.Settings.EnableBackgroundPolling = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public int BackgroundPollingIntervalSeconds
        {
            get => _settingsService.Settings.BackgroundPollingIntervalSeconds;
            set { _settingsService.Settings.BackgroundPollingIntervalSeconds = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool EnableNumberShortcuts
        {
            get => _settingsService.Settings.EnableNumberShortcuts;
            set { _settingsService.Settings.EnableNumberShortcuts = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool IsPreserveScrollSelected
        {
            get => _settingsService.Settings.RefreshBehavior == RefreshBehavior.PreserveScroll;
            set
            {
                if (value)
                {
                    _settingsService.Settings.RefreshBehavior = RefreshBehavior.PreserveScroll;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool IsPreserveIdentitySelected
        {
            get => _settingsService.Settings.RefreshBehavior == RefreshBehavior.PreserveIdentity;
            set
            {
                if (value)
                {
                    _settingsService.Settings.RefreshBehavior = RefreshBehavior.PreserveIdentity;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool IsPreserveIndexSelected
        {
            get => _settingsService.Settings.RefreshBehavior == RefreshBehavior.PreserveIndex;
            set
            {
                if (value)
                {
                    _settingsService.Settings.RefreshBehavior = RefreshBehavior.PreserveIndex;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public ObservableCollection<string> AvailableShortcutModifiers { get; } = new ObservableCollection<string>
        {
            "Alt", "Ctrl", "Shift", "None"
        };

        public string SelectedShortcutModifier
        {
            get => ModifierValueToString(_settingsService.Settings.NumberShortcutModifier);
            set
            {
                _settingsService.Settings.NumberShortcutModifier = StringToModifierValue(value);
                OnPropertyChanged();
                _settingsService.SaveSettings();
            }
        }

        private static string ModifierValueToString(uint value) => Services.ModifierKeyFlags.ToString(value);

        private static uint StringToModifierValue(string value) => Services.ModifierKeyFlags.FromString(value);



        public SettingsViewModel(SettingsService settingsService, ThemeService themeService, IEnumerable<PluginInfo> plugins)
        {
            _settingsService = settingsService;
            _themeService = themeService;

            // Initialize enabled state
            foreach (var plugin in plugins)
            {
                if (_settingsService.Settings.DisabledPlugins.Contains(plugin.Name))
                {
                    plugin.IsEnabled = false;
                }
            }

            LoadedPlugins = new ObservableCollection<PluginInfo>(plugins);

            ExcludedProcesses = new ObservableCollection<string>(_settingsService.Settings.ExcludedProcesses);
            AvailableThemes = new ObservableCollection<string>(_themeService.AvailableThemes.Select(t => t.Name));

            _selectedTheme = _settingsService.Settings.CurrentTheme;

            TogglePluginCommand = new RelayCommand(param => TogglePlugin(param));
            AddExcludedProcessCommand = new RelayCommand(_ => AddExcludedProcess(), _ => !string.IsNullOrWhiteSpace(NewExcludedProcessName));
            RemoveExcludedProcessCommand = new RelayCommand(_ => RemoveExcludedProcess(), _ => !string.IsNullOrEmpty(SelectedExcludedProcess));
        }



        public string HotKeyString
        {
            get
            {
                var mods = (uint)_settingsService.Settings.HotKeyModifiers;
                var key = (uint)_settingsService.Settings.HotKeyKey;

                var parts = new System.Collections.Generic.List<string>();
                if ((mods & 1) != 0) parts.Add("Alt");
                if ((mods & 2) != 0) parts.Add("Ctrl");
                if ((mods & 4) != 0) parts.Add("Shift");
                if ((mods & 8) != 0) parts.Add("Win");

                parts.Add(((System.Windows.Forms.Keys)key).ToString());
                return string.Join(" + ", parts);
            }
        }

        public void UpdateHotKey(uint modifiers, uint key)
        {
            _settingsService.Settings.HotKeyModifiers = modifiers;
            _settingsService.Settings.HotKeyKey = key;
            _settingsService.SaveSettings();
            OnPropertyChanged(nameof(HotKeyString));
        }

        // Excluded Processes Logic
        private string _newExcludedProcessName = "";
        private string _selectedExcludedProcess = "";

        public ObservableCollection<string> ExcludedProcesses { get; set; }

        public string NewExcludedProcessName
        {
            get => _newExcludedProcessName;
            set { _newExcludedProcessName = value; OnPropertyChanged(); }
        }

        public string SelectedExcludedProcess
        {
            get => _selectedExcludedProcess;
            set { _selectedExcludedProcess = value; OnPropertyChanged(); }
        }

        public ICommand AddExcludedProcessCommand { get; }
        public ICommand RemoveExcludedProcessCommand { get; }
        public ICommand TogglePluginCommand { get; }

        private void TogglePlugin(object? param)
        {
            if (param is PluginInfo plugin)
            {
                // IsEnabled is bound to the CheckBox, so it's already updated in the object
                // We just need to sync with Settings
                if (plugin.IsEnabled)
                {
                    _settingsService.Settings.DisabledPlugins.Remove(plugin.Name);
                }
                else
                {
                    if (!_settingsService.Settings.DisabledPlugins.Contains(plugin.Name))
                    {
                        _settingsService.Settings.DisabledPlugins.Add(plugin.Name);
                    }
                }
                _settingsService.SaveSettings();
            }
        }

        private void AddExcludedProcess()
        {
            if (!ExcludedProcesses.Contains(NewExcludedProcessName))
            {
                ExcludedProcesses.Add(NewExcludedProcessName);
                _settingsService.Settings.ExcludedProcesses.Add(NewExcludedProcessName);
                _settingsService.SaveSettings();
                NewExcludedProcessName = "";
            }
        }

        private void RemoveExcludedProcess()
        {
            var processToRemove = SelectedExcludedProcess;
            if (!string.IsNullOrEmpty(processToRemove) && ExcludedProcesses.Contains(processToRemove))
            {
                ExcludedProcesses.Remove(processToRemove);
                _settingsService.Settings.ExcludedProcesses.Remove(processToRemove);
                _settingsService.SaveSettings();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
