namespace SwitchBlade.Core
{
    public class PluginInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
    }
}
