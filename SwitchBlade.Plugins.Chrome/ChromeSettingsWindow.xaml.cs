using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml; // WinUI namespace
using Microsoft.UI.Xaml.Controls;
using SwitchBlade.Contracts;

namespace SwitchBlade.Plugins.Chrome
{
    public sealed partial class ChromeSettingsWindow : Window
    {
        private readonly IPluginSettingsService _settingsService;
        private readonly ObservableCollection<string> _processes;

        public ChromeSettingsWindow(IPluginSettingsService settingsService, List<string> currentProcesses)
        {
            this.InitializeComponent();

            // Set simple title
            this.Title = "Chrome Plugin Settings";

            // WinUI resizing/centering is manual unless configured via AppWindow. 
            // For now, we let the OS position it or use default.

            _settingsService = settingsService;
            _processes = new ObservableCollection<string>(currentProcesses);
            ProcessList.ItemsSource = _processes;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var processName = NewProcessTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(processName) && !_processes.Contains(processName))
            {
                _processes.Add(processName);
                NewProcessTextBox.Text = string.Empty;
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessList.SelectedItem is string selected)
            {
                _processes.Remove(selected);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Save on close
            _settingsService.SetStringList("BrowserProcesses", new List<string>(_processes));
            this.Close();
        }
    }
}
