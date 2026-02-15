using System.Collections.Generic;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    public interface IWindowReconciler
    {
        /// <summary>
        /// Reconciles incoming items with the global window cache for a specific provider.
        /// Returns the resolved item list. Mutates internal cache state.
        /// </summary>
        List<WindowItem> Reconcile(
            IList<WindowItem> incoming,
            IWindowProvider provider);

        /// <summary>Adds an item to both HWND and provider caches.</summary>
        void AddToCache(WindowItem item);

        /// <summary>Removes an item from both HWND and provider caches.</summary>
        void RemoveFromCache(WindowItem item);

        /// <summary>
        /// Populates icons for the given window items.
        /// Should be called asynchronously to avoid blocking the UI or orchestration lock.
        /// </summary>
        void PopulateIcons(IEnumerable<WindowItem> items);

        /// <summary>
        /// Gets the total number of cached window items (HWND + Provider records).
        /// Used for memory diagnostics.
        /// </summary>
        int CacheCount { get; }

        // Test helpers
        int GetHwndCacheCount();
        int GetProviderCacheCount();
    }
}
