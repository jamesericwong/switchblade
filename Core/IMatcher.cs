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
        /// Calculates a match score using the pre-normalized form of the target,
        /// avoiding re-normalization when scoring many items per keystroke.
        /// </summary>
        /// <param name="title">The original string to search in (used for exact-match fast path).</param>
        /// <param name="normalizedTitle">The canonical normalization of title.</param>
        /// <param name="query">The search query.</param>
        /// <returns>A score >= 0. Higher scores indicate better matches. 0 means no match.</returns>
        int ScoreWithNormalizedTitle(string title, string normalizedTitle, string query);

        /// <summary>
        /// Checks if a query matches a title.
        /// </summary>
        /// <param name="title">The string to search in.</param>
        /// <param name="query">The search query.</param>
        /// <returns>True if there is any match (score > 0).</returns>
        bool IsMatch(string title, string query);
    }
}
