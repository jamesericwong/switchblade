using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared plugin discovery used by both the host application and the UIA worker,
    /// so the two can never diverge on which assemblies count as plugins.
    /// </summary>
    public static class PluginDiscovery
    {
        private const string PluginAssemblyPrefix = "SwitchBlade.Plugins.";

        /// <summary>
        /// True when a file name follows the plugin assembly naming convention
        /// (e.g., SwitchBlade.Plugins.Chrome.dll).
        /// </summary>
        public static bool IsPluginAssembly(string fileName)
            => !string.IsNullOrEmpty(fileName) && fileName.StartsWith(PluginAssemblyPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Recursively enumerates plugin DLLs under a Plugins directory (subfolders included),
        /// in deterministic path order. Returns an empty list when the directory does not exist.
        /// </summary>
        public static List<string> EnumeratePluginDlls(string pluginsDirectory)
        {
            if (string.IsNullOrEmpty(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
            {
                return new List<string>();
            }

            return Directory
                .GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories)
                .Where(path => IsPluginAssembly(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Loads the given plugin assemblies and instantiates every concrete IWindowProvider they contain.
        /// Failures are isolated per assembly and per type so one bad plugin cannot block the rest.
        /// </summary>
        /// <param name="dllPaths">Full paths of plugin DLLs (typically from <see cref="EnumeratePluginDlls"/>).</param>
        /// <param name="logger">Optional logger for discovery diagnostics.</param>
        /// <param name="reportTypeFailure">Optional callback invoked with a message when a provider type cannot be instantiated.</param>
        public static List<IWindowProvider> DiscoverProviders(
            IEnumerable<string> dllPaths,
            ILogger? logger = null,
            Action<string>? reportTypeFailure = null)
        {
            var providers = new List<IWindowProvider>();

            foreach (var dllPath in dllPaths)
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(dllPath);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Failed to load plugin assembly: {dllPath}", ex);
                    continue;
                }

                IEnumerable<Type> providerTypes;
                try
                {
                    providerTypes = assembly.GetTypes()
                        .Where(t => typeof(IWindowProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Failed to inspect plugin assembly: {dllPath}", ex);
                    continue;
                }

                foreach (var type in providerTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IWindowProvider instance)
                        {
                            providers.Add(instance);
                            logger?.Log($"Discovered plugin provider: {type.Name} from {Path.GetFileName(dllPath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        string message = $"Failed to instantiate plugin {type.FullName}: {ex.Message}";
                        logger?.LogError(message, ex);
                        reportTypeFailure?.Invoke(message);
                    }
                }
            }

            return providers;
        }
    }
}
