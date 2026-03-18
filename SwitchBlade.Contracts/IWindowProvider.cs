using System;
using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Base interface for all window source plugins.
    /// Simplified to follow Interface Segregation Principle.
    /// </summary>
    public interface IWindowProvider
    {
        /// <summary>
        /// Unique plugin name used for Registry storage path.
        /// </summary>
        string PluginName { get; }

        /// <summary>
        /// Called after instantiation to pass dependencies via context.
        /// </summary>
        void Initialize(IPluginContext context);

        /// <summary>
        /// Scans for windows handled by this provider.
        /// </summary>
        IEnumerable<WindowItem> GetWindows();

        /// <summary>
        /// Activates the specified window.
        /// </summary>
        void ActivateWindow(WindowItem item);
    }

    /// <summary>
    /// Optional interface for plugins that support user-configurable settings.
    /// </summary>
    public interface IConfigurablePlugin
    {
        bool HasSettings { get; }
        ISettingsControl? SettingsControl { get; }
        void ReloadSettings();
    }

    /// <summary>
    /// Optional interface for providers that handle specific processes or need exclusions.
    /// </summary>
    public interface IProviderExclusionSettings
    {
        IEnumerable<string> GetHandledProcesses();
        void SetExclusions(IEnumerable<string> exclusions);
    }

    /// <summary>
    /// Optional interface defining the execution strategy (e.g. out-of-process UIA).
    /// </summary>
    public interface IExtrusionStrategy
    {
        bool IsUiaProvider { get; }
    }
}
