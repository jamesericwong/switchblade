namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Default implementation of IPluginContext.
    /// </summary>
    public class PluginContext : IPluginContext
    {
        public ILogger Logger { get; }
        public IPluginSettingsService? Settings { get; }
        public IWindowInterop Interop { get; }
        public IRegistryService Registry { get; }

        public PluginContext(ILogger logger, IWindowInterop interop, IRegistryService registry, IPluginSettingsService? settings = null)
        {
            Logger = logger;
            Interop = interop;
            Registry = registry;
            Settings = settings;
        }
    }
}
