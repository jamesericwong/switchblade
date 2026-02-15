using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Runtime.CompilerServices;
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
    public class UiaWorkerClient : IUiaWorkerClient
    {
        private readonly string _workerPath;
        private readonly ILogger? _logger;
        private readonly TimeSpan _timeout;
        private bool _disposed;
        
        // Concurrency management
        private Process? _activeProcess;
        private readonly object _processLock = new();
        private readonly CancellationTokenSource _disposeCts = new();

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
        /// Runs a UIA scan in the worker process with STREAMING results.
        /// Each plugin's results are yielded immediately as they complete.
        /// </summary>
        /// <param name="disabledPlugins">Set of disabled plugin names to skip.</param>
        /// <param name="excludedProcesses">Set of process names to exclude from scanning.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async stream of plugin results as they arrive.</returns>
        public async IAsyncEnumerable<UiaPluginResult> ScanStreamingAsync(
            ISet<string>? disabledPlugins = null,
            ISet<string>? excludedProcesses = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UiaWorkerClient));

            if (!File.Exists(_workerPath))
            {
                _logger?.Log($"[UiaWorkerClient] Worker executable not found: {_workerPath}");
                yield break;
            }

            var request = new UiaRequest
            {
                Command = "scan",
                DisabledPlugins = disabledPlugins != null ? new List<string>(disabledPlugins) : null,
                ExcludedProcesses = excludedProcesses != null ? new List<string>(excludedProcesses) : null
            };

            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            combinedCts.CancelAfter(_timeout);

            // Pass Parent PID for watchdog
            int currentPid = Environment.ProcessId;
            var args = SwitchBlade.Core.Logger.IsDebugEnabled 
                ? $"/debug --parent {currentPid}" 
                : $"--parent {currentPid}";

            var psi = new ProcessStartInfo
            {
                FileName = _workerPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // We don't use 'using' block here because we need to reference the process in the finally block for cleanup
            // and potentially kill it in Dispose()
            var process = new Process { StartInfo = psi };

            lock (_processLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(UiaWorkerClient));
                _activeProcess = process;
            }

            _logger?.Log($"[UiaWorkerClient] Starting streaming worker: {_workerPath} (ParentPID={currentPid})");
            var startTime = Stopwatch.GetTimestamp();

            process.Start();

            // Send request via stdin
            string requestJson = JsonSerializer.Serialize(request, JsonOptions);
            await process.StandardInput.WriteLineAsync(requestJson);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Task to read input (stdout)
            var readOutputTask = Task.Run(async () => 
            {
                var localResults = new List<UiaPluginResult>();
                 while (!combinedCts.Token.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = await process.StandardOutput.ReadLineAsync(combinedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (line == null) break;

                    try
                    {
                        var result = JsonSerializer.Deserialize<UiaPluginResult>(line, JsonOptions);
                        if (result != null) localResults.Add(result);
                    }
                    catch (JsonException ex)
                    {
                        _logger?.Log($"[UiaWorkerClient] Failed to parse streaming line: {ex.Message}");
                    }
                }
                return localResults;
            }, combinedCts.Token);


            // BUT wait, I need to yield return. I cannot yield return from inside a Task.Run.
            // I need to read stdout in the main loop, but I MUST drain stderr in the background.

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Log worker stderr to main log with a prefix
                    _logger?.Log($"[UiaWorker STDERR] {e.Data}");
                }
            };
            process.BeginErrorReadLine();

            // Read streaming responses line by line (STDOUT)
            try
            {
                while (!combinedCts.Token.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await process.StandardOutput.ReadLineAsync(combinedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger?.Log("[UiaWorkerClient] Streaming read cancelled/timed out.");
                    try { process.Kill(entireProcessTree: true); } catch { }
                    yield break;
                }

                if (line == null)
                {
                    // Process ended or closed stdout
                    break;
                }

                UiaPluginResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<UiaPluginResult>(line, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger?.Log($"[UiaWorkerClient] Failed to parse streaming line: {ex.Message}");
                    continue;
                }

                if (result == null)
                    continue;

                if (result.IsFinal)
                {
                    _logger?.Log("[UiaWorkerClient] Received final marker.");
                    break;
                }

                _logger?.Log($"[UiaWorkerClient] Received {result.Windows?.Count ?? 0} windows from {result.PluginName}");
                yield return result;
            }

            }
            finally
            {
                // Ensure active process is cleared and potentially killed if we are exiting abnormally
                // or just to ensure cleanup
                lock (_processLock)
                {
                    _activeProcess = null;
                }

                // Wait for process to exit or kill if needed
                try
                {
                    // Give it a grace period to exit naturally if it hasn't already
                    if (!process.HasExited)
                    {
                        try 
                        {
                            // If cancellation was requested, we should kill it.
                            if (combinedCts.Token.IsCancellationRequested)
                            {
                                process.Kill(entireProcessTree: true);
                            }
                            else
                            {
                                // Otherwise wait briefly
                                await process.WaitForExitAsync(combinedCts.Token);
                            }
                        }
                        catch 
                        {
                             // Last resort kill
                             if (!process.HasExited) process.Kill(entireProcessTree: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[UiaWorkerClient] Error during process cleanup: {ex.Message}");
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                }

                process.Dispose();
                combinedCts.Dispose();
                
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                _logger?.Log($"[UiaWorkerClient] Streaming worker completed in {elapsed.TotalMilliseconds:F0}ms");
            }
        }

        /// <summary>
        /// Runs a UIA scan in the worker process.
        /// Convenience wrapper that collects all streaming results into a single list.
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

            var allWindows = new List<WindowItem>();

            try
            {
                await foreach (var result in ScanStreamingAsync(disabledPlugins, excludedProcesses, cancellationToken))
                {
                    if (result.Error != null)
                    {
                        _logger?.Log($"[UiaWorkerClient] Plugin {result.PluginName} error: {result.Error}");
                    }

                    allWindows.AddRange(ConvertToWindowItems(result.Windows));
                }
                return allWindows;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("[UiaWorkerClient] Scan cancelled.");
                return allWindows; // Return any results collected before cancellation
            }
            catch (Exception ex)
            {
                _logger?.LogError("[UiaWorkerClient] ScanAsync failed mid-stream", ex);
                return allWindows;
            }
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
                    IsFallback = r.IsFallback,
                    // Note: Source is set later by WindowOrchestrationService when merging results
                    Source = null
                });
            }
            return items;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            lock (_processLock)
            {
                if (_disposed) return;
                _disposed = true;

                // Signal any ongoing scans to cancel
                _disposeCts.Cancel();

                // Ruthlessly kill the active process if it exists
                if (_activeProcess != null)
                {
                    try
                    {
                        if (!_activeProcess.HasExited)
                        {
                            _logger?.Log($"[UiaWorkerClient] Dispose called - killing active worker PID {_activeProcess.Id}");
                            _activeProcess.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[UiaWorkerClient] Failed to kill active process on Dispose: {ex.Message}");
                    }
                    finally
                    {
                        _activeProcess = null;
                    }
                }
            }
            
            _disposeCts.Dispose();
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
    /// Streaming response for a single plugin's results (NDJSON protocol).
    /// Must match SwitchBlade.UiaWorker.UiaPluginResult.
    /// </summary>
    public sealed class UiaPluginResult
    {
        public string PluginName { get; set; } = "";
        public List<UiaWindowResult>? Windows { get; set; }
        public string? Error { get; set; }
        public bool IsFinal { get; set; }
    }

    /// <summary>
    /// Window result from UIA Worker.
    /// </summary>
    public sealed class UiaWindowResult
    {
        public long Hwnd { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string? ExecutablePath { get; set; }
        public string PluginName { get; set; } = "";
        public bool IsFallback { get; set; }
    }
}
