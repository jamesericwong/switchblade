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
    }
}
