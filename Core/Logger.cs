using System;
using System.IO;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    /// <summary>
    /// File logger with per-instance configuration (log path + debug gate). App-wide state lives on
    /// <see cref="Instance"/>. Tests use their own instance, so global state never has to be mutated.
    /// </summary>
    public class Logger : ILogger
    {
        private readonly object _lock = new();

        public bool IsDebugEnabled { get; set; }

        // Default preserved from the previous static design (temp folder debug log).
        public string LogFilePath { get; set; } = Path.Combine(Path.GetTempPath(), "switchblade_debug.log");

        public void Log(string message)
        {
            if (!IsDebugEnabled)
            {
                return;
            }

            WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        public void LogError(string context, Exception ex)
        {
            // Errors are always written, regardless of the debug gate (preserved behavior).
            WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR [{context}]: {ex.Message}\nStack: {ex.StackTrace}{Environment.NewLine}");
        }

        private void WriteLine(string line)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // A logging failure must never crash the caller.
            }
        }

        public static Logger Instance { get; } = new Logger();
    }
}
