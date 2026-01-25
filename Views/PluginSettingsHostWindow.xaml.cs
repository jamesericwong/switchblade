using System;
using System.Windows;
using System.Windows.Input;
using SwitchBlade.Contracts;

namespace SwitchBlade.Views
{
    public partial class PluginSettingsHostWindow : Window
    {
        private readonly ISettingsControl _settingsControl;

        public PluginSettingsHostWindow(string pluginName, ISettingsControl settingsControl)
        {
            InitializeComponent();
            _settingsControl = settingsControl ?? throw new ArgumentNullException(nameof(settingsControl));
            TitleText.Text = $"{pluginName} Settings";

            // Create and host the plugin's control
            object control = _settingsControl.CreateSettingsControl();
            if (control is FrameworkElement element)
            {
                SettingsContent.Content = element;
            }
            else
            {
                // Fallback for non-FrameworkElement objects
                SettingsContent.Content = control;
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _settingsControl.CancelSettings();
                this.DialogResult = false;
                this.Close();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsControl.SaveSettings();
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsControl.CancelSettings();
            this.DialogResult = false;
            this.Close();
        }
    }
}
