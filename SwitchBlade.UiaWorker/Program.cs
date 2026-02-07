using System.Reflection;
using System.Text.Json;
using System.IO;
using System.Linq;
using SwitchBlade.Contracts;

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

    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "switchblade_uia_debug.log");
    private static bool _loggingEnabled = false;

    internal static void DebugLog(string message)
    {
        if (!_loggingEnabled) return;
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* Ignore logging errors */ }
    }

    [STAThread] // Required for UI Automation
    public static void Main(string[] args)
    {
        // Check for debug flag
        _loggingEnabled = args.Any(arg =>
            arg.Equals("/debug", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));

        if (_loggingEnabled)
        {
            // Append to log on startup if debug enabled
            try 
            { 
                File.AppendAllText(LogFile, $"{Environment.NewLine}------------------------------------------------------------{Environment.NewLine}");
                File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] UIA Worker Started. BaseDir: {AppContext.BaseDirectory}{Environment.NewLine}"); 
            } 
            catch { }
        }

        UiaResponse response;

        try
        {
            // Read request from stdin
            string? requestLine = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                DebugLog("Error: No request received on stdin.");
                response = new UiaResponse { Success = false, Error = "No request received on stdin" };
                WriteResponse(response);
                return;
            }

            DebugLog($"Received request: {requestLine}");

            var request = JsonSerializer.Deserialize<UiaRequest>(requestLine, JsonOptions);
            if (request == null)
            {
                DebugLog("Error: Failed to parse request JSON.");
                response = new UiaResponse { Success = false, Error = "Failed to parse request JSON" };
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
                response = new UiaResponse { Success = false, Error = $"Unknown command: {request.Command}" };
            }
        }
        catch (Exception ex)
        {
            DebugLog($"Unhandled exception: {ex}");
            response = new UiaResponse { Success = false, Error = $"Unhandled exception: {ex.Message}" };
        }

        WriteResponse(response);
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

        // Initialize plugins with a minimal context (no logger in worker to keep it simple)
        var context = new MinimalPluginContext();
        foreach (var plugin in plugins)
        {
            try
            {
                DebugLog($"Initializing {plugin.PluginName}...");
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
                plugin.ReloadSettings();
                plugin.SetExclusions(excludedProcesses);

                var pluginWindows = plugin.GetWindows().ToList();
                DebugLog($"Plugin {plugin.PluginName} found {pluginWindows.Count} windows.");

                var windowResults = pluginWindows.Select(w => new UiaWindowResult
                {
                    Hwnd = w.Hwnd.ToInt64(),
                    Title = w.Title,
                    ProcessName = w.ProcessName,
                    ExecutablePath = w.ExecutablePath,
                    PluginName = plugin.PluginName
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

        // Wait for all plugins to complete
        Task.WaitAll(tasks);

        // Write final marker
        WriteFinalResult();

        DebugLog($"All plugins completed. Errors: {errors.Count}");

        // Return legacy response for compatibility (not used in streaming mode)
        return new UiaResponse
        {
            Success = errors.Count == 0,
            Error = errors.Count > 0 ? string.Join("; ", errors) : null,
            Windows = new List<UiaWindowResult>() // Results already streamed
        };
    }

    /// <summary>
    /// Thread-safe writer lock to prevent output interleaving between parallel plugins.
    /// </summary>
    private static readonly object WriteLock = new();

    /// <summary>
    /// Writes a single plugin's results as one atomic JSON line.
    /// Thread-safe: only one plugin can write at a time.
    /// </summary>
    private static void WritePluginResult(string pluginName, List<UiaWindowResult>? windows, string? error = null)
    {
        var result = new UiaPluginResult
        {
            PluginName = pluginName,
            Windows = windows,
            Error = error,
            IsFinal = false
        };

        lock (WriteLock)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            Console.Out.Flush();
        }

        DebugLog($"Streamed {windows?.Count ?? 0} windows from {pluginName}");
    }

    /// <summary>
    /// Writes the final marker indicating all plugins have completed.
    /// </summary>
    private static void WriteFinalResult()
    {
        var result = new UiaPluginResult { IsFinal = true };
        lock (WriteLock)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            Console.Out.Flush();
        }
        DebugLog("Wrote final marker.");
    }

    /// <summary>
    /// Dynamically loads all IWindowProvider implementations from the Plugins directory.
    /// Only loads plugins that use UIA (IsUiaProvider == true).
    /// </summary>
    private static List<IWindowProvider> LoadPlugins(List<string> errors)
    {
        var plugins = new List<IWindowProvider>();

        // Get the directory where UiaWorker.exe is located
        string baseDir = AppContext.BaseDirectory;
        string pluginsDir = Path.Combine(baseDir, "Plugins");

        DebugLog($"Checking Plugins dir: {pluginsDir}");

        if (!Directory.Exists(pluginsDir))
        {
            string msg = $"Plugins directory not found: {pluginsDir}";
            DebugLog(msg);
            errors.Add(msg);
            return plugins;
        }

        // Search for all DLLs in Plugins and subdirectories
        var dllFiles = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories);
        DebugLog($"Found {dllFiles.Length} DLLs in Plugins folder.");

        foreach (var dllPath in dllFiles)
        {
            try
            {
                // Skip non-plugin assemblies (e.g., SwitchBlade.Contracts if present)
                string fileName = Path.GetFileName(dllPath);
                if (!fileName.StartsWith("SwitchBlade.Plugins.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DebugLog($"Inspecting: {fileName}");
                var assembly = Assembly.LoadFrom(dllPath);

                // Find all types that implement IWindowProvider
                var providerTypes = assembly.GetTypes()
                    .Where(t => typeof(IWindowProvider).IsAssignableFrom(t)
                                && !t.IsInterface
                                && !t.IsAbstract);

                foreach (var type in providerTypes)
                {
                    try
                    {
                        DebugLog($"Found provider type: {type.FullName}");
                        var provider = (IWindowProvider)Activator.CreateInstance(type)!;

                        // Only load UIA providers (the whole point of this worker)
                        if (provider.IsUiaProvider)
                        {
                            DebugLog($"Adding UIA provider: {type.FullName}");
                            plugins.Add(provider);
                        }
                        else
                        {
                            DebugLog($"Skipping non-UIA provider: {type.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        string msg = $"Failed to instantiate {type.FullName}: {ex.Message}";
                        DebugLog(msg);
                        errors.Add(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to load assembly {dllPath}: {ex}");
                // Skip DLLs that can't be loaded (not .NET assemblies, etc.)
                // Don't add to errors - this is expected for native DLLs
            }
        }

        return plugins;
    }

    private static void WriteResponse(UiaResponse response)
    {
        string json = JsonSerializer.Serialize(response, JsonOptions);
        Console.WriteLine(json);
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
}

/// <summary>
/// Logger that bridges plugin logs to the UIA Worker's internal DebugLog.
/// This allows us to see detailed scan logs from plugins in switchblade_uia_debug.log.
/// </summary>
internal sealed class BridgedLogger : ILogger
{
    public static readonly BridgedLogger Instance = new();
    private BridgedLogger() { }

    public void Log(string message) => Program.DebugLog(message);
    public void LogError(string context, Exception ex) => Program.DebugLog($"ERROR [{context}]: {ex}");
}
