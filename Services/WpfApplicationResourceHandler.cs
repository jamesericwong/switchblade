using Microsoft.UI.Xaml;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Handles access to WinUI application resources.
    /// </summary>
    public class WinUIApplicationResourceHandler : IApplicationResourceHandler
    {
        public void AddMergedDictionary(ResourceDictionary dictionary)
        {
            Application.Current?.Resources?.MergedDictionaries.Add(dictionary);
        }

        public void RemoveMergedDictionary(ResourceDictionary dictionary)
        {
            Application.Current?.Resources?.MergedDictionaries.Remove(dictionary);
        }
    }
}
