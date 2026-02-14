namespace SwitchBlade.Services
{
    /// <summary>
    /// Exposes cache-count statistics for memory diagnostics.
    /// Implemented by services that maintain internal caches.
    /// </summary>
    public interface IDiagnosticsProvider
    {
        /// <summary>
        /// Gets the total number of cached entries.
        /// </summary>
        int CacheCount { get; }
    }
}
