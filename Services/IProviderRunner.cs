using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Strategy interface for executing window providers.
    /// Enables different execution models (in-process, out-of-process) without modifying the orchestrator.
    /// </summary>
    public interface IProviderRunner
    {
        /// <summary>
        /// Executes the given providers and reports results via the callback.
        /// </summary>
        /// <param name="providers">The providers to execute.</param>
        /// <param name="disabledPlugins">Set of disabled plugin names to skip.</param>
        /// <param name="handledProcesses">Set of process names handled by plugins (for exclusion).</param>
        /// <param name="onResults">Callback invoked with each provider's results. Must be thread-safe.</param>
        Task RunAsync(
            IList<IWindowProvider> providers,
            ISet<string> disabledPlugins,
            HashSet<string> handledProcesses,
            Action<IWindowProvider, List<WindowItem>> onResults);
    }
}
