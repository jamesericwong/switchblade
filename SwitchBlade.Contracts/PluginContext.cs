namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Default implementation of IPluginContext.
    /// </summary>
    public class PluginContext : IPluginContext
    {
        public ILogger Logger { get; }
        public IPluginSettingsService? Settings { get; }

        public PluginContext(ILogger logger, IPluginSettingsService? settings = null)
        {
            Logger = logger;
            Settings = settings;
        }
    }
}
