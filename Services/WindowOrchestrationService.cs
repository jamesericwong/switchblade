using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates parallel execution of window providers.
    /// Handles structural diffing, caching, and icon population (via IWindowReconciler).
    /// 
    /// UIA providers (IsUiaProvider=true) run in a separate worker process to prevent memory leaks.
    /// </summary>
    public class WindowOrchestrationService : IWindowOrchestrationService
    {
        private readonly List<IWindowProvider> _providers;
        private readonly IWindowReconciler _reconciler;
        private readonly ISettingsService _settingsService;
        private readonly UiaWorkerClient _uiaWorkerClient;
        private readonly List<WindowItem> _allWindows = new();
        
        private readonly object _lock = new();
        // Re-entrancy guard: Prevents concurrent RefreshAsync from creating RCWs faster than GC can clean
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public event EventHandler<WindowListUpdatedEventArgs>? WindowListUpdated;

        public IReadOnlyList<WindowItem> AllWindows
        {
            get
            {
                lock (_lock)
                {
                    return _allWindows.ToList();
                }
            }
        }

        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IWindowReconciler reconciler, ISettingsService? settingsService = null)
        {
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            _settingsService = settingsService!; // Can be null in tests, we handle below
            var timeoutSeconds = settingsService?.Settings.UiaWorkerTimeoutSeconds ?? 60;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            _uiaWorkerClient = new UiaWorkerClient(Logger.Instance, timeout);
        }

        // Backward compatibility constructor for tests
        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IIconService? iconService = null, ISettingsService? settingsService = null)
            : this(providers, new WindowReconciler(iconService), settingsService)
        {
        }

        public async Task RefreshAsync(ISet<string> disabledPlugins)
        {
            // Non-blocking re-entrancy guard: skip if another refresh is in progress.
            if (!await _refreshLock.WaitAsync(0))
            {
                Logger.Log("RefreshAsync skipped: scan already in progress.");
                return;
            }

            try
            {
                disabledPlugins ??= new HashSet<string>();

                // Clear process cache for fresh lookups
                NativeInterop.ClearProcessCache();

                // 1. Reload settings and gather handled processes (for all providers)
                var handledProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var provider in _providers)
                {
                    try
                    {
                        provider.ReloadSettings();
                        foreach (var p in provider.GetHandledProcesses())
                        {
                            handledProcesses.Add(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error reloading settings for {provider.PluginName}", ex);
                    }
                }

                // 2. Inject exclusions (for all providers)
                foreach (var provider in _providers)
                {
                    provider.SetExclusions(handledProcesses);
                }

                // 3. Split providers into UIA (out-of-process) and non-UIA (in-process)
                var nonUiaProviders = _providers.Where(p => !p.IsUiaProvider).ToList();
                var uiaProviders = _providers.Where(p => p.IsUiaProvider).ToList();

                var allTasks = new List<Task>();

                // 4a. Run NON-UIA providers in-process (fast, no memory leak issues)
                foreach (var provider in nonUiaProviders)
                {
                    allTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            bool isDisabled = disabledPlugins.Contains(provider.PluginName);
                            var results = isDisabled ? new List<WindowItem>() : provider.GetWindows().ToList();
                            ProcessProviderResults(provider, results);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Provider {provider.PluginName} failed during GetWindows()", ex);
                            // Process empty results to clear stale data for this provider
                            ProcessProviderResults(provider, new List<WindowItem>());
                        }
                    }));
                }

                // 4b. Run UIA providers OUT-OF-PROCESS via UiaWorkerClient with STREAMING
                // Results are processed incrementally as each plugin completes.
                if (uiaProviders.Count > 0)
                {
                    var uiaDisabled = new HashSet<string>(
                        uiaProviders.Where(p => disabledPlugins.Contains(p.PluginName)).Select(p => p.PluginName),
                        StringComparer.OrdinalIgnoreCase);

                    // Build a lookup for fast provider resolution by name
                    var providerLookup = uiaProviders.ToDictionary(
                        p => p.PluginName,
                        p => p,
                        StringComparer.OrdinalIgnoreCase);

                    // Pre-build map for O(1) fallback lookup (OCP fix)
                    var processProviderMap = BuildProcessToProviderMap(uiaProviders);

                    allTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            Logger.Log($"[UIA] Starting streaming scan for {uiaProviders.Count} UIA providers...");

                            // Stream results as each plugin completes
                            await foreach (var pluginResult in _uiaWorkerClient.ScanStreamingAsync(uiaDisabled, handledProcesses))
                            {
                                if (pluginResult.Error != null)
                                {
                                    Logger.Log($"[UIA] Plugin {pluginResult.PluginName} error: {pluginResult.Error}");
                                    // Don't continue; we still need to process empty results to clear stale data if needed
                                    // But if it's just an error string, we might not have windows.
                                    // If windows is null, use empty list.
                                }

                                // Find the provider for this plugin's results
                                if (!providerLookup.TryGetValue(pluginResult.PluginName, out var uiaProvider))
                                {
                                    // Fallback: dynamically resolve by process name using providers' GetHandledProcesses()
                                    if (pluginResult.Windows?.FirstOrDefault() is { } firstWindow
                                        && processProviderMap.TryGetValue(firstWindow.ProcessName, out var resolvedProvider))
                                    {
                                        uiaProvider = resolvedProvider;
                                    }
                                }

                                if (uiaProvider == null)
                                {
                                    Logger.Log($"[UIA] No provider found for plugin {pluginResult.PluginName}, skipping results.");
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

                                Logger.Log($"[UIA] Plugin {pluginResult.PluginName} returned {windowItems.Count} windows - processing immediately.");

                                // Process and emit event IMMEDIATELY
                                ProcessProviderResults(uiaProvider, windowItems);
                            }

                            Logger.Log("[UIA] Streaming scan complete.");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"UIA Worker streaming error: {ex.Message}", ex);
                        }
                    }));
                }

                await Task.WhenAll(allTasks);

                // 5. No more in-process UIA cleanup needed - worker process handles it!
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private Dictionary<string, IWindowProvider> BuildProcessToProviderMap(List<IWindowProvider> providers)
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

        private void ProcessProviderResults(IWindowProvider provider, List<WindowItem> results)
        {
            WindowListUpdatedEventArgs? args = null;
            lock (_lock)
            {
                // LKG PROTECTION: If incoming results are ALL fallback items,
                // preserve existing results for this provider to prevent transient
                // UIA scan failures from wiping valid cached data.
                if (results.Count > 0 && results.All(r => r.IsFallback))
                {
                    bool hasExistingRealItems = _allWindows.Any(w => w.Source == provider && !w.IsFallback);
                    if (hasExistingRealItems)
                    {
                        Logger.Log($"[LKG] {provider.PluginName}: Transient failure (only fallback items received). Preserving {_allWindows.Count(w => w.Source == provider)} existing items.");
                        args = new WindowListUpdatedEventArgs(provider, false);
                        goto EmitEvent;
                    }
                }

                // Normal path: Replace existing items with new results
                for (int i = _allWindows.Count - 1; i >= 0; i--)
                {
                    if (_allWindows[i].Source == provider)
                        _allWindows.RemoveAt(i);
                }

                var reconciled = _reconciler.Reconcile(results, provider);
                _allWindows.AddRange(reconciled);

                args = new WindowListUpdatedEventArgs(provider, true);
            }

            EmitEvent:
            if (args != null)
            {
                WindowListUpdated?.Invoke(this, args);
            }
        }

        #region Encapsulated Cache Mutators (Delegated to Reconciler)

        /// <summary>
        /// Gets the total number of cached window items (HWND + Provider records).
        /// Used for memory diagnostics.
        /// </summary>
        public int CacheCount => _reconciler.CacheCount;

        #endregion

        /// <summary>
        /// Runs an action on a dedicated STA thread and returns when the thread exits.
        /// Kept for legacy compatibility if needed, though UIA worker uses its own process.
        /// </summary>
        private static Task RunOnStaThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        #region Test Helpers (Internal)

        /// <summary>
        /// Gets the total count of items across all HWND cache lists (for testing).
        /// </summary>
        internal int GetInternalHwndCacheCount() => _reconciler.GetHwndCacheCount();

        /// <summary>
        /// Gets the total count of items across all provider sets (for testing).
        /// </summary>
        internal int GetInternalProviderCacheCount() => _reconciler.GetProviderCacheCount();

        #endregion
    }
}
