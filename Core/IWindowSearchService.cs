using System.Collections.Generic;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Abstraction for window search and filtering operations.
    /// Enables testability and separation from ViewModel concerns.
    /// </summary>
    public interface IWindowSearchService
    {
        /// <summary>
        /// Searches and filters windows based on the query.
        /// </summary>
        /// <param name="windows">The collection of windows to search.</param>
        /// <param name="query">The search query (empty returns all windows sorted).</param>
        /// <param name="useFuzzy">Whether to use fuzzy matching.</param>
        /// <returns>Filtered and sorted list of matching windows.</returns>
        IList<WindowItem> Search(IEnumerable<WindowItem> windows, string query, bool useFuzzy);
    }
}
