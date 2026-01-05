using System;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Handles keyboard input for the main window - WinUI 3 version.
    /// Extracted from MainWindow.xaml.cs for Single Responsibility Principle.
    /// </summary>
    public class KeyboardInputHandler
    {
        private readonly IWindowListViewModel _viewModel;
        private readonly ILogger _logger;
        private readonly ISettingsService _settingsService;
        private readonly Action<WindowItem?> _activateWindow;

        public KeyboardInputHandler(
            IWindowListViewModel viewModel,
            ILogger logger,
            ISettingsService settingsService,
            Action<WindowItem?> activateWindow)
        {
            _viewModel = viewModel;
            _logger = logger;
            _settingsService = settingsService;
            _activateWindow = activateWindow;
        }

        /// <summary>
        /// Handles the KeyDown event for WinUI.
        /// </summary>
        public void HandleKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape || e.Key == VirtualKey.Enter ||
                e.Key == VirtualKey.Down || e.Key == VirtualKey.Up)
            {
                _logger.Log($"KeyboardInputHandler KeyDown: {e.Key}");
            }

            // Get current modifiers
            var modifiers = GetCurrentModifiers();

            if (HandleKeyInput(e.Key, modifiers))
            {
                e.Handled = true;
            }
        }

        private ModifierKeys GetCurrentModifiers()
        {
            ModifierKeys mods = ModifierKeys.None;

            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);

            if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= ModifierKeys.Control;
            if (shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= ModifierKeys.Shift;
            if (altState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= ModifierKeys.Alt;

            return mods;
        }

        public bool HandleKeyInput(VirtualKey key, ModifierKeys modifiers)
        {
            if (key == VirtualKey.Escape)
            {
                // Escape typically handled by caller
                return false;
            }
            else if (key == VirtualKey.Down)
            {
                _viewModel.MoveSelection(1);
                return true;
            }
            else if (key == VirtualKey.Up)
            {
                _viewModel.MoveSelection(-1);
                return true;
            }
            else if (key == VirtualKey.Enter)
            {
                _activateWindow(_viewModel.SelectedWindow);
                return true;
            }
            else if (key == VirtualKey.Home && modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToFirst();
                return true;
            }
            else if (key == VirtualKey.End && modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToLast();
                return true;
            }
            else if (key == VirtualKey.PageUp)
            {
                int pageSize = 10; // Default page size
                _viewModel.MoveSelectionByPage(-1, pageSize);
                return true;
            }
            else if (key == VirtualKey.PageDown)
            {
                int pageSize = 10;
                _viewModel.MoveSelectionByPage(1, pageSize);
                return true;
            }
            // Number Shortcuts Feature
            else if (_settingsService.Settings.EnableNumberShortcuts)
            {
                var settings = _settingsService.Settings;
                if (IsModifierKeyPressed(settings.NumberShortcutModifier, modifiers))
                {
                    int? index = GetNumberKeyIndex(key);
                    if (index.HasValue)
                    {
                        ActivateWindowByIndex(index.Value);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsModifierKeyPressed(uint modifier, ModifierKeys currentModifiers)
        {
            return modifier switch
            {
                ModifierKeyFlags.None => true,
                ModifierKeyFlags.Alt => currentModifiers.HasFlag(ModifierKeys.Alt),
                ModifierKeyFlags.Ctrl => currentModifiers.HasFlag(ModifierKeys.Control),
                ModifierKeyFlags.Shift => currentModifiers.HasFlag(ModifierKeys.Shift),
                _ => false
            };
        }

        private static int? GetNumberKeyIndex(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Number1 or VirtualKey.NumberPad1 => 0,
                VirtualKey.Number2 or VirtualKey.NumberPad2 => 1,
                VirtualKey.Number3 or VirtualKey.NumberPad3 => 2,
                VirtualKey.Number4 or VirtualKey.NumberPad4 => 3,
                VirtualKey.Number5 or VirtualKey.NumberPad5 => 4,
                VirtualKey.Number6 or VirtualKey.NumberPad6 => 5,
                VirtualKey.Number7 or VirtualKey.NumberPad7 => 6,
                VirtualKey.Number8 or VirtualKey.NumberPad8 => 7,
                VirtualKey.Number9 or VirtualKey.NumberPad9 => 8,
                VirtualKey.Number0 or VirtualKey.NumberPad0 => 9,
                _ => null
            };
        }

        private void ActivateWindowByIndex(int index)
        {
            if (index >= 0 && index < _viewModel.FilteredWindows.Count)
            {
                var windowItem = _viewModel.FilteredWindows[index];
                _logger.Log($"Number shortcut activated: index {index} -> '{windowItem.Title}'");
                _activateWindow(windowItem);
            }
        }
    }

    /// <summary>
    /// Custom ModifierKeys enum for WinUI (WPF's ModifierKeys is not available)
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }
}
