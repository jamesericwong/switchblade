using Microsoft.Win32;

namespace SwitchBlade.Contracts
{
    public interface IRegistryService
    {
        object? GetCurrentUserValue(string keyPath, string valueName);
        void SetCurrentUserValue(string keyPath, string valueName, object value, RegistryValueKind valueKind);
        void DeleteCurrentUserValue(string keyPath, string valueName, bool throwOnMissing);
    }
}
