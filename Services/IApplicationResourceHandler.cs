using System.Windows;

namespace SwitchBlade.Services
{
    public interface IApplicationResourceHandler
    {
        void AddMergedDictionary(ResourceDictionary dictionary);
        void RemoveMergedDictionary(ResourceDictionary dictionary);
    }
}
