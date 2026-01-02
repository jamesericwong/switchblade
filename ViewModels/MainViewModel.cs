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

namespace SwitchBlade.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly List<IWindowProvider> _windowProviders;
        private readonly SwitchBlade.Services.SettingsService? _settingsService;
        private ObservableCollection<WindowItem> _allWindows = new ObservableCollection<WindowItem>();
        private ObservableCollection<WindowItem> _filteredWindows = new ObservableCollection<WindowItem>();
        private WindowItem? _selectedWindow;
        private string _searchText = "";
        private bool _enablePreviews = true;
        private bool _isUpdating = false;

        public MainViewModel(IEnumerable<IWindowProvider> windowProviders, SwitchBlade.Services.SettingsService? settingsService = null)
        {
            _windowProviders = windowProviders.ToList();
            _settingsService = settingsService;
            _filteredWindows = new ObservableCollection<WindowItem>();
            
            if (_settingsService != null)
            {
                EnablePreviews = _settingsService.Settings.EnablePreviews;
                _settingsService.SettingsChanged += () => 
                {
                    EnablePreviews = _settingsService.Settings.EnablePreviews;
                    OnPropertyChanged(nameof(ShowInTaskbar));
                };
            }
        }

        public bool EnablePreviews
        {
            get => _enablePreviews;
            set { _enablePreviews = value; OnPropertyChanged(); }
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
            // However, we should preserve selection state across the update
            UpdateSearch();

            var tasks = _windowProviders.Select(provider => Task.Run(() =>
            {
                try
                {
                    var results = provider.GetWindows().ToList();
                    
                    // Optimization: Diff check
                    // Check if the current list for this provider is identical to the new results.
                    // If so, skip the UI update entirely to prevent flicker.
                    bool isIdentical = false;
                    
                    // We need a thread-safe snapshot or to lock, but _allWindows is bound to UI.
                    // Reading it from a background thread is risky if it's being modified.
                    // However, we can capture the items for this provider safely in the Invoke block logic
                    // OR we can do the check inside Invoke. Doing it inside Invoke is safer.
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                // Capture current selection to attempt restoration
                IntPtr? selectedHwnd = SelectedWindow?.Hwnd;

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

                // This assignment might trigger SelectedWindow = null via binding if the old selection is removed
                // But _isUpdating = true suppresses the notification, preventing flicker.
                FilteredWindows = new ObservableCollection<WindowItem>(sortedResults);

                if (FilteredWindows.Count > 0)
                {
                    // Try to find the previously selected window
                    var preservedSelection = FilteredWindows.FirstOrDefault(w => w.Hwnd == selectedHwnd);
                    if (preservedSelection != null)
                    {
                        SelectedWindow = preservedSelection;
                    }
                    else
                    {
                        // Only default to first if we didn't have a valid selection or it's gone
                        // And only if we are not actively typing (optional, but good UX)
                        // For now, simple logic: if selection lost, pick first.
                        if (SelectedWindow == null || !FilteredWindows.Contains(SelectedWindow))
                        {
                            SelectedWindow = FilteredWindows[0];
                        }
                    }
                }
                else
                {
                    SelectedWindow = null;
                }
            }
            finally
            {
                _isUpdating = false;
                // Force update now that we are stable
                OnPropertyChanged(nameof(SelectedWindow));
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
