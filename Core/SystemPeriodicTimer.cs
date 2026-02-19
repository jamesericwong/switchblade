using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    public class SystemPeriodicTimer : IPeriodicTimer
    {
        private readonly PeriodicTimer _timer;

        public SystemPeriodicTimer(TimeSpan period)
        {
            _timer = new PeriodicTimer(period);
        }

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        {
            return _timer.WaitForNextTickAsync(cancellationToken);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
