using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Background service for tracking memory usage and cache statistics to diagnose leaks.
    /// Runs every 60 seconds.
    /// </summary>
    public class MemoryDiagnosticsService : IDisposable
    {
        private readonly PeriodicTimer _timer;
        private readonly CancellationTokenSource _cts;
        private Task? _executionTask;

        private readonly IWindowOrchestrationService _orchestrationService;
        private readonly IIconService _iconService;
        private readonly IWindowSearchService _searchService;
        private readonly ILogger _logger;

        public MemoryDiagnosticsService(
            IWindowOrchestrationService orchestrationService,
            IIconService iconService,
            IWindowSearchService searchService,
            ILogger logger)
        {
            _orchestrationService = orchestrationService;
            _iconService = iconService;
            _searchService = searchService;
            _logger = logger;

            _timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            _cts = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Log("MemoryDiagnosticsService starting...");
            _executionTask = RunDiagnosticsLoop();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Log("MemoryDiagnosticsService stopping...");
            _cts.Cancel();
            if (_executionTask != null)
            {
                try
                {
                    await _executionTask;
                }
                catch (OperationCanceledException) { }
            }
        }

        private async Task RunDiagnosticsLoop()
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    LogMemoryStats();
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError("MemoryDiagnosticsService loop error", ex);
            }
        }

        private void LogMemoryStats()
        {
            try
            {
                // Force a check on the current process
                using var proc = Process.GetCurrentProcess();
                proc.Refresh();

                long managedMemory = GC.GetTotalMemory(false); // Bytes
                long workingSet = proc.WorkingSet64;           // Bytes (RAM)
                long privateBytes = proc.PrivateMemorySize64;  // Bytes (Committed)
                int handleCount = proc.HandleCount;
                int threadCount = proc.Threads.Count;

                // Cache Stats — using IDiagnosticsProvider.CacheCount via interfaces
                int winCache = _orchestrationService.CacheCount;
                int iconCache = _iconService.CacheCount;

                string msg = $"\n[MEM-DIAG] " +
                             $"Managed: {FormatBytes(managedMemory)} | " +
                             $"Private: {FormatBytes(privateBytes)} | " +
                             $"WorkingSet: {FormatBytes(workingSet)} | " +
                             $"Handles: {handleCount} | " +
                             $"Threads: {threadCount} | " +
                             $"caches(Win/Icon): {winCache}/{iconCache}";

                _logger.Log(msg);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to log memory stats", ex);
            }
        }

        private static string FormatBytes(long bytes)
        {
            return $"{bytes / 1024 / 1024} MB";
        }

        public void Dispose()
        {
            _cts.Dispose();
            _timer.Dispose();
        }
    }
}
