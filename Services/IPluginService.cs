using System.Collections.Generic;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Manages plugin discovery and lifecycle.
    /// </summary>
    public interface IPluginService
    {
        /// <summary>
        /// Gets all loaded window providers (internal + external plugins).
        /// </summary>
        IReadOnlyList<IWindowProvider> Providers { get; }

        /// <summary>
        /// Reloads plugins from the Plugins directory.
        /// </summary>
        void ReloadPlugins();

        /// <summary>
        /// Gets plugin metadata for UI display.
        /// </summary>
        IEnumerable<PluginInfo> GetPluginInfos();
    }
}
