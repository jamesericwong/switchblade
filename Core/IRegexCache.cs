using System.Text.RegularExpressions;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Abstraction for caching compiled regex patterns.
    /// Enables testability and swappable caching strategies.
    /// </summary>
    public interface IRegexCache
    {
        /// <summary>
        /// Gets an existing regex from cache or creates a new one.
        /// </summary>
        /// <param name="pattern">The regex pattern to get or create.</param>
        /// <returns>A compiled Regex object, or null if the pattern is invalid.</returns>
        Regex? GetOrCreate(string pattern);

        /// <summary>
        /// Clears all cached regex patterns.
        /// </summary>
        void Clear();
    }
}
