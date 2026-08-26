using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Response sent from UIA worker back to main app.
    /// </summary>
    public sealed class UiaResponse
    {
        /// <summary>Protocol revision the worker speaks (see <see cref="UiaProtocol"/>). 0 means a pre-versioning legacy peer.</summary>
        public int ProtocolVersion { get; set; }

        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<UiaWindowResult>? Windows { get; set; }
    }
}
