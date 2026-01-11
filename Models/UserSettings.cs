using System.Collections.Generic;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Represents all user-configurable settings for the application.
    /// Persisted to and loaded from the Windows Registry.
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// List of process names to exclude from the window list.
        /// </summary>
        public List<string> ExcludedProcesses { get; set; } = new List<string> { "SwitchBlade" };

        /// <summary>
        /// List of plugin names that have been disabled by the user.
        /// </summary>
        public List<string> DisabledPlugins { get; set; } = new List<string>();

        /// <summary>
        /// Current theme name (e.g., "Light", "Dark").
        /// </summary>
        public string CurrentTheme { get; set; } = "Light";

        // UI Options

        /// <summary>
        /// Whether to show live DWM thumbnails of the selected window.
        /// </summary>
        public bool EnablePreviews { get; set; } = true;

        /// <summary>
        /// Duration of fade in/out animations in milliseconds.
        /// </summary>
        public int FadeDurationMs { get; set; } = 200;

        /// <summary>
        /// Target opacity of the main window (0.0 to 1.0).
        /// </summary>
        public double WindowOpacity { get; set; } = 1.0;

        /// <summary>
        /// Height of each item in the window list in pixels.
        /// </summary>
        public double ItemHeight { get; set; } = 50.0;

        /// <summary>
        /// Whether to show window icons in the list.
        /// </summary>
        public bool ShowIcons { get; set; } = true;

        /// <summary>
        /// Whether to hide the taskbar icon (tray-only mode).
        /// </summary>
        public bool HideTaskbarIcon { get; set; } = true;

        /// <summary>
        /// Whether to launch the application on Windows startup.
        /// </summary>
        public bool LaunchOnStartup { get; set; } = false;

        /// <summary>
        /// Whether to run the application with Administrator privileges.
        /// Some plugins require this for full inspection of elevated windows.
        /// </summary>
        public bool RunAsAdministrator { get; set; } = false;

        // Background Polling Options

        /// <summary>
        /// Whether to enable automatic background polling of window lists.
        /// </summary>
        public bool EnableBackgroundPolling { get; set; } = true;

        /// <summary>
        /// Interval between background polls in seconds.
        /// </summary>
        public int BackgroundPollingIntervalSeconds { get; set; } = 30;

        // Number Shortcuts (press 1-9, 0 to quick-switch)

        /// <summary>
        /// Whether to enable number key shortcuts for quick window switching.
        /// </summary>
        public bool EnableNumberShortcuts { get; set; } = true;

        /// <summary>
        /// Whether to enable staggered badge animations when showing window list.
        /// </summary>
        public bool EnableBadgeAnimations { get; set; } = true;

        /// <summary>
        /// Modifier key for number shortcuts. Values: Alt=1, Ctrl=2, Shift=4, Win=8, None=0.
        /// </summary>
        public uint NumberShortcutModifier { get; set; } = ModifierKeyFlags.Alt;

        /// <summary>
        /// Behavior for preserving selection state when the window list refreshes.
        /// </summary>
        public RefreshBehavior RefreshBehavior { get; set; } = RefreshBehavior.PreserveScroll;

        // Window Size

        /// <summary>
        /// Persisted width of the main window.
        /// </summary>
        public double WindowWidth { get; set; } = 800.0;

        /// <summary>
        /// Persisted height of the main window.
        /// </summary>
        public double WindowHeight { get; set; } = 600.0;

        // Hotkey Options (Defaults: Ctrl + Shift + Q)

        /// <summary>
        /// Modifier keys for the global hotkey. Values: Alt=1, Ctrl=2, Shift=4, Win=8 (can be combined).
        /// </summary>
        public uint HotKeyModifiers { get; set; } = ModifierKeyFlags.Ctrl | ModifierKeyFlags.Shift; // 6

        /// <summary>
        /// Virtual key code for the global hotkey.
        /// </summary>
        public uint HotKeyKey { get; set; } = 0x51; // VK_Q
    }
}
