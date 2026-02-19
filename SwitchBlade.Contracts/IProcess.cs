using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SwitchBlade.Contracts
{
    public interface IProcess : IDisposable
    {
        int Id { get; }
        bool HasExited { get; }
        void Kill(bool entireProcessTree);
        Task WaitForExitAsync(CancellationToken cancellationToken = default);
        
        TextWriter StandardInput { get; }
        TextReader StandardOutput { get; }
        TextReader StandardError { get; }
        
        void BeginErrorReadLine();
        event DataReceivedEventHandler ErrorDataReceived;
        
        // Memory stats
        void Refresh();
        long WorkingSet64 { get; }
        long PrivateMemorySize64 { get; }
        int HandleCount { get; }
        int ThreadCount { get; }
        
        /// <summary>
        /// Gets the full path to the executable file for the process.
        /// </summary>
        string? MainModuleFileName { get; }
    }
}
