using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Client that spawns the UIA Worker process for out-of-process UI Automation scanning.
    /// 
    /// This eliminates UIA memory leaks by running all UIA scans in a separate process that
    /// terminates after each scan. When the process exits, Windows releases all UIA COM objects.
    /// </summary>
    public class UiaWorkerClient : IDisposable
    {
        private readonly string _workerPath;
        private readonly ILogger? _logger;
        private readonly TimeSpan _timeout;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Creates a new UIA Worker Client.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="timeout">Timeout for worker process execution. Default 10 seconds.</param>
        public UiaWorkerClient(ILogger? logger = null, TimeSpan? timeout = null)
        {
            _logger = logger;
            _timeout = timeout ?? TimeSpan.FromSeconds(10);

            // Find the worker executable relative to the main app
            var appDir = AppContext.BaseDirectory;
            _workerPath = Path.Combine(appDir, "SwitchBlade.UiaWorker.exe");

            if (!File.Exists(_workerPath))
            {
                _logger?.Log($"[UiaWorkerClient] WARNING: Worker not found at {_workerPath}");
            }
        }

        /// <summary>
        /// Runs a UIA scan in the worker process.
        /// </summary>
        /// <param name="disabledPlugins">Set of disabled plugin names to skip.</param>
        /// <param name="excludedProcesses">Set of process names to exclude from scanning.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered windows, or empty list on failure.</returns>
        public async Task<List<WindowItem>> ScanAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UiaWorkerClient));

            if (!File.Exists(_workerPath))
            {
                _logger?.Log($"[UiaWorkerClient] Worker executable not found: {_workerPath}");
                return new List<WindowItem>();
            }

            var request = new UiaRequest
            {
                Command = "scan",
                DisabledPlugins = disabledPlugins != null ? new List<string>(disabledPlugins) : null,
                ExcludedProcesses = excludedProcesses != null ? new List<string>(excludedProcesses) : null
            };

            try
            {
                var response = await ExecuteWorkerAsync(request, cancellationToken);

                if (!response.Success)
                {
                    _logger?.Log($"[UiaWorkerClient] Worker reported error: {response.Error}");
                }

                return ConvertToWindowItems(response.Windows);
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("[UiaWorkerClient] Scan cancelled.");
                return new List<WindowItem>();
            }
            catch (Exception ex)
            {
                _logger?.Log($"[UiaWorkerClient] Error: {ex.Message}");
                return new List<WindowItem>();
            }
        }

        private async Task<UiaResponse> ExecuteWorkerAsync(UiaRequest request, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var psi = new ProcessStartInfo
            {
                FileName = _workerPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };

            _logger?.Log($"[UiaWorkerClient] Starting worker: {_workerPath}");
            var startTime = Stopwatch.GetTimestamp();

            process.Start();

            // Send request via stdin
            string requestJson = JsonSerializer.Serialize(request, JsonOptions);
            await process.StandardInput.WriteLineAsync(requestJson);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Read response from stdout
            string? responseJson = await process.StandardOutput.ReadLineAsync(cts.Token);

            // Wait for process to exit (with timeout)
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout - kill the process
                _logger?.Log("[UiaWorkerClient] Worker timed out, killing process.");
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            _logger?.Log($"[UiaWorkerClient] Worker completed in {elapsed.TotalMilliseconds:F0}ms, exit code: {process.ExitCode}");

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
                return new UiaResponse
                {
                    Success = false,
                    Error = $"No response from worker. Exit code: {process.ExitCode}. Stderr: {stderr}"
                };
            }

            var response = JsonSerializer.Deserialize<UiaResponse>(responseJson, JsonOptions);
            return response ?? new UiaResponse { Success = false, Error = "Failed to parse response JSON" };
        }

        private List<WindowItem> ConvertToWindowItems(List<UiaWindowResult>? results)
        {
            if (results == null || results.Count == 0)
                return new List<WindowItem>();

            var items = new List<WindowItem>(results.Count);
            foreach (var r in results)
            {
                items.Add(new WindowItem
                {
                    Hwnd = new IntPtr(r.Hwnd),
                    Title = r.Title,
                    ProcessName = r.ProcessName,
                    ExecutablePath = r.ExecutablePath,
                    // Note: Source is set later by WindowOrchestrationService when merging results
                    Source = null
                });
            }
            return items;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // No persistent resources to dispose
        }
    }

    /// <summary>
    /// Request DTO for UIA Worker (must match SwitchBlade.UiaWorker.UiaRequest).
    /// </summary>
    internal sealed class UiaRequest
    {
        public string Command { get; set; } = "scan";
        public List<string>? Plugins { get; set; }
        public List<string>? ExcludedProcesses { get; set; }
        public List<string>? DisabledPlugins { get; set; }
    }

    /// <summary>
    /// Response DTO from UIA Worker (must match SwitchBlade.UiaWorker.UiaResponse).
    /// </summary>
    internal sealed class UiaResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<UiaWindowResult>? Windows { get; set; }
    }

    /// <summary>
    /// Window result from UIA Worker.
    /// </summary>
    internal sealed class UiaWindowResult
    {
        public long Hwnd { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string? ExecutablePath { get; set; }
        public string PluginName { get; set; } = "";
    }
}
