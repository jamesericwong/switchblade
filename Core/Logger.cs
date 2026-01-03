using System;
using System.IO;

namespace SwitchBlade.Core
{
    public class Logger : SwitchBlade.Contracts.ILogger
    {
        public static bool IsDebugEnabled { get; set; } = false;
        private static readonly object _lock = new object();

        // Singleton instance for static bridge
        public static Logger Instance { get; } = new Logger();

        private static string LogPath => Path.Combine(Path.GetTempPath(), "switchblade_debug.log");

        // Instance methods implementing ILogger
        void SwitchBlade.Contracts.ILogger.Log(string message) => LogStatic(message);
        void SwitchBlade.Contracts.ILogger.LogError(string context, Exception ex) => LogErrorStatic(context, ex);

        // Static methods for backward compatibility
        public static void LogStatic(string message)
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

        // Bridge for old static calls
        public static void Log(string message) => LogStatic(message);


        public static void LogErrorStatic(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR: {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch { }
        }

        // Bridge for old static calls
        public static void LogError(string message) => LogErrorStatic(message);


        public static void LogErrorStatic(string context, Exception ex)
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

        // Bridge for old static calls
        public static void LogError(string context, Exception ex) => LogErrorStatic(context, ex);
    }

}
