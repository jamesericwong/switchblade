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

        // Apply theme immediately
        _themeService.LoadCurrentTheme();

        // Create Tray Icon
        _trayIcon = new NotifyIcon
        {
            Icon = GetIcon(),
            Text = "SwitchBlade",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, (s, args) => OpenSettings());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, args) => Shutdown());
        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (s, args) => OpenSettings();
    }

    private Icon GetIcon()
    {
        try
        {
            // Look for icon.png in the same directory as the executable
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (System.IO.File.Exists(iconPath))
            {
                using var bitmap = new Bitmap(iconPath);
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch 
        {
            // Fallback to default if loading fails
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

