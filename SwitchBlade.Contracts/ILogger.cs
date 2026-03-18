using System;

namespace SwitchBlade.Contracts
{
    public interface ILogger
    {
        bool IsDebugEnabled { get; set; }
        void Log(string message);
        void LogError(string context, Exception ex);
    }
}
