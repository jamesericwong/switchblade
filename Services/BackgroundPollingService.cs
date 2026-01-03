using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
namespace SwitchBlade.Services
{
    /// <summary>
    /// Service that polls all window providers in the background at a configurable interval.
    /// Uses a SemaphoreSlim to prevent concurrent refresh operations.
    /// </summary>
    public class BackgroundPollingService : IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly Func<Task> _refreshAction;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private System.Timers.Timer? _timer;
        private bool _disposed;

        public BackgroundPollingService(ISettingsService settingsService, IDispatcherService dispatcherService, Func<Task> refreshAction)
        {
            _settingsService = settingsService;
            _dispatcherService = dispatcherService;
            _refreshAction = refreshAction;

            // Subscribe to settings changes to dynamically update timer
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Initialize timer based on current settings
            ConfigureTimer();
        }

        private void ConfigureTimer()
        {
            // Dispose existing timer if any
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            if (!_settingsService.Settings.EnableBackgroundPolling)
            {
                SwitchBlade.Core.Logger.Log("BackgroundPollingService: Polling disabled.");
                return;
            }

            int intervalMs = _settingsService.Settings.BackgroundPollingIntervalSeconds * 1000;
            if (intervalMs < 1000) intervalMs = 1000; // Minimum 1 second

            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();

            SwitchBlade.Core.Logger.Log($"BackgroundPollingService: Polling enabled with interval {intervalMs}ms.");
        }

        private void OnSettingsChanged()
        {
            // Re-configure timer when settings change
            ConfigureTimer();
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Try to acquire lock without blocking; if already refreshing, skip this tick
            if (!_refreshLock.Wait(0))
            {
                SwitchBlade.Core.Logger.Log("BackgroundPollingService: Refresh already in progress, skipping tick.");
                return;
            }

            try
            {
                SwitchBlade.Core.Logger.Log("BackgroundPollingService: Running background refresh.");

                // Dispatch to UI thread since RefreshWindows updates ObservableCollection
                await _dispatcherService.InvokeAsync(async () =>
                {
                    await _refreshAction();
                });
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("BackgroundPollingService: Error during refresh", ex);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _settingsService.SettingsChanged -= OnSettingsChanged;
            _timer?.Stop();
            _timer?.Dispose();
            _refreshLock.Dispose();
        }
    }
}
