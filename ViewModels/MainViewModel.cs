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
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly List<IWindowProvider> _windowProviders;
        private readonly ISettingsService? _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private ObservableCollection<WindowItem> _allWindows = new ObservableCollection<WindowItem>();
        private ObservableCollection<WindowItem> _filteredWindows = new ObservableCollection<WindowItem>();
        private WindowItem? _selectedWindow;
        private string _searchText = "";
        private bool _enablePreviews = true;
        private bool _isUpdating = false;
        private HashSet<string> _disabledPlugins = new HashSet<string>();
        private readonly object _lock = new object();

        /// <summary>Gets the list of window providers for this ViewModel.</summary>
        public IReadOnlyList<IWindowProvider> WindowProviders => _windowProviders;

        public MainViewModel(IEnumerable<IWindowProvider> windowProviders, ISettingsService? settingsService = null, IDispatcherService? dispatcherService = null)
        {
            _windowProviders = windowProviders.ToList();
            _settingsService = settingsService;
            _dispatcherService = dispatcherService ?? new WpfDispatcherService();
            _filteredWindows = new ObservableCollection<WindowItem>();

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

        public ObservableCollection<WindowItem> FilteredWindows
        {
            get => _filteredWindows;
            set { _filteredWindows = value; OnPropertyChanged(); }
        }

        public WindowItem? SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                _selectedWindow = value;
                if (!_isUpdating)
                {
                    OnPropertyChanged();
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
                    UpdateSearch();
                }
            }
        }

        public async Task RefreshWindows()
        {
            // Do not clear _allWindows here. 
            // We want to keep the "old" state visible until the "new" state for each provider is ready.
            // This prevents the UI from flashing blank.
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

                    // Optimization: Diff check
                    // Check if the current list for this provider is identical to the new results.
                    // If so, skip the UI update entirely to prevent flicker.
                    bool isIdentical = false;

                    // We need a thread-safe snapshot or to lock, but _allWindows is bound to UI.
                    // Reading it from a background thread is risky if it's being modified.
                    // However, we can capture the items for this provider safely in the Invoke block logic
                    // OR we can do the check inside Invoke. Doing it inside Invoke is safer.

                    _dispatcherService.Invoke(() =>
                   {
                       var existingItems = _allWindows.Where(x => x.Source == provider).ToList();

                       // Fast count check
                       if (existingItems.Count == results.Count)
                       {
                           // Deep check
                           // We cannot use ToDictionary(Hwnd) because Chrome Tabs share the same Hwnd.
                           // We need to compare the content of the collections (Bag Equality).
                           // Simplest way for small lists: Sort and SequenceEqual.

                           var existingKeys = existingItems
                              .Select(x => new { x.Hwnd, x.Title })
                              .OrderBy(x => x.Hwnd.ToInt64())
                              .ThenBy(x => x.Title)
                              .ToList();

                           var newKeys = results
                               .Select(x => new { x.Hwnd, x.Title })
                               .OrderBy(x => x.Hwnd.ToInt64())
                               .ThenBy(x => x.Title)
                               .ToList();

                           isIdentical = existingKeys.SequenceEqual(newKeys);
                       }

                       if (!isIdentical)
                       {
                           // 1. Remove outdated items from this specific provider
                           // Iterate backwards to safely remove
                           for (int i = _allWindows.Count - 1; i >= 0; i--)
                           {
                               if (_allWindows[i].Source == provider)
                               {
                                   _allWindows.RemoveAt(i);
                               }
                           }

                           // 2. Add fresh items
                           foreach (var item in results)
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

        private void UpdateSearch()
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
                    try
                    {
                        Regex regex = new Regex(SearchText, RegexOptions.IgnoreCase);
                        sortedResults = _allWindows.Where(w => regex.IsMatch(w.Title)).ToList();
                    }
                    catch (ArgumentException)
                    {
                        sortedResults = _allWindows.Where(w => w.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    }
                }

                // Apply stable sort: Process Name -> Title -> Hwnd
                sortedResults = sortedResults
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.Title)
                    .ThenBy(w => w.Hwnd.ToInt64())
                    .ToList();

                // Synchronize FilteredWindows in-place to preserve UI state (scroll/selection)
                SyncCollection(FilteredWindows, sortedResults);

                WindowItem? previousSelection = SelectedWindow;
                bool selectionChanged = false;

                if (FilteredWindows.Count > 0)
                {
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
            }
        }

        private void SyncCollection(ObservableCollection<WindowItem> collection, IList<WindowItem> target)
        {
            // Use Hwnd+Title as composite key for identity comparison
            var targetDict = target.ToDictionary(w => (w.Hwnd, w.Title), w => w);
            var existingKeysSet = new HashSet<(IntPtr, string)>(collection.Select(w => (w.Hwnd, w.Title)));
            var targetKeysSet = new HashSet<(IntPtr, string)>(target.Select(w => (w.Hwnd, w.Title)));

            // 1. Remove items not in target (by key, not reference)
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                var key = (collection[i].Hwnd, collection[i].Title);
                if (!targetKeysSet.Contains(key))
                {
                    collection.RemoveAt(i);
                }
            }

            // 2. Add/Move items to match target order
            for (int i = 0; i < target.Count; i++)
            {
                var targetItem = target[i];
                var targetKey = (targetItem.Hwnd, targetItem.Title);

                // Find if an item with this key already exists in collection
                int currentIndex = -1;
                for (int j = 0; j < collection.Count; j++)
                {
                    if (collection[j].Hwnd == targetKey.Item1 && collection[j].Title == targetKey.Item2)
                    {
                        currentIndex = j;
                        break;
                    }
                }

                if (currentIndex == -1)
                {
                    // Item not in collection, insert it
                    collection.Insert(i, targetItem);
                }
                else if (currentIndex != i)
                {
                    // Item exists but at wrong index, move it
                    collection.Move(currentIndex, i);
                }
                // If currentIndex == i, item is already in the correct position
            }
        }

        public void MoveSelection(int direction)
        {
            if (FilteredWindows.Count == 0) return;

            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;
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
