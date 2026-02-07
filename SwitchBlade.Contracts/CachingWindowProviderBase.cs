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
                
                var currentPidGroups = rawResults.GroupBy(w => 
                {
                    // Try to finding PID from NativeInterop cache if possible, or fallback to 0
                    // Since WindowItem doesn't hold PID directly, we might need a way to group.
                    // Ideally WindowItem should have PID, but for now we can infer from Hwnd via helper.
                    NativeInterop.GetWindowThreadProcessId(w.Hwnd, out uint pid);
                    return (int)pid;
                }).ToList();

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
                            var (procName, _) = NativeInterop.GetProcessInfo((uint)pid);
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
                // (This handles closed applications)
                // We must be careful: ScanWindowsCore might filter PIDs itself. 
                // We should only remove PIDs if we are sure they are gone.
                // Ideally, we iterate _lastKnownGoodResults keys and check if they are in pidsSeenInThisScan.
                // BUT, if ScanWindowsCore returns NO results for a PID, does it mean it's closed?
                // Yes, usually.
                
                var deadPids = _lastKnownGoodResults.Keys.Where(k => !pidsSeenInThisScan.Contains(k)).ToList();
                foreach (var deadPid in deadPids)
                {
                    _lastKnownGoodResults.Remove(deadPid);
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

