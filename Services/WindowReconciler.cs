using System.Collections.Generic;
using System.Linq;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    public class WindowReconciler : IWindowReconciler
    {
        private readonly IIconService? _iconService;
        private readonly Dictionary<IntPtr, List<WindowItem>> _windowItemCache = new();
        // Performance optimization: Secondary index for O(1) provider lookups
        private readonly Dictionary<IWindowProvider, HashSet<WindowItem>> _providerItems = new();
        private readonly object _lock = new();

        private readonly ILogger? _logger;

        public WindowReconciler(IIconService? iconService, ILogger? logger = null)
        {
            _iconService = iconService;
            _logger = logger;
        }

        public List<WindowItem> Reconcile(IList<WindowItem> incomingItems, IWindowProvider provider)
        {
            lock (_lock)
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
                        // Icon population is now async - do NOT call PopulateIconIfMissing here

                        resolvedItems.Add(match);
                        claimedItems.Add(match);
                        unusedCacheItems.Remove(match);
                    }
                    else
                    {
                        incoming.ResetBadgeAnimation();
                        incoming.Source = provider;
                        // Icon population is now async - do NOT call PopulateIconIfMissing here

                        // Lock-free internal method — we already hold _lock
                        AddToCacheInternal(incoming);

                        resolvedItems.Add(incoming);
                        claimedItems.Add(incoming);
                    }
                }

                // Cleanup unused items using lock-free internal method
                foreach (var unused in unusedCacheItems)
                {
                    RemoveFromCacheInternal(unused);
                }

                return resolvedItems;
            }
        }

        public void PopulateIcons(IEnumerable<WindowItem> items)
        {
            if (_iconService == null) return;

            // No lock needed here - items are already reconciled and local to this list
            // Icon extraction is thread-safe and cached
            int count = 0;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();

            foreach (var item in items)
            {
                if (item.Icon == null && !string.IsNullOrEmpty(item.ExecutablePath))
                {
                    try
                    {
                        item.Icon = _iconService.GetIcon(item.ExecutablePath);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to populate icon for {item.ExecutablePath}", ex);
                    }
                }
            }

            if (count > 0 && _logger != null && SwitchBlade.Core.Logger.IsDebugEnabled)
            {
                var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
                _logger.Log($"[Perf] Populated {count} icons in {elapsed.TotalMilliseconds:F2}ms");
            }
        }

        public void AddToCache(WindowItem item)
        {
            lock (_lock)
            {
                AddToCacheInternal(item);
            }
        }

        public void RemoveFromCache(WindowItem item)
        {
            lock (_lock)
            {
                RemoveFromCacheInternal(item);
            }
        }

        /// <summary>
        /// Lock-free internal method for adding to cache.
        /// Caller must hold _lock.
        /// </summary>
        private void AddToCacheInternal(WindowItem item)
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
        /// Lock-free internal method for removing from cache.
        /// Caller must hold _lock.
        /// </summary>
        private void RemoveFromCacheInternal(WindowItem item)
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

        public int CacheCount
        {
            get
            {
                lock (_lock)
                {
                    return _windowItemCache.Count + _providerItems.Values.Sum(s => s.Count);
                }
            }
        }

        public int GetHwndCacheCount()
        {
            lock (_lock)
            {
                return _windowItemCache.Values.Sum(l => l.Count);
            }
        }

        public int GetProviderCacheCount()
        {
            lock (_lock)
            {
                return _providerItems.Values.Sum(s => s.Count);
            }
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
