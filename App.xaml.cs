using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Views;
using Application = System.Windows.Application;

namespace SwitchBlade;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private SettingsService _settingsService;
    private ThemeService _themeService;

    public App()
    {
        _settingsService = new SettingsService();
        _themeService = new ThemeService(_settingsService);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handling
        // Global exception handling
        this.DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var crashLog = System.IO.Path.Combine(desktop, "switchblade_crash.txt");
                var errorMsg = $"[{DateTime.Now}] CRASH: {args.Exception.Message}\nStack: {args.Exception.StackTrace}\nInner: {args.Exception.InnerException}";
                System.IO.File.WriteAllText(crashLog, errorMsg);
                System.Windows.MessageBox.Show($"SwitchBlade Crashed!\n\nReason: {args.Exception.Message}\n\nLog saved to Desktop: switchblade_crash.txt", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception lastResort)
            {
                System.Windows.MessageBox.Show($"Double Crash: {lastResort.Message}\nOrig: {args.Exception.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            args.Handled = true;
            Shutdown();
        };

        // Apply theme immediately
        _themeService.LoadCurrentTheme();

        // 1. Initialize Settings (Registry migration happens here)
        // _settingsService is already initialized in the constructor.
        _settingsService.LoadSettings();

        // 2. Setup Tray Icon
        _trayIcon = new NotifyIcon
        {
            Icon = GetIcon(),
            Visible = true,
            Text = "SwitchBlade"
        };

        // Context Menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, (s, args) => OpenSettings());
        contextMenu.Items.Add("-"); // Separator
        contextMenu.Items.Add("Exit", null, (s, args) => Shutdown());
        _trayIcon.ContextMenuStrip = contextMenu;

        // Click to toggle
        _trayIcon.Click += (s, args) =>
        {
            if (args is System.Windows.Forms.MouseEventArgs me && me.Button == MouseButtons.Left)
            {
                // Toggle Window logic if needed, or just show settings?
                // Typically user uses Hotkey. 
                // Let's just OpenSettings for now or nothing.
                // Implementation choice: do nothing on single click, or bring to front?
            }
        };
    }

    private Icon GetIcon()
    {
        try
        {
            // Load from Embedded Resource
            var uri = new Uri("pack://application:,,,/icon.png");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                using var bitmap = new Bitmap(stream);
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch (Exception)
        {
             // Log error if needed: MessageBox.Show("Icon Error: " + ex.Message);
        }
        return SystemIcons.Application;
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_settingsService, _themeService);
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };
        settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
    
    // Services access for MainWindow (Service Locator pattern for simplicity in this small app)
    public SettingsService SettingsService => _settingsService;
    public ThemeService ThemeService => _themeService;
}

