using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

[assembly: DisableRuntimeMarshalling]

namespace SwitchBlade.UiaWorker;

/// <summary>
/// Entry point for the UIA Worker process.
/// 
/// This process is spawned by the main SwitchBlade app to perform UI Automation scans.
/// When this process exits, Windows releases all UIA COM objects, preventing memory leaks.
/// 
/// Protocol:
/// - Reads a single JSON line from stdin (UiaRequest)
/// - Writes a single JSON line to stdout (UiaResponse)
/// - Exits immediately
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Timeout parity with v1.9.16: there is NO separate per-plugin deadline here. The host's whole-stream
    // budget (user setting UiaWorkerTimeoutSeconds, default 60s) is the only timeout — on expiry it cancels
    // the stream read and kills this worker. A hard-coded shorter cap (30s, introduced in eaa8d2a) aborted
    // slow-but-valid scans, e.g. Chrome over a large tab tree at ~45-60s, well before the configured budget.

    // Set once when the scan finishes; the worker is one-shot per process. Late writes from plugins that
    // complete after the final marker are discarded so they can't corrupt the stream.
    private static volatile bool _scanFinished;

    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "switchblade_uia_debug.log");
    private static bool _loggingEnabled = false;

    internal static void DebugLog(string message)
    {
        if (!_loggingEnabled)
        {
            return;
        }

        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* Ignore logging errors */ }
    }

    private static TextWriter _jsonWriter = TextWriter.Null;

    [STAThread] // Required for UI Automation
    public static void Main(string[] args)
    {
        // Capture the original stdout for our JSON protocol
        _jsonWriter = Console.Out;

        // Redirect Console.Out to null to prevent plugins/dependencies from polluting the stream
        Console.SetOut(TextWriter.Null);

        // Check for debug flag
        _loggingEnabled = args.Any(arg =>
            arg.Equals("/debug", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));

        // Check for parent PID for watchdog
        int parentPid = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--parent" || args[i] == "-parent") && i + 1 < args.Length)
            {
                _ = int.TryParse(args[i + 1], out parentPid);
                break;
            }
        }

        if (_loggingEnabled)
        {
            // Append to log on startup if debug enabled
            try 
            { 
                File.AppendAllText(LogFile, $"{Environment.NewLine}------------------------------------------------------------{Environment.NewLine}");
                File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] UIA Worker Started. PID: {Environment.ProcessId}. BaseDir: {AppContext.BaseDirectory}{Environment.NewLine}");
                if (parentPid > 0)
                {
                    File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] Monitoring Parent PID: {parentPid}{Environment.NewLine}");
                }
            } 
            catch { }
        }

        // Start Watchdog if parent PID is provided
        if (parentPid > 0)
        {
            StartParentWatchdog(parentPid);
        }

        UiaResponse response;

        try
        {
            // Read request from stdin
            string? requestLine = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                DebugLog("Error: No request received on stdin.");
                response = ErrorResponse("No request received on stdin");
                WriteResponse(response);
                return;
            }

            DebugLog($"Received request: {requestLine}");

            var request = JsonSerializer.Deserialize<UiaRequest>(requestLine, JsonOptions);
            if (request == null)
            {
                DebugLog("Error: Failed to parse request JSON.");
                response = ErrorResponse("Failed to parse request JSON");
                WriteResponse(response);
                return;
            }

            // Fail fast if the host speaks a protocol revision this worker cannot handle.
            if (!UiaProtocol.IsCompatibleVersion(request.ProtocolVersion))
            {
                DebugLog($"Unsupported protocol version {request.ProtocolVersion}.");
                response = ErrorResponse(
                    $"Unsupported protocol version {request.ProtocolVersion}; " +
                    $"worker supports v{UiaProtocol.LegacyVersion}-v{UiaProtocol.CurrentVersion}");
                WriteResponse(response);
                return;
            }

            // Process the scan command
            if (request.Command?.Equals("scan", StringComparison.OrdinalIgnoreCase) == true)
            {
                DebugLog(" executing scan...");
                response = ExecuteScan(request);
            }
            else
            {
                DebugLog($"Unknown command: {request.Command}");
                response = ErrorResponse($"Unknown command: {request.Command}");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"Unhandled exception: {ex}");
            response = ErrorResponse($"Unhandled exception: {ex.Message}");
        }

        WriteResponse(response);
    }

    private static void StartParentWatchdog(int parentPid)
    {
        Task.Run(() =>
        {
            try
            {
                // If parent is already gone, GetProcessById might throw or return a process that has exited
                var parent = System.Diagnostics.Process.GetProcessById(parentPid);
                parent.WaitForExit();
                DebugLog($"Parent process {parentPid} exited. Terminating worker.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                // Parent likely doesn't exist or we can't access it -> assume it's dead
                DebugLog($"Parent watchdog exception (assuming parent died): {ex.Message}");
                Environment.Exit(0);
            }
        });
    }

    private static UiaResponse ExecuteScan(UiaRequest request)
    {
        var errors = new List<string>();

        var disabledPlugins = new HashSet<string>(
            request.DisabledPlugins ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var excludedProcesses = new HashSet<string>(
            request.ExcludedProcesses ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        // Dynamically load plugins from Plugins directory
        DebugLog("Loading plugins...");
        var plugins = LoadPlugins(errors);
        DebugLog($"Loaded {plugins.Count} plugins.");

        // Initialize plugins with a minimal context
        foreach (var plugin in plugins)
        {
            try
            {
                DebugLog($"Initializing {plugin.PluginName}...");
                // Create per-plugin context with settings
                var registry = new RegistryServiceWrapper();
                var interop = new WindowInterop();
                var context = new MinimalPluginContext
                {
                    Settings = new PluginSettingsService(plugin.PluginName, registry, BridgedLogger.Instance),
                    Interop = interop,
                    Registry = registry
                };
                plugin.Initialize(context);
            }
            catch (Exception ex)
            {
                DebugLog($"Error initializing {plugin.PluginName}: {ex}");
                errors.Add($"Init failed for {plugin.PluginName}: {ex.Message}");
            }
        }

        // Get enabled plugins only
        var enabledPlugins = plugins
            .Where(p => !disabledPlugins.Contains(p.PluginName))
            .ToList();

        DebugLog($"Running {enabledPlugins.Count} enabled plugins in parallel...");

        // Run plugins in PARALLEL and stream results as each completes
        var tasks = enabledPlugins.Select(plugin => Task.Run(() =>
        {
            try
            {
                DebugLog($"Running plugin: {plugin.PluginName}");
                if (plugin is IConfigurablePlugin configurable)
                {
                    configurable.ReloadSettings();
                }

                if (plugin is IProviderExclusionSettings exclusionSettings)
                {
                    exclusionSettings.SetExclusions(excludedProcesses);
                }

                var pluginWindows = plugin.GetWindows().ToList();
                DebugLog($"Plugin {plugin.PluginName} found {pluginWindows.Count} windows.");

                var windowResults = pluginWindows.Select(w => new UiaWindowResult
                {
                    Hwnd = (long)w.Hwnd,
                    Title = w.Title,
                    ProcessName = w.ProcessName,
                    ExecutablePath = w.ExecutablePath,
                    PluginName = plugin.PluginName,
                    IsFallback = w.IsFallback
                }).ToList();

                // Stream this plugin's results immediately
                WritePluginResult(plugin.PluginName, windowResults);
            }
            catch (Exception ex)
            {
                DebugLog($"Plugin {plugin.PluginName} failed: {ex}");
                WritePluginResult(plugin.PluginName, null, ex.Message);
                lock (errors)
                {
                    errors.Add($"Plugin {plugin.PluginName} failed: {ex.Message}");
                }
            }
        })).ToArray();

        // No per-plugin deadline (v1.9.16 parity): plugins run until they finish; a hung one is reaped when
        // the host's whole-stream budget expires and kills this process. Completed results stream as they land,
        // so one slow plugin no longer truncates the others' budgets. Each task catches its own exceptions
        // (reported per-plugin), so WhenAll here only waits for completion, it cannot fault.
        Task.WhenAll(tasks).GetAwaiter().GetResult();

        _scanFinished = true;
        WriteFinalResult();

        DebugLog($"All plugins completed. Errors: {errors.Count}");

        // Return legacy response for compatibility (not used in streaming mode)
        return new UiaResponse
        {
            ProtocolVersion = UiaProtocol.CurrentVersion,
            Success = errors.Count == 0,
            Error = errors.Count > 0 ? string.Join("; ", errors) : null,
            Windows = [] // Results already streamed
        };
    }

    /// <summary>
    /// Thread-safe writer lock to prevent output interleaving between parallel plugins.
    /// </summary>
    private static readonly System.Threading.Lock WriteLock = new();

    /// <summary>
    /// Writes a single plugin's results as one atomic JSON line.
    /// Thread-safe: only one plugin can write at a time.
    /// </summary>
    private static void WritePluginResult(string pluginName, List<UiaWindowResult>? windows, string? error = null)
    {
        if (_scanFinished)
        {
            DebugLog($"Discarding late result from {pluginName} (scan already finished).");
            return;
        }

        var result = new UiaPluginResult
        {
            ProtocolVersion = UiaProtocol.CurrentVersion,
            PluginName = pluginName,
            Windows = windows,
            Error = error,
            IsFinal = false
        };

        lock (WriteLock)
        {
            _jsonWriter.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            _jsonWriter.Flush();
        }

        DebugLog($"Streamed {windows?.Count ?? 0} windows from {pluginName}");
    }

    /// <summary>
    /// Writes the final marker indicating all plugins have completed.
    /// </summary>
    private static void WriteFinalResult()
    {
        var result = new UiaPluginResult { ProtocolVersion = UiaProtocol.CurrentVersion, IsFinal = true };
        lock (WriteLock)
        {
            _jsonWriter.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            _jsonWriter.Flush();
        }
        DebugLog("Wrote final marker.");
    }

    /// <summary>
    /// Dynamically loads all IWindowProvider implementations from the Plugins directory.
    /// Discovery is shared with the host app via PluginDiscovery so both sides agree on
    /// which assemblies count as plugins (naming convention + subfolders).
    /// Only loads plugins that use UIA (IsUiaProvider == true).
    /// </summary>
    private static List<IWindowProvider> LoadPlugins(List<string> errors)
    {
        // Get the directory where UiaWorker.exe is located
        string baseDir = AppContext.BaseDirectory;
        string pluginsDir = Path.Combine(baseDir, "Plugins");

        DebugLog($"Checking Plugins dir: {pluginsDir}");

        if (!Directory.Exists(pluginsDir))
        {
            string msg = $"Plugins directory not found: {pluginsDir}";
            DebugLog(msg);
            errors.Add(msg);
            return new List<IWindowProvider>();
        }

        var dllFiles = PluginDiscovery.EnumeratePluginDlls(pluginsDir);
        DebugLog($"Found {dllFiles.Count} plugin DLLs in Plugins folder.");

        // Only load UIA providers (the whole point of this worker)
        return PluginDiscovery.DiscoverProviders(dllFiles, BridgedLogger.Instance, msg => errors.Add(msg))
            .Where(provider => provider is IExtrusionStrategy { IsUiaProvider: true })
            .ToList();
    }

    /// <summary>
    /// Builds a failure response that also declares this worker's protocol revision,
    /// so the host can always tell which build it is talking to.
    /// </summary>
    private static UiaResponse ErrorResponse(string message) => new()
    {
        Success = false,
        Error = message,
        ProtocolVersion = UiaProtocol.CurrentVersion
    };

    private static void WriteResponse(UiaResponse response)
    {
        string json = JsonSerializer.Serialize(response, JsonOptions);
        _jsonWriter.WriteLine(json);
        _jsonWriter.Flush();
        DebugLog($"Sent response. Success={response.Success}, Windows={response.Windows?.Count ?? 0}, Errors={response.Error}");
    }
}

/// <summary>
/// Minimal plugin context for the worker process.
/// We don't need logging in the worker - errors are returned via the response.
/// </summary>
internal sealed class MinimalPluginContext : IPluginContext
{
    public ILogger Logger => BridgedLogger.Instance;
    public IPluginSettingsService? Settings { get; init; }
    public IWindowInterop Interop { get; init; } = default!;
    public IRegistryService Registry { get; init; } = default!;
}

/// <summary>
/// Logger that bridges plugin logs to the UIA Worker's internal DebugLog.
/// This allows us to see detailed scan logs from plugins in switchblade_uia_debug.log.
/// </summary>
internal sealed class BridgedLogger : ILogger
{
    public static readonly BridgedLogger Instance = new();
    private BridgedLogger() { }

    public bool IsDebugEnabled { get; set; } = true;
    public void Log(string message) => Program.DebugLog(message);
    public void LogWarning(string message) => Program.DebugLog($"WARNING {message}");
    public void LogError(string context, Exception ex) => Program.DebugLog($"ERROR [{context}]: {ex}");
}
