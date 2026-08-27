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
    public class MainViewModel : INotifyPropertyChanged, IWindowListViewModel, IDisposable
    {
        private readonly IWindowOrchestrationService _orchestrationService;
        private readonly IWindowSearchService _searchService;
        private readonly INavigationService _navigationService;
        private readonly ISettingsService? _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private bool _disposed;
        private ObservableCollection<WindowItem> _filteredWindows = [];
        private WindowItem? _selectedWindow;
        private string _searchText = "";
        private bool _enablePreviews = true;
        private bool _isUpdating = false;
        private HashSet<string> _disabledPlugins = [];
        private readonly System.Threading.Lock _settingsLock = new();
        private readonly System.Threading.Lock _updateLock = new();

        /// <summary>Event fired when filtered results are updated.</summary>
        public event EventHandler? ResultsUpdated;

        /// <summary>Event fired when search text changes (user typing).</summary>
        public event EventHandler? SearchTextChanged;

        /// <summary>
        /// Event fired when an update pass renumbered one or more rows. Handlers use this to give the affected
        /// badges a brief pulse so streamed re-sorts read as intentional updates instead of silent jumps (option C).
        /// </summary>
        public event EventHandler<IReadOnlyList<WindowItem>>? Renumbered;

        /// <summary>Event fired when settings opening is requested.</summary>
        public event EventHandler? OpenSettingsRequested;

        /// <summary>Command to open the Settings window.</summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>Gets the window providers from the orchestration service.</summary>
        public IReadOnlyList<IWindowProvider> WindowProviders =>
            [.. _orchestrationService.AllWindows
                .Where(w => w.Source != null)
                .Select(w => w.Source!)
                .Distinct()];

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
                lock (_settingsLock)
                {
                    _disabledPlugins = [.._settingsService.Settings.DisabledPlugins];
                }
                EnablePreviews = _settingsService.Settings.EnablePreviews;

                _settingsService.SettingsChanged += OnSettingsChanged;
            }

            OpenSettingsCommand = new RelayCommand(_ => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        }

        private void OnSettingsChanged()
        {
            // Only subscribed when _settingsService is non-null (see constructor).
            lock (_settingsLock)
            {
                _disabledPlugins = [.. _settingsService!.Settings.DisabledPlugins];
            }
            EnablePreviews = _settingsService.Settings.EnablePreviews;
            OnPropertyChanged(nameof(ShowInTaskbar));
            OnPropertyChanged(nameof(ShowIcons));
            OnPropertyChanged(nameof(EnableNumberShortcuts));
            OnPropertyChanged(nameof(ShortcutModifierText));
            OnPropertyChanged(nameof(ItemHeight));
            OnPropertyChanged(nameof(EnableSearchHighlighting));
            OnPropertyChanged(nameof(EnableFuzzySearch));
        }

        /// <summary>
        /// Unsubscribes from the long-lived services this view model listens to.
        /// Safe to call multiple times; required so a replaced view model does not keep
        /// receiving (and reacting to) orchestration/settings updates through stale handlers.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _orchestrationService.WindowListUpdated -= OnWindowListUpdated;

            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
        }

        private void OnWindowListUpdated(object? sender, WindowListUpdatedEventArgs e)
        {
            _dispatcherService.Invoke(() => UpdateSearch());
        }

        public double ItemHeight => _settingsService?.Settings.ItemHeight ?? 64.0;

        public bool EnablePreviews
        {
            get => _enablePreviews;
            set { _enablePreviews = value; OnPropertyChanged(nameof(EnablePreviews)); }
        }

        public bool EnableNumberShortcuts => _settingsService?.Settings.EnableNumberShortcuts ?? true;

        public bool EnableSearchHighlighting => _settingsService?.Settings.EnableSearchHighlighting ?? true;
        public string SearchHighlightColor => _settingsService?.Settings.SearchHighlightColor ?? "#FF0078D4";

        public bool EnableFuzzySearch => _settingsService?.Settings.EnableFuzzySearch ?? true;

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
                    OnPropertyChanged(nameof(FilteredWindows));
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
                        OnPropertyChanged(nameof(SelectedWindow));
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
                    OnPropertyChanged(nameof(SearchText));
                    SearchTextChanged?.Invoke(this, EventArgs.Empty);
                    UpdateSearch(resetSelection: true);
                }
            }
        }

        public async Task RefreshWindows()
        {
            HashSet<string> disabled;
            lock (_settingsLock)
            {
                disabled = [.. _disabledPlugins];
            }
            await _orchestrationService.RefreshAsync(disabled);
        }

        private void UpdateSearch(bool resetSelection = false)
        {
            lock (_updateLock)
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

                    // Update shortcut indices. Capture rows whose number changed so the view can pulse those badges
                    // (option C); unchanged rows never fire, so steady-state passes stay side-effect free.
                    List<WindowItem>? renumbered = null;
                    for (int i = 0; i < FilteredWindows.Count; i++)
                    {
                        var item = FilteredWindows[i];
                        int newIndex = (i < 10) ? i : -1;

                        if (item.ShortcutIndex != newIndex)
                        {
                            renumbered ??= [];
                            renumbered.Add(item);
                        }

                        item.ShortcutIndex = newIndex;
                    }

                    if (renumbered is not null)
                    {
                        Renumbered?.Invoke(this, renumbered);
                    }

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
                }
            }

            // Fire event outside the lock to prevent deadlocks in listeners
            ResultsUpdated?.Invoke(this, EventArgs.Empty);
        }

        private static void SyncCollection(ObservableCollection<WindowItem> collection, IList<WindowItem> source)
        {
            ObservableCollectionSync.Sync(collection, source);
        }

        public void MoveSelection(int direction)
        {
            if (FilteredWindows.Count == 0)
            {
                return;
            }

            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : -1;
            int newIndex = _navigationService.CalculateMoveIndex(currentIndex, direction, FilteredWindows.Count);
            if (newIndex >= 0 && newIndex < FilteredWindows.Count)
            {
                SelectedWindow = FilteredWindows[newIndex];
            }
        }

        public void MoveSelectionToFirst()
        {
            if (FilteredWindows.Count > 0)
            {
                SelectedWindow = FilteredWindows[0];
            }
        }

        public void MoveSelectionToLast()
        {
            if (FilteredWindows.Count > 0)
            {
                SelectedWindow = FilteredWindows[^1];
            }
        }

        public void MoveSelectionByPage(int direction, int pageSize)
        {
            if (FilteredWindows.Count == 0 || pageSize <= 0)
            {
                return;
            }

            int currentIndex = SelectedWindow != null ? FilteredWindows.IndexOf(SelectedWindow) : 0;
            int newIndex = _navigationService.CalculatePageMoveIndex(currentIndex, direction, pageSize, FilteredWindows.Count);
            if (newIndex >= 0)
            {
                SelectedWindow = FilteredWindows[newIndex];
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
