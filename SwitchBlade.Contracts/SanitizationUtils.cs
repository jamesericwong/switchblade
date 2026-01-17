using System;
using System.Linq;
using System.IO;

namespace SwitchBlade.Contracts
{
    public static class SanitizationUtils
    {
        /// <summary>
        /// Sanitizes a process name by trimming whitespace, removing .exe extension,
        /// and stripping illegal filename characters.
        /// </summary>
        /// <param name="input">The raw process name input.</param>
        /// <returns>A sanitized process name.</returns>
        public static string SanitizeProcessName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string sanitized = input.Trim();

            // Remove .exe extension if present
            if (sanitized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Substring(0, sanitized.Length - 4);
            }

            // Remove illegal filename characters (common for process names)
            char[] illegalChars = Path.GetInvalidFileNameChars();
            return new string(sanitized.Where(c => !illegalChars.Contains(c)).ToArray()).ToLowerInvariant();
        }
    }
}
