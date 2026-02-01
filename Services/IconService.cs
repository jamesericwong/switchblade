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

        // Default to a safe limit if settings unavailable (though they should be)
        private const int FallbackMaxCacheSize = 200;

        public IconService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
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
            int limit = _settingsService?.Settings?.IconCacheSize ?? FallbackMaxCacheSize;

            // If cache is full and this is a new item, clear it to prevent unbounded growth
            if (_iconCache.Count >= limit && !_iconCache.ContainsKey(executablePath))
            {
                _iconCache.Clear();
                Core.Logger.Log($"Icon cache limit ({limit}) reached. Cleared cache.");
            }

            return _iconCache.GetOrAdd(executablePath, ExtractIcon);
        }

        /// <summary>
        /// Clears the icon cache to free memory.
        /// </summary>
        public void ClearCache()
        {
            _iconCache.Clear();
        }

        /// <summary>
        /// Extracts a small icon from the specified executable file.
        /// </summary>
        private static ImageSource? ExtractIcon(string executablePath)
        {
            IntPtr[]? smallIcons = null;
            try
            {
                // Extract one small icon from the executable
                smallIcons = new IntPtr[1];
                var largeIcons = new IntPtr[1];

                uint count = NativeInterop.ExtractIconEx(executablePath, 0, largeIcons, smallIcons, 1);

                if (count == 0 || smallIcons[0] == IntPtr.Zero)
                {
                    // Try large icon as fallback
                    if (largeIcons[0] != IntPtr.Zero)
                    {
                        var largeBitmapSource = CreateBitmapSourceFromHIcon(largeIcons[0]);
                        NativeInterop.DestroyIcon(largeIcons[0]);
                        return largeBitmapSource;
                    }
                    return null;
                }

                // Convert HICON to BitmapSource
                var bitmapSource = CreateBitmapSourceFromHIcon(smallIcons[0]);

                // Clean up large icon if extracted
                if (largeIcons[0] != IntPtr.Zero)
                {
                    NativeInterop.DestroyIcon(largeIcons[0]);
                }

                return bitmapSource;
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"Failed to extract icon from '{executablePath}'", ex);
                return null;
            }
            finally
            {
                // Always clean up the small icon handle
                if (smallIcons != null && smallIcons[0] != IntPtr.Zero)
                {
                    NativeInterop.DestroyIcon(smallIcons[0]);
                }
            }
        }

        /// <summary>
        /// Creates a frozen BitmapSource from an HICON handle.
        /// The returned BitmapSource is frozen for cross-thread access.
        /// </summary>
        private static BitmapSource? CreateBitmapSourceFromHIcon(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero)
                return null;

            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Freeze for cross-thread access and performance
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }
    }
}
