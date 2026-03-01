using Microsoft.Win32;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    public class RegistryServiceWrapper : IRegistryService
    {
        public object? GetCurrentUserValue(string keyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }

        public void SetCurrentUserValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            var key = Registry.CurrentUser.CreateSubKey(keyPath);
            try
            {
                key!.SetValue(valueName, value, valueKind);
            }
            finally
            {
                key!.Dispose();
            }
        }

        public void DeleteCurrentUserValue(string keyPath, string valueName, bool throwOnMissing)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key != null)
            {
                key.DeleteValue(valueName, throwOnMissing);
            }
        }
    }
}
