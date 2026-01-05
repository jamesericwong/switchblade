using Microsoft.UI.Dispatching;

namespace SwitchBlade.Services
{
    /// <summary>
    /// WinUI implementation of IDispatcherService using DispatcherQueue.
    /// </summary>
    public class WinUIDispatcherService : IDispatcherService
    {
        private readonly DispatcherQueue? _dispatcherQueue;

        public WinUIDispatcherService()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public void InvokeAsync(Action action)
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() => action());
            }
            else
            {
                // Fallback: just invoke directly
                action();
            }
        }
    }
}
