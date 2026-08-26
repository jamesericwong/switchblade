using System;
using System.Collections.Generic;
using System.Linq;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Last-Known-Good (LKG) result-stabilization policy for window scans.
    /// 
    /// Tracks the best ("non-fallback") results per PID across scans:
    /// - A scan that yields "Good" items (<see cref="WindowItem.IsFallback"/> == false) for a PID updates its LKG entry.
    /// - A scan that yields only fallback items for a PID whose process is still alive restores the LKG entry instead.
    /// - PIDs missing from a scan keep their LKG data while any of their windows remain valid; otherwise it is discarded.
    /// - Windows with an unresolvable PID (0) are surfaced as-is, without LKG tracking.
    /// 
    /// The policy is stateful and thread-safe: call <see cref="Apply"/> with each raw scan result set
    /// to obtain the stabilized results to surface.
    /// </summary>
    public sealed class LastKnownGoodStrategy
    {
        private readonly Func<string> _name;
        private readonly Func<ILogger?> _logger;
        private readonly IWindowIntrospection _windows;

        // Map PID -> "Good" (non-fallback) items from the last successful scan for that PID.
        private readonly Dictionary<int, List<WindowItem>> _lastKnownGoodResults = new();
        private readonly object _gate = new();

        /// <summary>
        /// Creates a new LKG policy.
        /// </summary>
        /// <param name="name">Plugin/provider name used as the log prefix (resolved at log time).</param>
        /// <param name="logger">Logger provider; may return null to disable logging.</param>
        /// <param name="windows">OS-level window queries used by the policy.</param>
        public LastKnownGoodStrategy(Func<string> name, Func<ILogger?> logger, IWindowIntrospection windows)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        }

        /// <summary>
        /// Stabilizes a raw scan result set using the LKG policy and returns the results to surface.
        /// </summary>
        public List<WindowItem> Apply(IEnumerable<WindowItem> rawResults)
        {
            if (rawResults is null)
            {
                throw new ArgumentNullException(nameof(rawResults));
            }

            lock (_gate)
            {
                var processedResults = new List<WindowItem>();

                // 1. Group current results by PID
                // 2. For each PID, check if we got "Good" items (IsFallback == false) or only "Fallback" items.
                // 3. If "Good", update LKG cache.
                // 4. If "Fallback Only" AND we have LKG data, check if process is alive and restore LKG data.
                var currentPidGroups = rawResults.GroupBy(w => _windows.GetPid(w.Hwnd)).ToList();

                var pidsSeenInThisScan = new HashSet<int>();

                foreach (var group in currentPidGroups)
                {
                    int pid = group.Key;
                    var items = group.ToList();

                    if (pid == 0)
                    {
                        // PID unresolvable: surface the windows as-is without LKG tracking —
                        // they can't be grouped by process, so stabilization would mix unrelated windows.
                        processedResults.AddRange(items);
                        continue;
                    }

                    pidsSeenInThisScan.Add(pid);

                    bool hasGoodItems = items.Any(i => !i.IsFallback);

                    if (hasGoodItems)
                    {
                        // Success! Update LKG cache.
                        _lastKnownGoodResults[pid] = items;
                        processedResults.AddRange(items);
                    }
                    else
                    {
                        // Only fallback items found (or empty). Do we have LKG data?
                        if (_lastKnownGoodResults.TryGetValue(pid, out var lkgItems))
                        {
                            // Verify process is still alive and accessible.
                            var (procName, _) = _windows.GetProcessInfo((uint)pid);
                            if (procName != "Unknown" && procName != "System")
                            {
                                Log($"Transient failure for PID {pid}. Restoring {lkgItems.Count} items from LKG cache.");
                                processedResults.AddRange(lkgItems);
                            }
                            else
                            {
                                // Process likely dead, use current (fallback/empty) and clear LKG.
                                processedResults.AddRange(items);
                                _lastKnownGoodResults.Remove(pid);
                            }
                        }
                        else
                        {
                            // No LKG data, accept fallback.
                            processedResults.AddRange(items);
                        }
                    }
                }

                // Cleanup LKG: Remove PIDs that were NOT seen in this scan at all.
                var deadPids = _lastKnownGoodResults.Keys.Where(k => !pidsSeenInThisScan.Contains(k)).ToList();
                foreach (var deadPid in deadPids)
                {
                    // SMART CLEANUP:
                    // Before removing an LKG entry, check if its windows are still valid.
                    // If the scan returned 0 items for this PID (transient failure), it won't be in pidsSeenInThisScan.
                    // But if the windows still exist, we should KEEP the LKG data.
                    var lkgItems = _lastKnownGoodResults[deadPid];
                    bool anyWindowStillValid = lkgItems.Any(item => _windows.IsWindowValid(item.Hwnd));

                    if (anyWindowStillValid)
                    {
                        // The process/windows still exist, but the scan missed them.
                        // Preserve LKG and add to current results.
                        Log($"PID {deadPid} missing from scan, but windows still valid. Preserving {lkgItems.Count} LKG items.");
                        processedResults.AddRange(lkgItems);
                    }
                    else
                    {
                        // Windows are truly gone. Remove from LKG.
                        _lastKnownGoodResults.Remove(deadPid);
                    }
                }

                return processedResults;
            }
        }

        private void Log(string message) => _logger()?.Log($"{_name()}: {message}");
    }
}
