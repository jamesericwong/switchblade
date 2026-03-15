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

        private readonly ILogger? _logger;
        private readonly IRegistryService _registryService;

        public PluginSettingsService(string pluginName, IRegistryService registryService, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be empty", nameof(pluginName));

            PluginName = pluginName;
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logger = logger;
        }

        /// <summary>
        /// Gets a value from the plugin's Registry key.
        /// </summary>
        public T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                var value = _registryService.GetCurrentUserValue(RegistryPath, key);
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
            catch (Exception ex)
            {
                _logger?.LogError($"PluginSettings[{PluginName}].GetValue('{key}')", ex);
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
                if (typeof(T) == typeof(bool))
                {
                    _registryService.SetCurrentUserValue(RegistryPath, key, (bool)(object)value! ? 1 : 0, RegistryValueKind.DWord);
                }
                else if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
                {
                    _registryService.SetCurrentUserValue(RegistryPath, key, value!, RegistryValueKind.DWord);
                }
                else
                {
                    _registryService.SetCurrentUserValue(RegistryPath, key, value?.ToString() ?? "", RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                // Ignore write errors but log them
                 _logger?.LogError($"PluginSettings[{PluginName}].SetValue('{key}')", ex);
            }
        }

        /// <summary>
        /// Gets a list of strings from the plugin's Registry key (stored as JSON).
        /// </summary>
        public List<string> GetStringList(string key, List<string>? defaultValue = null)
        {
            try
            {
                var json = _registryService.GetCurrentUserValue(RegistryPath, key) as string;
                if (string.IsNullOrEmpty(json)) return defaultValue ?? [];

                return JsonSerializer.Deserialize<List<string>>(json) ?? defaultValue ?? [];
            }
            catch (Exception ex)
            {
                _logger?.LogError($"PluginSettings[{PluginName}].GetStringList('{key}')", ex);
                return defaultValue ?? [];
            }
        }

        /// <summary>
        /// Sets a list of strings in the plugin's Registry key (stored as JSON).
        /// </summary>
        public void SetStringList(string key, List<string> value)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                _registryService.SetCurrentUserValue(RegistryPath, key, json, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                // Ignore write errors but log them
                 _logger?.LogError($"PluginSettings[{PluginName}].SetStringList('{key}')", ex);
            }
        }

        public bool SettingsExist()
        {
            return _registryService.KeyExists(RegistryPath);
        }

        /// <summary>
        /// Checks if a specific key exists in the plugin's Registry.
        /// </summary>
        public bool KeyExists(string key)
        {
            try
            {
                return _registryService.GetCurrentUserValue(RegistryPath, key) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
