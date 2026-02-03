namespace SwitchBlade.UiaWorker;

/// <summary>
/// Response sent from UIA worker back to main app.
/// </summary>
public sealed class UiaResponse
{
    /// <summary>
    /// Whether the scan completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// List of discovered windows.
    /// </summary>
    public List<UiaWindowResult>? Windows { get; set; }
}

/// <summary>
/// Streaming response for a single plugin's results (NDJSON protocol).
/// Each plugin emits ONE line containing all its windows to prevent interleaving.
/// </summary>
public sealed class UiaPluginResult
{
    /// <summary>
    /// Name of the plugin that produced these results.
    /// </summary>
    public string PluginName { get; set; } = "";

    /// <summary>
    /// Windows discovered by this plugin.
    /// </summary>
    public List<UiaWindowResult>? Windows { get; set; }

    /// <summary>
    /// Error message if the plugin failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// True if this is the final message (all plugins have completed).
    /// </summary>
    public bool IsFinal { get; set; }
}

/// <summary>
/// Serializable representation of a discovered window.
/// We don't serialize the full WindowItem because it contains non-serializable fields (Icon, Source).
/// </summary>
public sealed class UiaWindowResult
{
    /// <summary>
    /// Window handle as a long (IntPtr serialization).
    /// </summary>
    public long Hwnd { get; set; }

    /// <summary>
    /// Window or tab title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Process name (e.g., "chrome", "WindowsTerminal").
    /// </summary>
    public string ProcessName { get; set; } = "";

    /// <summary>
    /// Full path to the executable.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Name of the plugin that discovered this window.
    /// </summary>
    public string PluginName { get; set; } = "";
}
