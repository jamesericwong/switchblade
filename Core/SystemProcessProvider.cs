using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    internal class SystemProcessProvider : ISystemProcessProvider
    {
        public Process? Start(ProcessStartInfo startInfo)
        {
            return Process.Start(startInfo);
        }

        public Process GetCurrentProcess()
        {
            return Process.GetCurrentProcess();
        }

        public string? ProcessPath => Environment.ProcessPath;
    }
}
