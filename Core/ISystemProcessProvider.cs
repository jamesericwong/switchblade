using System.Diagnostics;

namespace SwitchBlade.Core
{
    public interface ISystemProcessProvider
    {
        Process? Start(ProcessStartInfo startInfo);
        Process GetCurrentProcess();
        string? ProcessPath { get; }
    }
}
