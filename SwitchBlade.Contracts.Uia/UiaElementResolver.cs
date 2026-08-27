using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Configuration for <see cref="UiaElementResolver"/> controlling retry and fallback behavior.
    /// </summary>
    public sealed class UiaResolverOptions
    {
        /// <summary>
        /// Maximum resolution attempts (default 3 — UIA provider startup is flaky and every failed
        /// strategy aborts fast, so extra attempts are cheap on the failure path).
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds (default 50).
        /// </summary>
        public int RetryDelayMs { get; init; } = 50;

        /// <summary>
        /// If true, attempt FromPoint fallback using the window center after HWND binding, desktop
        /// search, and walker all fail (default true — last-resort strategy, ownership-verified).
        /// </summary>
        public bool UseFromPointFallback { get; init; } = true;

        /// <summary>
        /// Canonical options shared by every UIA plugin: bounded retries plus the FromPoint last resort.
        /// One definition keeps resolver behavior identical across plugins (previously only Windows
        /// Terminal opted into these values while Chrome/Notepad++/Teams used single-attempt defaults).
        /// </summary>
        public static UiaResolverOptions Default { get; } = new();
    }

    /// <summary>
    /// Shared multi-stage fallback for acquiring an <see cref="AutomationElement"/> from an HWND:
    /// 1. <see cref="AutomationElement.FromHandle(IntPtr)"/>
    /// 2. Desktop <see cref="AutomationElement.FindFirst"/> by PID + HWND
    /// 3. Desktop <see cref="TreeWalker"/> by PID + HWND
    /// 4. (Optional) <see cref="AutomationElement.FromPoint"/> via window center, verified against the HWND
    ///
    /// Strategies 2-4 verify NativeWindowHandle so a multi-window process can never yield
    /// an element belonging to one of its other windows.
    /// Eliminates duplicated TryGetAutomationElement implementations across plugins.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class UiaElementResolver
    {
        /// <summary>
        /// Attempts to resolve an <see cref="AutomationElement"/> for the given HWND/PID
        /// using a multi-stage fallback chain.
        /// </summary>
        public static AutomationElement? TryResolve(
            IntPtr hwnd,
            int pid,
            string callerName,
            ILogger? logger,
            UiaResolverOptions? options = null)
        {
            var opts = options ?? UiaResolverOptions.Default;

            for (int attempt = 1; attempt <= opts.MaxRetries; attempt++)
            {
                // Strategy 1: Direct HWND binding (Fastest)
                try
                {
                    return AutomationElement.FromHandle(hwnd);
                }
                catch (Exception ex)
                {
                    if (ex is System.Runtime.InteropServices.COMException comEx && (uint)comEx.HResult == 0x80004005)
                    {
                        logger?.Log($"{callerName}: Direct HWND access failed (E_FAIL). Attempting Desktop Root fallback...");
                    }
                    else
                    {
                        logger?.Log($"{callerName}: Direct HWND access failed: {ex.Message}. Attempting fallback...");
                    }
                }

                // Strategy 2: Desktop Root Search (Slower but more robust)
                try
                {
                    var root = AutomationElement.RootElement;
                    // Match by PID AND HWND so a multi-window process can't yield the wrong top-level window.
                    var condition = new AndCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
                        new PropertyCondition(AutomationElement.NativeWindowHandleProperty, (int)(long)hwnd));
                    var match = root.FindFirst(TreeScope.Children, condition);

                    if (match != null)
                    {
                        logger?.Log($"{callerName}: Successfully acquired root via Desktop FindFirst for HWND {hwnd}.");
                        return match;
                    }
                }
                catch (Exception fallbackEx)
                {
                    logger?.Log($"{callerName}: Desktop FindFirst fallback failed: {fallbackEx.Message}. Attempting TreeWalker...");
                }

                // Strategy 3: Desktop Walker (Most Robust, Slowest)
                try
                {
                    var walker = TreeWalker.ControlViewWalker;
                    var child = walker.GetFirstChild(AutomationElement.RootElement);

                    while (child != null)
                    {
                        try
                        {
                            if (child.Current.ProcessId == pid && child.Current.NativeWindowHandle == (int)(long)hwnd)
                            {
                                logger?.Log($"{callerName}: Successfully acquired root via Desktop Walker for HWND {hwnd}.");
                                return child;
                            }
                        }
                        catch { /* Skip restricted windows */ }

                        try
                        {
                            child = walker.GetNextSibling(child);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
                catch (Exception walkerEx)
                {
                    logger?.Log($"{callerName}: Desktop Walker fallback failed: {walkerEx.Message}");
                }

                // Strategy 4: FromPoint (optional, for UIPI/focus edge cases)
                if (opts.UseFromPointFallback)
                {
                    try
                    {
                        if (NativeInterop.GetWindowRect(hwnd, out var rect))
                        {
                            var centerX = rect.Left + (rect.Right - rect.Left) / 2;
                            var centerY = rect.Top + (rect.Bottom - rect.Top) / 2;
                            var point = new System.Windows.Point(centerX, centerY);

                            var element = AutomationElement.FromPoint(point);
                            if (element != null && IsOwnedByWindow(element, hwnd))
                            {
                                logger?.Log($"{callerName}: Successfully acquired root via FromPoint for HWND {hwnd}.");
                                return element;
                            }
                        }
                    }
                    catch (Exception pointEx)
                    {
                        logger?.Log($"{callerName}: FromPoint fallback failed: {pointEx.Message}");
                    }
                }

                if (attempt < opts.MaxRetries)
                {
                    System.Threading.Thread.Sleep(opts.RetryDelayMs);
                }
            }

            if (opts.MaxRetries > 1)
            {
                logger?.Log($"{callerName}: All fallback strategies failed for PID {pid} after {opts.MaxRetries} attempts.");
            }

            return null;
        }

        /// <summary>
        /// Maximum ancestor levels to walk when verifying FromPoint ownership.
        /// UIA trees are shallow (desktop → window → control), so this bounds pathological walks.
        /// </summary>
        private const int MaxOwnershipWalkDepth = 32;

        /// <summary>
        /// True when the element is the target window itself or a descendant of it.
        /// FromPoint can land on a child control that has no HWND of its own, so walk up
        /// the tree until an ancestor exposes the target NativeWindowHandle.
        /// </summary>
        private static bool IsOwnedByWindow(AutomationElement element, IntPtr hwnd)
        {
            int targetHandle = (int)(long)hwnd;
            var current = element;

            for (int depth = 0; current != null && depth < MaxOwnershipWalkDepth; depth++)
            {
                try
                {
                    if (current.Current.NativeWindowHandle == targetHandle)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Property unavailable on this element — keep walking up.
                }

                try
                {
                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }
                catch
                {
                    break;
                }
            }

            return false;
        }
    }
}
