using System;

namespace SwitchBlade.Contracts
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string context, Exception ex);
    }
}
