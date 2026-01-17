using System;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IWindowListViewModel
    {
        private readonly List<IWindowProvider> _windowProviders;
        private readonly ISettingsService? _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly IIconService? _iconService;
        private ObservableCollection<WindowItem> _allWindows = new ObservableCollection<WindowItem>();
        private ObservableCollection<WindowItem> _filteredWindows = new ObservableCollection<WindowItem>();
        private WindowItem? _selectedWindow;
        private string _searchText = "";
        private bool _enablePreviews = true;
        private bool _isUpdating = false;
        private HashSet<string> _disabledPlugins = new HashSet<string>();
        private readonly object _lock = new object();
        private Dictionary<IntPtr, List<WindowItem>> _windowItemCache;
        private readonly Dictionary<string, Regex> _regexCache = new();
        private readonly LinkedList<string> _regexLruList = new();

        /// <summary>Event fired when filtered results are updated.</summary>
        public event EventHandler? ResultsUpdated;

        /// <summary>Event fired when search text changes (user typing).</summary>
        public event EventHandler? SearchTextChanged;

        /// <summary>Gets the list of window providers for this ViewModel.</summary>
        public IReadOnlyList<IWindowProvider> WindowProviders => _windowProviders;

        public MainViewModel(IEnumerable<IWindowProvider> windowProviders, ISettingsService? settingsService = null, IDispatcherService? dispatcherService = null, IIconService? iconService = null)
        {
            _windowProviders = windowProviders.ToList();
            _settingsService = settingsService;
            _dispatcherService = dispatcherService ?? new WpfDispatcherService();
            _iconService = iconService;
            _filteredWindows = new ObservableCollection<WindowItem>();

            // Cache for preserving WindowItem state (e.g. HasBeenAnimated) across filter operations
            // Key: HWND. Value: List of items (to handle shared HWNDs like browser tabs)
            _windowItemCache = new Dictionary<IntPtr, List<WindowItem>>();

            if (_settingsService != null)
            {
                // Initialize disabled plugins safely
                lock (_lock)
                {
                    _disabledPlugins = new HashSet<string>(_settingsService.Settings.DisabledPlugins);
                }

                EnablePreviews = _settingsService.Settings.EnablePreviews;
                _settingsService.SettingsChanged += () =>
            {
                lock (_lock)
                {
                    _disabledPlugins = new HashSet<string>(_settingsService.Settings.DisabledPlugins);
                }
                EnablePreviews = _settingsService.Settings.EnablePreviews;
                OnPropertyChanged(nameof(ShowInTaskbar));
                OnPropertyChanged(nameof(ShowIcons));
                OnPropertyChanged(nameof(EnableNumberShortcuts));
                OnPropertyChanged(nameof(ShortcutModifierText));
                OnPropertyChanged(nameof(ItemHeight));
            };
            }
        }

        public double ItemHeight
        {
            get => _settingsService?.Settings.ItemHeight ?? 50.0;
        }

        public bool EnablePreviews
        {
            get => _enablePreviews;
            set { _enablePreviews = value; OnPropertyChanged(); }
        }

        public bool EnableNumberShortcuts
        {
            get => _settingsService?.Settings.EnableNumberShortcuts ?? true;
        }

        public string ShortcutModifierText
        {
            get
            {
                var modifier = _settingsService?.Settings.NumberShortcutModifier ?? SwitchBlade.Services.ModifierKeyFlags.Alt;
                return SwitchBlade.Services.ModifierKeyFlags.ToString(modifier);
            }
        }

        public bool ShowInTaskbar
        {
            get => !_settingsService?.Settings.HideTaskbarIcon ?? true;
        }

        public bool ShowIcons
        {
            get => _settingsService?.Settings.ShowIcons ?? true;
        }

        public ObservableCollection<WindowItem> FilteredWindows
        {
            get => _filteredWindows;
            set
            {
                if (value != null)
                {
                    _filteredWindows = value;
                    OnPropertyChanged();
                }
            }
        }

        public WindowItem? SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                if (_selectedWindow != value)
                {
                    _selectedWindow = value;
                    if (!_isUpdating)
                    {
                        OnPropertyChanged();
                    }
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    SearchTextChanged?.Invoke(this, EventArgs.Empty);
                    UpdateSearch(resetSelection: true);
                }
            }
        }

        public async Task RefreshWindows()
        {
            // Do not clear _allWindows here. 
            // We want to keep the "old" state visible until the "new" state for each provider is ready.
            // This prevents the UI from flashing blank.

            // Clear process name cache to ensure freshness for reused PIDs in this cycle
            NativeInterop.ClearProcessCache();

            UpdateSearch();

            // 1. Reload settings and Collect Exclusions
            // We need to do this sequentially or carefully to ensure we have the full list before WindowFinder runs

            var handledProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First pass: Reload settings and gather handled processes
            foreach (var provider in _windowProviders)
            {
                try
                {
                    provider.ReloadSettings();
                    foreach (var p in provider.GetHandledProcesses())
                    {
                        handledProcesses.Add(p);
                        SwitchBlade.Core.Logger.Log($"MainViewModel: Added exclusion '{p}' from {provider.PluginName}");
                    }
                }
                catch (Exception ex)
                {
                    SwitchBlade.Core.Logger.LogError($"Error reloading settings for {provider.PluginName}", ex);
                }
            }

            // 2. Inject exclusions into all providers via interface (no concrete type check)
            foreach (var provider in _windowProviders)
            {
                provider.SetExclusions(handledProcesses);
            }


            // 3. Parallel fetch
            var tasks = _windowProviders.Select(provider => Task.Run(() =>
            {
                try
                {
                    // Check if disabled
                    bool isDisabled;
                    lock (_lock)
                    {
                        isDisabled = _disabledPlugins.Contains(provider.PluginName);
                    }

                    List<WindowItem> results;
                    if (isDisabled)
                    {
                        results = new List<WindowItem>();
                    }
                    else
                    {
                        // Already reloaded settings above
                        results = provider.GetWindows().ToList();
                    }


                    // Structural diff check: detect windows added/removed vs title-only changes
                    // Title-only changes update in-place to preserve badge animation state

                    _dispatcherService.Invoke(() =>
                   {
                       var existingItems = _allWindows.Where(x => x.Source == provider).ToList();

                       // Check STRUCTURAL change (only HWNDs, not titles)
                       // This detects windows being added/removed, not title changes
                       // Optimized: Use HashSet for O(n) comparison instead of sorting
                       bool isStructurallyIdentical = false;
                       if (existingItems.Count == results.Count)
                       {
                           // Build set of existing HWNDs for O(1) lookup
                           var existingHwndSet = new HashSet<long>(existingItems.Select(x => x.Hwnd.ToInt64()));
                           isStructurallyIdentical = results.All(r => existingHwndSet.Contains(r.Hwnd.ToInt64()));
                       }

                       if (isStructurallyIdentical)
                       {
                           // Same windows exist, just update titles in-place
                           // This preserves HasBeenAnimated state (no badge re-animation)
                           bool anyTitleChanged = false;
                           foreach (var incoming in results)
                           {
                               // Find matching existing item by HWND
                               // For shared HWNDs (e.g. browser tabs), match by title if possible
                               var existing = existingItems.FirstOrDefault(e =>
                                   e.Hwnd == incoming.Hwnd && e.Title == incoming.Title);

                               if (existing == null)
                               {
                                   // Title changed - find by HWND only
                                   existing = existingItems.FirstOrDefault(e => e.Hwnd == incoming.Hwnd);
                               }

                               if (existing != null)
                               {
                                   if (existing.Title != incoming.Title)
                                   {
                                       existing.Title = incoming.Title;
                                       anyTitleChanged = true;
                                   }

                                   // Populate icon if missing
                                   if (existing.Icon == null && _iconService != null && !string.IsNullOrEmpty(incoming.ExecutablePath))
                                   {
                                       existing.Icon = _iconService.GetIcon(incoming.ExecutablePath);
                                   }
                               }
                           }

                           // Re-sort if any title changed (affects sort order)
                           if (anyTitleChanged)
                           {
                               UpdateSearch();
                           }
                       }
                       else
                       {
                           // Structural change: windows added or removed
                           // 1. Remove outdated items from this specific provider
                           for (int i = _allWindows.Count - 1; i >= 0; i--)
                           {
                               if (_allWindows[i].Source == provider)
                               {
                                   _allWindows.RemoveAt(i);
                               }
                           }

                           // 2. Add fresh items via ReconcileItems (handles badge state)
                           var reconciled = ReconcileItems(results, provider);
                           foreach (var item in reconciled)
                           {
                               _allWindows.Add(item);
                           }

                           // 3. Refresh the view to show changes and SORT
                           UpdateSearch();
                       }
                   });
                }
                catch (Exception ex)
                {
                    SwitchBlade.Core.Logger.LogError($"Provider error in RefreshWindows: {ex.Message}", ex);
                }
            })).ToList();

            await Task.WhenAll(tasks);
        }

        private void UpdateSearch(bool resetSelection = false)
        {
            _isUpdating = true;
            try
            {
                // Capture current selection state
                IntPtr? selectedHwnd = SelectedWindow?.Hwnd;
                string? selectedTitle = SelectedWindow?.Title;
                int selectedIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;

                // Clamp to valid range (in case of stale reference)
                if (selectedIndex < 0 || selectedIndex >= FilteredWindows.Count)
                {
                    selectedIndex = 0;
                }

                List<WindowItem> sortedResults;

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    sortedResults = _allWindows.ToList();
                }
                else
                {
                    bool useFuzzy = _settingsService?.Settings.EnableFuzzySearch ?? true;

                    if (useFuzzy)
                    {
                        // Fuzzy search: Score all items and filter/sort by score
                        var scored = _allWindows
                            .Select(w => new { Item = w, Score = FuzzyMatcher.Score(w.Title, SearchText) })
                            .Where(x => x.Score > 0)
                            .OrderByDescending(x => x.Score)
                            .ThenBy(x => x.Item.ProcessName)
                            .ThenBy(x => x.Item.Title)
                            .Select(x => x.Item)
                            .ToList();

                        sortedResults = scored;
                    }
                    else
                    {
                        // Legacy regex/substring matching
                        try
                        {
                            // LRU Regex Cache logic
                            if (!_regexCache.TryGetValue(SearchText, out var regex))
                            {
                                // NonBacktracking is memory-efficient and prevents ReDoS for user-provided patterns
                                // Available in .NET 7+. SwitchBlade 1.5.1+ uses .NET 9 features.
                                regex = new Regex(SearchText, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

                                // Add to cache
                                _regexCache[SearchText] = regex;
                                _regexLruList.AddFirst(SearchText);

                                // Evict if exceeded
                                int maxSize = _settingsService?.Settings.RegexCacheSize ?? 50;
                                while (_regexCache.Count > maxSize && _regexLruList.Count > 0)
                                {
                                    var last = _regexLruList.Last;
                                    if (last != null)
                                    {
                                        _regexCache.Remove(last.Value);
                                        _regexLruList.RemoveLast();
                                    }
                                }
                            }
                            else
                            {
                                // Move to front (LRU update)
                                _regexLruList.Remove(SearchText);
                                _regexLruList.AddFirst(SearchText);
                            }

                            sortedResults = _allWindows.Where(w => regex.IsMatch(w.Title)).ToList();
                        }
                        catch (Exception) // Catch all regex errors (e.g. invalid pattern)
                        {
                            sortedResults = _allWindows.Where(w => w.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                        }

                        // Apply stable sort for non-fuzzy results: Process Name -> Title -> Hwnd
                        sortedResults = sortedResults
                            .Distinct()
                            .OrderBy(w => w.ProcessName)
                            .ThenBy(w => w.Title)
                            .ThenBy(w => w.Hwnd.ToInt64())
                            .ToList();
                    }
                }

                // For fuzzy results, ensure deduplication (sorting already done above)
                if (!string.IsNullOrWhiteSpace(SearchText) && (_settingsService?.Settings.EnableFuzzySearch ?? true))
                {
                    sortedResults = sortedResults.Distinct().ToList();
                }
                else if (string.IsNullOrWhiteSpace(SearchText))
                {
                    // Apply stable sort for empty search: Process Name -> Title -> Hwnd
                    sortedResults = sortedResults
                        .Distinct()
                        .OrderBy(w => w.ProcessName)
                        .ThenBy(w => w.Title)
                        .ThenBy(w => w.Hwnd.ToInt64())
                        .ToList();
                }

                // Synchronize FilteredWindows in-place to preserve UI state (scroll/selection)
                SyncCollection(FilteredWindows, sortedResults);

                // Update shortcut indices
                for (int i = 0; i < FilteredWindows.Count; i++)
                {
                    // Only assign 0-9 indices
                    FilteredWindows[i].ShortcutIndex = (i < 10) ? i : -1;
                }

                WindowItem? previousSelection = SelectedWindow;
                bool selectionChanged = false;

                if (FilteredWindows.Count > 0)
                {
                    // If resetSelection is requested (e.g., user typed in search box),
                    // force select first item with visible highlight
                    if (resetSelection)
                    {
                        SelectedWindow = FilteredWindows[0];
                        _isUpdating = false;
                        OnPropertyChanged(nameof(SelectedWindow));
                        return;
                    }

                    var behavior = _settingsService?.Settings.RefreshBehavior ?? RefreshBehavior.PreserveScroll;

                    if (behavior == RefreshBehavior.PreserveIdentity)
                    {
                        // 1. IDENTITY: Try to find valid item by Hwnd+Title
                        var sameItem = FilteredWindows.FirstOrDefault(w =>
                            w.Hwnd == selectedHwnd && w.Title == selectedTitle);

                        if (sameItem != null)
                        {
                            SelectedWindow = sameItem;
                        }
                        else
                        {
                            // Identity lost, select first
                            SelectedWindow = FilteredWindows[0];
                        }
                        selectionChanged = (SelectedWindow != previousSelection);
                    }
                    else if (behavior == RefreshBehavior.PreserveIndex)
                    {
                        // 2. INDEX: Try to keep same numeric index
                        int newIndex = Math.Min(selectedIndex, FilteredWindows.Count - 1);
                        if (newIndex < 0) newIndex = 0;
                        SelectedWindow = FilteredWindows[newIndex];
                        selectionChanged = (SelectedWindow != previousSelection);
                    }
                    else // PreserveScroll (Default)
                    {
                        // 3. SCROLL:
                        // With in-place updates, the scroll position is naturally preserved.
                        // We update selection silently without triggering ScrollIntoView.
                        // (View reacts to PropertyChanged on SelectedWindow by calling ScrollIntoView)

                        // If there was no previous selection (e.g., fresh search), auto-select first
                        if (selectedHwnd == null || selectedHwnd == IntPtr.Zero)
                        {
                            SelectedWindow = FilteredWindows[0];
                            // Fire PropertyChanged so the UI highlights the selection
                            // But don't set selectionChanged to avoid ScrollIntoView
                            _isUpdating = false;
                            OnPropertyChanged(nameof(SelectedWindow));
                            return; // Exit early since we already fired notification
                        }
                        else
                        {
                            var sameItem = FilteredWindows.FirstOrDefault(w =>
                                w.Hwnd == selectedHwnd && w.Title == selectedTitle);

                            if (sameItem != null)
                            {
                                // Keep same item selected (silently, no scroll)
                                if (SelectedWindow != sameItem)
                                {
                                    SelectedWindow = sameItem;
                                }
                                // Do NOT mark selectionChanged to prevent ScrollIntoView
                            }
                            else
                            {
                                // Item gone. Keep index to avoid jumping wildly.
                                int newIndex = Math.Min(selectedIndex, FilteredWindows.Count - 1);
                                if (newIndex < 0) newIndex = 0;
                                SelectedWindow = FilteredWindows[newIndex];
                                // Do NOT mark selectionChanged to prevent ScrollIntoView
                            }
                        }
                    }
                }
                else
                {
                    SelectedWindow = null;
                    selectionChanged = (previousSelection != null);
                }

                // Fire PropertyChanged for non-PreserveScroll behaviors if selection changed
                // This triggers ScrollIntoView in the View
                if (selectionChanged)
                {
                    _isUpdating = false; // Allow notification to fire
                    OnPropertyChanged(nameof(SelectedWindow));
                    return; // Skip finally's _isUpdating = false
                }
            }
            finally
            {
                _isUpdating = false;
                // Fire event to notify listeners (e.g., for badge animation)
                ResultsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private List<WindowItem> ReconcileItems(IList<WindowItem> incomingItems, IWindowProvider provider)
        {
            var resolvedItems = new List<WindowItem>();
            var claimedItems = new HashSet<WindowItem>();

            // Only consider cache items belonging to this provider for removal tracking
            var unusedCacheItems = new HashSet<WindowItem>(
                _windowItemCache.Values.SelectMany(x => x).Where(w => w.Source == provider));

            foreach (var incoming in incomingItems)
            {
                WindowItem? match = null;
                List<WindowItem>? candidates = null;

                // 1. Try exact match (Hwnd + Title)
                if (_windowItemCache.TryGetValue(incoming.Hwnd, out candidates))
                {
                    // Filter candidates to find one that is NOT claimed
                    match = candidates.FirstOrDefault(w => w.Title == incoming.Title && !claimedItems.Contains(w));

                    if (match == null)
                    {
                        // Fallback logic for title changes
                        // Try to find ANY unclaimed candidate (heuristic: simple reuse)
                        match = candidates.FirstOrDefault(w => !claimedItems.Contains(w));
                    }
                }

                if (match != null)
                {
                    // Update existing
                    match.Title = incoming.Title;
                    match.ProcessName = incoming.ProcessName;
                    if (incoming.Source != null && match.Source != incoming.Source)
                    {
                        match.Source = incoming.Source;
                    }
                    else if (match.Source == null)
                    {
                        match.Source = provider;
                    }

                    // Populate icon if missing (e.g., cached item from before IconService was available)
                    if (match.Icon == null && _iconService != null && !string.IsNullOrEmpty(incoming.ExecutablePath))
                    {
                        match.Icon = _iconService.GetIcon(incoming.ExecutablePath);
                    }

                    resolvedItems.Add(match);
                    claimedItems.Add(match);
                    unusedCacheItems.Remove(match); // Mark as used
                }
                else
                {
                    // New Item. Reset state.
                    incoming.ResetBadgeAnimation();
                    incoming.Source = provider;

                    // Populate icon from executable path
                    if (_iconService != null && !string.IsNullOrEmpty(incoming.ExecutablePath))
                    {
                        incoming.Icon = _iconService.GetIcon(incoming.ExecutablePath);
                    }

                    // Add to cache
                    if (!_windowItemCache.TryGetValue(incoming.Hwnd, out var list))
                    {
                        list = new List<WindowItem>();
                        _windowItemCache[incoming.Hwnd] = list;
                    }

                    if (!list.Contains(incoming))
                    {
                        list.Add(incoming);
                    }

                    resolvedItems.Add(incoming);
                    claimedItems.Add(incoming);
                }
            }

            // Remove unused items from cache (for this provider only)
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

        /// <summary>
        /// Syncs the target collection to match the source list, preserving order.
        /// Assumes objects in 'source' are the canonical instances we want in 'target'.
        /// </summary>
        private void SyncCollection(ObservableCollection<WindowItem> collection, IList<WindowItem> source)
        {
            // 1. Remove items not in source
            var sourceSet = new HashSet<WindowItem>(source);
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!sourceSet.Contains(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }

            // 2. Add/Move items
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                int currentIndex = collection.IndexOf(item);

                if (currentIndex == -1)
                {
                    collection.Insert(i, item);
                }
                else if (currentIndex != i)
                {
                    collection.Move(currentIndex, i);
                }
            }
        }

        public void MoveSelection(int direction)
        {
            if (FilteredWindows.Count == 0) return;

            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;

            // Handle case where nothing is selected
            if (currentIndex == -1)
            {
                // Down (direction=1) -> select first item
                // Up (direction=-1) -> select last item
                SelectedWindow = direction > 0
                    ? FilteredWindows[0]
                    : FilteredWindows[FilteredWindows.Count - 1];
                return;
            }

            int newIndex = currentIndex + direction;

            if (newIndex >= 0 && newIndex < FilteredWindows.Count)
            {
                SelectedWindow = FilteredWindows[newIndex];
            }
        }

        public void MoveSelectionToFirst()
        {
            if (FilteredWindows.Count == 0) return;
            SelectedWindow = FilteredWindows[0];
        }

        public void MoveSelectionToLast()
        {
            if (FilteredWindows.Count == 0) return;
            SelectedWindow = FilteredWindows[FilteredWindows.Count - 1];
        }

        public void MoveSelectionByPage(int direction, int pageSize)
        {
            if (FilteredWindows.Count == 0 || pageSize <= 0) return;

            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : 0;
            int newIndex = currentIndex + (direction * pageSize);

            // Clamp to valid range
            newIndex = Math.Max(0, Math.Min(newIndex, FilteredWindows.Count - 1));
            SelectedWindow = FilteredWindows[newIndex];
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
