using System;
using System.Diagnostics;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Windows;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            const string appName = "Global\\SwitchBlade_SingleInstance_Mutex";
            bool createdNew;

            using (var mutex = new Mutex(true, appName, out createdNew))
            {
                // If mutex not immediately acquired, try waiting for a bit (handles restart scenarios)
                if (!createdNew)
                {
                    // Wait up to 2 seconds for previous instance to release mutex
                    // This handles the case where we're restarting and the old process is shutting down
                    bool acquired = mutex.WaitOne(2000);
                    if (!acquired)
                    {
                        System.Windows.MessageBox.Show("SwitchBlade is already running!", "SwitchBlade", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // Successfully acquired mutex after waiting, continue normally
                }

                // Check for /debug flag before anything else
                var args = Environment.GetCommandLineArgs();
                bool debugEnabled = Array.Exists(args, arg =>
                    arg.Equals("/debug", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));

                SwitchBlade.Core.Logger.IsDebugEnabled = debugEnabled;

                // Initialize DI container early so we can use ILogger
                var serviceProvider = ServiceConfiguration.ConfigureServices();
                var logger = serviceProvider.GetRequiredService<ILogger>();

                // Check if elevation is required but not running as admin
                if (ShouldElevate() && !IsRunningAsAdmin())
                {
                    logger.Log("RunAsAdministrator enabled but not elevated. Restarting with elevation...");
                    try
                    {
                        var processPath = Environment.ProcessPath;
                        if (string.IsNullOrEmpty(processPath))
                        {
                            processPath = Process.GetCurrentProcess().MainModule?.FileName;
                        }

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = processPath ?? "SwitchBlade.exe",
                            UseShellExecute = true,
                            Verb = "runas",
                            Arguments = string.Join(" ", args, 1, args.Length - 1), // Skip first arg (exe path)
                            WorkingDirectory = Path.GetDirectoryName(processPath) ?? ""
                        };

                        // Release mutex before starting new process to prevent "already running" error
                        mutex.ReleaseMutex();
                        mutex.Dispose();

                        Process.Start(startInfo);
                        Environment.Exit(0);
                        return;
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        // User cancelled UAC prompt or other elevation error
                        logger.Log($"Elevation failed/cancelled: {ex.Message}");
                        // If we released the mutex and failed to start, we are in a bad state.
                        // Better to exit than run without single-instance protection.
                        System.Windows.MessageBox.Show($"Failed to restart as Administrator: {ex.Message}", "SwitchBlade", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        Environment.Exit(1);
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Elevation Error", ex);
                        System.Windows.MessageBox.Show($"Failed to restart as Administrator: {ex.Message}", "SwitchBlade", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        Environment.Exit(1);
                        return;
                    }
                }

                try
                {
                    logger.Log("Process Started (Managed Entry Point Hit)");

                    // Parse command-line arguments for /minimized

                    bool startMinimized = Array.Exists(args, arg =>
                        arg.Equals("/minimized", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase));

                    // Parse command-line arguments for /enablestartup (set by MSI installer)
                    bool enableStartup = Array.Exists(args, arg =>
                        arg.Equals("/enablestartup", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--enablestartup", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-enablestartup", StringComparison.OrdinalIgnoreCase));

                    App.StartMinimized = startMinimized;
                    App.EnableStartupOnFirstRun = enableStartup;

                    if (startMinimized)
                    {
                        logger.Log("Starting in minimized mode");
                    }
                    if (enableStartup)
                    {
                        logger.Log("Enable startup on first run requested");
                    }

                    var app = new App(serviceProvider);
                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    logger.LogError("STARTUP CRASH", ex);
                    System.Windows.MessageBox.Show($"Critical Startup Error: {ex.Message}\n\nLog saved to %TEMP%\\switchblade_debug.log", "SwitchBlade Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Checks if the current process is running with Administrator privileges.
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the RunAsAdministrator setting is enabled in the registry.
        /// This is read directly from registry to avoid loading full SettingsService before elevation.
        /// </summary>
        private static bool ShouldElevate()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\SwitchBlade");
                if (key != null)
                {
                    var value = key.GetValue("RunAsAdministrator");
                    if (value != null)
                    {
                        return Convert.ToInt32(value) == 1;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
