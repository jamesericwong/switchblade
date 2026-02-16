using System.Diagnostics;

namespace SwitchBlade.Contracts
{
    public interface IProcessFactory
    {
        IProcess Start(ProcessStartInfo startInfo);
        IProcess GetCurrentProcess();
        string? ProcessPath { get; }
    }
}
