using System;
using Microsoft.Win32;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Manages Windows startup registry entries for the application.
    /// Extracted from SettingsService to follow Single Responsibility Principle.
    /// </summary>
    public class WindowsStartupManager : IWindowsStartupManager
    {
        private const string APP_REGISTRY_KEY = @"Software\SwitchBlade";
        private const string STARTUP_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string STARTUP_VALUE_NAME = "SwitchBlade";

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void EnableStartup(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                throw new ArgumentException("Executable path cannot be null or empty.", nameof(executablePath));
            }

            try
            {
                using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, writable: true))
                {
                    if (runKey == null) return;

                    // Add /minimized so app starts in background on Windows startup
                    runKey.SetValue(STARTUP_VALUE_NAME, $"\"{executablePath}\" /minimized");
                }
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Failed to enable startup registry entry", ex);
            }
        }

        /// <inheritdoc />
        public void DisableStartup()
        {
            try
            {
                using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, writable: true))
                {
                    if (runKey == null) return;

                    if (runKey.GetValue(STARTUP_VALUE_NAME) != null)
                    {
                        runKey.DeleteValue(STARTUP_VALUE_NAME, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Failed to disable startup registry entry", ex);
            }
        }

        /// <inheritdoc />
        public bool CheckAndApplyStartupMarker()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(APP_REGISTRY_KEY, writable: true))
                {
                    if (key != null)
                    {
                        object? markerValue = key.GetValue("EnableStartupOnFirstRun");
                        if (markerValue != null)
                        {
                            // Check if it's "1" (string from MSI) or 1 (integer)
                            string markerStr = markerValue.ToString() ?? "0";
                            bool shouldEnable = markerStr == "1";

                            // Always delete the marker after checking (it's a one-time flag)
                            key.DeleteValue("EnableStartupOnFirstRun", throwOnMissingValue: false);

                            return shouldEnable;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Failed to read/delete EnableStartupOnFirstRun marker", ex);
            }

            return false;
        }
    }
}
