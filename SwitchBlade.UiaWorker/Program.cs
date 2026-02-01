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

    private static void DebugLog(string message)
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
            // Clear log on startup if debug enabled
            try { File.WriteAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] UIA Worker Started. BaseDir: {AppContext.BaseDirectory}{Environment.NewLine}"); } catch { }
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
        var windows = new List<UiaWindowResult>();
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

        // Run each enabled plugin
        foreach (var plugin in plugins)
        {
            if (disabledPlugins.Contains(plugin.PluginName))
            {
                DebugLog($"Skipping disabled plugin: {plugin.PluginName}");
                continue;
            }

            try
            {
                DebugLog($"Running plugin: {plugin.PluginName}");
                plugin.ReloadSettings();
                plugin.SetExclusions(excludedProcesses);

                var pluginWindows = plugin.GetWindows().ToList();
                DebugLog($"Plugin {plugin.PluginName} found {pluginWindows.Count} windows.");

                foreach (var window in pluginWindows)
                {
                    windows.Add(new UiaWindowResult
                    {
                        Hwnd = window.Hwnd.ToInt64(),
                        Title = window.Title,
                        ProcessName = window.ProcessName,
                        ExecutablePath = window.ExecutablePath,
                        PluginName = plugin.PluginName
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Plugin {plugin.PluginName} failed: {ex}");
                errors.Add($"Plugin {plugin.PluginName} failed: {ex.Message}");
            }
        }

        var response = new UiaResponse
        {
            Success = errors.Count == 0,
            Error = errors.Count > 0 ? string.Join("; ", errors) : null,
            Windows = windows
        };

        return response;
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
    public ILogger Logger => NullLogger.Instance;
}

/// <summary>
/// Dummy logger that does nothing.
/// </summary>
internal sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    private NullLogger() { }

    public void Log(string message) { }
    public void LogError(string context, Exception ex) { }
}
