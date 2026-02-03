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
    /// Handles structural diffing, caching, and icon population.
    /// 
    /// UIA providers (IsUiaProvider=true) run in a separate worker process to prevent memory leaks.
    /// </summary>
    public class WindowOrchestrationService : IWindowOrchestrationService
    {
        private readonly List<IWindowProvider> _providers;
        private readonly IIconService? _iconService;
        private readonly ISettingsService _settingsService;
        private readonly UiaWorkerClient _uiaWorkerClient;
        private readonly List<WindowItem> _allWindows = new();
        private readonly Dictionary<IntPtr, List<WindowItem>> _windowItemCache = new();
        // Performance optimization: Secondary index for O(1) provider lookups
        private readonly Dictionary<IWindowProvider, HashSet<WindowItem>> _providerItems = new();
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

        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IIconService? iconService = null, ISettingsService? settingsService = null)
        {
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _iconService = iconService;
            _settingsService = settingsService!; // Can be null in tests, we handle below
            var timeoutSeconds = settingsService?.Settings.UiaWorkerTimeoutSeconds ?? 60;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            _uiaWorkerClient = new UiaWorkerClient(Logger.Instance, timeout);
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
                            Logger.LogError($"Provider {provider.PluginName} error: {ex.Message}", ex);
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
                                    continue;
                                }

                                // Find the provider for this plugin's results
                                if (!providerLookup.TryGetValue(pluginResult.PluginName, out var uiaProvider))
                                {
                                    // Fallback: try to match by process name mapping
                                    var matchedProviderName = pluginResult.Windows?.FirstOrDefault() is { } firstWindow
                                        ? GetPluginNameForProcess(firstWindow.ProcessName)
                                        : null;

                                    if (matchedProviderName != null)
                                    {
                                        providerLookup.TryGetValue(matchedProviderName, out uiaProvider);
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
                // We can remove the aggressive GC and Automation.RemoveAllEventHandlers()
                // since we never create UIA objects in this process anymore.
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Maps a process name to the plugin that handles it.
        /// Used to assign Source to windows returned from the UIA worker.
        /// </summary>
        private string GetPluginNameForProcess(string processName)
        {
            // ChromeTabFinder handles browsers
            var browserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "msedge", "brave", "vivaldi", "opera", "opera_gx",
                "chromium", "thorium", "iron", "epic", "yandex", "arc", "comet"
            };
            if (browserProcesses.Contains(processName))
                return "ChromeTabFinder";

            // WindowsTerminalPlugin handles terminals
            if (string.Equals(processName, "WindowsTerminal", StringComparison.OrdinalIgnoreCase))
                return "WindowsTerminalPlugin";

            // NotepadPlusPlusPlugin handles Notepad++
            if (string.Equals(processName, "notepad++", StringComparison.OrdinalIgnoreCase))
                return "NotepadPlusPlusPlugin";

            return "Unknown";
        }


        private void ProcessProviderResults(IWindowProvider provider, List<WindowItem> results)
        {
            WindowListUpdatedEventArgs? args = null;
            lock (_lock)
            {
                // Always perform full reconciliation to correctly handle providers 
                // where multiple items share an HWND (like Chrome tabs).

                // 1. Remove existing items for this provider from the master list
                for (int i = _allWindows.Count - 1; i >= 0; i--)
                {
                    if (_allWindows[i].Source == provider)
                        _allWindows.RemoveAt(i);
                }

                // 2. Reconcile incoming results with the global window cache
                var reconciled = ReconcileItems(results, provider);
                _allWindows.AddRange(reconciled);

                // 3. Prepare event args (invoked outside lock)
                args = new WindowListUpdatedEventArgs(provider, true); // Always signal update to be safe
            }

            if (args != null)
            {
                WindowListUpdated?.Invoke(this, args);
            }
        }

        private List<WindowItem> ReconcileItems(IList<WindowItem> incomingItems, IWindowProvider provider)
        {
            var resolvedItems = new List<WindowItem>();
            var claimedItems = new HashSet<WindowItem>();

            // Performance optimization: O(1) lookup instead of O(N) SelectMany scan
            HashSet<WindowItem> unusedCacheItems;
            if (_providerItems.TryGetValue(provider, out var existing))
            {
                unusedCacheItems = new HashSet<WindowItem>(existing);
            }
            else
            {
                unusedCacheItems = new HashSet<WindowItem>();
            }

            foreach (var incoming in incomingItems)
            {
                WindowItem? match = null;

                if (_windowItemCache.TryGetValue(incoming.Hwnd, out var candidates))
                {
                    match = candidates.FirstOrDefault(w => w.Title == incoming.Title && !claimedItems.Contains(w))
                         ?? candidates.FirstOrDefault(w => !claimedItems.Contains(w));
                }

                if (match != null)
                {
                    match.Title = incoming.Title;
                    match.ProcessName = incoming.ProcessName;
                    match.Source ??= provider;
                    PopulateIconIfMissing(match, incoming.ExecutablePath);

                    resolvedItems.Add(match);
                    claimedItems.Add(match);
                    unusedCacheItems.Remove(match);
                }
                else
                {
                    incoming.ResetBadgeAnimation();
                    incoming.Source = provider;
                    PopulateIconIfMissing(incoming, incoming.ExecutablePath);

                    // Use encapsulated mutator
                    AddToCache(incoming);

                    resolvedItems.Add(incoming);
                    claimedItems.Add(incoming);
                }
            }

            // Cleanup unused items using encapsulated mutator
            foreach (var unused in unusedCacheItems)
            {
                RemoveFromCache(unused);
            }

            return resolvedItems;
        }

        #region Encapsulated Cache Mutators

        /// <summary>
        /// Gets the total number of cached window items (HWND + Provider records).
        /// Used for memory diagnostics.
        /// </summary>
        public int CacheCount
        {
            get
            {
                lock (_lock)
                {
                    // Sum of HWND cache keys (unique windows tracked) + unique items in provider buckets
                    return _windowItemCache.Count + _providerItems.Values.Sum(s => s.Count);
                }
            }
        }

        /// <summary>
        /// Adds an item to both the HWND cache and the provider index atomically.
        /// Must be called within _lock.
        /// </summary>
        private void AddToCache(WindowItem item)
        {
            // 1. Update HWND lookup
            if (!_windowItemCache.TryGetValue(item.Hwnd, out var list))
            {
                list = new List<WindowItem>();
                _windowItemCache[item.Hwnd] = list;
            }
            if (!list.Contains(item))
                list.Add(item);

            // 2. Update Provider lookup
            if (item.Source != null)
            {
                if (!_providerItems.TryGetValue(item.Source, out var set))
                {
                    set = new HashSet<WindowItem>();
                    _providerItems[item.Source] = set;
                }
                set.Add(item);
            }
        }

        /// <summary>
        /// Removes an item from both the HWND cache and the provider index atomically.
        /// Must be called within _lock.
        /// </summary>
        private void RemoveFromCache(WindowItem item)
        {
            // 1. Remove from HWND lookup
            if (_windowItemCache.TryGetValue(item.Hwnd, out var list))
            {
                list.Remove(item);
                if (list.Count == 0)
                    _windowItemCache.Remove(item.Hwnd);
            }

            // 2. Remove from Provider lookup
            if (item.Source != null && _providerItems.TryGetValue(item.Source, out var set))
            {
                set.Remove(item);
                if (set.Count == 0)
                    _providerItems.Remove(item.Source);
            }
        }

        #endregion

        private void PopulateIconIfMissing(WindowItem item, string? executablePath)
        {
            if (item.Icon == null && _iconService != null && !string.IsNullOrEmpty(executablePath))
            {
                item.Icon = _iconService.GetIcon(executablePath);
            }
        }

        /// <summary>
        /// Runs an action on a dedicated STA thread and returns when the thread exits.
        /// 
        /// CRITICAL FOR MEMORY MANAGEMENT:
        /// UI Automation COM objects are apartment-threaded. When created on an MTA thread,
        /// the RCW release is unpredictable - GC.Collect merely schedules the release, but
        /// the actual COM Release may happen much later (or never if UIA caches hold refs).
        /// 
        /// By running on a dedicated STA thread that TERMINATES after the work is done,
        /// we force Windows to immediately release ALL COM objects that were created on
        /// that thread. This is the most reliable way to prevent UIA memory leaks.
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
        internal int GetInternalHwndCacheCount()
        {
            lock (_lock)
            {
                return _windowItemCache.Values.Sum(l => l.Count);
            }
        }

        /// <summary>
        /// Gets the total count of items across all provider sets (for testing).
        /// </summary>
        internal int GetInternalProviderCacheCount()
        {
            lock (_lock)
            {
                return _providerItems.Values.Sum(s => s.Count);
            }
        }



        #endregion
    }
}
