namespace SwitchBlade.Services
{
    /// <summary>
    /// Service for extracting and caching application icons by executable path.
    /// </summary>
    public interface IIconService : IDiagnosticsProvider
    {
        /// <summary>
        /// Gets the icon for the specified executable. Returns null if extraction fails.
        /// </summary>
        System.Windows.Media.ImageSource? GetIcon(string? executablePath);

        /// <summary>
        /// Clears the icon cache.
        /// </summary>
        void ClearCache();
    }
}
