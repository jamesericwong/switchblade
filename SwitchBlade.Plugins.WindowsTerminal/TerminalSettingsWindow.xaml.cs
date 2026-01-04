using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.WindowsTerminal
{
    public partial class TerminalSettingsWindow : Window
    {
        private readonly IPluginSettingsService _settingsService;
        private readonly ObservableCollection<string> _processes;

        public TerminalSettingsWindow(IPluginSettingsService settingsService, List<string> currentProcesses)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _processes = new ObservableCollection<string>(currentProcesses);
            ProcessList.ItemsSource = _processes;

            // Enable dragging via MouseLeftButtonDown
            this.MouseLeftButtonDown += (s, e) => this.DragMove();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var processName = NewProcessTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(processName) && !_processes.Contains(processName))
            {
                _processes.Add(processName);
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.SetStringList("TerminalProcesses", new List<string>(_processes));
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
