using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class WpfUIService : IUIService
    {
        public System.Windows.MessageBoxResult ShowMessageBox(string message, string title, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        {
            return System.Windows.MessageBox.Show(message, title, button, icon);
        }

        public void RestartApplication()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                processPath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(processPath))
            {
                System.Windows.MessageBox.Show("Unable to determine application path for restart.", "Restart Failed",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var currentPid = Process.GetCurrentProcess().Id;
            var workingDir = Path.GetDirectoryName(processPath) ?? "";
            bool isElevated = Program.IsRunningAsAdmin();

            var startInfo = RestartLogic.BuildRestartStartInfo(processPath, workingDir, currentPid, isElevated);

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to restart: {ex.Message}", "Restart Failed",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            System.Windows.Application.Current.Shutdown();
        }

        public bool IsRunningAsAdmin()
        {
            return Program.IsRunningAsAdmin();
        }
    }
}
