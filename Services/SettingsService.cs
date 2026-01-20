using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace SwitchBlade.Services
{
    // UserSettings and RefreshBehavior have been moved to Models/ directory

    public class SettingsService : ISettingsService
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        private readonly IWindowsStartupManager _startupManager;
        public UserSettings Settings { get; private set; }

        public event Action? SettingsChanged;

        public SettingsService() : this(new WindowsStartupManager())
        {
        }

        public SettingsService(IWindowsStartupManager startupManager)
        {
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            Settings = new UserSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            bool settingsDirty = false;
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
            {
                if (key != null)
                {
                    try
                    {
                        // Helper to read/convert and mark dirty if missing
                        T GetValue<T>(string name, T defaultValue)
                        {
                            object? val = key.GetValue(name);
                            if (val == null)
                            {
                                settingsDirty = true;
                                return defaultValue;
                            }
                            try
                            {
                                return (T)Convert.ChangeType(val, typeof(T));
                            }
                            catch
                            {
                                settingsDirty = true;
                                return defaultValue;
                            }
                        }

                        // Excluded Processes
                        string? excludedJson = key.GetValue("ExcludedProcesses") as string;
                        if (!string.IsNullOrEmpty(excludedJson))
                        {
                            var loaded = JsonSerializer.Deserialize<List<string>>(excludedJson);
                            if (loaded != null && loaded.Count > 0) Settings.ExcludedProcesses = loaded;
                        }
                        else settingsDirty = true; // Ensure we save defaults if missing

                        // Disabled Plugins
                        string? disabledJson = key.GetValue("DisabledPlugins") as string;
                        if (!string.IsNullOrEmpty(disabledJson))
                        {
                            var loaded = JsonSerializer.Deserialize<List<string>>(disabledJson);
                            if (loaded != null) Settings.DisabledPlugins = loaded;
                        }
                        else settingsDirty = true;

                        // Theme
                        Settings.CurrentTheme = GetValue("CurrentTheme", "Light");

                        // UI Options
                        Settings.EnablePreviews = Convert.ToBoolean(GetValue<int>("EnablePreviews", 1));
                        Settings.FadeDurationMs = GetValue("FadeDurationMs", 200);

                        // Handle opacity/doubles
                        string opacityStr = GetValue("WindowOpacity", "1.0");
                        if (double.TryParse(opacityStr, out double opacity)) Settings.WindowOpacity = opacity;

                        string heightStr = GetValue("ItemHeight", "50.0");
                        if (double.TryParse(heightStr, out double height)) Settings.ItemHeight = height;

                        string widthStr = GetValue("WindowWidth", "800.0");
                        if (double.TryParse(widthStr, out double w)) Settings.WindowWidth = w;

                        string winHeightStr = GetValue("WindowHeight", "600.0");
                        if (double.TryParse(winHeightStr, out double h)) Settings.WindowHeight = h;

                        Settings.ShowIcons = Convert.ToBoolean(GetValue<int>("ShowIcons", 1));
                        Settings.HideTaskbarIcon = Convert.ToBoolean(GetValue<int>("HideTaskbarIcon", 1));
                        Settings.LaunchOnStartup = Convert.ToBoolean(GetValue<int>("LaunchOnStartup", 0));
                        Settings.RunAsAdministrator = Convert.ToBoolean(GetValue<int>("RunAsAdministrator", 0));
                        SwitchBlade.Core.Logger.Log($"SettingsService: Loaded RunAsAdministrator = {Settings.RunAsAdministrator}");

                        // Hotkey - Critical Fix: Ensure defaults are enforced and saved if missing
                        Settings.HotKeyModifiers = Convert.ToUInt32(GetValue<int>("HotKeyModifiers", 6));

                        var loadedKey = GetValue<int>("HotKeyKey", 0x51);
                        SwitchBlade.Core.Logger.Log($"SettingsService: Loaded HotKeyKey from Registry: {loadedKey} (Default: 81/0x51)");
                        Settings.HotKeyKey = Convert.ToUInt32(loadedKey);

                        // Background Polling
                        Settings.EnableBackgroundPolling = Convert.ToBoolean(GetValue<int>("EnableBackgroundPolling", 1));
                        Settings.BackgroundPollingIntervalSeconds = GetValue("BackgroundPollingIntervalSeconds", 30);

                        // Number Shortcuts
                        Settings.EnableNumberShortcuts = Convert.ToBoolean(GetValue<int>("EnableNumberShortcuts", 1));
                        Settings.NumberShortcutModifier = Convert.ToUInt32(GetValue<int>("NumberShortcutModifier", 1));

                        // Badge Animations
                        Settings.EnableBadgeAnimations = Convert.ToBoolean(GetValue<int>("EnableBadgeAnimations", 1));

                        // Refresh Behavior
                        Settings.RefreshBehavior = (RefreshBehavior)GetValue<int>("RefreshBehavior", (int)RefreshBehavior.PreserveScroll);

                        // Regex Cache Size
                        Settings.RegexCacheSize = GetValue("RegexCacheSize", 50);

                        // Fuzzy Search
                        Settings.EnableFuzzySearch = Convert.ToBoolean(GetValue<int>("EnableFuzzySearch", 1));
                    }
                    catch (Exception ex)
                    {
                        // Fatal read error? Reset to safe defaults and force save
                        SwitchBlade.Core.Logger.LogError("Settings load failed, resetting to defaults", ex);
                        Settings = new UserSettings(); // Reset
                        settingsDirty = true;
                    }
                }
                else
                {
                    // Key doesn't exist at all
                    settingsDirty = true;
                }
            }

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

        public void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        // Browser Processes are now managed by plugins

                        string excludedJson = JsonSerializer.Serialize(Settings.ExcludedProcesses);
                        key.SetValue("ExcludedProcesses", excludedJson);

                        string disabledJson = JsonSerializer.Serialize(Settings.DisabledPlugins);
                        key.SetValue("DisabledPlugins", disabledJson);

                        key.SetValue("CurrentTheme", Settings.CurrentTheme);
                        key.SetValue("EnablePreviews", Settings.EnablePreviews ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("FadeDurationMs", Settings.FadeDurationMs, RegistryValueKind.DWord);
                        key.SetValue("WindowOpacity", Settings.WindowOpacity.ToString());
                        key.SetValue("ItemHeight", Settings.ItemHeight.ToString());
                        key.SetValue("WindowWidth", Settings.WindowWidth.ToString());
                        key.SetValue("WindowHeight", Settings.WindowHeight.ToString());
                        key.SetValue("ShowIcons", Settings.ShowIcons ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("HideTaskbarIcon", Settings.HideTaskbarIcon ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("LaunchOnStartup", Settings.LaunchOnStartup ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("RunAsAdministrator", Settings.RunAsAdministrator ? 1 : 0, RegistryValueKind.DWord);

                        SwitchBlade.Core.Logger.Log($"SettingsService: Saved RunAsAdministrator = {Settings.RunAsAdministrator}");

                        key.SetValue("HotKeyModifiers", Settings.HotKeyModifiers, RegistryValueKind.DWord);
                        key.SetValue("HotKeyKey", Settings.HotKeyKey, RegistryValueKind.DWord);

                        // Background Polling
                        key.SetValue("EnableBackgroundPolling", Settings.EnableBackgroundPolling ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("BackgroundPollingIntervalSeconds", Settings.BackgroundPollingIntervalSeconds, RegistryValueKind.DWord);

                        // Number Shortcuts
                        key.SetValue("EnableNumberShortcuts", Settings.EnableNumberShortcuts ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("NumberShortcutModifier", Settings.NumberShortcutModifier, RegistryValueKind.DWord);

                        // Badge Animations
                        key.SetValue("EnableBadgeAnimations", Settings.EnableBadgeAnimations ? 1 : 0, RegistryValueKind.DWord);

                        // Refresh Behavior
                        key.SetValue("RefreshBehavior", (int)Settings.RefreshBehavior, RegistryValueKind.DWord);

                        // Regex Cache Size
                        key.SetValue("RegexCacheSize", Settings.RegexCacheSize, RegistryValueKind.DWord);

                        // Fuzzy Search
                        key.SetValue("EnableFuzzySearch", Settings.EnableFuzzySearch ? 1 : 0, RegistryValueKind.DWord);

                        // Flush to ensure all writes are committed before any restart
                        key.Flush();
                    }
                }
                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Failed to save settings to registry", ex);
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

