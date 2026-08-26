using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Default implementation of IPluginContext. App-only: the UIA worker builds its own context,
    /// so this concrete class no longer belongs in the shared Contracts kernel.
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
