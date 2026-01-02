using System;
using System.IO;

namespace SwitchBlade.Core
{
    public static class Logger
    {
        public static bool IsDebugEnabled { get; set; } = false;
        private static readonly object _lock = new object();

        private static string LogPath => Path.Combine(Path.GetTempPath(), "switchblade_debug.log");

        public static void Log(string message)
        {
            if (!IsDebugEnabled) return;

            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch (Exception)
            {
                // Silently fail to avoid recursive crashes
            }
        }

        public static void LogError(string context, Exception ex)
        {
            // We force logging for errors even if debug is disabled, 
            // but we might want to reconsider if strict silence is required.
            // For now, let's allow errors to be logged to the new temp location for diagnostics.
            try 
            {
                 var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR [{context}]: {ex.Message}\nStack: {ex.StackTrace}{Environment.NewLine}";
                 lock (_lock)
                 {
                    File.AppendAllText(LogPath, line);
                 }
            }
            catch { }
        }
    }

    public class LoggerBridge : SwitchBlade.Contracts.ILogger
    {
        public void Log(string message) => Logger.Log(message);
        public void LogError(string context, Exception ex) => Logger.LogError(context, ex);
    }
}
