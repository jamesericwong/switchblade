using System;
using System.Windows.Input;
using SwitchBlade.Contracts;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Implementation of INumberShortcutService for handling number-based shortcuts.
    /// Following SOLID principles by isolating shortcut logic from input handling.
    /// </summary>
    public class NumberShortcutService : INumberShortcutService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger _logger;

        public NumberShortcutService(ISettingsService settingsService, ILogger logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool HandleShortcut(Key key, ModifierKeys modifiers, IWindowListViewModel viewModel, Action<WindowItem?> activateWindow)
        {
            if (!_settingsService.Settings.EnableNumberShortcuts) return false;

            var settings = _settingsService.Settings;
            
            // Check if the required modifier key is pressed
            if (IsModifierKeyPressed(settings.NumberShortcutModifier, modifiers))
            {
                int? index = GetNumberKeyIndex(key);
                if (index.HasValue)
                {
                    ActivateWindowByIndex(index.Value, viewModel, activateWindow);
                    return true;
                }
            }

            return false;
        }

        private static bool IsModifierKeyPressed(uint modifier, ModifierKeys currentModifiers)
        {
            return modifier switch
            {
                ModifierKeyFlags.None => true, // No modifier required
                ModifierKeyFlags.Alt => currentModifiers.HasFlag(ModifierKeys.Alt),
                ModifierKeyFlags.Ctrl => currentModifiers.HasFlag(ModifierKeys.Control),
                ModifierKeyFlags.Shift => currentModifiers.HasFlag(ModifierKeys.Shift),
                _ => false
            };
        }

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

        private void ActivateWindowByIndex(int index, IWindowListViewModel viewModel, Action<WindowItem?> activateWindow)
        {
            if (index < viewModel.FilteredWindows.Count)
            {
                var windowItem = viewModel.FilteredWindows[index];
                _logger.Log($"Number shortcut activated: index {index} -> '{windowItem.Title}'");
                activateWindow(windowItem);
            }
        }
    }
}
