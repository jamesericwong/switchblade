using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Runs non-UIA window providers in-process using parallel tasks.
    /// These providers are fast and do not leak memory, so in-process execution is safe.
    /// </summary>
    public class InProcessProviderRunner(ILogger? logger = null) : IProviderRunner
    {
        private readonly ILogger? _logger = logger;

        /// <inheritdoc />
        public async Task RunAsync(
            IList<IWindowProvider> providers,
            IEnumerable<string> disabledPlugins,
            IEnumerable<string> handledProcesses,
            Action<IWindowProvider, List<WindowItem>> onResults)
        {
            var tasks = new List<Task>();
            foreach (var provider in providers)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        bool isDisabled = disabledPlugins.Contains(provider.PluginName);
                        var results = isDisabled ? [] : provider.GetWindows().ToList();
                        onResults(provider, results);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Provider {provider.PluginName} failed during GetWindows()", ex);
                        // Process empty results to clear stale data for this provider
                        onResults(provider, []);
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
    }
}
