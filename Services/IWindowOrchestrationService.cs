using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates parallel execution of window providers and manages the master window list.
    /// </summary>
    public interface IWindowOrchestrationService
    {
        /// <summary>
        /// Event raised when the window list has been updated.
        /// </summary>
        event EventHandler<WindowListUpdatedEventArgs>? WindowListUpdated;

        /// <summary>
        /// Refreshes windows from all enabled providers in parallel.
        /// </summary>
        /// <param name="disabledPlugins">Set of disabled plugin names.</param>
        Task RefreshAsync(ISet<string> disabledPlugins);

        /// <summary>
        /// Gets the current master list of all windows.
        /// </summary>
        IReadOnlyList<WindowItem> AllWindows { get; }
    }

    /// <summary>
    /// Event args for window list updates.
    /// </summary>
    public class WindowListUpdatedEventArgs : EventArgs
    {
        public IWindowProvider Provider { get; }
        public bool IsStructuralChange { get; }

        public WindowListUpdatedEventArgs(IWindowProvider provider, bool isStructuralChange)
        {
            Provider = provider;
            IsStructuralChange = isStructuralChange;
        }
    }
}
