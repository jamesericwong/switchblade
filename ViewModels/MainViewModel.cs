using System;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IWindowListViewModel
    {
        private readonly IWindowOrchestrationService _orchestrationService;
        private readonly IWindowSearchService _searchService;
        private readonly INavigationService _navigationService;
        private readonly ISettingsService? _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private ObservableCollection<WindowItem> _filteredWindows = new();
        private WindowItem? _selectedWindow;
        private string _searchText = "";
        private bool _enablePreviews = true;
        private bool _isUpdating = false;
        private HashSet<string> _disabledPlugins = new();
        private readonly object _lock = new();

        /// <summary>Event fired when filtered results are updated.</summary>
        public event EventHandler? ResultsUpdated;

        /// <summary>Event fired when search text changes (user typing).</summary>
        public event EventHandler? SearchTextChanged;

        /// <summary>Gets the window providers from the orchestration service.</summary>
        public IReadOnlyList<IWindowProvider> WindowProviders =>
            _orchestrationService is WindowOrchestrationService wos
                ? wos.AllWindows.Select(w => w.Source!).Distinct().ToList()
                : new List<IWindowProvider>();

        // Primary constructor with all dependencies
        public MainViewModel(
            IWindowOrchestrationService orchestrationService,
            IWindowSearchService searchService,
            INavigationService navigationService,
            ISettingsService? settingsService = null,
            IDispatcherService? dispatcherService = null)
        {
            _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _settingsService = settingsService;
            _dispatcherService = dispatcherService ?? new WpfDispatcherService();

            // Subscribe to orchestration updates
            _orchestrationService.WindowListUpdated += OnWindowListUpdated;

            if (_settingsService != null)
            {
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

        // Legacy constructor for backward compatibility
        public MainViewModel(IEnumerable<IWindowProvider> windowProviders, ISettingsService? settingsService = null, IDispatcherService? dispatcherService = null, IIconService? iconService = null)
            : this(
                new WindowOrchestrationService(windowProviders, iconService),
                new WindowSearchService(new LruRegexCache(settingsService?.Settings.RegexCacheSize ?? 50)),
                new NavigationService(),
                settingsService,
                dispatcherService)
        {
        }

        private void OnWindowListUpdated(object? sender, WindowListUpdatedEventArgs e)
        {
            _dispatcherService.Invoke(() => UpdateSearch());
        }

        public double ItemHeight => _settingsService?.Settings.ItemHeight ?? 50.0;

        public bool EnablePreviews
        {
            get => _enablePreviews;
            set { _enablePreviews = value; OnPropertyChanged(); }
        }

        public bool EnableNumberShortcuts => _settingsService?.Settings.EnableNumberShortcuts ?? true;

        public string ShortcutModifierText
        {
            get
            {
                var modifier = _settingsService?.Settings.NumberShortcutModifier ?? ModifierKeyFlags.Alt;
                return ModifierKeyFlags.ToString(modifier);
            }
        }

        public bool ShowInTaskbar => !_settingsService?.Settings.HideTaskbarIcon ?? true;

        public bool ShowIcons => _settingsService?.Settings.ShowIcons ?? true;

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
                    SearchTextChanged?.Invoke(this, EventArgs.Empty);
                    UpdateSearch(resetSelection: true);
                }
            }
        }

        public async Task RefreshWindows()
        {
            HashSet<string> disabled;
            lock (_lock)
            {
                disabled = new HashSet<string>(_disabledPlugins);
            }
            await _orchestrationService.RefreshAsync(disabled);
        }

        private void UpdateSearch(bool resetSelection = false)
        {
            _isUpdating = true;
            try
            {
                // Capture current state
                IntPtr? selectedHwnd = SelectedWindow?.Hwnd;
                string? selectedTitle = SelectedWindow?.Title;
                int selectedIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;
                WindowItem? previousSelection = SelectedWindow;

                // Delegate search to service
                bool useFuzzy = _settingsService?.Settings.EnableFuzzySearch ?? true;
                var allWindows = _orchestrationService.AllWindows;
                var sortedResults = _searchService.Search(allWindows, SearchText, useFuzzy);

                // Sync collection in-place
                SyncCollection(FilteredWindows, sortedResults);

                // Update shortcut indices
                for (int i = 0; i < FilteredWindows.Count; i++)
                    FilteredWindows[i].ShortcutIndex = (i < 10) ? i : -1;

                // Delegate selection resolution to navigation service
                var behavior = _settingsService?.Settings.RefreshBehavior ?? RefreshBehavior.PreserveScroll;
                var newSelection = _navigationService.ResolveSelection(
                    FilteredWindows, selectedHwnd, selectedTitle, selectedIndex, behavior, resetSelection);

                SelectedWindow = newSelection;

                // Fire notification if selection changed meaningfully
                if (resetSelection || (SelectedWindow != previousSelection && behavior != RefreshBehavior.PreserveScroll))
                {
                    _isUpdating = false;
                    OnPropertyChanged(nameof(SelectedWindow));
                }
            }
            finally
            {
                _isUpdating = false;
                ResultsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SyncCollection(ObservableCollection<WindowItem> collection, IList<WindowItem> source)
        {
            var sourceSet = new HashSet<WindowItem>(source);
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!sourceSet.Contains(collection[i]))
                    collection.RemoveAt(i);
            }

            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                int currentIndex = collection.IndexOf(item);
                if (currentIndex == -1)
                    collection.Insert(i, item);
                else if (currentIndex != i)
                    collection.Move(currentIndex, i);
            }
        }

        public void MoveSelection(int direction)
        {
            if (FilteredWindows.Count == 0) return;
            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;
            int newIndex = _navigationService.CalculateMoveIndex(currentIndex, direction, FilteredWindows.Count);
            if (newIndex >= 0 && newIndex < FilteredWindows.Count)
                SelectedWindow = FilteredWindows[newIndex];
        }

        public void MoveSelectionToFirst()
        {
            if (FilteredWindows.Count > 0)
                SelectedWindow = FilteredWindows[0];
        }

        public void MoveSelectionToLast()
        {
            if (FilteredWindows.Count > 0)
                SelectedWindow = FilteredWindows[^1];
        }

        public void MoveSelectionByPage(int direction, int pageSize)
        {
            if (FilteredWindows.Count == 0 || pageSize <= 0) return;
            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : 0;
            int newIndex = _navigationService.CalculatePageMoveIndex(currentIndex, direction, pageSize, FilteredWindows.Count);
            if (newIndex >= 0)
                SelectedWindow = FilteredWindows[newIndex];
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
