using System;

namespace SwitchBlade.Contracts
{
    public interface ILogger
    {
        bool IsDebugEnabled { get; set; }
        void Log(string message);

        /// <summary>
        /// Logs a warning. Unlike <see cref="Log"/>, warnings are diagnostic signals that must be
        /// visible even when debug logging is disabled (e.g., high transient-invalidation rates).
        /// </summary>
        void LogWarning(string message);

        void LogError(string context, Exception ex);
    }
}
