namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Default implementation of IPluginContext.
    /// </summary>
    public class PluginContext : IPluginContext
    {
        public ILogger Logger { get; }

        public PluginContext(ILogger logger)
        {
            Logger = logger;
        }
    }
}
