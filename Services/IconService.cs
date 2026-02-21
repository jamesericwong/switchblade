using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Extracts and caches application icons from executable files.
    /// Icons are cached by full executable path to handle different versions of same-named executables.
    /// </summary>
    public class IconService : IIconService
    {
        private readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ISettingsService _settingsService;
        private readonly IIconExtractor _iconExtractor;

        public int CacheCount => _iconCache.Count;

        // Default to a safe limit if settings unavailable (though they should be)
        private const int FallbackMaxCacheSize = 200;

        public IconService(ISettingsService settingsService, IIconExtractor? iconExtractor = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _iconExtractor = iconExtractor ?? new IconExtractor();
        }

        /// <summary>
        /// Gets the icon for the specified executable path.
        /// Uses a cache to avoid repeated extractions.
        /// </summary>
        public ImageSource? GetIcon(string? executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
                return null;

            // Check cache size limit before adding new items
            int limit = _settingsService.Settings?.IconCacheSize ?? FallbackMaxCacheSize;

            // If cache is full and this is a new item, clear it to prevent unbounded growth
            if (_iconCache.Count >= limit && !_iconCache.ContainsKey(executablePath))
            {
                _iconCache.Clear();
                Core.Logger.Log($"Icon cache limit ({limit}) reached. Cleared cache.");
            }

            return _iconCache.GetOrAdd(executablePath, path => _iconExtractor.ExtractIcon(path));
        }

        /// <summary>
        /// Clears the icon cache to free memory.
        /// </summary>
        public void ClearCache()
        {
            _iconCache.Clear();
        }
    }
}
