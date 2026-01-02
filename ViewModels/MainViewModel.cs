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
            set { _selectedWindow = value; OnPropertyChanged(); }
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
                    
                    // Update UI with new results from this provider
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Provider error: {ex}");
                }
            })).ToList();

            await Task.WhenAll(tasks);
        }

        private void UpdateSearch()
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
