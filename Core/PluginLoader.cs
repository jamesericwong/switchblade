using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class PluginLoader
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
        /// Does NOT call Initialize — the caller (PluginService) is responsible for
        /// providing per-plugin contexts and initializing each provider exactly once.
        /// </summary>
        public List<IWindowProvider> LoadPlugins()
        {
            var providers = new List<IWindowProvider>();

            if (!Directory.Exists(_pluginsPath))
            {
                try
                {
                    Directory.CreateDirectory(_pluginsPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to create plugins directory: {_pluginsPath}", ex);
                    return providers;
                }
            }

            var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll");

            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);

                    var providerTypes = assembly.GetTypes()
                        .Where(t => typeof(IWindowProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in providerTypes)
                    {
                        try
                        {
                            var instance = Activator.CreateInstance(type) as IWindowProvider;
                            if (instance != null)
                            {
                                providers.Add(instance);
                                _logger?.Log($"Discovered plugin provider: {type.Name} from {Path.GetFileName(dll)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"Failed to instantiate plugin {type.Name}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to load plugin assembly: {dll}", ex);
                }
            }

            return providers;
        }
    }
}
