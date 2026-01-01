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

                var bootLog = Path.Combine(Path.GetTempPath(), "switchblade_boot.log");
                try
                {
                    File.WriteAllText(bootLog, $"[{DateTime.Now}] Process Started (Managed Entry Point Hit)\n");
                    
                    // Parse command-line arguments for /minimized
                    var args = Environment.GetCommandLineArgs();
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
                        File.AppendAllText(bootLog, $"[{DateTime.Now}] Starting in minimized mode\n");
                    }
                    if (enableStartup)
                    {
                        File.AppendAllText(bootLog, $"[{DateTime.Now}] Enable startup on first run requested\n");
                    }
                    
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var crashLog = Path.Combine(desktop, "switchblade_startup_crash.txt");
                    var errorMsg = $"[{DateTime.Now}] STARTUP CRASH: {ex.Message}\nStack: {ex.StackTrace}";
                    
                    try { File.WriteAllText(crashLog, errorMsg); } catch {}
                    
                    System.Windows.MessageBox.Show($"Critical Startup Error: {ex.Message}\n\nLog saved to Desktop: switchblade_startup_crash.txt", "SwitchBlade Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
