using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Helper class for plugins to store and retrieve settings from Registry.
    /// Settings are stored under HKCU\Software\SwitchBlade\Plugins\{PluginName}
    /// </summary>
    public class PluginSettingsService : IPluginSettingsService
    {
        private const string BASE_REGISTRY_PATH = @"Software\SwitchBlade\Plugins";

        public string PluginName { get; }
        public string RegistryPath => $@"{BASE_REGISTRY_PATH}\{PluginName}";

        public PluginSettingsService(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be empty", nameof(pluginName));

            PluginName = pluginName;
        }

        /// <summary>
        /// Gets a value from the plugin's Registry key.
        /// </summary>
        public T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (regKey == null) return defaultValue;

                var value = regKey.GetValue(key);
                if (value == null) return defaultValue;

                // Handle type conversions
                if (typeof(T) == typeof(bool))
                    return (T)(object)Convert.ToBoolean(value);
                if (typeof(T) == typeof(int))
                    return (T)(object)Convert.ToInt32(value);
                if (typeof(T) == typeof(string))
                    return (T)(object)value.ToString()!;
                if (typeof(T) == typeof(uint))
                    return (T)(object)Convert.ToUInt32(value);

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a value in the plugin's Registry key.
        /// </summary>
        public void SetValue<T>(string key, T value)
        {
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
                if (regKey == null) return;

                if (typeof(T) == typeof(bool))
                    regKey.SetValue(key, (bool)(object)value! ? 1 : 0, RegistryValueKind.DWord);
                else if (typeof(T) == typeof(int))
                    regKey.SetValue(key, value!, RegistryValueKind.DWord);
                else if (typeof(T) == typeof(uint))
                    regKey.SetValue(key, value!, RegistryValueKind.DWord);
                else if (typeof(T) == typeof(string))
                    regKey.SetValue(key, value?.ToString() ?? "", RegistryValueKind.String);
                else
                    regKey.SetValue(key, value?.ToString() ?? "");
            }
            catch
            {
                // Ignore write errors
            }
        }

        /// <summary>
        /// Gets a list of strings from the plugin's Registry key (stored as JSON).
        /// </summary>
        public List<string> GetStringList(string key, List<string>? defaultValue = null)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (regKey == null) return defaultValue ?? new List<string>();

                var json = regKey.GetValue(key) as string;
                if (string.IsNullOrEmpty(json)) return defaultValue ?? new List<string>();

                return JsonSerializer.Deserialize<List<string>>(json) ?? defaultValue ?? new List<string>();
            }
            catch
            {
                return defaultValue ?? new List<string>();
            }
        }

        /// <summary>
        /// Sets a list of strings in the plugin's Registry key (stored as JSON).
        /// </summary>
        public void SetStringList(string key, List<string> value)
        {
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
                if (regKey == null) return;

                var json = JsonSerializer.Serialize(value);
                regKey.SetValue(key, json, RegistryValueKind.String);
            }
            catch
            {
                // Ignore write errors
            }
        }

        /// <summary>
        /// Checks if the plugin's Registry key exists.
        /// </summary>
        public bool SettingsExist()
        {
            using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return regKey != null;
        }

        /// <summary>
        /// Checks if a specific key exists in the plugin's Registry.
        /// </summary>
        public bool KeyExists(string key)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
                return regKey?.GetValue(key) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
