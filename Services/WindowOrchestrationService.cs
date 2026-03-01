using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates parallel execution of window providers.
    /// Handles structural diffing, caching, and icon population (via IWindowReconciler).
    /// 
    /// Delegates provider execution to <see cref="IProviderRunner"/> strategies:
    /// - In-process runners for fast, non-UIA providers
    /// - Out-of-process runners for UIA providers (prevents memory leaks)
    /// </summary>
    public class WindowOrchestrationService : IWindowOrchestrationService, IDisposable
    {
        private readonly List<IWindowProvider> _providers;
        private readonly IWindowReconciler _reconciler;
        private readonly INativeInteropWrapper _nativeInterop;
        private readonly IProviderRunner _fastRunner;
        private readonly IProviderRunner _uiaRunner;
        private readonly IUiaWorkerClient _uiaWorkerClient;
        private readonly ILogger? _logger;
        private readonly List<WindowItem> _allWindows = new();

        private readonly object _lock = new();
        // Re-entrancy guard for fast (Non-UIA) providers.
        private readonly SemaphoreSlim _fastRefreshLock = new(1, 1);
        private bool _disposed;

        public event EventHandler<WindowListUpdatedEventArgs>? WindowListUpdated;

        public IReadOnlyList<WindowItem> AllWindows
        {
            get
            {
                lock (_lock)
                {
                    return _allWindows.ToList();
                }
            }
        }

        /// <summary>
        /// Primary constructor with explicit runner strategies.
        /// </summary>
        public WindowOrchestrationService(
            IEnumerable<IWindowProvider> providers,
            IWindowReconciler reconciler,
            IUiaWorkerClient uiaWorkerClient,
            INativeInteropWrapper nativeInterop,
            IProviderRunner fastRunner,
            IProviderRunner uiaRunner,
            ILogger? logger = null,
            ISettingsService? settingsService = null)
        {
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            _uiaWorkerClient = uiaWorkerClient ?? throw new ArgumentNullException(nameof(uiaWorkerClient));
            _nativeInterop = nativeInterop ?? throw new ArgumentNullException(nameof(nativeInterop));
            _fastRunner = fastRunner ?? throw new ArgumentNullException(nameof(fastRunner));
            _uiaRunner = uiaRunner ?? throw new ArgumentNullException(nameof(uiaRunner));
            _logger = logger;
        }

        /// <summary>
        /// Backward compatibility constructor — creates default runners internally.
        /// </summary>
        public WindowOrchestrationService(
            IEnumerable<IWindowProvider> providers,
            IWindowReconciler reconciler,
            IUiaWorkerClient uiaWorkerClient,
            INativeInteropWrapper nativeInterop,
            ILogger? logger = null,
            ISettingsService? settingsService = null)
            : this(providers, reconciler, uiaWorkerClient, nativeInterop,
                   new InProcessProviderRunner(logger),
                   new UiaProviderRunner(uiaWorkerClient, logger),
                   logger, settingsService)
        {
        }

        // Backward compatibility constructor for tests
        public WindowOrchestrationService(IEnumerable<IWindowProvider> providers, IIconService? iconService = null, ISettingsService? settingsService = null)
            : this(providers, new WindowReconciler(iconService), new NullUiaWorkerClient(), new SwitchBlade.Core.NativeInteropWrapper(), null, settingsService)
        {
        }

        public async Task RefreshAsync(ISet<string> disabledPlugins)
        {
            // Non-blocking re-entrancy guard for fast (Non-UIA) providers.
            if (!await _fastRefreshLock.WaitAsync(0))
            {
                _logger?.Log("RefreshAsync skipped: fast-path scan already in progress.");
                return;
            }

            try
            {
                await Task.Run(async () =>
                {
                    disabledPlugins ??= new HashSet<string>();

                    // Clear process cache for fresh lookups
                    _nativeInterop.ClearProcessCache();

                    // 1. Reload settings and gather handled processes (for all providers)
                    var handledProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var provider in _providers)
                    {
                        try
                        {
                            provider.ReloadSettings();
                            foreach (var p in provider.GetHandledProcesses())
                            {
                                handledProcesses.Add(p);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"Error reloading settings for {provider.PluginName}", ex);
                        }
                    }

                    // 2. Inject exclusions (for all providers)
                    foreach (var provider in _providers)
                    {
                        provider.SetExclusions(handledProcesses);
                    }

                    // 3. Split providers into UIA (out-of-process) and non-UIA (in-process)
                    var nonUiaProviders = _providers.Where(p => !p.IsUiaProvider).ToList();
                    var uiaProviders = _providers.Where(p => p.IsUiaProvider).ToList();

                    // 4a. Run fast providers via the fast runner strategy
                    await _fastRunner.RunAsync(nonUiaProviders, disabledPlugins, handledProcesses, ProcessProviderResults);

                    // 4b. Run UIA providers via the UIA runner strategy (fire-and-forget)
                    if (uiaProviders.Count > 0)
                    {
                        await _uiaRunner.RunAsync(uiaProviders, disabledPlugins, handledProcesses, ProcessProviderResults);
                    }
                });
            }
            finally
            {
                _fastRefreshLock.Release();
            }
        }

        private void ProcessProviderResults(IWindowProvider provider, List<WindowItem> results)
        {
            WindowListUpdatedEventArgs args = null!;
            List<WindowItem>? reconciled = null;
            lock (_lock)
            {
                // Check LKG condition
                if (results.Count > 0 && results.All(r => r.IsFallback))
                {
                    bool hasExistingRealItems = HasExistingRealItems(provider);
                    if (hasExistingRealItems)
                    {
                        _logger?.Log($"[LKG] {provider.PluginName}: Transient failure (only fallback items received). Preserving {_allWindows.Count(w => w.Source == provider)} existing items.");

                        // DEFER Event Emission to outside the lock
                        args = new WindowListUpdatedEventArgs(provider, false);
                        goto EmitAndReturn;
                    }
                }

                long start = System.Diagnostics.Stopwatch.GetTimestamp();

                // Normal path: Replace existing items with new results
                for (int i = _allWindows.Count - 1; i >= 0; i--)
                {
                    if (_allWindows[i].Source == provider)
                        _allWindows.RemoveAt(i);
                }

                reconciled = _reconciler.Reconcile(results, provider);
                _allWindows.AddRange(reconciled);

                args = new WindowListUpdatedEventArgs(provider, true);

                if (_logger != null && SwitchBlade.Core.Logger.IsDebugEnabled)
                {
                    var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
                    _logger.Log($"[Perf] Reconciled {reconciled.Count} items for {provider.PluginName} in {elapsed.TotalMilliseconds:F2}ms");
                }
            }

        EmitAndReturn:
            // Emit event IMMEDIATELY so UI shows text - icons will pop in later
            EmitEvent(args);

            // If we jumped here from LKG, reconciled is null/empty, so we shouldn't populate icons.
            if (reconciled != null && reconciled.Count > 0)
            {
                Task.Run(() =>
                {
                    try
                    {
                        _reconciler.PopulateIcons(reconciled);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error populating icons for {provider.PluginName}", ex);
                    }
                });
            }
            return;
        }

        private void EmitEvent(WindowListUpdatedEventArgs args)
        {
            WindowListUpdated?.Invoke(this, args);
        }

        /// <summary>
        /// Checks whether any cached items for the given provider are non-fallback (real).
        /// Extracted for deterministic branch coverage of both conditions.
        /// </summary>
        private bool HasExistingRealItems(IWindowProvider provider)
        {
            foreach (var w in _allWindows)
            {
                if (w.Source == provider && !w.IsFallback)
                    return true;
            }
            return false;
        }

        #region Encapsulated Cache Mutators (Delegated to Reconciler)

        /// <summary>
        /// Gets the total number of cached window items (HWND + Provider records).
        /// Used for memory diagnostics.
        /// </summary>
        public int CacheCount => _reconciler.CacheCount;

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var provider in _providers)
            {
                if (provider is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error disposing provider {provider.PluginName}", ex);
                    }
                }
            }

            _uiaWorkerClient.Dispose();
            _fastRefreshLock.Dispose();

            if (_uiaRunner is IDisposable disposableRunner)
            {
                disposableRunner.Dispose();
            }
        }
    }
}
