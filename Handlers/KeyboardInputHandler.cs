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
        private readonly IWindowListViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly INumberShortcutService _numberShortcutService;
        private readonly Action _hideWindow;
        private readonly Action<WindowItem?> _activateWindow;
        private readonly Func<double> _getListBoxHeight;

        /// <summary>
        /// Creates a new keyboard input handler.
        /// </summary>
        /// <param name="viewModel">The main view model.</param>
        /// <param name="settingsService">Settings service for configuration.</param>
        /// <param name="numberShortcutService">Service for handling number shortcuts.</param>
        /// <param name="hideWindow">Action to hide the window.</param>
        /// <param name="activateWindow">Action to activate a selected window.</param>
        /// <param name="getListBoxHeight">Function to get the current list box height for page calculations.</param>
        public KeyboardInputHandler(
            IWindowListViewModel viewModel,
            ISettingsService settingsService,
            INumberShortcutService numberShortcutService,
            Action hideWindow,
            Action<WindowItem?> activateWindow,
            Func<double> getListBoxHeight)
        {
            _viewModel = viewModel;
            _settingsService = settingsService;
            _numberShortcutService = numberShortcutService;
            _hideWindow = hideWindow;
            _activateWindow = activateWindow;
            _getListBoxHeight = getListBoxHeight;
        }

        /// <summary>
        /// Handles the PreviewKeyDown event for the main window.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        public void HandleKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only log non-character keys to avoid spam
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Down || e.Key == Key.Up)
            {
                Logger.Log($"KeyboardInputHandler KeyDown: {e.Key}");
            }

            // Extract modifiers relative to this event if possible, but Keyboard.Modifiers is static.
            // For the purpose of this handler, we use global modifiers.
            if (HandleKeyInput(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Processes key input. Returns true if handled.
        /// Public for unit testing.
        /// </summary>
        public bool HandleKeyInput(Key key, ModifierKeys modifiers)
        {
            if (key == Key.Escape)
            {
                _hideWindow();
                return false; // Escape hides window but doesn't strictly "handle" input in a way that prevents bubble? Actually MainWindow logic usually handles it.
                // But looking at original code: _hideWindow(); -> implicitly handled? Original didn't set e.Handled = true for Escape.
            }
            else if (key == Key.Down)
            {
                _viewModel.MoveSelection(1);
                return true;
            }
            else if (key == Key.Up)
            {
                _viewModel.MoveSelection(-1);
                return true;
            }
            else if (key == Key.Enter)
            {
                _activateWindow(_viewModel.SelectedWindow);
                return true;
            }
            else if (key == Key.Home && modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToFirst();
                return true;
            }
            else if (key == Key.End && modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToLast();
                return true;
            }
            else if (key == Key.PageUp)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(-1, pageSize);
                return true;
            }
            else if (key == Key.PageDown)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(1, pageSize);
                return true;
            }
            
            // Delegate Number Shortcuts to the dedicated service
            if (_numberShortcutService.HandleShortcut(key, modifiers, _viewModel, _activateWindow))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates the number of items visible in the ListBox (page size).
        /// </summary>
        private int CalculatePageSize()
        {
            double itemHeight = _settingsService.Settings.ItemHeight;
            if (itemHeight <= 0) itemHeight = 64; // Fallback default

            double listBoxHeight = _getListBoxHeight();
            if (listBoxHeight <= 0) listBoxHeight = 400; // Fallback default

            int pageSize = (int)(listBoxHeight / itemHeight);
            return Math.Max(1, pageSize); // At least 1
        }
    }
}
