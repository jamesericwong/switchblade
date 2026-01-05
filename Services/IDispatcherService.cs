using System;
using System.Threading.Tasks;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Abstraction for UI thread dispatching - works with both WPF and WinUI.
    /// </summary>
    public interface IDispatcherService
    {
        void InvokeAsync(Action action);
    }
}
