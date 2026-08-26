using System;
using System.Collections.Generic;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Probes whether any of the given process names currently has a running process.
    /// Used to decide whether stale windows from a failed scan cycle should be kept
    /// (the app is still alive — a failed read is more likely than all tabs disappearing)
    /// or cleared (the app is gone — the entries are true ghosts).
    /// </summary>
    public interface IProcessLivenessChecker
    {
        /// <summary>
        /// Returns true if at least one of the given process names has a running process.
        /// Blank/whitespace names are ignored; an empty or all-blank set returns false.
        /// </summary>
        bool IsAnyRunning(IEnumerable<string> processNames);
    }
}
