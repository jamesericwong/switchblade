using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace SwitchBlade.Services
{
    public class UserSettings
    {
        public List<string> BrowserProcesses { get; set; } = new List<string> 
        { 
            "chrome", "msedge", "brave", "vivaldi", "opera", "opera_gx", 
            "chromium", "thorium", "iron", "epic", "yandex", "arc", "comet" 
        };
        public string CurrentTheme { get; set; } = "Light";
        
        // UI Options
        public bool EnablePreviews { get; set; } = true;
        public int FadeDurationMs { get; set; } = 200;
        public double WindowOpacity { get; set; } = 1.0;
        public double ItemHeight { get; set; } = 50.0;
        public bool ShowIcons { get; set; } = true;
        public bool HideTaskbarIcon { get; set; } = true;

        public double WindowWidth { get; set; } = 800.0;
        public double WindowHeight { get; set; } = 600.0;

        // Hotkey Options (Defaults: Ctrl + Shift + Tab)
        // Modifiers: Alt=1, Ctrl=2, Shift=4, Win=8
        public uint HotKeyModifiers { get; set; } = 6; // Ctrl (2) + Shift (4)
        public uint HotKeyKey { get; set; } = 0x51; // VK_Q
    }

    public class SettingsService
    {
        private const string REGISTRY_KEY = @"Software\SwitchBlade";
        public UserSettings Settings { get; private set; }

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

                        key.SetValue("CurrentTheme", Settings.CurrentTheme);
                        key.SetValue("EnablePreviews", Settings.EnablePreviews ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("FadeDurationMs", Settings.FadeDurationMs, RegistryValueKind.DWord);
                        key.SetValue("WindowOpacity", Settings.WindowOpacity.ToString());
                        key.SetValue("ItemHeight", Settings.ItemHeight.ToString());
                        key.SetValue("WindowWidth", Settings.WindowWidth.ToString());
                        key.SetValue("WindowHeight", Settings.WindowHeight.ToString());
                        key.SetValue("ShowIcons", Settings.ShowIcons ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("HideTaskbarIcon", Settings.HideTaskbarIcon ? 1 : 0, RegistryValueKind.DWord);
                        
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
        }
    }
}
