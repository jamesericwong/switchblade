namespace SwitchBlade.Core
{
    /// <summary>
    /// Abstraction for string matching/scoring algorithms.
    /// Enables swapping between fuzzy, regex, or exact matching strategies.
    /// </summary>
    public interface IMatcher
    {
        /// <summary>
        /// Calculates a match score between a search query and a target string.
        /// </summary>
        /// <param name="title">The string to search in.</param>
        /// <param name="query">The search query.</param>
        /// <returns>A score >= 0. Higher scores indicate better matches. 0 means no match.</returns>
        int Score(string title, string query);

        /// <summary>
        /// Checks if a query matches a title.
        /// </summary>
        /// <param name="title">The string to search in.</param>
        /// <param name="query">The search query.</param>
        /// <returns>True if there is any match (score > 0).</returns>
        bool IsMatch(string title, string query);
    }
}
