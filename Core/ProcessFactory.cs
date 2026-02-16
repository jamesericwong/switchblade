using System;
using System.Diagnostics;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class ProcessFactory : IProcessFactory
    {
        public IProcess Start(ProcessStartInfo startInfo)
        {
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Process.Start returned null");
            return new ProcessWrapper(process);
        }

        public IProcess GetCurrentProcess()
        {
            return new ProcessWrapper(Process.GetCurrentProcess());
        }

        public string? ProcessPath => Environment.ProcessPath;
    }
}
