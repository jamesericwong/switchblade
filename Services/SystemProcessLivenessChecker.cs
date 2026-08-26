using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SwitchBlade.Services
{
    /// <summary>
    /// OS-backed implementation of <see cref="IProcessLivenessChecker"/> using the process table.
    /// Uses GetProcessesByName per target so inaccessible processes are simply excluded
    /// from results instead of throwing during enumeration.
    /// </summary>
    public sealed class SystemProcessLivenessChecker : IProcessLivenessChecker
    {
        public bool IsAnyRunning(IEnumerable<string> processNames)
        {
            if (processNames is null)
            {
                throw new ArgumentNullException(nameof(processNames));
            }

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in processNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    targets.Add(name);
                }
            }

            if (targets.Count == 0)
            {
                return false;
            }

            foreach (var target in targets)
            {
                if (Process.GetProcessesByName(target).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
