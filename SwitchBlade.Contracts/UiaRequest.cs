using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Request DTO for UIA Worker.
    /// </summary>
    public sealed class UiaRequest
    {
        public string Command { get; set; } = "scan";

        /// <summary>Protocol revision the host speaks (see <see cref="UiaProtocol"/>). 0 means a pre-versioning legacy peer.</summary>
        public int ProtocolVersion { get; set; }
        public List<string>? Plugins { get; set; }
        public List<string>? ExcludedProcesses { get; set; }
        public List<string>? DisabledPlugins { get; set; }
    }
}
