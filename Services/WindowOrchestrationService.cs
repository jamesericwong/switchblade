using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates parallel execution of window providers.
    /// Handles structural diffing, caching, and icon population (via IWindowReconciler).
    /// 
    /// UIA providers (IsUiaProvider=true) run in a separate worker process to prevent memory leaks.
    /// </summary>
    public class WindowOrchestrationService : IWindowOrchestrationService, IDisposable
    {
        private readonly List<IWindowProvider> _providers;
        private readonly IWindowReconciler _reconciler;
        private readonly ISettingsService _settingsService;
        private readonly IUiaWorkerClient _uiaWorkerClient;
        private readonly INativeInteropWrapper _nativeInterop;
        private readonly ILogger? _logger;
        private readonly List<WindowItem> _allWindows = new();
        
        private readonly object _lock = new();
        // Separate re-entrancy guards: Non-UIA (fast) providers run independently of UIA (slow) providers.
        // This ensures core window title updates are never blocked by slow plugin scans.
        private readonly SemaphoreSlim _fastRefreshLock = new(1, 1);
        private readonly SemaphoreSlim _uiaRefreshLock = new(1, 1);
        private bool _disposed;

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

        public WindowOrchestrationService(
            IEnumerable<IWindowProvider> providers,
            IWindowReconciler reconciler,
            IUiaWorkerClient uiaWorkerClient,
            INativeInteropWrapper nativeInterop,
            ILogger? logger = null,
            ISettingsService? settingsService = null)
        {
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            _uiaWorkerClient = uiaWorkerClient ?? throw new ArgumentNullException(nameof(uiaWorkerClient));
            _nativeInterop = nativeInterop ?? throw new ArgumentNullException(nameof(nativeInterop));
            _logger = logger;
            _settingsService = settingsService!; // Can be null in tests, we handle below
        }

        // Backward compatibility constructor for tests
        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IIconService? iconService = null, ISettingsService? settingsService = null)
            : this(providers, new WindowReconciler(iconService), new NullUiaWorkerClient(), new SwitchBlade.Core.NativeInteropWrapper(), null, settingsService)
        {
        }

        public async Task RefreshAsync(ISet<string> disabledPlugins)
        {
            // Non-blocking re-entrancy guard for fast (Non-UIA) providers.
            // Core window title updates must never be blocked by slow UIA plugin scans.
            if (!await _fastRefreshLock.WaitAsync(0))
            {
                _logger?.Log("RefreshAsync skipped: fast-path scan already in progress.");
                return;
            }

            try
            {
                // Offload the entire refresh operation (including setup) to a background thread
                // to ensure the UI thread is never blocked by settings reloading or process enumeration.
                await Task.Run(async () =>
                {
                    disabledPlugins ??= new HashSet<string>();

                    // Clear process cache for fresh lookups
                    _nativeInterop.ClearProcessCache();

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
                            _logger?.LogError($"Error reloading settings for {provider.PluginName}", ex);
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

                    // 4a. Run NON-UIA providers in-process (fast, no memory leak issues)
                    var fastTasks = new List<Task>();
                    foreach (var provider in nonUiaProviders)
                    {
                        fastTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                bool isDisabled = disabledPlugins.Contains(provider.PluginName);
                                var results = isDisabled ? new List<WindowItem>() : provider.GetWindows().ToList();
                                ProcessProviderResults(provider, results);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError($"Provider {provider.PluginName} failed during GetWindows()", ex);
                                // Process empty results to clear stale data for this provider
                                ProcessProviderResults(provider, new List<WindowItem>());
                            }
                        }));
                    }

                    // Await ONLY the fast (Non-UIA) tasks — these must complete on the polling interval.
                    await Task.WhenAll(fastTasks);

                    // 4b. Run UIA providers OUT-OF-PROCESS via UiaWorkerClient with STREAMING.
                    // Launched as fire-and-forget under a separate lock so slow UIA scans
                    // don't block the next polling cycle's core window updates.
                    if (uiaProviders.Count > 0)
                    {
                        LaunchUiaRefresh(uiaProviders, disabledPlugins, handledProcesses);
                    }
                });
            }
            finally
            {
                _fastRefreshLock.Release();
            }
        }

        /// <summary>
        /// Launches UIA provider scanning as a fire-and-forget background task.
        /// Uses a separate lock so slow UIA scans don't block core window updates.
        /// </summary>
        private void LaunchUiaRefresh(
            List<IWindowProvider> uiaProviders,
            ISet<string> disabledPlugins,
            HashSet<string> handledProcesses)
        {
            // Non-blocking: skip if a previous UIA scan is still running.
            if (!_uiaRefreshLock.Wait(0))
            {
                _logger?.Log("UIA refresh skipped: previous UIA scan still in progress.");
                return;
            }

            // Fire-and-forget: runs independently of the fast-path refresh.
            _ = Task.Run(async () =>
            {
                try
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

                    _logger?.Log($"[UIA] Starting streaming scan for {uiaProviders.Count} UIA providers...");

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
                            // Fallback: dynamically resolve by process name using providers' GetHandledProcesses()
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
                        ProcessProviderResults(uiaProvider, windowItems);
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
            WindowListUpdatedEventArgs args = null!;
            List<WindowItem>? reconciled = null;
            lock (_lock)
            {
                // Correct Implementation:
                // Check LKG condition
                if (results.Count > 0 && results.All(r => r.IsFallback))
                {
                    bool hasExistingRealItems = _allWindows.Any(w => w.Source == provider && !w.IsFallback);
                    if (hasExistingRealItems)
                    {
                        _logger?.Log($"[LKG] {provider.PluginName}: Transient failure (only fallback items received). Preserving {_allWindows.Count(w => w.Source == provider)} existing items.");
                        
                        // DEFER Event Emission to outside the lock
                        args = new WindowListUpdatedEventArgs(provider, false);
                        goto EmitAndReturn;
                    }
                }

                long start = System.Diagnostics.Stopwatch.GetTimestamp();

                // Normal path: Replace existing items with new results
                for (int i = _allWindows.Count - 1; i >= 0; i--)
                {
                    if (_allWindows[i].Source == provider)
                        _allWindows.RemoveAt(i);
                }

                reconciled = _reconciler.Reconcile(results, provider);
                _allWindows.AddRange(reconciled);

                args = new WindowListUpdatedEventArgs(provider, true);

                 if (_logger != null && SwitchBlade.Core.Logger.IsDebugEnabled)
                {
                    var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
                    _logger.Log($"[Perf] Reconciled {reconciled.Count} items for {provider.PluginName} in {elapsed.TotalMilliseconds:F2}ms");
                }
            }

            EmitAndReturn:
            // Emit event IMMEDIATELY so UI shows text - icons will pop in later
            EmitEvent(args);

            // If we jumped here from LKG, reconciled is null/empty, so we shouldn't populate icons.
            if (reconciled != null && reconciled.Count > 0)
            {
                Task.Run(() =>
                {
                    try
                    {
                        // ... existing icon population logic ...
                        _reconciler.PopulateIcons(reconciled);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error populating icons for {provider.PluginName}", ex);
                    }
                });
            }
            return;
        }

        private void EmitEvent(WindowListUpdatedEventArgs args)
        {
            WindowListUpdated?.Invoke(this, args);
        }

        #region Encapsulated Cache Mutators (Delegated to Reconciler)

        /// <summary>
        /// Gets the total number of cached window items (HWND + Provider records).
        /// Used for memory diagnostics.
        /// </summary>
        public int CacheCount => _reconciler.CacheCount;

        #endregion

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            foreach (var provider in _providers)
            {
                if (provider is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error disposing provider {provider.PluginName}", ex);
                    }
                }
            }

            _uiaWorkerClient.Dispose();
            _fastRefreshLock.Dispose();
            _uiaRefreshLock.Dispose();
        }
    }
}
