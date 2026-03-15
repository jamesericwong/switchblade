using System;
using System.Diagnostics;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class ProcessFactory : IProcessFactory
    {
        private readonly ISystemProcessProvider _provider;

        public ProcessFactory(ISystemProcessProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IProcess? Start(ProcessStartInfo startInfo)
        {
            var process = _provider.Start(startInfo);
            return process != null ? new ProcessWrapper(process) : null;
        }

        public IProcess GetCurrentProcess()
        {
            return new ProcessWrapper(_provider.GetCurrentProcess());
        }

        public string? ProcessPath => _provider.ProcessPath;
    }
}
