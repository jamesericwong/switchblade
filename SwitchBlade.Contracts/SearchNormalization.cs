using System;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Canonical normalization for fuzzy window-title search: lowercase and strip
    /// space/underscore/dash delimiters. This is the single reference implementation —
    /// <see cref="WindowItem.NormalizedTitle"/> caches it per item so callers (e.g., FuzzyMatcher)
    /// can score without re-normalizing titles on every keystroke.
    /// </summary>
    public static class SearchNormalization
    {
        /// <summary>
        /// Maximum number of raw characters to normalize; longer titles are truncated for perf.
        /// Must match the truncation limit used by FuzzyMatcher's scoring path.
        /// </summary>
        public const int MaxLength = 512;

        /// <summary>
        /// Normalizes a string for fuzzy search: lowercase, remove delimiters (space, underscore, dash).
        /// </summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            int length = Math.Min(input.Length, MaxLength);
            Span<char> buffer = length <= 256 ? stackalloc char[length] : new char[length];
            int writeIndex = 0;

            for (int i = 0; i < length && writeIndex < buffer.Length; i++)
            {
                char c = input[i];
                if (c == ' ' || c == '_' || c == '-')
                {
                    continue;
                }

                buffer[writeIndex++] = char.ToLowerInvariant(c);
            }

            return new string(buffer[..writeIndex]);
        }
    }
}
