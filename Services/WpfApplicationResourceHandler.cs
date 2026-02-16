using System.Windows;
using System.Diagnostics.CodeAnalysis;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
    public class WpfApplicationResourceHandler : IApplicationResourceHandler
    {
        public void AddMergedDictionary(ResourceDictionary dictionary)
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public void RemoveMergedDictionary(ResourceDictionary dictionary)
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        }
    }
}
