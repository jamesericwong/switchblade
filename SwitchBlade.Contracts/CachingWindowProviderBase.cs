using System;
using System.Collections.Generic;
using System.Linq;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Abstract base class for window providers that provides automatic
    /// concurrency protection and result caching.
    /// 
    /// When a scan is already in progress, subsequent calls to GetWindows()
    /// will return the cached results immediately instead of starting
    /// a duplicate scan.
    /// 
    /// Plugin developers should inherit from this class and override
    /// <see cref="ScanWindowsCore"/> with their scanning logic.
    /// </summary>
    public abstract class CachingWindowProviderBase : IWindowProvider
    {
        private readonly object _scanLock = new object();
        private volatile bool _isScanRunning = false;
        private IList<WindowItem> _cachedWindows = new List<WindowItem>();

        /// <summary>
        /// Logger instance provided by the plugin context.
        /// Derived classes can use this for logging.
        /// </summary>
        protected ILogger? Logger { get; private set; }

        /// <summary>
        /// Indicates whether a scan is currently in progress.
        /// When true, GetWindows() will return cached results.
        /// </summary>
        public bool IsScanRunning => _isScanRunning;

        /// <summary>
        /// Returns the currently cached windows from the last successful scan.
        /// </summary>
        public IReadOnlyList<WindowItem> CachedWindows => (IReadOnlyList<WindowItem>)_cachedWindows;

        /// <inheritdoc />
        public abstract string PluginName { get; }

        /// <inheritdoc />
        public abstract bool HasSettings { get; }

        /// <inheritdoc />
        public virtual void Initialize(IPluginContext context)
        {
            Logger = context.Logger;
        }

        /// <inheritdoc />
        public virtual void ReloadSettings()
        {
            // Override in derived classes if needed
        }

        /// <inheritdoc />
        public virtual IEnumerable<string> GetHandledProcesses() => Array.Empty<string>();

        /// <inheritdoc />
        public virtual void SetExclusions(IEnumerable<string> exclusions)
        {
            // Default implementation: do nothing
        }

        /// <inheritdoc />
        public abstract void ShowSettingsDialog(IntPtr ownerHwnd);

        /// <inheritdoc />
        public abstract void ActivateWindow(WindowItem item);

        /// <summary>
        /// Returns windows, either by running a new scan or returning cached results
        /// if a scan is already in progress.
        /// </summary>
        public IEnumerable<WindowItem> GetWindows()
        {
            // Fast path: if a scan is already running, return cached results
            if (_isScanRunning)
            {
                Logger?.Log($"{PluginName}: Scan in progress, returning {_cachedWindows.Count} cached results");
                return _cachedWindows.ToList(); // Return a copy to avoid collection modification issues
            }

            lock (_scanLock)
            {
                // Double-check after acquiring lock
                if (_isScanRunning)
                {
                    Logger?.Log($"{PluginName}: Scan in progress (after lock), returning cached results");
                    return _cachedWindows.ToList();
                }

                _isScanRunning = true;
            }

            try
            {
                Logger?.Log($"{PluginName}: Starting window scan");
                var results = ScanWindowsCore().ToList();

                lock (_scanLock)
                {
                    _cachedWindows = results;
                }

                Logger?.Log($"{PluginName}: Scan complete, found {results.Count} windows");
                return results;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginName}: Error during scan", ex);
                // Return cached results on error
                return _cachedWindows.ToList();
            }
            finally
            {
                lock (_scanLock)
                {
                    _isScanRunning = false;
                }
            }
        }

        /// <summary>
        /// Override this method with the actual window scanning logic.
        /// This method is called by GetWindows() when no scan is in progress.
        /// </summary>
        /// <returns>The scanned window items.</returns>
        protected abstract IEnumerable<WindowItem> ScanWindowsCore();

        /// <summary>
        /// Clears the cached windows. Call this if you need to force
        /// a fresh scan on the next GetWindows() call.
        /// </summary>
        protected void ClearCache()
        {
            lock (_scanLock)
            {
                _cachedWindows = new List<WindowItem>();
            }
        }
    }
}
