namespace SwitchBlade.Services
{
    /// <summary>
    /// Immutable application startup options derived from command-line arguments.
    /// Replaces the former public static flags on App.
    /// </summary>
    /// <param name="StartMinimized">When true, the app starts without showing the main window (background mode).</param>
    /// <param name="EnableStartupOnFirstRun">When true (set via /enablestartup from the MSI installer), enables the Windows startup registry entry.</param>
    public sealed record StartupOptions(bool StartMinimized = false, bool EnableStartupOnFirstRun = false);
}
