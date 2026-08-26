namespace SwitchBlade.Services
{
    /// <summary>
    /// App-only UI service (message boxes, restart, admin check). Lives in the main assembly —
    /// not part of the shared Contracts kernel consumed by plugins and the UIA worker.
    /// </summary>
    public interface IUIService
    {
        System.Windows.MessageBoxResult ShowMessageBox(string message, string title, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon);
        void RestartApplication();
        bool IsRunningAsAdmin();

        /// <summary>
        /// Tracks whether a modal dialog (e.g., Settings) is currently open.
        /// When true, the global hotkey must not toggle the main window.
        /// </summary>
        bool IsModalDialogOpen { get; set; }
    }
}
