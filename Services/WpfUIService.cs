using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
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

            // Determine if we're de-elevating (currently admin, turning it off)
            bool isDeElevating = Program.IsRunningAsAdmin();

            ProcessStartInfo startInfo;

            if (isDeElevating)
            {
                var escapedPath = processPath.Replace("\"", "`\"");
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process explorer.exe -ArgumentList '\"{escapedPath}\"'";

                startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };
            }
            else
            {
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process '{processPath}' -WorkingDirectory '{workingDir}'";

                startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };
            }

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
