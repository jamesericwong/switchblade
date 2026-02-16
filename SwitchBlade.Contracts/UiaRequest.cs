using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Request DTO for UIA Worker.
    /// </summary>
    public sealed class UiaRequest
    {
        public string Command { get; set; } = "scan";
        public List<string>? Plugins { get; set; }
        public List<string>? ExcludedProcesses { get; set; }
        public List<string>? DisabledPlugins { get; set; }
    }
}
