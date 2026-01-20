using System;
using System.Collections.Generic;
using System.Linq;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Provides window search and filtering capabilities.
    /// Extracted from MainViewModel to follow Single Responsibility Principle.
    /// </summary>
    public class WindowSearchService : IWindowSearchService
    {
        private readonly IRegexCache _regexCache;

        /// <summary>
        /// Creates a new WindowSearchService with the specified regex cache.
        /// </summary>
        /// <param name="regexCache">The regex cache to use for pattern compilation.</param>
        public WindowSearchService(IRegexCache regexCache)
        {
            _regexCache = regexCache ?? throw new ArgumentNullException(nameof(regexCache));
        }

        /// <inheritdoc />
        public IList<WindowItem> Search(IEnumerable<WindowItem> windows, string query, bool useFuzzy)
        {
            if (windows == null)
                return new List<WindowItem>();

            var windowList = windows.ToList();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Empty query: return all windows sorted alphabetically
                return windowList
                    .Distinct()
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.Title)
                    .ThenBy(w => w.Hwnd.ToInt64())
                    .ToList();
            }

            List<WindowItem> results;

            if (useFuzzy)
            {
                // Fuzzy search: Score all items and filter/sort by score
                results = windowList
                    .Select(w => new { Item = w, Score = FuzzyMatcher.Score(w.Title, query) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Item.ProcessName)
                    .ThenBy(x => x.Item.Title)
                    .Select(x => x.Item)
                    .Distinct()
                    .ToList();
            }
            else
            {
                // Regex/substring matching
                var regex = _regexCache.GetOrCreate(query);

                if (regex != null)
                {
                    results = windowList
                        .Where(w => regex.IsMatch(w.Title))
                        .ToList();
                }
                else
                {
                    // Fallback to substring matching if regex is invalid
                    results = windowList
                        .Where(w => w.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                // Apply stable sort: Process Name -> Title -> Hwnd
                results = results
                    .Distinct()
                    .OrderBy(w => w.ProcessName)
                    .ThenBy(w => w.Title)
                    .ThenBy(w => w.Hwnd.ToInt64())
                    .ToList();
            }

            return results;
        }
    }
}
