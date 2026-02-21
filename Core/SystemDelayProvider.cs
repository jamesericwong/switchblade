using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    public class SystemDelayProvider : IDelayProvider
    {
        public Task Delay(int millisecondsDelay, CancellationToken cancellationToken = default)
        {
            return Task.Delay(millisecondsDelay, cancellationToken);
        }
    }
}
