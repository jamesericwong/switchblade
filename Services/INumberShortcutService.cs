using System;
using System.Collections.Generic;
using System.Windows.Input;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Service for handling number-based shortcuts.
    /// Extracts shortcut logic from input handlers to follow SRP and improve testability.
    /// </summary>
    public interface INumberShortcutService
    {
        /// <summary>
        /// Attempts to handle a key as a number shortcut.
        /// Returns true if the key process should be considered handled.
        /// </summary>
        /// <param name="key">The key pressed.</param>
        /// <param name="modifiers">Current modifier keys.</param>
        /// <param name="windows">The filtered window list to index into.</param>
        /// <param name="activateWindow">Action to perform when a shortcut is triggered.</param>
        /// <returns>True if a shortcut was triggered and handled.</returns>
        bool HandleShortcut(Key key, ModifierKeys modifiers, IReadOnlyList<WindowItem> windows, Action<WindowItem?> activateWindow);
    }
}
