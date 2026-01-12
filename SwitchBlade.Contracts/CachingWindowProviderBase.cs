using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
    /// Uses ReaderWriterLockSlim for efficient concurrent cache reads.
    /// 
    /// Plugin developers should inherit from this class and override
    /// <see cref="ScanWindowsCore"/> with their scanning logic.
    /// </summary>
    public abstract class CachingWindowProviderBase : IWindowProvider
    {
        private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
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
        public IReadOnlyList<WindowItem> CachedWindows
        {
            get
            {
                _cacheLock.EnterReadLock();
                try
                {
                    return (IReadOnlyList<WindowItem>)_cachedWindows;
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
            }
        }

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
            // Fast path: if a scan is already running, return cached results (read lock only)
            if (_isScanRunning)
            {
                _cacheLock.EnterReadLock();
                try
                {
                    Logger?.Log($"{PluginName}: Scan in progress, returning {_cachedWindows.Count} cached results");
                    return _cachedWindows.ToList(); // Return a copy to avoid collection modification issues
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
            }

            // Acquire write lock to set scan running flag
            _cacheLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring lock
                if (_isScanRunning)
                {
                    Logger?.Log($"{PluginName}: Scan in progress (after lock), returning cached results");
                    return _cachedWindows.ToList();
                }

                _isScanRunning = true;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }

            try
            {
                Logger?.Log($"{PluginName}: Starting window scan");
                var results = ScanWindowsCore().ToList();

                _cacheLock.EnterWriteLock();
                try
                {
                    _cachedWindows = results;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                Logger?.Log($"{PluginName}: Scan complete, found {results.Count} windows");
                return results;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginName}: Error during scan", ex);
                // Return cached results on error (read lock only)
                _cacheLock.EnterReadLock();
                try
                {
                    return _cachedWindows.ToList();
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
            }
            finally
            {
                _cacheLock.EnterWriteLock();
                try
                {
                    _isScanRunning = false;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
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
            _cacheLock.EnterWriteLock();
            try
            {
                _cachedWindows = new List<WindowItem>();
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }
    }
}

