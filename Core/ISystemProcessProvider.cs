using System.Diagnostics;

namespace SwitchBlade.Core
{
    internal interface ISystemProcessProvider
    {
        Process? Start(ProcessStartInfo startInfo);
        Process GetCurrentProcess();
        string? ProcessPath { get; }
    }
}
