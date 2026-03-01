namespace SwitchBlade.Core
{
    /// <summary>
    /// Adapter that wraps the static <see cref="FuzzyMatcher"/> behind the <see cref="IMatcher"/> interface.
    /// This preserves the high-performance static implementation while enabling DI and testability.
    /// </summary>
    public class FuzzyMatcherAdapter : IMatcher
    {
        /// <inheritdoc />
        public int Score(string title, string query) => FuzzyMatcher.Score(title, query);

        /// <inheritdoc />
        public bool IsMatch(string title, string query) => FuzzyMatcher.IsMatch(title, query);
    }
}
