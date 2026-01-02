using System;
using System.IO;
using System.Windows;
using System.Threading;

namespace SwitchBlade
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            const string appName = "Global\\SwitchBlade_SingleInstance_Mutex";
            bool createdNew;

            using (var mutex = new Mutex(true, appName, out createdNew))
            {
                if (!createdNew)
                {
                    System.Windows.MessageBox.Show("SwitchBlade is already running!", "SwitchBlade", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Check for /debug flag before anything else
                var args = Environment.GetCommandLineArgs();
                bool debugEnabled = Array.Exists(args, arg => 
                    arg.Equals("/debug", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));
                
                SwitchBlade.Core.Logger.IsDebugEnabled = debugEnabled;

                try
                {
                    SwitchBlade.Core.Logger.Log("Process Started (Managed Entry Point Hit)");
                    
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
                        SwitchBlade.Core.Logger.Log("Starting in minimized mode");
                    }
                    if (enableStartup)
                    {
                        SwitchBlade.Core.Logger.Log("Enable startup on first run requested");
                    }
                    
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var crashLog = Path.Combine(desktop, "switchblade_startup_crash.txt");
                    SwitchBlade.Core.Logger.LogError("STARTUP CRASH", ex);
                    

                    
                    System.Windows.MessageBox.Show($"Critical Startup Error: {ex.Message}\n\nLog saved to %TEMP%\\switchblade_debug.log", "SwitchBlade Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
