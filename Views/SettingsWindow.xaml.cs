using System.Windows;
using System.Windows.Input;
using SwitchBlade.ViewModels;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

namespace SwitchBlade.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            this.Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure the window can receive keyboard input for ESC key
            this.Focus();
            Keyboard.Focus(this);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Logger.Log($"SettingsWindow KeyDown: {e.Key}");
            if (e.Key == Key.Escape)
            {
                Logger.Log("SettingsWindow: Closing on ESC");
                this.Close();
                e.Handled = true;
            }
        }

        private void HotKeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Logger.Log($"HotKeyBox KeyDown: {e.Key}");
            // Ignore Escape key if it wasn't handled by Window (double check)
            if (e.Key == Key.Escape) return;

            e.Handled = true;

            // Get modifiers
            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) mods |= NativeInterop.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) mods |= NativeInterop.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) mods |= NativeInterop.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) mods |= NativeInterop.MOD_WIN;

            // Get the key
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Ignore modifier keys themselves
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Convert WPF Key to Virtual Key
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);

            if (DataContext is SettingsViewModel vm)
            {
                vm.UpdateHotKey(mods, (uint)virtualKey);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PluginSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is PluginInfo pluginInfo)
            {
                if (pluginInfo.Provider != null && pluginInfo.HasSettings)
                {
                    var settingsControl = pluginInfo.Provider.SettingsControl;
                    if (settingsControl != null)
                    {
                        var hostWindow = new PluginSettingsHostWindow(pluginInfo.Name, settingsControl);
                        hostWindow.Owner = this;
                        hostWindow.ShowDialog();
                    }
                }
            }
        }
    }
}
