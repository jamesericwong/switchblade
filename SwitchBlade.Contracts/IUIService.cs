namespace SwitchBlade.Contracts
{
    public interface IUIService
    {
        System.Windows.MessageBoxResult ShowMessageBox(string message, string title, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon);
        void RestartApplication();
        bool IsRunningAsAdmin();
    }
}
