namespace SwitchBlade.UiaWorker;

/// <summary>
/// Request sent from main app to UIA worker.
/// </summary>
public sealed class UiaRequest
{
    /// <summary>
    /// Command to execute. Currently only "scan" is supported.
    /// </summary>
    public string Command { get; set; } = "scan";

    /// <summary>
    /// List of plugin names to run (e.g., "Chrome", "WindowsTerminal", "NotepadPlusPlus").
    /// If empty or null, all UIA plugins are run.
    /// </summary>
    public List<string>? Plugins { get; set; }

    /// <summary>
    /// List of process names to exclude from scanning.
    /// </summary>
    public List<string>? ExcludedProcesses { get; set; }

    /// <summary>
    /// List of disabled plugin names to skip entirely.
    /// </summary>
    public List<string>? DisabledPlugins { get; set; }
}
