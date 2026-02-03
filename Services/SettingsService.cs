using System;
using System.Collections.Generic;

namespace SwitchBlade.Services
{
    // UserSettings and RefreshBehavior have been moved to Models/ directory

    /// <summary>
    /// Manages user settings with support for pluggable storage backends.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        private readonly ISettingsStorage _storage;
        private readonly IWindowsStartupManager _startupManager;

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
        public SettingsService(ISettingsStorage storage, IWindowsStartupManager startupManager)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            Settings = new UserSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            bool settingsDirty = false;

            // String Lists
            var excludedProcesses = _storage.GetStringList("ExcludedProcesses");
            if (!_storage.HasKey("ExcludedProcesses"))
                settingsDirty = true;

            if (excludedProcesses.Count > 0)
                Settings.ExcludedProcesses = excludedProcesses;

            var disabledPlugins = _storage.GetStringList("DisabledPlugins");
            if (!_storage.HasKey("DisabledPlugins"))
                settingsDirty = true;
            Settings.DisabledPlugins = disabledPlugins;

            // Theme
            Settings.CurrentTheme = Load("CurrentTheme", "Super Light", ref settingsDirty);

            // UI Options
            Settings.EnablePreviews = Load("EnablePreviews", true, ref settingsDirty);
            Settings.FadeDurationMs = Load("FadeDurationMs", 200, ref settingsDirty);
            Settings.WindowOpacity = Load("WindowOpacity", 1.0, ref settingsDirty);

            Settings.ItemHeight = Load("ItemHeight", 64.0, ref settingsDirty);

            Settings.WindowWidth = Load("WindowWidth", 800.0, ref settingsDirty);
            Settings.WindowHeight = Load("WindowHeight", 600.0, ref settingsDirty);

            Settings.ShowIcons = Load("ShowIcons", true, ref settingsDirty);
            Settings.HideTaskbarIcon = Load("HideTaskbarIcon", true, ref settingsDirty);
            Settings.LaunchOnStartup = Load("LaunchOnStartup", false, ref settingsDirty);
            Settings.RunAsAdministrator = Load("RunAsAdministrator", false, ref settingsDirty);
            SwitchBlade.Core.Logger.Log($"SettingsService: Loaded RunAsAdministrator = {Settings.RunAsAdministrator}");

            // Hotkey
            Settings.HotKeyModifiers = Load<uint>("HotKeyModifiers", 6, ref settingsDirty);
            Settings.HotKeyKey = Load<uint>("HotKeyKey", 0x51, ref settingsDirty);
            SwitchBlade.Core.Logger.Log($"SettingsService: Loaded HotKeyKey = {Settings.HotKeyKey}");

            // Background Polling
            Settings.EnableBackgroundPolling = Load("EnableBackgroundPolling", true, ref settingsDirty);
            Settings.BackgroundPollingIntervalSeconds = Load("BackgroundPollingIntervalSeconds", 30, ref settingsDirty);

            // Number Shortcuts
            Settings.EnableNumberShortcuts = Load("EnableNumberShortcuts", true, ref settingsDirty);
            Settings.NumberShortcutModifier = Load<uint>("NumberShortcutModifier", 1, ref settingsDirty);

            // Badge Animations
            Settings.EnableBadgeAnimations = Load("EnableBadgeAnimations", true, ref settingsDirty);

            // Refresh Behavior
            Settings.RefreshBehavior = Load("RefreshBehavior", RefreshBehavior.PreserveScroll, ref settingsDirty);

            // Regex Cache Size
            Settings.RegexCacheSize = Load("RegexCacheSize", 50, ref settingsDirty);

            // Fuzzy Search
            Settings.EnableFuzzySearch = Load("EnableFuzzySearch", true, ref settingsDirty);

            // UIA Worker Timeout
            Settings.UiaWorkerTimeoutSeconds = Load("UiaWorkerTimeoutSeconds", 60, ref settingsDirty);

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
                _storage.SetStringList("ExcludedProcesses", Settings.ExcludedProcesses);
                _storage.SetStringList("DisabledPlugins", Settings.DisabledPlugins);

                // Theme
                _storage.SetValue("CurrentTheme", Settings.CurrentTheme);

                // UI Options
                _storage.SetValue("EnablePreviews", Settings.EnablePreviews);
                _storage.SetValue("FadeDurationMs", Settings.FadeDurationMs);
                _storage.SetValue("WindowOpacity", Settings.WindowOpacity);
                _storage.SetValue("ItemHeight", Settings.ItemHeight);
                _storage.SetValue("WindowWidth", Settings.WindowWidth);
                _storage.SetValue("WindowHeight", Settings.WindowHeight);

                _storage.SetValue("ShowIcons", Settings.ShowIcons);
                _storage.SetValue("HideTaskbarIcon", Settings.HideTaskbarIcon);
                _storage.SetValue("LaunchOnStartup", Settings.LaunchOnStartup);
                _storage.SetValue("RunAsAdministrator", Settings.RunAsAdministrator);
                SwitchBlade.Core.Logger.Log($"SettingsService: Saved RunAsAdministrator = {Settings.RunAsAdministrator}");

                // Hotkey
                _storage.SetValue("HotKeyModifiers", Settings.HotKeyModifiers);
                _storage.SetValue("HotKeyKey", Settings.HotKeyKey);

                // Background Polling
                _storage.SetValue("EnableBackgroundPolling", Settings.EnableBackgroundPolling);
                _storage.SetValue("BackgroundPollingIntervalSeconds", Settings.BackgroundPollingIntervalSeconds);

                // Number Shortcuts
                _storage.SetValue("EnableNumberShortcuts", Settings.EnableNumberShortcuts);
                _storage.SetValue("NumberShortcutModifier", Settings.NumberShortcutModifier);

                // Badge Animations
                _storage.SetValue("EnableBadgeAnimations", Settings.EnableBadgeAnimations);

                // Refresh Behavior
                _storage.SetValue("RefreshBehavior", Settings.RefreshBehavior);

                // Regex Cache Size
                _storage.SetValue("RegexCacheSize", Settings.RegexCacheSize);

                // Fuzzy Search
                _storage.SetValue("EnableFuzzySearch", Settings.EnableFuzzySearch);

                // UIA Worker Timeout
                _storage.SetValue("UiaWorkerTimeoutSeconds", Settings.UiaWorkerTimeoutSeconds);

                // Flush to ensure all writes are committed
                _storage.Flush();

                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Failed to save settings", ex);
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
