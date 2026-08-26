using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    public class PluginLoader : IPluginLoader
    {
        private readonly string _pluginsPath;
        private readonly ILogger? _logger;

        public PluginLoader(string pluginsPath, ILogger? logger = null)
        {
            _pluginsPath = pluginsPath;
            _logger = logger;
        }

        /// <summary>
        /// Discovers and instantiates IWindowProvider implementations from plugin DLLs.
        /// Discovery is shared with the UIA worker via <see cref="PluginDiscovery"/> so both
        /// sides agree on which assemblies count as plugins (naming convention + subfolders).
        /// Does NOT call Initialize — the caller (PluginService) is responsible for
        /// providing per-plugin contexts and initializing each provider exactly once.
        /// </summary>
        public List<IWindowProvider> LoadPlugins()
        {
            if (!Directory.Exists(_pluginsPath))
            {
                try
                {
                    Directory.CreateDirectory(_pluginsPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to create plugins directory: {_pluginsPath}", ex);
                    return new List<IWindowProvider>();
                }
            }

            var dllFiles = PluginDiscovery.EnumeratePluginDlls(_pluginsPath);
            return PluginDiscovery.DiscoverProviders(dllFiles, _logger);
        }
    }
}
