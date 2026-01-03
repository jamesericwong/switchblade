using System;

namespace SwitchBlade.Services
{
    public class WpfDispatcherService : IDispatcherService
    {
        public void Invoke(Action action)
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                // Fallback for non-WPF contexts (unlikely in prod, but safe)
                action();
            }
        }
    }
}
