using System;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Service interface for managing application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>Gets the current settings instance.</summary>
        UserSettings Settings { get; }

        /// <summary>Raised when settings are saved.</summary>
        event Action? SettingsChanged;

        /// <summary>Saves current settings to persistent storage.</summary>
        void SaveSettings();

        /// <summary>Loads settings from persistent storage.</summary>
        void LoadSettings();

        /// <summary>Checks if application is set to run at Windows startup.</summary>
        bool IsStartupEnabled();
    }
}

