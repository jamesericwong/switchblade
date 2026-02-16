using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Registry-based implementation of <see cref="ISettingsStorage"/>.
    /// Consolidates all Windows Registry operations for settings persistence.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RegistrySettingsStorage : ISettingsStorage
    {
        private readonly string _registryKeyPath;

        /// <summary>
        /// Creates a new RegistrySettingsStorage with the specified registry path.
        /// </summary>
        /// <param name="registryKeyPath">The path under HKEY_CURRENT_USER, e.g., "Software\SwitchBlade".</param>
        public RegistrySettingsStorage(string registryKeyPath)
        {
            _registryKeyPath = registryKeyPath ?? throw new ArgumentNullException(nameof(registryKeyPath));
        }

        /// <inheritdoc/>
        public bool HasKey(string key)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(_registryKeyPath);
                if (regKey == null) return false;
                return regKey.GetValue(key) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(_registryKeyPath);
                if (regKey == null) return defaultValue;

                var rawValue = regKey.GetValue(key);
                if (rawValue == null) return defaultValue;

                // Handle common type conversions
                var targetType = typeof(T);

                // Boolean (stored as int 0/1)
                if (targetType == typeof(bool))
                {
                    if (rawValue is int intVal)
                        return (T)(object)(intVal != 0);
                    if (int.TryParse(rawValue.ToString(), out var parsed))
                        return (T)(object)(parsed != 0);
                    return defaultValue;
                }

                // Double (stored as string)
                if (targetType == typeof(double))
                {
                    if (double.TryParse(rawValue.ToString(), out var doubleVal))
                        return (T)(object)doubleVal;
                    return defaultValue;
                }

                // Enum
                if (targetType.IsEnum)
                {
                    if (rawValue is int enumInt)
                        return (T)Enum.ToObject(targetType, enumInt);
                    return defaultValue;
                }

                // Direct conversion for int, uint, string
                return (T)Convert.ChangeType(rawValue, targetType);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public void SetValue<T>(string key, T value)
        {
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(_registryKeyPath);
                if (regKey == null) return;

                var targetType = typeof(T);

                // Boolean -> int
                if (targetType == typeof(bool))
                {
                    regKey.SetValue(key, (bool)(object)value! ? 1 : 0, RegistryValueKind.DWord);
                }
                // Double -> string
                else if (targetType == typeof(double))
                {
                    regKey.SetValue(key, value!.ToString()!);
                }
                // Enum -> int
                else if (targetType.IsEnum)
                {
                    regKey.SetValue(key, Convert.ToInt32(value), RegistryValueKind.DWord);
                }
                // Int/uint -> DWord
                else if (targetType == typeof(int) || targetType == typeof(uint))
                {
                    regKey.SetValue(key, Convert.ToInt32(value), RegistryValueKind.DWord);
                }
                // String -> string
                else
                {
                    regKey.SetValue(key, value?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError($"Failed to set registry value '{key}'", ex);
            }
        }

        /// <inheritdoc/>
        public List<string> GetStringList(string key)
        {
            try
            {
                using var regKey = Registry.CurrentUser.OpenSubKey(_registryKeyPath);
                if (regKey == null) return new List<string>();

                var json = regKey.GetValue(key) as string;
                if (string.IsNullOrEmpty(json)) return new List<string>();

                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public void SetStringList(string key, List<string> value)
        {
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(_registryKeyPath);
                if (regKey == null) return;

                var json = JsonSerializer.Serialize(value);
                regKey.SetValue(key, json);
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError($"Failed to set registry list '{key}'", ex);
            }
        }

        /// <inheritdoc/>
        public void Flush()
        {
            try
            {
                using var regKey = Registry.CurrentUser.CreateSubKey(_registryKeyPath);
                regKey?.Flush();
            }
            catch
            {
                // Best effort
            }
        }
    }
}
