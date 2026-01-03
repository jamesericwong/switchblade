using System;
using System.Windows;
using System.Windows.Input;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Handles keyboard input for the main window.
    /// Extracted from MainWindow.xaml.cs for Single Responsibility Principle.
    /// </summary>
    public class KeyboardInputHandler
    {
        private readonly MainViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly Action _hideWindow;
        private readonly Action<WindowItem?> _activateWindow;
        private readonly Func<double> _getListBoxHeight;

        /// <summary>
        /// Creates a new keyboard input handler.
        /// </summary>
        /// <param name="viewModel">The main view model.</param>
        /// <param name="settingsService">Settings service for configuration.</param>
        /// <param name="hideWindow">Action to hide the window.</param>
        /// <param name="activateWindow">Action to activate a selected window.</param>
        /// <param name="getListBoxHeight">Function to get the current list box height for page calculations.</param>
        public KeyboardInputHandler(
            MainViewModel viewModel,
            ISettingsService settingsService,
            Action hideWindow,
            Action<WindowItem?> activateWindow,
            Func<double> getListBoxHeight)
        {
            _viewModel = viewModel;
            _settingsService = settingsService;
            _hideWindow = hideWindow;
            _activateWindow = activateWindow;
            _getListBoxHeight = getListBoxHeight;
        }

        /// <summary>
        /// Handles the PreviewKeyDown event for the main window.
        /// </summary>
        public void HandleKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only log non-character keys to avoid spam, or log special keys
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Down || e.Key == Key.Up)
            {
                Logger.Log($"KeyboardInputHandler KeyDown: {e.Key}");
            }

            if (e.Key == Key.Escape)
            {
                _hideWindow();
            }
            else if (e.Key == Key.Down)
            {
                _viewModel.MoveSelection(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _viewModel.MoveSelection(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                _activateWindow(_viewModel.SelectedWindow);
                e.Handled = true;
            }
            else if (e.Key == Key.Home && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToFirst();
                e.Handled = true;
            }
            else if (e.Key == Key.End && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToLast();
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(-1, pageSize);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(1, pageSize);
                e.Handled = true;
            }
            // Number Shortcuts Feature
            else if (_settingsService.Settings.EnableNumberShortcuts)
            {
                var settings = _settingsService.Settings;
                // Check if the required modifier key is pressed
                if (IsModifierKeyPressed(settings.NumberShortcutModifier))
                {
                    // When Alt is pressed, WPF sets e.Key to Key.System and the actual key is in e.SystemKey
                    Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
                    int? index = GetNumberKeyIndex(actualKey);
                    if (index.HasValue)
                    {
                        ActivateWindowByIndex(index.Value);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the number of items visible in the ListBox (page size).
        /// </summary>
        private int CalculatePageSize()
        {
            double itemHeight = _settingsService.Settings.ItemHeight;
            if (itemHeight <= 0) itemHeight = 50; // Fallback default

            double listBoxHeight = _getListBoxHeight();
            if (listBoxHeight <= 0) listBoxHeight = 400; // Fallback default

            int pageSize = (int)(listBoxHeight / itemHeight);
            return Math.Max(1, pageSize); // At least 1
        }

        /// <summary>
        /// Checks if the specified modifier key is currently pressed.
        /// Modifier values: Alt=1, Ctrl=2, Shift=4, None=0
        /// </summary>
        private static bool IsModifierKeyPressed(uint modifier)
        {
            return modifier switch
            {
                ModifierKeyFlags.None => true, // No modifier required
                ModifierKeyFlags.Alt => Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
                ModifierKeyFlags.Ctrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
                ModifierKeyFlags.Shift => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
                _ => false
            };
        }

        /// <summary>
        /// Maps a key to a window index (0-9). Returns null if the key is not a number key.
        /// Keys 1-9 map to indices 0-8, key 0 maps to index 9.
        /// </summary>
        private static int? GetNumberKeyIndex(Key key)
        {
            return key switch
            {
                Key.D1 or Key.NumPad1 => 0,
                Key.D2 or Key.NumPad2 => 1,
                Key.D3 or Key.NumPad3 => 2,
                Key.D4 or Key.NumPad4 => 3,
                Key.D5 or Key.NumPad5 => 4,
                Key.D6 or Key.NumPad6 => 5,
                Key.D7 or Key.NumPad7 => 6,
                Key.D8 or Key.NumPad8 => 7,
                Key.D9 or Key.NumPad9 => 8,
                Key.D0 or Key.NumPad0 => 9,
                _ => null
            };
        }

        /// <summary>
        /// Activates a window by its index in the filtered list.
        /// </summary>
        private void ActivateWindowByIndex(int index)
        {
            if (index >= 0 && index < _viewModel.FilteredWindows.Count)
            {
                var windowItem = _viewModel.FilteredWindows[index];
                Logger.Log($"Number shortcut activated: index {index} -> '{windowItem.Title}'");
                _activateWindow(windowItem);
            }
        }
    }
}
