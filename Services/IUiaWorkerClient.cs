using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Client that spawns and manages the out-of-process UIA Worker for UI Automation scanning.
    /// Supports both streaming (per-plugin incremental) and batch result modes.
    /// </summary>
    public interface IUiaWorkerClient : IDisposable
    {
        /// <summary>
        /// Runs a UIA scan with streaming results. Each plugin's results are yielded
        /// immediately as they complete in the worker process.
        /// </summary>
        IAsyncEnumerable<UiaPluginResult> ScanStreamingAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience wrapper that collects all streaming results into a single list.
        /// </summary>
        Task<List<WindowItem>> ScanAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            CancellationToken cancellationToken = default);
    }
}
