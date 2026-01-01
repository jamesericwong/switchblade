using System.Windows;
using System.Windows.Input;
using SwitchBlade.ViewModels;
using SwitchBlade.Core; 

namespace SwitchBlade.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void HotKeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            // Get modifiers
            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) mods |= Interop.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) mods |= Interop.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) mods |= Interop.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) mods |= Interop.MOD_WIN;

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
    }
}
