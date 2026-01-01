using System;
using System.IO;
using System.Windows;

namespace SwitchBlade
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var bootLog = Path.Combine(Path.GetTempPath(), "switchblade_boot.log");
            try
            {
                File.WriteAllText(bootLog, $"[{DateTime.Now}] Process Started (Managed Entry Point Hit)\n");
                
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
