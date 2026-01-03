using System;

namespace SwitchBlade.Services
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
        System.Threading.Tasks.Task InvokeAsync(Func<System.Threading.Tasks.Task> action);
    }
}
