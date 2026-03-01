using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using SwitchBlade.Services;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

namespace SwitchBlade.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ISettingsService _settingsService;
        private readonly ThemeService _themeService;
        private readonly IUIService _uiService;
        private string _selectedTheme;
        private System.Threading.Timer? _saveTimer;
        private const int SaveDebounceMs = 300;

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
            set { _settingsService.Settings.EnablePreviews = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool HideTaskbarIcon
        {
            get => _settingsService.Settings.HideTaskbarIcon;
            set { _settingsService.Settings.HideTaskbarIcon = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool ShowIcons
        {
            get => _settingsService.Settings.ShowIcons;
            set { _settingsService.Settings.ShowIcons = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool LaunchOnStartup
        {
            get => _settingsService.Settings.LaunchOnStartup;
            set
            {
                if (_settingsService.Settings.LaunchOnStartup != value)
                {
                    _settingsService.Settings.LaunchOnStartup = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool RunAsAdministrator
        {
            get => _settingsService.Settings.RunAsAdministrator;
            set
            {
                if (_settingsService.Settings.RunAsAdministrator != value)
                {
                    // Don't save yet - ask user first
                    bool needsRestart = (value && !_uiService.IsRunningAsAdmin()) || (!value && _uiService.IsRunningAsAdmin());

                    if (needsRestart)
                    {
                        string message = value
                            ? "This setting requires restarting SwitchBlade with Administrator privileges. Restart now?"
                            : "To run without Administrator privileges, SwitchBlade needs to restart. Restart now?";

                        var result = _uiService.ShowMessageBox(
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
                            _uiService.RestartApplication();
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

        // RestartApplication logic moved to IUIService implementation

        public int FadeDurationMs
        {
            get => _settingsService.Settings.FadeDurationMs;
            set { _settingsService.Settings.FadeDurationMs = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public double WindowOpacity
        {
            get => _settingsService.Settings.WindowOpacity;
            set { _settingsService.Settings.WindowOpacity = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public double ItemHeight
        {
            get => _settingsService.Settings.ItemHeight;
            set { _settingsService.Settings.ItemHeight = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool EnableBackgroundPolling
        {
            get => _settingsService.Settings.EnableBackgroundPolling;
            set { _settingsService.Settings.EnableBackgroundPolling = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public int BackgroundPollingIntervalSeconds
        {
            get => _settingsService.Settings.BackgroundPollingIntervalSeconds;
            set { _settingsService.Settings.BackgroundPollingIntervalSeconds = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool EnableNumberShortcuts
        {
            get => _settingsService.Settings.EnableNumberShortcuts;
            set { _settingsService.Settings.EnableNumberShortcuts = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool EnableBadgeAnimations
        {
            get => _settingsService.Settings.EnableBadgeAnimations;
            set { _settingsService.Settings.EnableBadgeAnimations = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public int RegexCacheSize
        {
            get => _settingsService.Settings.RegexCacheSize;
            set { _settingsService.Settings.RegexCacheSize = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool EnableFuzzySearch
        {
            get => _settingsService.Settings.EnableFuzzySearch;
            set { _settingsService.Settings.EnableFuzzySearch = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public bool EnableSearchHighlighting
        {
            get => _settingsService.Settings.EnableSearchHighlighting;
            set { _settingsService.Settings.EnableSearchHighlighting = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public string SearchHighlightColor
        {
            get => _settingsService.Settings.SearchHighlightColor;
            set
            {
                if (_settingsService.Settings.SearchHighlightColor != value)
                {
                    _settingsService.Settings.SearchHighlightColor = value;
                    OnPropertyChanged();
                    ScheduleSave();
                }
            }
        }

        public ICommand SetHighlightColorCommand { get; }

        public int IconCacheSize
        {
            get => _settingsService.Settings.IconCacheSize;
            set { _settingsService.Settings.IconCacheSize = value; OnPropertyChanged(); ScheduleSave(); }
        }

        public int UiaWorkerTimeoutSeconds
        {
            get => _settingsService.Settings.UiaWorkerTimeoutSeconds;
            set { _settingsService.Settings.UiaWorkerTimeoutSeconds = value; OnPropertyChanged(); ScheduleSave(); }
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
                    ScheduleSave();
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
                    ScheduleSave();
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
                    ScheduleSave();
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
                ScheduleSave();
            }
        }

        private static string ModifierValueToString(uint value) => Services.ModifierKeyFlags.ToString(value);

        private static uint StringToModifierValue(string value) => Services.ModifierKeyFlags.FromString(value);



        public SettingsViewModel(ISettingsService settingsService, ThemeService themeService, IPluginService pluginService, IUIService uiService)
        {
            _settingsService = settingsService;
            _themeService = themeService;
            _uiService = uiService;

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
            SetHighlightColorCommand = new RelayCommand(param => { if (param is string color) SearchHighlightColor = color; });
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

        /// <summary>
        /// Schedules a debounced save. Multiple rapid calls reset the timer,
        /// resulting in a single save after the debounce period.
        /// </summary>
        private void ScheduleSave()
        {
            _saveTimer?.Dispose();
            _saveTimer = new System.Threading.Timer(_ => _settingsService.SaveSettings(), null, SaveDebounceMs, Timeout.Infinite);
        }

        /// <summary>
        /// Flushes any pending debounced save immediately.
        /// Call this in tests or during teardown to ensure all changes are persisted.
        /// </summary>
        public void FlushPendingSave()
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            _settingsService.SaveSettings();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
