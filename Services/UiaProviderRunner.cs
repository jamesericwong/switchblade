using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Runs UIA providers out-of-process via the UIA Worker Client with streaming.
    /// Uses a separate lock so slow UIA scans don't block core window updates.
    /// </summary>
    public class UiaProviderRunner : IProviderRunner, IDisposable
    {
        private readonly IUiaWorkerClient _uiaWorkerClient;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _uiaRefreshLock = new(1, 1);
        private bool _disposed;

        public UiaProviderRunner(IUiaWorkerClient uiaWorkerClient, ILogger? logger = null)
        {
            _uiaWorkerClient = uiaWorkerClient ?? throw new ArgumentNullException(nameof(uiaWorkerClient));
            _logger = logger;
        }

        /// <inheritdoc />
        public Task RunAsync(
            IList<IWindowProvider> providers,
            ISet<string> disabledPlugins,
            HashSet<string> handledProcesses,
            Action<IWindowProvider, List<WindowItem>> onResults)
        {
            // Non-blocking: skip if a previous UIA scan is still running.
            if (!_uiaRefreshLock.Wait(0))
            {
                _logger?.Log("UIA refresh skipped: previous UIA scan still in progress.");
                return Task.CompletedTask;
            }

            // Fire-and-forget: runs independently of the fast-path refresh.
            _ = Task.Run(async () =>
            {
                try
                {
                    var uiaDisabled = new HashSet<string>(
                        providers.Where(p => disabledPlugins.Contains(p.PluginName)).Select(p => p.PluginName),
                        StringComparer.OrdinalIgnoreCase);

                    // Build a lookup for fast provider resolution by name
                    var providerLookup = providers.ToDictionary(
                        p => p.PluginName,
                        p => p,
                        StringComparer.OrdinalIgnoreCase);

                    // Pre-build map for O(1) fallback lookup
                    var processProviderMap = BuildProcessToProviderMap(providers);

                    _logger?.Log($"[UIA] Starting streaming scan for {providers.Count} UIA providers...");

                    // Stream results as each plugin completes
                    await foreach (var pluginResult in _uiaWorkerClient.ScanStreamingAsync(uiaDisabled, handledProcesses))
                    {
                        if (pluginResult.Error != null)
                        {
                            _logger?.Log($"[UIA] Plugin {pluginResult.PluginName} error: {pluginResult.Error}");
                        }

                        // Find the provider for this plugin's results
                        if (!providerLookup.TryGetValue(pluginResult.PluginName, out var uiaProvider))
                        {
                            // Fallback: dynamically resolve by process name
                            if (pluginResult.Windows?.FirstOrDefault() is { } firstWindow
                                && processProviderMap.TryGetValue(firstWindow.ProcessName, out var resolvedProvider))
                            {
                                uiaProvider = resolvedProvider;
                            }
                        }

                        if (uiaProvider == null)
                        {
                            _logger?.Log($"[UIA] No provider found for plugin {pluginResult.PluginName}, skipping results.");
                            continue;
                        }

                        // Convert to WindowItems and set Source
                        var windowItems = pluginResult.Windows?
                            .Select(w => new WindowItem
                            {
                                Hwnd = new IntPtr(w.Hwnd),
                                Title = w.Title,
                                ProcessName = w.ProcessName,
                                ExecutablePath = w.ExecutablePath,
                                IsFallback = w.IsFallback,
                                Source = uiaProvider
                            })
                            .ToList() ?? new List<WindowItem>();

                        _logger?.Log($"[UIA] Plugin {pluginResult.PluginName} returned {windowItems.Count} windows - processing immediately.");

                        // Process and emit event IMMEDIATELY
                        onResults(uiaProvider, windowItems);
                    }

                    _logger?.Log("[UIA] Streaming scan complete.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"UIA Worker streaming error: {ex.Message}", ex);
                }
                finally
                {
                    _uiaRefreshLock.Release();
                }
            });

            return Task.CompletedTask;
        }

        private static Dictionary<string, IWindowProvider> BuildProcessToProviderMap(IList<IWindowProvider> providers)
        {
            var map = new Dictionary<string, IWindowProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in providers)
            {
                foreach (var process in provider.GetHandledProcesses())
                {
                    if (!map.ContainsKey(process))
                    {
                        map[process] = provider;
                    }
                }
            }
            return map;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _uiaRefreshLock.Dispose();
        }
    }
}
