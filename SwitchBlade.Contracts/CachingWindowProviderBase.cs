using System;
using System.Collections.Generic;
using System.Linq;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Abstract base class for window providers that provides automatic concurrency protection,
    /// result caching, and Last-Known-Good (LKG) stabilization.
    /// 
    /// When a scan is already in progress, subsequent calls to GetWindows() will return the cached
    /// results immediately instead of starting a duplicate scan.
    /// 
    /// The behavior is composed from two focused services: <see cref="CachingScanCoordinator"/>
    /// (single-flight scanning + cache) and <see cref="LastKnownGoodStrategy"/> (per-PID LKG policy).
    /// Plugins that need only part of this behavior can compose those services directly instead of
    /// inheriting from this class.
    /// 
    /// Plugin developers should inherit from this class and override
    /// <see cref="ScanWindowsCore"/> with their scanning logic.
    /// </summary>
    public abstract class CachingWindowProviderBase : IWindowProvider, IConfigurablePlugin, IProviderExclusionSettings, IExtrusionStrategy, IDisposable, IWindowIntrospection
    {
        private readonly CachingScanCoordinator _scanCoordinator;
        private readonly LastKnownGoodStrategy _lastKnownGood;

        protected CachingWindowProviderBase()
        {
            // The lambdas defer virtual dispatch until call time (inside GetWindows()), so
            // derived-constructor and Initialize() ordering cannot affect service construction.
            _scanCoordinator = new(() => PluginName, () => Logger);
            _lastKnownGood = new(() => PluginName, () => Logger, this);
        }

        /// <summary>
        /// Logger instance provided by the plugin context.
        /// Derived classes can use this for logging.
        /// </summary>
        protected ILogger? Logger { get; private set; }

        /// <summary>
        /// Native interop wrapper provided by the plugin context.
        /// Derived classes can use this for window and process operations.
        /// </summary>
        protected IWindowInterop? Interop { get; private set; }

        /// <summary>
        /// Indicates whether a scan is currently in progress.
        /// When true, GetWindows() will return cached results.
        /// </summary>
        public bool IsScanRunning => _scanCoordinator.IsRunning;

        /// <summary>
        /// Returns the currently cached windows from the last successful scan.
        /// </summary>
        public IReadOnlyList<WindowItem> CachedWindows => _scanCoordinator.CachedResults;

        /// <inheritdoc />
        public abstract string PluginName { get; }

        /// <inheritdoc />
        public abstract bool HasSettings { get; }

        /// <inheritdoc />
        public virtual bool IsUiaProvider => false;

        /// <inheritdoc />
        public virtual ISettingsControl? SettingsControl => null;

        /// <inheritdoc />
        public virtual void Initialize(IPluginContext context)
        {
            Logger = context.Logger;
            Interop = context.Interop;
        }

        /// <summary>
        /// Reloads settings for the provider.
        /// Default implementation is no-op.
        /// </summary>
        public virtual void ReloadSettings() { }

        /// <inheritdoc />
        public virtual IEnumerable<string> GetHandledProcesses() => Array.Empty<string>();

        /// <inheritdoc />
        public virtual void SetExclusions(IEnumerable<string> exclusions) { }

        /// <inheritdoc />
        public abstract void ActivateWindow(WindowItem item);

        /// <summary>
        /// Returns windows, either by running a new scan or returning cached results
        /// if a scan is already in progress. Raw scan results are stabilized through the LKG policy.
        /// </summary>
        public IEnumerable<WindowItem> GetWindows()
        {
            return _scanCoordinator.Run(() =>
            {
                var rawResults = ScanWindowsCore().ToList();
                return _lastKnownGood.Apply(rawResults);
            });
        }

        // IWindowIntrospection — explicit implementation so the protected virtuals below remain the
        // plugin-facing override surface (no new public members are added to this class).
        int IWindowIntrospection.GetPid(IntPtr hwnd) => GetPid(hwnd);

        (string ProcessName, string? ExecutablePath) IWindowIntrospection.GetProcessInfo(uint pid) => GetProcessInfo(pid);

        bool IWindowIntrospection.IsWindowValid(IntPtr hwnd) => IsWindowValid(hwnd);

        /// <summary>
        /// Retrieves the PID for a given window handle. Virtual for testability.
        /// Contract: returns 0 when the PID cannot be resolved — 0 is the shared "unknown PID"
        /// sentinel and can never be a real Windows process ID. Overrides must honor this.
        /// </summary>
        protected virtual int GetPid(IntPtr hwnd)
        {
            try
            {
                if (Interop != null)
                {
                    Interop.GetWindowThreadProcessId(hwnd, out uint pid);
                    return (int)pid;
                }

                NativeInterop.GetWindowThreadProcessId(hwnd, out uint pidStatic);
                return (int)pidStatic;
            }
            catch
            {
                return 0; // PID unresolvable — shared "unknown" sentinel
            }
        }

        /// <summary>
        /// Retrieves process info for a given PID. Virtual for testability.
        /// </summary>
        protected virtual (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
            return Interop?.GetProcessInfo(pid) ?? NativeInterop.GetProcessInfo(pid);
        }

        /// <summary>
        /// Checks if a window handle is still valid. Virtual for testability.
        /// </summary>
        protected virtual bool IsWindowValid(IntPtr hwnd)
        {
            return Interop?.IsWindowVisible(hwnd) ?? NativeInterop.IsWindowVisible(hwnd);
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
        protected void ClearCache() => _scanCoordinator.ClearCache();

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scanCoordinator.Dispose();
        }
    }
}
