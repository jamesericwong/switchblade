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

        public PluginLoader(string pluginsPath)
        {
            _pluginsPath = pluginsPath;
        }

        public List<IWindowProvider> LoadPlugins(IPluginContext context)
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
                    Logger.LogError($"Failed to create plugins directory: {_pluginsPath}", ex);
                    return providers;
                }
            }

            var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll");

            foreach (var dll in dllFiles)
            {
                try
                {
                    // Load assembly
                    // We use LoadFrom context usually for simple plugins
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
                                instance.Initialize(context);
                                providers.Add(instance);
                                Logger.Log($"Loaded plugin provider: {type.Name} from {Path.GetFileName(dll)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to instantiate plugin {type.Name}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load plugin assembly: {dll}", ex);
                }
            }

            return providers;
        }
    }
}
