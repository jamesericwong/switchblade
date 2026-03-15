namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Context provided to plugins during initialization.
    /// Contains all dependencies a plugin may need.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>Logger for plugin diagnostics.</summary>
        ILogger Logger { get; }

        /// <summary>
        /// Pre-configured settings service for the plugin.
        /// Null when not available (backward compatibility).
        /// </summary>
        IPluginSettingsService? Settings { get; }

        /// <summary>
        /// Native interop wrapper for window and process operations.
        /// </summary>
        IWindowInterop Interop { get; }

        /// <summary>
        /// Registry service for direct access if needed.
        /// </summary>
        IRegistryService Registry { get; }
    }
}
