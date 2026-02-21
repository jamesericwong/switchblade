using System.Collections.Generic;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public interface IPluginLoader
    {
        List<IWindowProvider> LoadPlugins();
    }
}
