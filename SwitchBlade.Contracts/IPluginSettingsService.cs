using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    public interface IPluginSettingsService
    {
        string PluginName { get; }
        // string RegistryPath { get; } // Probably internal detail? keeping off interface if possible

        T GetValue<T>(string key, T defaultValue);
        void SetValue<T>(string key, T value);
        List<string> GetStringList(string key, List<string>? defaultValue = null);
        void SetStringList(string key, List<string> value);
        bool SettingsExist();
        bool KeyExists(string key);
    }
}
