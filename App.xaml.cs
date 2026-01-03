using System;
using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Views;
using SwitchBlade.Core;
using Application = System.Windows.Application;

namespace SwitchBlade;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private IServiceProvider _serviceProvider = null!;
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
        // Configure DI container
        _serviceProvider = ServiceConfiguration.ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SwitchBlade.Core.Logger.Log("Application Starting...");
        base.OnStartup(e);

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

        // Get services from DI container
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();

        // Apply theme immediately
        themeService.LoadCurrentTheme();

        // Handle MSI installer startup flag - if /enablestartup was passed, enable Windows startup
        if (EnableStartupOnFirstRun)
        {
            SwitchBlade.Core.Logger.Log("EnableStartupOnFirstRun flag detected - enabling Windows startup");
            settingsService.Settings.LaunchOnStartup = true;
            settingsService.SaveSettings(); // This writes to Windows Run registry
        }

        // Setup Tray Icon
        _trayIcon = new NotifyIcon
        {
            Icon = GetIcon(),
            Visible = true,
            Text = "SwitchBlade"
        };

        // Context Menu
        var contextMenu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show", null, (s, args) => ShowMainWindow());
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add("Settings", null, (s, args) => OpenSettings());
        contextMenu.Items.Add("-"); // Separator
        contextMenu.Items.Add("Exit", null, (s, args) => Shutdown());
        _trayIcon.ContextMenuStrip = contextMenu;

        // Double Click to Show
        _trayIcon.DoubleClick += (s, args) => ShowMainWindow();

        // Create MainWindow with injected dependencies
        _mainWindow = new MainWindow(_serviceProvider);

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

    private void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            // Replicate the logic from HotKeyService/MainWindow HotKey handler
            // to ensure consistent "fresh" state (preview hidden, search box focused)
            _mainWindow.Opacity = 0;
            _mainWindow.Show();

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();

            // Call public method on MainWindow that handles "Open"
            _mainWindow.ForceOpen();
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
        var plugins = new System.Collections.Generic.List<PluginInfo>();
        if (_mainWindow != null)
        {
            foreach (var provider in _mainWindow.Providers)
            {
                var type = provider.GetType();
                var assembly = type.Assembly;
                plugins.Add(new PluginInfo
                {
                    Name = provider.PluginName, // Use PluginName from interface
                    TypeName = type.FullName ?? type.Name,
                    AssemblyName = assembly.GetName().Name ?? "Unknown",
                    Version = assembly.GetName().Version?.ToString() ?? "0.0.0",
                    IsInternal = assembly == typeof(App).Assembly,
                    HasSettings = provider.HasSettings,
                    Provider = provider
                });
            }
        }

        var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        var settingsVm = new SettingsViewModel(settingsService, themeService, plugins);
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };
        settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }

    /// <summary>
    /// Gets the DI service provider for dependency resolution.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;
}
