namespace SwitchBlade.Contracts
{
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
