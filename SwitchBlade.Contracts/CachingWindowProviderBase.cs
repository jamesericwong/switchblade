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
    public abstract class CachingWindowProviderBase : IWindowProvider, IDisposable
    {
        private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
        private volatile bool _isScanRunning = false;
        private List<WindowItem> _cachedWindows = new();
        
        // Map PID -> List of "Good" (non-fallback) items from the last successful scan
        private readonly Dictionary<int, List<WindowItem>> _lastKnownGoodResults = new();

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
                    return _cachedWindows.AsReadOnly();
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
        /// <remarks>
        /// Base implementation returns false. Override to return true in UIA plugins.
        /// </remarks>
        public virtual bool IsUiaProvider => false;

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
        public virtual ISettingsControl? SettingsControl => null;

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
                var rawResults = ScanWindowsCore().ToList();
                var processedResults = new List<WindowItem>();

                // GLOBAL LKG STRATEGY:
                // 1. Group current results by PID
                // 2. For each PID, check if we got "Good" items (IsFallback == false) or only "Fallback" items.
                // 3. If "Good", update LKG cache.
                // 4. If "Fallback Only" AND we have LKG data, check if process is alive and restore LKG data.
                
                var currentPidGroups = rawResults.GroupBy(w => GetPid(w.Hwnd)).ToList();

                var pidsSeenInThisScan = new HashSet<int>();

                foreach (var group in currentPidGroups)
                {
                    int pid = group.Key;
                    if (pid == 0) continue; // Skip invalid PIDs

                    pidsSeenInThisScan.Add(pid);
                    var items = group.ToList();

                    bool hasGoodItems = items.Any(i => !i.IsFallback);

                    if (hasGoodItems)
                    {
                        // Success! Update LKG cache
                        _lastKnownGoodResults[pid] = items;
                        processedResults.AddRange(items);
                    }
                    else
                    {
                        // Only fallback items found (or empty).
                        // Do we have LKG data?
                        if (_lastKnownGoodResults.TryGetValue(pid, out var lkgItems))
                        {
                            // Verify process is still alive and accessible
                            var (procName, _) = GetProcessInfo((uint)pid);
                            if (procName != "Unknown" && procName != "System")
                            {
                                Logger?.Log($"{PluginName}: Transient failure for PID {pid}. Restoring {lkgItems.Count} items from LKG cache.");
                                processedResults.AddRange(lkgItems);
                            }
                            else
                            {
                                // Process likely dead, use current (fallback/empty) and clear LKG
                                processedResults.AddRange(items);
                                _lastKnownGoodResults.Remove(pid);
                            }
                        }
                        else
                        {
                            // No LKG data, accept fallback
                            processedResults.AddRange(items);
                        }
                    }
                }

                // Cleanup LKG: Remove PIDs that were NOT seen in this scan at all
                var deadPids = _lastKnownGoodResults.Keys.Where(k => !pidsSeenInThisScan.Contains(k)).ToList();
                foreach (var deadPid in deadPids)
                {
                    // SMART CLEANUP:
                    // Before removing an LKG entry, check if its windows are still valid.
                    // If the plugin returned 0 items (transient failure), the PID won't be in pidsSeenInThisScan.
                    // But if the windows still exist, we should KEEP the LKG data.
                    
                    var lkgItems = _lastKnownGoodResults[deadPid];
                    bool anyWindowStillValid = lkgItems.Any(item => IsWindowValid(item.Hwnd));

                    if (anyWindowStillValid)
                    {
                        // The process/windows still exist, but the scan missed them.
                        // Preserve LKG and add to current results.
                        Logger?.Log($"{PluginName}: PID {deadPid} missing from scan, but windows still valid. Preserving {lkgItems.Count} LKG items.");
                        processedResults.AddRange(lkgItems);
                    }
                    else
                    {
                        // Windows are truly gone. Remove from LKG.
                        _lastKnownGoodResults.Remove(deadPid);
                    }
                }

                _cacheLock.EnterWriteLock();
                try
                {
                    _cachedWindows = processedResults;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                Logger?.Log($"{PluginName}: Scan complete, found {processedResults.Count} windows");
                return processedResults;
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
        /// Retrieves the PID for a given window handle. Virtual for testability.
        /// </summary>
        protected virtual int GetPid(IntPtr hwnd)
        {
            NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
            return (int)pid;
        }

        /// <summary>
        /// Retrieves process info for a given PID. Virtual for testability.
        /// </summary>
        protected virtual (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
            return NativeInterop.GetProcessInfo(pid);
        }

        /// <summary>
        /// Checks if a window handle is still valid. Virtual for testability.
        /// </summary>
        protected virtual bool IsWindowValid(IntPtr hwnd)
        {
            return NativeInterop.IsWindow(hwnd);
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

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cacheLock.Dispose();
        }
    }
}

