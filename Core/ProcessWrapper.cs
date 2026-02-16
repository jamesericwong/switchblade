using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class ProcessWrapper : IProcess
    {
        private readonly Process _process;

        public ProcessWrapper(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public int Id => _process.Id;
        public bool HasExited => _process.HasExited;

        public TextWriter StandardInput => _process.StandardInput;
        public TextReader StandardOutput => _process.StandardOutput;
        public TextReader StandardError => _process.StandardError;

        public long WorkingSet64 => _process.WorkingSet64;
        public long PrivateMemorySize64 => _process.PrivateMemorySize64;
        public int HandleCount => _process.HandleCount;
        public int ThreadCount => _process.Threads.Count;

        public void Kill(bool entireProcessTree)
        {
            if (entireProcessTree) _process.Kill(true);
            else _process.Kill();
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            return _process.WaitForExitAsync(cancellationToken);
        }

        public void BeginErrorReadLine()
        {
            _process.BeginErrorReadLine();
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }

        public void Refresh()
        {
            _process.Refresh();
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
