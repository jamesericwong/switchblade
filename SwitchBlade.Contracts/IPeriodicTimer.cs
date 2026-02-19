using System;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchBlade.Contracts
{
    public interface IPeriodicTimer : IDisposable
    {
        ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default);
    }
}
