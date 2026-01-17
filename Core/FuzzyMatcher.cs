using System;
using System.Runtime.CompilerServices;

namespace SwitchBlade.Core
{
    /// <summary>
    /// High-performance fuzzy matching service for window title search.
    /// Implements subsequence matching with delimiter normalization and intelligent scoring.
    /// </summary>
    /// <remarks>
    /// Performance optimizations:
    /// - Uses <see cref="Span{T}"/> and stackalloc for zero-allocation string operations
    /// - Early termination when match is impossible
    /// - Static methods to avoid object allocations
    /// - Inline aggressive methods for hot paths
    /// </remarks>
    public static class FuzzyMatcher
    {
        // Scoring constants
        private const int BaseMatchScore = 1;
        private const int ContiguityBonus = 2;
        private const int WordBoundaryBonus = 3;
        private const int StartsWithBonus = 5;

        // Maximum title length we'll process (longer titles are truncated for perf)
        private const int MaxNormalizedLength = 512;

        /// <summary>
        /// Calculates a fuzzy match score between a search query and a window title.
        /// </summary>
        /// <param name="title">The window title to search in.</param>
        /// <param name="query">The user's search query.</param>
        /// <returns>
        /// A score >= 0. Higher scores indicate better matches.
        /// Returns 0 if there is no match.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Score(string title, string query)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(title))
                return 0;

            // Fast path: exact contains check (common case)
            if (title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                int exactScore = query.Length * (BaseMatchScore + ContiguityBonus);
                // Bonus if title starts with query
                if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    exactScore += StartsWithBonus;
                return exactScore;
            }

            // Normalize and perform fuzzy match
            return ScoreNormalized(title.AsSpan(), query.AsSpan());
        }

        /// <summary>
        /// Performs fuzzy matching on normalized spans.
        /// </summary>
        private static int ScoreNormalized(ReadOnlySpan<char> title, ReadOnlySpan<char> query)
        {
            // Allocate normalized buffers on stack for small strings
            int titleLen = Math.Min(title.Length, MaxNormalizedLength);
            int queryLen = Math.Min(query.Length, MaxNormalizedLength);

            Span<char> normalizedTitle = titleLen <= 256 ? stackalloc char[titleLen] : new char[titleLen];
            Span<char> normalizedQuery = queryLen <= 64 ? stackalloc char[queryLen] : new char[queryLen];

            int actualTitleLen = Normalize(title.Slice(0, titleLen), normalizedTitle);
            int actualQueryLen = Normalize(query.Slice(0, queryLen), normalizedQuery);

            if (actualQueryLen == 0)
                return 0;

            // Trim the spans to actual normalized lengths
            normalizedTitle = normalizedTitle.Slice(0, actualTitleLen);
            normalizedQuery = normalizedQuery.Slice(0, actualQueryLen);

            // Quick rejection: if query is longer than title, no match possible
            if (actualQueryLen > actualTitleLen)
                return 0;

            return CalculateSubsequenceScore(normalizedTitle, normalizedQuery);
        }

        /// <summary>
        /// Normalizes a string for fuzzy matching:
        /// - Converts to lowercase
        /// - Treats spaces, underscores, and dashes as equivalent (normalized to empty/skip)
        /// </summary>
        /// <returns>The actual length of the normalized output (may be shorter due to delimiter removal).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Normalize(ReadOnlySpan<char> input, Span<char> output)
        {
            int writeIndex = 0;
            for (int i = 0; i < input.Length && writeIndex < output.Length; i++)
            {
                char c = input[i];

                // Skip delimiters entirely (space, underscore, dash)
                if (c == ' ' || c == '_' || c == '-')
                    continue;

                // Convert to lowercase inline
                output[writeIndex++] = char.ToLowerInvariant(c);
            }
            return writeIndex;
        }

        /// <summary>
        /// Calculates the fuzzy match score using subsequence matching with bonuses.
        /// </summary>
        private static int CalculateSubsequenceScore(ReadOnlySpan<char> title, ReadOnlySpan<char> query)
        {
            int queryIndex = 0;
            int score = 0;
            int lastMatchIndex = -1;
            bool matchedAtStart = false;

            for (int titleIndex = 0; titleIndex < title.Length && queryIndex < query.Length; titleIndex++)
            {
                if (title[titleIndex] == query[queryIndex])
                {
                    // Base score for matching character
                    score += BaseMatchScore;

                    // Contiguity bonus: consecutive matches
                    if (lastMatchIndex == titleIndex - 1)
                    {
                        score += ContiguityBonus;
                    }

                    // Word boundary bonus: match at position 0 or after a word boundary
                    // Since we removed delimiters, position 0 in normalized string or
                    // the first character match gets a bonus
                    if (titleIndex == 0)
                    {
                        matchedAtStart = true;
                        score += WordBoundaryBonus;
                    }

                    lastMatchIndex = titleIndex;
                    queryIndex++;
                }
            }

            // Did we match all query characters?
            if (queryIndex < query.Length)
                return 0; // Incomplete match

            // Starts-with bonus: if matching started at the beginning
            if (matchedAtStart && lastMatchIndex < title.Length)
            {
                score += StartsWithBonus;
            }

            return score;
        }

        /// <summary>
        /// Checks if a query matches a title using fuzzy matching.
        /// </summary>
        /// <param name="title">The window title to search in.</param>
        /// <param name="query">The user's search query.</param>
        /// <returns>True if there is any match (score > 0).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMatch(string title, string query)
        {
            return Score(title, query) > 0;
        }
    }
}
