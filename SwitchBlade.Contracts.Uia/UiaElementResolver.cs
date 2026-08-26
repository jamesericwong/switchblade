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
        /// Maximum retry attempts (default 1 = no retries).
        /// </summary>
        public int MaxRetries { get; init; } = 1;

        /// <summary>
        /// Delay between retries in milliseconds (default 50).
        /// </summary>
        public int RetryDelayMs { get; init; } = 50;

        /// <summary>
        /// If true, attempt FromPoint fallback using the window center (default false).
        /// </summary>
        public bool UseFromPointFallback { get; init; } = false;

        /// <summary>
        /// Default options: single attempt, no FromPoint.
        /// </summary>
        public static UiaResolverOptions Default { get; } = new();
    }

    /// <summary>
    /// Shared three-stage fallback for acquiring an <see cref="AutomationElement"/> from an HWND:
    /// 1. <see cref="AutomationElement.FromHandle(IntPtr)"/>
    /// 2. Desktop <see cref="AutomationElement.FindFirst"/> by PID
    /// 3. Desktop <see cref="TreeWalker"/> by PID
    /// 4. (Optional) <see cref="AutomationElement.FromPoint"/> via window center
    ///
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
                    var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, pid);
                    var match = root.FindFirst(TreeScope.Children, condition);

                    if (match != null)
                    {
                        logger?.Log($"{callerName}: Successfully acquired root via Desktop FindFirst for PID {pid}.");
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
                            if (child.Current.ProcessId == pid)
                            {
                                logger?.Log($"{callerName}: Successfully acquired root via Desktop Walker for PID {pid}.");
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
                            if (element != null && element.Current.ProcessId == pid)
                            {
                                logger?.Log($"{callerName}: Successfully acquired root via FromPoint for PID {pid}.");
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
    }
}
