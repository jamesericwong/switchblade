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
                _settingsService.SettingsChanged += () => EnablePreviews = _settingsService.Settings.EnablePreviews;
            }
        }

        public bool EnablePreviews
        {
            get => _enablePreviews;
            set { _enablePreviews = value; OnPropertyChanged(); }
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
            var windows = await Task.Run(() =>
            {
                var list = new List<WindowItem>();
                foreach (var provider in _windowProviders)
                {
                    list.AddRange(provider.GetWindows());
                }
                return list;
            });

            _allWindows = new ObservableCollection<WindowItem>(windows);
            UpdateSearch();
        }

        private void UpdateSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredWindows = new ObservableCollection<WindowItem>(_allWindows);
            }
            else
            {
                try
                {
                    Regex regex = new Regex(SearchText, RegexOptions.IgnoreCase);
                    var results = _allWindows.Where(w => regex.IsMatch(w.Title)).ToList();
                    FilteredWindows = new ObservableCollection<WindowItem>(results);
                }
                catch (ArgumentException)
                {
                    var results = _allWindows.Where(w => w.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    FilteredWindows = new ObservableCollection<WindowItem>(results);
                }
            }

            if (FilteredWindows.Count > 0)
            {
                SelectedWindow = FilteredWindows[0];
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
