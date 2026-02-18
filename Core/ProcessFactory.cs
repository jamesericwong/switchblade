using System;
using System.Diagnostics;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class ProcessFactory : IProcessFactory
    {
        public IProcess? Start(ProcessStartInfo startInfo)
        {
            var process = Process.Start(startInfo);
            return process != null ? new ProcessWrapper(process) : null;
        }

        public IProcess GetCurrentProcess()
        {
            return new ProcessWrapper(Process.GetCurrentProcess());
        }

        public string? ProcessPath => Environment.ProcessPath;
    }
}
