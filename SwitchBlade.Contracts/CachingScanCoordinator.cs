using System;
using System.Collections.Generic;
using System.Threading;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Coordinates window-scan execution with automatic concurrency protection and result caching.
    /// 
    /// When a scan is already in progress, subsequent calls to <see cref="Run"/> return the cached
    /// results immediately instead of starting a duplicate scan. If a scan throws, the last successful
    /// cache is returned and the running flag is reset so the next call retries.
    /// 
    /// Uses ReaderWriterLockSlim for efficient concurrent cache reads.
    /// </summary>
    public sealed class CachingScanCoordinator : IDisposable
    {
        private readonly Func<string> _name;
        private readonly Func<ILogger?> _logger;

        private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
        private volatile bool _isScanRunning = false;
        private List<WindowItem> _cachedWindows = new();
        private bool _disposed;

        /// <summary>
        /// Creates a new scan coordinator.
        /// </summary>
        /// <param name="name">Plugin/provider name used as the log prefix (resolved at log time).</param>
        /// <param name="logger">Logger provider; may return null to disable logging.</param>
        public CachingScanCoordinator(Func<string> name, Func<ILogger?> logger)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Indicates whether a scan is currently in progress.
        /// When true, Run() will return cached results.
        /// </summary>
        public bool IsRunning => _isScanRunning;

        /// <summary>
        /// Returns the windows cached from the last successful scan.
        /// </summary>
        public IReadOnlyList<WindowItem> CachedResults
        {
            get
            {
                ThrowIfDisposed();
                _cacheLock.EnterReadLock();
                try
                {
                    return _cachedWindows.AsReadOnly();
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Runs the scan (deduplicated), caches its result and returns it.
        /// Callers that arrive while a scan is in progress receive the cached results instead.
        /// </summary>
        public IEnumerable<WindowItem> Run(Func<IEnumerable<WindowItem>> scan)
        {
            if (scan is null)
            {
                throw new ArgumentNullException(nameof(scan));
            }

            ThrowIfDisposed();

            // Fast path: if a scan is already running, return cached results (read lock only).
            if (_isScanRunning)
            {
                _cacheLock.EnterReadLock();
                try
                {
                    Log($"Scan in progress, returning {_cachedWindows.Count} cached results");
                    return _cachedWindows.ToList(); // Return a copy to avoid collection modification issues.
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
            }

            // Acquire write lock to set scan running flag.
            _cacheLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring lock.
                if (_isScanRunning)
                {
                    Log("Scan in progress (after lock), returning cached results");
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
                Log("Starting window scan");
                var results = scan().ToList();

                _cacheLock.EnterWriteLock();
                try
                {
                    _cachedWindows = results;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                Log($"Scan complete, found {results.Count} windows");

                // Defensive copy: the cached list must not be aliased to callers —
                // mutating the returned collection would corrupt the cache.
                return new List<WindowItem>(results);
            }
            catch (Exception ex)
            {
                LogError("Error during scan", ex);
                // Return cached results on error (read lock only).
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
        /// Clears the cached windows. Call this if you need to force a fresh scan on the next Run() call.
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();
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

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cacheLock.Dispose();
        }

        private void Log(string message) => _logger()?.Log($"{_name()}: {message}");

        private void LogError(string context, Exception ex) => _logger()?.LogError($"{_name()}: {context}", ex);

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CachingScanCoordinator));
            }
        }
    }
}
