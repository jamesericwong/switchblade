using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Thread-safe LRU cache for compiled regex patterns.
    /// Extracted from MainViewModel to follow Single Responsibility Principle.
    /// </summary>
    public class LruRegexCache : IRegexCache
    {
        private readonly Dictionary<string, Regex> _cache = new();
        private readonly LinkedList<string> _lruList = new();
        private readonly object _lock = new();
        private readonly int _maxSize;

        /// <summary>
        /// Creates a new LRU regex cache with the specified capacity.
        /// </summary>
        /// <param name="maxSize">Maximum number of patterns to cache.</param>
        public LruRegexCache(int maxSize = 50)
        {
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive.");

            _maxSize = maxSize;
        }

        /// <inheritdoc />
        public Regex? GetOrCreate(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return null;

            lock (_lock)
            {
                if (_cache.TryGetValue(pattern, out var existing))
                {
                    // Move to front (LRU update)
                    _lruList.Remove(pattern);
                    _lruList.AddFirst(pattern);
                    return existing;
                }

                try
                {
                    // NonBacktracking prevents ReDoS for user-provided patterns
                    // Available in .NET 7+
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

                    // Add to cache
                    _cache[pattern] = regex;
                    _lruList.AddFirst(pattern);

                    // Evict if exceeded capacity
                    while (_cache.Count > _maxSize && _lruList.Count > 0)
                    {
                        var last = _lruList.Last;
                        if (last != null)
                        {
                            _cache.Remove(last.Value);
                            _lruList.RemoveLast();
                        }
                    }

                    return regex;
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern
                    return null;
                }
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }

        /// <summary>
        /// Gets the current number of cached patterns.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }
}
