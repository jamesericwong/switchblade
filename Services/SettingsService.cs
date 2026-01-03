using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace SwitchBlade.Services
{
    public class UserSettings
    {
        public List<string> ExcludedProcesses { get; set; } = new List<string> { "SwitchBlade" };
        public List<string> DisabledPlugins { get; set; } = new List<string>();

        public string CurrentTheme { get; set; } = "Light";
        
        // UI Options
        public bool EnablePreviews { get; set; } = true;
        public int FadeDurationMs { get; set; } = 200;
        public double WindowOpacity { get; set; } = 1.0;
        public double ItemHeight { get; set; } = 50.0;
        public bool ShowIcons { get; set; } = true;
        public bool HideTaskbarIcon { get; set; } = true;
        public bool LaunchOnStartup { get; set; } = false;

        // Background Polling Options
        public bool EnableBackgroundPolling { get; set; } = true;
        public int BackgroundPollingIntervalSeconds { get; set; } = 30;

        // Number Shortcuts (press 1-9, 0 to quick-switch)
        public bool EnableNumberShortcuts { get; set; } = true;
        // Modifier key for number shortcuts: Alt=1, Ctrl=2, Shift=4, Win=8, None=0
        public uint NumberShortcutModifier { get; set; } = 1; // Alt

        // List Refresh Behavior
        public RefreshBehavior RefreshBehavior { get; set; } = RefreshBehavior.PreserveScroll;


        public double WindowWidth { get; set; } = 800.0;
        public double WindowHeight { get; set; } = 600.0;

        // Hotkey Options (Defaults: Ctrl + Shift + Tab)
        // Modifiers: Alt=1, Ctrl=2, Shift=4, Win=8
        public uint HotKeyModifiers { get; set; } = 6; // Ctrl (2) + Shift (4)
        public uint HotKeyKey { get; set; } = 0x51; // VK_Q
    }

    public enum RefreshBehavior
    {
        PreserveScroll,
        PreserveIdentity,
        PreserveIndex
    }

    public class SettingsService : ISettingsService
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        private const string STARTUP_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string STARTUP_VALUE_NAME = "SwitchBlade";
        public UserSettings Settings { get; private set; }

        public event Action? SettingsChanged;

        public SettingsService()
        {
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

                        // Refresh Behavior
                        Settings.RefreshBehavior = (RefreshBehavior)GetValue<int>("RefreshBehavior", (int)RefreshBehavior.PreserveScroll);
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
            bool actualStartupEnabled = IsStartupEnabled();
            if (Settings.LaunchOnStartup != actualStartupEnabled)
            {
                Settings.LaunchOnStartup = actualStartupEnabled;
                settingsDirty = true;
            }

            CheckAndApplyStartupMarker();

            // HEAL: If we found any missing/corrupt values, save the clean state now
            if (settingsDirty)
            {
                SaveSettings();
            }
        }

        /// <summary>
        /// Checks for the EnableStartupOnFirstRun marker set by the MSI installer.
        /// If found and equals 1, enables startup and clears the marker.
        /// </summary>
        private void CheckAndApplyStartupMarker()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, writable: true))
                {
                    if (key != null)
                    {
                        object? markerValue = key.GetValue("EnableStartupOnFirstRun");
                        if (markerValue != null)
                        {
                            // Check if it's "1" (string from MSI) or 1 (integer)
                            string markerStr = markerValue.ToString() ?? "0";
                            if (markerStr == "1")
                            {
                                // Enable startup
                                Settings.LaunchOnStartup = true;
                                SaveSettings(); // This will write to Windows Run registry
                            }

                            // Always delete the marker after checking (it's a one-time flag)
                            key.DeleteValue("EnableStartupOnFirstRun", throwOnMissingValue: false);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if we can't read/delete the marker
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
                        
                        key.SetValue("HotKeyModifiers", Settings.HotKeyModifiers, RegistryValueKind.DWord);
                        key.SetValue("HotKeyKey", Settings.HotKeyKey, RegistryValueKind.DWord);

                        // Background Polling
                        key.SetValue("EnableBackgroundPolling", Settings.EnableBackgroundPolling ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("BackgroundPollingIntervalSeconds", Settings.BackgroundPollingIntervalSeconds, RegistryValueKind.DWord);

                        // Number Shortcuts
                        key.SetValue("EnableNumberShortcuts", Settings.EnableNumberShortcuts ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("NumberShortcutModifier", Settings.NumberShortcutModifier, RegistryValueKind.DWord);

                        // Refresh Behavior
                        key.SetValue("RefreshBehavior", (int)Settings.RefreshBehavior, RegistryValueKind.DWord);
                    }
                }
                SettingsChanged?.Invoke();
            }
            catch
            {
                // Handle save error
            }

            // Sync startup registry entry
            UpdateStartupRegistryEntry();
        }

        /// <summary>
        /// Updates the Windows Run registry key based on the LaunchOnStartup setting.
        /// </summary>
        private void UpdateStartupRegistryEntry()
        {
            try
            {
                using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, writable: true))
                {
                    if (runKey == null) return;

                    if (Settings.LaunchOnStartup)
                    {
                        // Get the path to the currently running executable
                        string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            // Add /minimized so app starts in background on Windows startup
                            runKey.SetValue(STARTUP_VALUE_NAME, $"\"{exePath}\" /minimized");
                        }
                    }
                    else
                    {
                        // Remove the startup entry if it exists
                        if (runKey.GetValue(STARTUP_VALUE_NAME) != null)
                        {
                            runKey.DeleteValue(STARTUP_VALUE_NAME, throwOnMissingValue: false);
                        }
                    }
                }
            }
            catch
            {
                // Handle registry access errors silently
            }
        }

        /// <summary>
        /// Checks if SwitchBlade is currently set to run at startup.
        /// </summary>
        public bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY))
                {
                    return runKey?.GetValue(STARTUP_VALUE_NAME) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
