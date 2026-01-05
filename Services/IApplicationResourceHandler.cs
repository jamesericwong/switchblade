using Microsoft.UI.Xaml;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Interface for managing application resource dictionaries - WinUI version.
    /// </summary>
    public interface IApplicationResourceHandler
    {
        void AddMergedDictionary(ResourceDictionary dictionary);
        void RemoveMergedDictionary(ResourceDictionary dictionary);
    }
}
