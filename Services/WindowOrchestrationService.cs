using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates parallel execution of window providers.
    /// Handles structural diffing, caching, and icon population.
    /// </summary>
    public class WindowOrchestrationService : IWindowOrchestrationService
    {
        private readonly List<IWindowProvider> _providers;
        private readonly IIconService? _iconService;
        private readonly List<WindowItem> _allWindows = new();
        private readonly Dictionary<IntPtr, List<WindowItem>> _windowItemCache = new();
        private readonly object _lock = new();

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

        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IIconService? iconService = null)
        {
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _iconService = iconService;
        }

        public async Task RefreshAsync(ISet<string> disabledPlugins)
        {
            disabledPlugins ??= new HashSet<string>();

            // Clear process cache for fresh lookups
            NativeInterop.ClearProcessCache();

            // 1. Reload settings and gather handled processes
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

            // 2. Inject exclusions
            foreach (var provider in _providers)
            {
                provider.SetExclusions(handledProcesses);
            }

            // 3. Parallel fetch
            var tasks = _providers.Select(provider => Task.Run(() =>
            {
                try
                {
                    bool isDisabled = disabledPlugins.Contains(provider.PluginName);
                    var results = isDisabled ? new List<WindowItem>() : provider.GetWindows().ToList();

                    ProcessProviderResults(provider, results);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Provider error: {ex.Message}", ex);
                }
            })).ToList();

            await Task.WhenAll(tasks);
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
            var unusedCacheItems = new HashSet<WindowItem>(
                _windowItemCache.Values.SelectMany(x => x).Where(w => w.Source == provider));

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

                    if (!_windowItemCache.TryGetValue(incoming.Hwnd, out var list))
                    {
                        list = new List<WindowItem>();
                        _windowItemCache[incoming.Hwnd] = list;
                    }
                    if (!list.Contains(incoming))
                        list.Add(incoming);

                    resolvedItems.Add(incoming);
                    claimedItems.Add(incoming);
                }
            }

            // Cleanup
            foreach (var unused in unusedCacheItems)
            {
                if (_windowItemCache.TryGetValue(unused.Hwnd, out var list))
                {
                    list.Remove(unused);
                    if (list.Count == 0)
                        _windowItemCache.Remove(unused.Hwnd);
                }
            }

            return resolvedItems;
        }

        private void PopulateIconIfMissing(WindowItem item, string? executablePath)
        {
            if (item.Icon == null && _iconService != null && !string.IsNullOrEmpty(executablePath))
            {
                item.Icon = _iconService.GetIcon(executablePath);
            }
        }
    }
}
