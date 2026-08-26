using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Command-line argument construction helpers for relaunching the process.
    /// Implements Windows CommandLineToArgvW quoting rules so arguments containing
    /// whitespace or quotes survive a round-trip through ProcessStartInfo.Arguments.
    /// </summary>
    public static class CommandLineArgs
    {
        private const char Backslash = '\\';
        private const char DoubleQuote = '"';

        /// <summary>
        /// Quotes a single argument for safe re-parsing when it contains whitespace or quotes.
        /// Arguments without special characters are returned unchanged.
        /// </summary>
        public static string Quote(string argument)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(nameof(argument));
            }

            if (argument.Length == 0 || !ContainsSpecialCharacters(argument))
            {
                return argument;
            }

            var builder = new StringBuilder(argument.Length + 2);
            builder.Append(DoubleQuote);

            int backslashes = 0;
            foreach (var c in argument)
            {
                if (c == Backslash)
                {
                    backslashes++;
                    continue;
                }

                if (c == DoubleQuote)
                {
                    // Encode as (2L+1) backslashes + quote so it parses back to L literal backslashes + a literal quote.
                    builder.Append(Backslash, 2 * backslashes + 1);
                    builder.Append(DoubleQuote);
                    backslashes = 0;
                    continue;
                }

                builder.Append(Backslash, backslashes);
                builder.Append(c);
                backslashes = 0;
            }

            // Double trailing backslashes so they don't escape the closing quote.
            builder.Append(Backslash, 2 * backslashes);
            builder.Append(DoubleQuote);

            return builder.ToString();
        }

        /// <summary>
        /// Builds a ProcessStartInfo.Arguments string from individual arguments, quoting each as needed.
        /// </summary>
        public static string BuildArguments(IEnumerable<string>? arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return string.Join(' ', arguments.Select(Quote));
        }

        private static bool ContainsSpecialCharacters(string argument)
        {
            foreach (var c in argument)
            {
                if (char.IsWhiteSpace(c) || c == DoubleQuote)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
