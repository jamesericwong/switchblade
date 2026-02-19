using System;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchBlade.Contracts
{
    public interface IDelayProvider
    {
        Task Delay(int millisecondsDelay, CancellationToken cancellationToken = default);
    }
}
