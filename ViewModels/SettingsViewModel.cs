using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SwitchBlade.Services;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

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

        public bool ShowIcons
        {
            get => _settingsService.Settings.ShowIcons;
            set { _settingsService.Settings.ShowIcons = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
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
                    // Don't save yet - ask user first
                    bool needsRestart = (value && !Program.IsRunningAsAdmin()) || (!value && Program.IsRunningAsAdmin());

                    if (needsRestart)
                    {
                        string message = value
                            ? "This setting requires restarting SwitchBlade with Administrator privileges. Restart now?"
                            : "To run without Administrator privileges, SwitchBlade needs to restart. Restart now?";

                        var result = System.Windows.MessageBox.Show(
                            message,
                            "Restart Required",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            // User confirmed - now save the setting and restart
                            _settingsService.Settings.RunAsAdministrator = value;
                            OnPropertyChanged();
                            _settingsService.SaveSettings();
                            RestartApplication();
                        }
                        // else: User clicked No - don't change anything, checkbox reverts automatically via binding
                    }
                    else
                    {
                        // No restart needed (rare case: setting matches current state)
                        _settingsService.Settings.RunAsAdministrator = value;
                        OnPropertyChanged();
                        _settingsService.SaveSettings();
                    }
                }
            }
        }

        private static void RestartApplication()
        {
            // Note: Settings have already been saved by the time this is called
            // The new process will read RunAsAdministrator from registry and handle elevation in Program.Main()

            var processPath = System.Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(processPath))
            {
                System.Windows.MessageBox.Show("Unable to determine application path for restart.", "Restart Failed",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var workingDir = System.IO.Path.GetDirectoryName(processPath) ?? "";

            // Determine if we're de-elevating (currently admin, turning it off)
            // In this case, we need special handling to run at normal privilege level
            bool isDeElevating = Program.IsRunningAsAdmin();

            System.Diagnostics.ProcessStartInfo startInfo;

            if (isDeElevating)
            {
                // For de-elevation: Use a single PowerShell command that waits then uses explorer.exe
                // Explorer.exe always runs at the user's normal (non-elevated) privilege level
                // When explorer launches an app, that app also runs non-elevated
                var escapedPath = processPath.Replace("\"", "`\"");
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process explorer.exe -ArgumentList '\"{escapedPath}\"'";

                startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };
            }
            else
            {
                // Normal restart - use PowerShell to wait then start the process
                // Program.Main() will handle elevation if RunAsAdministrator is now enabled
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process '{processPath}' -WorkingDirectory '{workingDir}'";

                startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };
            }

            try
            {
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to restart: {ex.Message}", "Restart Failed",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            // Shutdown current application - the PowerShell script will wait for us to exit, then launch new instance
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

        public bool EnableBadgeAnimations
        {
            get => _settingsService.Settings.EnableBadgeAnimations;
            set { _settingsService.Settings.EnableBadgeAnimations = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public int RegexCacheSize
        {
            get => _settingsService.Settings.RegexCacheSize;
            set { _settingsService.Settings.RegexCacheSize = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public bool EnableFuzzySearch
        {
            get => _settingsService.Settings.EnableFuzzySearch;
            set { _settingsService.Settings.EnableFuzzySearch = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
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



        public SettingsViewModel(ISettingsService settingsService, ThemeService themeService, IPluginService pluginService)
        {
            _settingsService = (SettingsService)settingsService;
            _themeService = themeService;

            var plugins = pluginService.GetPluginInfos().ToList();

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
            var sanitized = SanitizationUtils.SanitizeProcessName(NewExcludedProcessName);
            if (!string.IsNullOrEmpty(sanitized) && !ExcludedProcesses.Contains(sanitized))
            {
                ExcludedProcesses.Add(sanitized);
                _settingsService.Settings.ExcludedProcesses.Add(sanitized);
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
