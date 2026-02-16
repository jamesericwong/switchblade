using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Response sent from UIA worker back to main app.
    /// </summary>
    public sealed class UiaResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<UiaWindowResult>? Windows { get; set; }
    }
}
