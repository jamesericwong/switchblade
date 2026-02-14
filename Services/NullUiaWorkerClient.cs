using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// No-op implementation of IUiaWorkerClient used in tests and when
    /// no UIA providers are registered. Returns empty results.
    /// </summary>
    internal sealed class NullUiaWorkerClient : IUiaWorkerClient
    {
        public async IAsyncEnumerable<UiaPluginResult> ScanStreamingAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<List<WindowItem>> ScanAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<WindowItem>());
        }

        public void Dispose() { }
    }
}
