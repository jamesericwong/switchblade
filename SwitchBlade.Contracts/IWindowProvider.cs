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
        /// Optional: Returns a settings control for modern WPF-based settings UI.
        /// If implemented, this is preferred over <see cref="ShowSettingsDialog"/>.
        /// </summary>
        ISettingsControl? SettingsControl => null;

        /// <summary>
        /// Called after instantiation to pass dependencies via context.
        /// </summary>
        /// <param name="context">Plugin context containing logger and other dependencies.</param>
        void Initialize(IPluginContext context);

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

        /// <summary>
        /// Sets dynamic exclusions for this provider. For providers that need to filter out
        /// processes handled by other plugins (e.g., WindowFinder excludes browser processes).
        /// Default implementation is no-op.
        /// </summary>
        /// <param name="exclusions">Process names to exclude.</param>
        void SetExclusions(IEnumerable<string> exclusions) { }

        IEnumerable<WindowItem> GetWindows();

        void ActivateWindow(WindowItem item);
    }
}
