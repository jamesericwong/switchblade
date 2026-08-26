using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Aggregated per-scan observability for transient UIA failures: counters are threaded through the
    /// scan and one summary line is emitted at the end, instead of logging every individual invalidation.
    /// Escalates to a warning when more than half of the probed elements failed transiently — that rate
    /// signals a zero-tab-class regression that must be diagnosable even with debug logging disabled.
    /// </summary>
    public sealed class ScanDiagnostics
    {
        private int _elementsProbed;
        private int _invalidatedElements;

        private readonly object _observationLock = new();
        private readonly Dictionary<string, int> _observations = new(StringComparer.Ordinal);

        /// <summary>Total UIA accesses attempted during the scan.</summary>
        public int ElementsProbed => Volatile.Read(ref _elementsProbed);

        /// <summary>UIA accesses that failed with a known transient error.</summary>
        public int InvalidatedElements => Volatile.Read(ref _invalidatedElements);

        /// <summary>True when the invalidation rate exceeds 50% of probed elements (and at least one probe occurred).</summary>
        public bool IsHighInvalidationRate => ElementsProbed > 0 && InvalidatedElements * 2 > ElementsProbed;

        /// <summary>Records a UIA access attempt.</summary>
        public void RecordProbe() => Interlocked.Increment(ref _elementsProbed);

        /// <summary>Records a transient failure of a UIA access.</summary>
        public void RecordInvalidation() => Interlocked.Increment(ref _invalidatedElements);

        /// <summary>
        /// Records the failure signature of a caught exception for per-scan observability. Every link in the
        /// InnerException chain contributes one signature: its type name, or "COMException(0xXXXXXXXX)" when
        /// the link carries an HRESULT — so nested raw COM failures stay visible behind framework wrappers.
        /// </summary>
        public void RecordObservation(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            var signatures = new List<string>(4);
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is COMException com)
                {
                    signatures.Add($"COMException(0x{(uint)com.HResult:X8})");
                }
                else
                {
                    signatures.Add(current.GetType().Name);
                }
            }

            lock (_observationLock)
            {
                foreach (var signature in signatures.Distinct())
                {
                    _observations[signature] = _observations.GetValueOrDefault(signature) + 1;
                }
            }
        }

        /// <summary>
        /// Formats the observed failure signatures, e.g. " [COMException(0x80040201)×3, ElementNotAvailableException×1]".
        /// Empty string when nothing failed — the summary line then stays in its original form.
        /// </summary>
        public string FormatObservations()
        {
            lock (_observationLock)
            {
                if (_observations.Count == 0)
                {
                    return string.Empty;
                }

                return " [" + string.Join(", ", _observations.Select(kv => $"{kv.Key}×{kv.Value}")) + "]";
            }
        }

        /// <summary>Formats the per-scan summary line, e.g. "ChromeTabFinder: 12 items from 40 probed elements; 3 invalidated [COMException(0x80040201)×2]".</summary>
        public string FormatSummary(string pluginName, int foundCount) =>
            $"{pluginName}: {foundCount} items from {ElementsProbed} probed elements; {InvalidatedElements} invalidated{FormatObservations()}";

        /// <summary>
        /// Emits the per-scan summary line. No-op when there is no logger or nothing was probed.
        /// </summary>
        public void Report(ILogger? logger, string pluginName, int foundCount)
        {
            if (logger == null || ElementsProbed == 0)
            {
                return;
            }

            var summary = FormatSummary(pluginName, foundCount);
            if (IsHighInvalidationRate)
            {
                logger.LogWarning(summary);
            }
            else
            {
                logger.Log(summary);
            }
        }
    }
}
