using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Streaming response for a single plugin's results (NDJSON protocol).
    /// </summary>
    public sealed class UiaPluginResult
    {
        public string PluginName { get; set; } = "";
        public List<UiaWindowResult>? Windows { get; set; }
        public string? Error { get; set; }
        public bool IsFinal { get; set; }
    }
}
