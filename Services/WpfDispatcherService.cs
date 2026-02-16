using System;
using System.Diagnostics.CodeAnalysis;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
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


        public async System.Threading.Tasks.Task InvokeAsync(Func<System.Threading.Tasks.Task> action)
        {
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
            }
            else
            {
                // Fallback for non-WPF contexts
                await action();
            }
        }
    }
}
