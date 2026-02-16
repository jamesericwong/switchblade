using System;
using Microsoft.Win32;
using SwitchBlade.Contracts;

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

        private readonly IRegistryService _registryService;

        public WindowsStartupManager(IRegistryService registryService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        }

        /// <inheritdoc />
        public bool IsStartupEnabled()
        {
            try
            {
                return _registryService.GetCurrentUserValue(STARTUP_REGISTRY_KEY, STARTUP_VALUE_NAME) != null;
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
                // Add /minimized so app starts in background on Windows startup
                _registryService.SetCurrentUserValue(STARTUP_REGISTRY_KEY, STARTUP_VALUE_NAME, $"\"{executablePath}\" /minimized", RegistryValueKind.String);
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
                _registryService.DeleteCurrentUserValue(STARTUP_REGISTRY_KEY, STARTUP_VALUE_NAME, false);
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
                object? markerValue = _registryService.GetCurrentUserValue(APP_REGISTRY_KEY, "EnableStartupOnFirstRun");
                if (markerValue != null)
                {
                    // Check if it's "1" (string from MSI) or 1 (integer)
                    string markerStr = markerValue.ToString() ?? "0";
                    bool shouldEnable = markerStr == "1";

                    // Always delete the marker after checking (it's a one-time flag)
                    _registryService.DeleteCurrentUserValue(APP_REGISTRY_KEY, "EnableStartupOnFirstRun", false);

                    return shouldEnable;
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
