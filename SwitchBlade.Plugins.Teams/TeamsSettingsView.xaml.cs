using SwitchBlade.Contracts;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SwitchBlade.Plugins.Teams
{
    public partial class TeamsSettingsView : UserControl
    {
        private readonly List<string> _processes;

        public TeamsSettingsView(List<string> processes)
        {
            InitializeComponent();
            _processes = new List<string>(processes); // Local copy

            // Populate list
            foreach (var proc in _processes)
            {
                ProcessList.Items.Add(proc);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newProc = NewProcessTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newProc) && !_processes.Contains(newProc))
            {
                _processes.Add(newProc);
                ProcessList.Items.Add(newProc);
                NewProcessTextBox.Clear();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessList.SelectedItem is string selected)
            {
                _processes.Remove(selected);
                ProcessList.Items.Remove(selected);
            }
        }

        public void Save(IPluginSettingsService settingsService)
        {
            settingsService.SetStringList("TeamsProcesses", _processes);
        }
    }

    public class TeamsSettingsControlProvider : ISettingsControl
    {
        private readonly IPluginSettingsService _settingsService;
        private readonly List<string> _currentProcesses;
        private TeamsSettingsView? _control;

        public TeamsSettingsControlProvider(IPluginSettingsService settingsService, List<string> currentProcesses)
        {
            _settingsService = settingsService;
            _currentProcesses = currentProcesses;
        }

        public object CreateSettingsControl()
        {
            _control = new TeamsSettingsView(_currentProcesses);
            return _control;
        }

        public void SaveSettings()
        {
            _control?.Save(_settingsService);
        }

        public void CancelSettings()
        {
            // Nothing to do, creates new every time
        }
    }
}
