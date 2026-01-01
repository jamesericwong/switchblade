using System;
using System.IO;

namespace SwitchBlade.Core
{
    public static class Logger
    {
        private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "switchblade_debug.log");

        public static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
            catch (Exception)
            {
                // Silently fail if we can't log, to avoid recursive crashes
            }
        }

        public static void LogError(string context, Exception ex)
        {
            Log($"ERROR [{context}]: {ex.Message}\nStack: {ex.StackTrace}");
        }
    }
}
