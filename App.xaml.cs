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
    private MainWindow? _mainWindow;

    /// <summary>
    /// When true, the app starts without showing the main window (background mode).
    /// </summary>
    public static bool StartMinimized { get; set; } = false;

    /// <summary>
    /// When true (set via /enablestartup command-line from MSI), enables Windows startup registry.
    /// </summary>
    public static bool EnableStartupOnFirstRun { get; set; } = false;

    public App()
    {
        _settingsService = new SettingsService();
        _themeService = new ThemeService(_settingsService);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SwitchBlade.Core.Logger.Log("Application Starting...");
        base.OnStartup(e);

        // Global exception handling
        // Global exception handling
        this.DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                SwitchBlade.Core.Logger.LogError("CRASH", args.Exception);
                System.Windows.MessageBox.Show($"SwitchBlade Crashed!\n\nReason: {args.Exception.Message}\n\nLog saved to %TEMP%\\switchblade_debug.log", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Handle MSI installer startup flag - if /enablestartup was passed, enable Windows startup
        if (EnableStartupOnFirstRun)
        {
            SwitchBlade.Core.Logger.Log("EnableStartupOnFirstRun flag detected - enabling Windows startup");
            _settingsService.Settings.LaunchOnStartup = true;
            _settingsService.SaveSettings(); // This writes to Windows Run registry
        }

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

        // Create MainWindow manually (removed StartupUri from App.xaml)
        _mainWindow = new MainWindow();
        
        // Only show the main window if not starting minimized
        if (!StartMinimized)
        {
            _mainWindow.Show();
        }
        else
        {
            SwitchBlade.Core.Logger.Log("Starting minimized - MainWindow hidden until hotkey is pressed");
        }
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
        var plugins = new System.Collections.Generic.List<SwitchBlade.Core.PluginInfo>();
        if (_mainWindow != null)
        {
            foreach (var provider in _mainWindow.Providers)
            {
                var type = provider.GetType();
                var assembly = type.Assembly;
                plugins.Add(new SwitchBlade.Core.PluginInfo
                {
                    Name = type.Name, // Using Type Name as display name for now
                    TypeName = type.FullName ?? type.Name,
                    AssemblyName = assembly.GetName().Name ?? "Unknown",
                    Version = assembly.GetName().Version?.ToString() ?? "0.0.0",
                    IsInternal = assembly == typeof(App).Assembly
                });
            }
        }

        var settingsVm = new SettingsViewModel(_settingsService, _themeService, plugins);
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

