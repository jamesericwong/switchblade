using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    public class UserSettings
    {
        public List<string> BrowserProcesses { get; set; } = new List<string> 
        { 
            "chrome", "msedge", "brave", "vivaldi", "opera", "opera_gx", 
            "chromium", "thorium", "iron", "epic", "yandex", "arc", "comet" 
        };
        public List<string> ExcludedProcesses { get; set; } = new List<string> { "SwitchBlade" };

        public string CurrentTheme { get; set; } = "Light";
        
        // UI Options
        public bool EnablePreviews { get; set; } = true;
        public int FadeDurationMs { get; set; } = 200;
        public double WindowOpacity { get; set; } = 1.0;
        public double ItemHeight { get; set; } = 50.0;
        public bool ShowIcons { get; set; } = true;
        public bool HideTaskbarIcon { get; set; } = true;
        public bool LaunchOnStartup { get; set; } = false;

        public double WindowWidth { get; set; } = 800.0;
        public double WindowHeight { get; set; } = 600.0;

        // Hotkey Options (Defaults: Ctrl + Shift + Tab)
        // Modifiers: Alt=1, Ctrl=2, Shift=4, Win=8
        public uint HotKeyModifiers { get; set; } = 6; // Ctrl (2) + Shift (4)
        public uint HotKeyKey { get; set; } = 0x51; // VK_Q
    }

    public class SettingsService : IBrowserSettingsProvider
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        private const string STARTUP_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string STARTUP_VALUE_NAME = "SwitchBlade";
        public UserSettings Settings { get; private set; }
        
        // Interface Implementation
        public List<string> BrowserProcesses => Settings.BrowserProcesses;

        public event Action? SettingsChanged;

        public SettingsService()
        {
            Settings = new UserSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
            {
                if (key != null)
                {
                    try
                    {
                        // Browser Processes
                        string? browsersJson = key.GetValue("BrowserProcesses") as string;
                        if (!string.IsNullOrEmpty(browsersJson))
                        {
                            var loaded = JsonSerializer.Deserialize<List<string>>(browsersJson);
                            if (loaded != null && loaded.Count > 0)
                            {
                                Settings.BrowserProcesses = loaded;
                            }
                        }

                        // Excluded Processes
                        string? excludedJson = key.GetValue("ExcludedProcesses") as string;
                        if (!string.IsNullOrEmpty(excludedJson))
                        {
                            var loaded = JsonSerializer.Deserialize<List<string>>(excludedJson);
                            if (loaded != null && loaded.Count > 0)
                            {
                                Settings.ExcludedProcesses = loaded;
                            }
                        }

                        // Theme
                        Settings.CurrentTheme = key.GetValue("CurrentTheme", "Light") as string ?? "Light";

                        // UI Options
                        Settings.EnablePreviews = Convert.ToBoolean(key.GetValue("EnablePreviews", 1));
                        Settings.FadeDurationMs = Convert.ToInt32(key.GetValue("FadeDurationMs", 200));
                        
                        // Handle opacity as string because Registry doesn't support double natively well
                        string opacityStr = key.GetValue("WindowOpacity", "1.0") as string ?? "1.0";
                        if (double.TryParse(opacityStr, out double opacity)) Settings.WindowOpacity = opacity;

                        string heightStr = key.GetValue("ItemHeight", "50.0") as string ?? "50.0";
                        if (double.TryParse(heightStr, out double height)) Settings.ItemHeight = height;

                        string widthStr = key.GetValue("WindowWidth", "800.0") as string ?? "800.0";
                        if (double.TryParse(widthStr, out double w)) Settings.WindowWidth = w;

                        string winHeightStr = key.GetValue("WindowHeight", "600.0") as string ?? "600.0";
                        if (double.TryParse(winHeightStr, out double h)) Settings.WindowHeight = h;

                        
                        Settings.ShowIcons = Convert.ToBoolean(key.GetValue("ShowIcons", 1));
                        Settings.HideTaskbarIcon = Convert.ToBoolean(key.GetValue("HideTaskbarIcon", 1));
                        Settings.LaunchOnStartup = Convert.ToBoolean(key.GetValue("LaunchOnStartup", 0));

                        // Hotkey
                        Settings.HotKeyModifiers = Convert.ToUInt32(key.GetValue("HotKeyModifiers", 6));
                        Settings.HotKeyKey = Convert.ToUInt32(key.GetValue("HotKeyKey", 0x09));
                    }
                    catch
                    {
                        // Fallback to defaults if registry read fails or data is corrupt
                    }
                }
                else
                {
                    // Key doesn't exist, seed with defaults
                    SaveSettings();
                }
            }

            // Sync LaunchOnStartup with actual Windows Run registry state
            // This handles the case where MSI installer set startup but our settings don't know about it
            bool actualStartupEnabled = IsStartupEnabled();
            if (Settings.LaunchOnStartup != actualStartupEnabled)
            {
                Settings.LaunchOnStartup = actualStartupEnabled;
            }

            // Check for MSI installer startup marker (EnableStartupOnFirstRun)
            // If the installer set this to 1, enable startup and clear the marker
            CheckAndApplyStartupMarker();
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
                        string browsersJson = JsonSerializer.Serialize(Settings.BrowserProcesses);
                        key.SetValue("BrowserProcesses", browsersJson);

                        string excludedJson = JsonSerializer.Serialize(Settings.ExcludedProcesses);
                        key.SetValue("ExcludedProcesses", excludedJson);

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
