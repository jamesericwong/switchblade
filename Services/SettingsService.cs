using System;
using System.Collections.Generic;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    // UserSettings and RefreshBehavior have been moved to Models/ directory

    /// <summary>
    /// Manages user settings with support for pluggable storage backends.
    /// Uses nameof(UserSettings.PropertyName) to ensure Load/Save keys stay in sync.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        private readonly ISettingsStorage _storage;
        private readonly IWindowsStartupManager _startupManager;
        private readonly ILogger? _logger;

        public UserSettings Settings { get; private set; }
        public event Action? SettingsChanged;

        /// <summary>
        /// Creates a SettingsService with the default Registry storage.
        /// </summary>
        public SettingsService() : this(new RegistrySettingsStorage(REGISTRY_KEY), new WindowsStartupManager())
        {
        }

        /// <summary>
        /// Creates a SettingsService with a custom startup manager (for testing).
        /// </summary>
        public SettingsService(IWindowsStartupManager startupManager)
            : this(new RegistrySettingsStorage(REGISTRY_KEY), startupManager)
        {
        }

        /// <summary>
        /// Creates a SettingsService with custom storage and startup manager (for testing).
        /// </summary>
        public SettingsService(ISettingsStorage storage, IWindowsStartupManager startupManager, ILogger? logger = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _logger = logger;
            Settings = new UserSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            bool settingsDirty = false;

            // String Lists
            var excludedProcesses = _storage.GetStringList(nameof(Settings.ExcludedProcesses));
            if (!_storage.HasKey(nameof(Settings.ExcludedProcesses)))
                settingsDirty = true;

            if (excludedProcesses.Count > 0)
                Settings.ExcludedProcesses = excludedProcesses;

            var disabledPlugins = _storage.GetStringList(nameof(Settings.DisabledPlugins));
            if (!_storage.HasKey(nameof(Settings.DisabledPlugins)))
                settingsDirty = true;
            Settings.DisabledPlugins = disabledPlugins;

            // Theme
            Settings.CurrentTheme = Load(nameof(Settings.CurrentTheme), "Super Light", ref settingsDirty);

            // UI Options
            Settings.EnablePreviews = Load(nameof(Settings.EnablePreviews), true, ref settingsDirty);
            Settings.FadeDurationMs = Load(nameof(Settings.FadeDurationMs), 200, ref settingsDirty);
            Settings.WindowOpacity = Load(nameof(Settings.WindowOpacity), 1.0, ref settingsDirty);

            Settings.ItemHeight = Load(nameof(Settings.ItemHeight), 64.0, ref settingsDirty);

            Settings.WindowWidth = Load(nameof(Settings.WindowWidth), 800.0, ref settingsDirty);
            Settings.WindowHeight = Load(nameof(Settings.WindowHeight), 600.0, ref settingsDirty);

            Settings.ShowIcons = Load(nameof(Settings.ShowIcons), true, ref settingsDirty);
            Settings.HideTaskbarIcon = Load(nameof(Settings.HideTaskbarIcon), true, ref settingsDirty);
            Settings.LaunchOnStartup = Load(nameof(Settings.LaunchOnStartup), false, ref settingsDirty);
            Settings.RunAsAdministrator = Load(nameof(Settings.RunAsAdministrator), false, ref settingsDirty);
            _logger?.Log($"SettingsService: Loaded RunAsAdministrator = {Settings.RunAsAdministrator}");

            // Hotkey
            Settings.HotKeyModifiers = Load<uint>(nameof(Settings.HotKeyModifiers), 6, ref settingsDirty);
            Settings.HotKeyKey = Load<uint>(nameof(Settings.HotKeyKey), 0x51, ref settingsDirty);
            _logger?.Log($"SettingsService: Loaded HotKeyKey = {Settings.HotKeyKey}");

            // Background Polling
            Settings.EnableBackgroundPolling = Load(nameof(Settings.EnableBackgroundPolling), true, ref settingsDirty);
            Settings.BackgroundPollingIntervalSeconds = Load(nameof(Settings.BackgroundPollingIntervalSeconds), 30, ref settingsDirty);

            // Number Shortcuts
            Settings.EnableNumberShortcuts = Load(nameof(Settings.EnableNumberShortcuts), true, ref settingsDirty);
            Settings.NumberShortcutModifier = Load<uint>(nameof(Settings.NumberShortcutModifier), 1, ref settingsDirty);

            // Badge Animations
            Settings.EnableBadgeAnimations = Load(nameof(Settings.EnableBadgeAnimations), true, ref settingsDirty);

            // Refresh Behavior
            Settings.RefreshBehavior = Load(nameof(Settings.RefreshBehavior), RefreshBehavior.PreserveScroll, ref settingsDirty);

            // Regex Cache Size
            Settings.RegexCacheSize = Load(nameof(Settings.RegexCacheSize), 50, ref settingsDirty);

            // Fuzzy Search
            Settings.EnableFuzzySearch = Load(nameof(Settings.EnableFuzzySearch), true, ref settingsDirty);

            // UIA Worker Timeout
            Settings.UiaWorkerTimeoutSeconds = Load(nameof(Settings.UiaWorkerTimeoutSeconds), 60, ref settingsDirty);

            // Sync LaunchOnStartup with actual Windows Run registry state
            bool actualStartupEnabled = _startupManager.IsStartupEnabled();
            if (Settings.LaunchOnStartup != actualStartupEnabled)
            {
                Settings.LaunchOnStartup = actualStartupEnabled;
                settingsDirty = true;
            }

            // Check for installer startup marker
            if (_startupManager.CheckAndApplyStartupMarker())
            {
                Settings.LaunchOnStartup = true;
                settingsDirty = true;
            }

            // HEAL: If we found any missing/corrupt values, save the clean state now
            if (settingsDirty)
            {
                SaveSettings();
            }
        }

        private T Load<T>(string key, T defaultValue, ref bool dirty)
        {
            if (!_storage.HasKey(key))
            {
                dirty = true;
                return defaultValue;
            }
            return _storage.GetValue(key, defaultValue);
        }

        public void SaveSettings()
        {
            try
            {
                // String Lists
                _storage.SetStringList(nameof(Settings.ExcludedProcesses), Settings.ExcludedProcesses);
                _storage.SetStringList(nameof(Settings.DisabledPlugins), Settings.DisabledPlugins);

                // Theme
                _storage.SetValue(nameof(Settings.CurrentTheme), Settings.CurrentTheme);

                // UI Options
                _storage.SetValue(nameof(Settings.EnablePreviews), Settings.EnablePreviews);
                _storage.SetValue(nameof(Settings.FadeDurationMs), Settings.FadeDurationMs);
                _storage.SetValue(nameof(Settings.WindowOpacity), Settings.WindowOpacity);
                _storage.SetValue(nameof(Settings.ItemHeight), Settings.ItemHeight);
                _storage.SetValue(nameof(Settings.WindowWidth), Settings.WindowWidth);
                _storage.SetValue(nameof(Settings.WindowHeight), Settings.WindowHeight);

                _storage.SetValue(nameof(Settings.ShowIcons), Settings.ShowIcons);
                _storage.SetValue(nameof(Settings.HideTaskbarIcon), Settings.HideTaskbarIcon);
                _storage.SetValue(nameof(Settings.LaunchOnStartup), Settings.LaunchOnStartup);
                _storage.SetValue(nameof(Settings.RunAsAdministrator), Settings.RunAsAdministrator);
                _logger?.Log($"SettingsService: Saved RunAsAdministrator = {Settings.RunAsAdministrator}");

                // Hotkey
                _storage.SetValue(nameof(Settings.HotKeyModifiers), Settings.HotKeyModifiers);
                _storage.SetValue(nameof(Settings.HotKeyKey), Settings.HotKeyKey);

                // Background Polling
                _storage.SetValue(nameof(Settings.EnableBackgroundPolling), Settings.EnableBackgroundPolling);
                _storage.SetValue(nameof(Settings.BackgroundPollingIntervalSeconds), Settings.BackgroundPollingIntervalSeconds);

                // Number Shortcuts
                _storage.SetValue(nameof(Settings.EnableNumberShortcuts), Settings.EnableNumberShortcuts);
                _storage.SetValue(nameof(Settings.NumberShortcutModifier), Settings.NumberShortcutModifier);

                // Badge Animations
                _storage.SetValue(nameof(Settings.EnableBadgeAnimations), Settings.EnableBadgeAnimations);

                // Refresh Behavior
                _storage.SetValue(nameof(Settings.RefreshBehavior), Settings.RefreshBehavior);

                // Regex Cache Size
                _storage.SetValue(nameof(Settings.RegexCacheSize), Settings.RegexCacheSize);

                // Fuzzy Search
                _storage.SetValue(nameof(Settings.EnableFuzzySearch), Settings.EnableFuzzySearch);

                // UIA Worker Timeout
                _storage.SetValue(nameof(Settings.UiaWorkerTimeoutSeconds), Settings.UiaWorkerTimeoutSeconds);

                // Flush to ensure all writes are committed
                _storage.Flush();

                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to save settings", ex);
            }

            // Sync startup registry entry via startup manager
            UpdateStartupRegistryEntry();
        }

        private void UpdateStartupRegistryEntry()
        {
            if (Settings.LaunchOnStartup)
            {
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    _startupManager.EnableStartup(exePath);
                }
            }
            else
            {
                _startupManager.DisableStartup();
            }
        }

        /// <summary>
        /// Checks if the application is currently set to run at Windows startup.
        /// Delegates to the startup manager.
        /// </summary>
        public bool IsStartupEnabled() => _startupManager.IsStartupEnabled();
    }
}
