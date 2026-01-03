using System;

namespace SwitchBlade.Services
{
    public interface ISettingsService
    {
        UserSettings Settings { get; }
        event Action? SettingsChanged;
        void SaveSettings();
    }
}
