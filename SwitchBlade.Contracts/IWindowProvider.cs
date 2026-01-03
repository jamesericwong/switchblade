using System;
using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    public interface IWindowProvider
    {
        /// <summary>
        /// Unique plugin name used for Registry storage path.
        /// Must be a valid folder name (no special characters).
        /// </summary>
        string PluginName { get; }

        /// <summary>
        /// Indicates whether this plugin has user-configurable settings.
        /// </summary>
        bool HasSettings { get; }

        /// <summary>
        /// Called after instantiation to pass dependencies.
        /// </summary>
        void Initialize(object settingsService, ILogger logger);

        /// <summary>
        /// Called before GetWindows to reload settings from Registry.
        /// Allows settings changes to take effect without app restart.
        /// </summary>
        void ReloadSettings();

        /// <summary>
        /// Returns a list of process names (without extension) that this plugin exclusively handles.
        /// The core WindowFinder will exclude these processes to prevent duplicates.
        /// </summary>
        IEnumerable<string> GetHandledProcesses() => Array.Empty<string>();

        IEnumerable<WindowItem> GetWindows();

        void ActivateWindow(WindowItem item);

        /// <summary>
        /// Opens a settings dialog for this plugin.
        /// </summary>
        /// <param name="ownerHwnd">Handle to the parent window for modal dialog.</param>
        void ShowSettingsDialog(IntPtr ownerHwnd);
    }
}
