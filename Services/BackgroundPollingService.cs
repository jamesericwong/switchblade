using System;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Service that polls all window providers in the background at a configurable interval.
    /// Uses Modern .NET 6+ PeriodicTimer for efficient async polling.
    /// </summary>
    public class BackgroundPollingService : IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly Func<Task> _refreshAction;

        private CancellationTokenSource? _cts;
        private Task? _pollingTask;
        private bool _disposed;

        public BackgroundPollingService(ISettingsService settingsService, IDispatcherService dispatcherService, Func<Task> refreshAction)
        {
            _settingsService = settingsService;
            _dispatcherService = dispatcherService;
            _refreshAction = refreshAction;

            // Subscribe to settings changes to dynamically update timer
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Initialize timer based on current settings
            StartPolling();
        }

        private void StartPolling()
        {
            // Cancel previous polling if any
            StopPolling();

            if (!_settingsService.Settings.EnableBackgroundPolling)
            {
                SwitchBlade.Core.Logger.Log("BackgroundPollingService: Polling disabled.");
                return;
            }

            int intervalMs = _settingsService.Settings.BackgroundPollingIntervalSeconds * 1000;
            if (intervalMs < 1000) intervalMs = 1000; // Minimum 1 second

            _cts = new CancellationTokenSource();
            _pollingTask = PollingLoop(TimeSpan.FromMilliseconds(intervalMs), _cts.Token);

            SwitchBlade.Core.Logger.Log($"BackgroundPollingService: Polling enabled with interval {intervalMs}ms.");
        }

        private void StopPolling()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            // We don't await _pollingTask here to avoid blocking properties/events, 
            // but the loop will exit on cancellation.
        }

        private void OnSettingsChanged()
        {
            // Re-configure timer when settings change
            StartPolling();
        }

        private async Task PollingLoop(TimeSpan interval, CancellationToken token)
        {
            // Modern "PeriodicTimer" pattern
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    try
                    {
                        // Skip refresh when the workstation is locked.
                        // UIA/COM calls against locked desktops can hang for 10-15s,
                        // blocking the UI thread and making the app unresponsive on wake.
                        if (SwitchBlade.Contracts.NativeInterop.IsWorkstationLocked())
                        {
                            SwitchBlade.Core.Logger.Log("BackgroundPollingService: Workstation locked, skipping refresh.");
                            continue;
                        }

                        SwitchBlade.Core.Logger.Log("BackgroundPollingService: Running background refresh.");

                        // Dispatch to UI thread since RefreshWindows updates ObservableCollection
                        await _dispatcherService.InvokeAsync(async () =>
                        {
                            await _refreshAction();
                        });
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash loop
                        SwitchBlade.Core.Logger.LogError("BackgroundPollingService: Error during refresh", ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _settingsService.SettingsChanged -= OnSettingsChanged;
            StopPolling();
        }
    }
}
