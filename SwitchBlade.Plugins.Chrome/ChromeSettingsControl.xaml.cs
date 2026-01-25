using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.Chrome
{
    /// <summary>
    /// UserControl for Chrome plugin settings, hosted by the main application.
    /// </summary>
    public partial class ChromeSettingsControl : UserControl
    {
        private readonly IPluginSettingsService _settingsService;
        private readonly ObservableCollection<string> _processes;
        private readonly List<string> _originalProcesses;

        public ChromeSettingsControl(IPluginSettingsService settingsService, List<string> currentProcesses)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _originalProcesses = new List<string>(currentProcesses);
            _processes = new ObservableCollection<string>(currentProcesses);
            ProcessList.ItemsSource = _processes;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var sanitized = SanitizationUtils.SanitizeProcessName(NewProcessTextBox.Text);
            if (!string.IsNullOrEmpty(sanitized) && !_processes.Contains(sanitized))
            {
                _processes.Add(sanitized);
                NewProcessTextBox.Clear();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessList.SelectedItem is string selected)
            {
                _processes.Remove(selected);
            }
        }

        /// <summary>
        /// Saves current settings to storage.
        /// </summary>
        public void Save()
        {
            _settingsService.SetStringList("BrowserProcesses", new List<string>(_processes));
        }

        /// <summary>
        /// Reverts to original settings (cancel action).
        /// </summary>
        public void Cancel()
        {
            // Restore original list (no save)
            _processes.Clear();
            foreach (var p in _originalProcesses)
            {
                _processes.Add(p);
            }
        }
    }

    /// <summary>
    /// ISettingsControl implementation for Chrome plugin.
    /// </summary>
    public class ChromeSettingsControlProvider : ISettingsControl
    {
        private readonly IPluginSettingsService _settingsService;
        private readonly List<string> _currentProcesses;
        private ChromeSettingsControl? _control;

        public ChromeSettingsControlProvider(IPluginSettingsService settingsService, List<string> currentProcesses)
        {
            _settingsService = settingsService;
            _currentProcesses = currentProcesses;
        }

        public object CreateSettingsControl()
        {
            _control = new ChromeSettingsControl(_settingsService, _currentProcesses);
            return _control;
        }

        public void SaveSettings()
        {
            _control?.Save();
        }

        public void CancelSettings()
        {
            _control?.Cancel();
        }
    }
}
