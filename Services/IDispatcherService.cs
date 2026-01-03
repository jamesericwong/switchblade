using System;

namespace SwitchBlade.Services
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
    }
}
