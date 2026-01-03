using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class PluginInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public bool HasSettings { get; set; }
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Reference to the actual provider instance for opening settings.
        /// </summary>
        public IWindowProvider? Provider { get; set; }
    }
}
