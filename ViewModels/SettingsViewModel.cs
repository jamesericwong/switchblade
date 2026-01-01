using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SwitchBlade.Services;

namespace SwitchBlade.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private readonly ThemeService _themeService;
        private string _newProcessName = "";
        private string _selectedProcess = "";
        private string _selectedTheme;

        public ObservableCollection<string> BrowserProcesses { get; set; }
        public ObservableCollection<string> AvailableThemes { get; set; }

        public string NewProcessName
        {
            get => _newProcessName;
            set { _newProcessName = value; OnPropertyChanged(); }
        }

        public string SelectedProcess
        {
            get => _selectedProcess;
            set { _selectedProcess = value; OnPropertyChanged(); }
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set 
            { 
                if (_selectedTheme != value)
                {
                    _selectedTheme = value; 
                    OnPropertyChanged(); 
                    _themeService.ApplyTheme(_selectedTheme);
                }
            }
        }

        public bool EnablePreviews
        {
            get => _settingsService.Settings.EnablePreviews;
            set { _settingsService.Settings.EnablePreviews = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public int FadeDurationMs
        {
            get => _settingsService.Settings.FadeDurationMs;
            set { _settingsService.Settings.FadeDurationMs = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }

        public double WindowOpacity
        {
            get => _settingsService.Settings.WindowOpacity;
            set { _settingsService.Settings.WindowOpacity = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }
        
        public double ItemHeight
        {
            get => _settingsService.Settings.ItemHeight;
            set { _settingsService.Settings.ItemHeight = value; OnPropertyChanged(); _settingsService.SaveSettings(); }
        }


        public ICommand AddProcessCommand { get; }
        public ICommand RemoveProcessCommand { get; }

        public SettingsViewModel(SettingsService settingsService, ThemeService themeService)
        {
            _settingsService = settingsService;
            _themeService = themeService;

            BrowserProcesses = new ObservableCollection<string>(_settingsService.Settings.BrowserProcesses);
            AvailableThemes = new ObservableCollection<string>(_themeService.AvailableThemes.Select(t => t.Name));
            
            _selectedTheme = _settingsService.Settings.CurrentTheme;

            AddProcessCommand = new RelayCommand(_ => AddProcess(), _ => !string.IsNullOrWhiteSpace(NewProcessName));
            RemoveProcessCommand = new RelayCommand(_ => RemoveProcess(), _ => !string.IsNullOrEmpty(SelectedProcess));
        }

        private void AddProcess()
        {
            if (!BrowserProcesses.Contains(NewProcessName))
            {
                BrowserProcesses.Add(NewProcessName);
                _settingsService.Settings.BrowserProcesses.Add(NewProcessName);
                _settingsService.SaveSettings();
                NewProcessName = "";
            }
        }

        private void RemoveProcess()
        {
            if (BrowserProcesses.Contains(SelectedProcess))
            {
                BrowserProcesses.Remove(SelectedProcess);
                _settingsService.Settings.BrowserProcesses.Remove(SelectedProcess);
                _settingsService.SaveSettings();
            }
        }

        public string HotKeyString
        {
            get
            {
                var mods = (uint)_settingsService.Settings.HotKeyModifiers;
                var key = (uint)_settingsService.Settings.HotKeyKey;
                
                var parts = new System.Collections.Generic.List<string>();
                if ((mods & 1) != 0) parts.Add("Alt");
                if ((mods & 2) != 0) parts.Add("Ctrl");
                if ((mods & 4) != 0) parts.Add("Shift");
                if ((mods & 8) != 0) parts.Add("Win");
                
                parts.Add(((System.Windows.Forms.Keys)key).ToString());
                return string.Join(" + ", parts);
            }
        }

        public void UpdateHotKey(uint modifiers, uint key)
        {
            _settingsService.Settings.HotKeyModifiers = modifiers;
            _settingsService.Settings.HotKeyKey = key;
            _settingsService.SaveSettings();
            OnPropertyChanged(nameof(HotKeyString));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
