using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using SwitchBlade.ViewModels;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using Windows.System;
using WinRT.Interop;

namespace SwitchBlade.Views
{
    public sealed partial class SettingsWindow : Window
    {
        public SettingsViewModel? ViewModel { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void HotKeyBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Logger.Log($"HotKeyBox KeyDown: {e.Key}");

            // Ignore Escape key
            if (e.Key == VirtualKey.Escape) return;

            e.Handled = true;

            // Get modifiers
            var modifiers = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            var winState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);

            uint mods = 0;
            if (modifiers.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= NativeInterop.MOD_ALT;
            if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= NativeInterop.MOD_CONTROL;
            if (shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= NativeInterop.MOD_SHIFT;
            if (winState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= NativeInterop.MOD_WIN;

            // Ignore modifier keys themselves
            if (e.Key == VirtualKey.Control || e.Key == VirtualKey.LeftControl || e.Key == VirtualKey.RightControl ||
                e.Key == VirtualKey.Menu || e.Key == VirtualKey.LeftMenu || e.Key == VirtualKey.RightMenu ||
                e.Key == VirtualKey.Shift || e.Key == VirtualKey.LeftShift || e.Key == VirtualKey.RightShift ||
                e.Key == VirtualKey.LeftWindows || e.Key == VirtualKey.RightWindows)
            {
                return;
            }

            // Convert to virtual key code
            int virtualKey = (int)e.Key;

            if (ViewModel != null)
            {
                ViewModel.UpdateHotKey(mods, (uint)virtualKey);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PluginSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PluginInfo pluginInfo)
            {
                if (pluginInfo.Provider != null && pluginInfo.HasSettings)
                {
                    var hwnd = WindowNative.GetWindowHandle(this);
                    pluginInfo.Provider.ShowSettingsDialog(hwnd);
                }
            }
        }
    }
}
