using System;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Views;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using Application = System.Windows.Application;

namespace SwitchBlade;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
[ExcludeFromCodeCoverage]
public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private IServiceProvider _serviceProvider = null!;
    private MainWindow? _mainWindow;
    private ILogger? _logger;

    /// <summary>
    /// When true, the app starts without showing the main window (background mode).
    /// </summary>
    public static bool StartMinimized { get; set; } = false;

    /// <summary>
    /// When true (set via /enablestartup command-line from MSI), enables Windows startup registry.
    /// </summary>
    public static bool EnableStartupOnFirstRun { get; set; } = false;

    /// <summary>
    /// Tracks whether a modal dialog (e.g., Settings) is currently open.
    /// When true, the global hotkey will not toggle the main window.
    /// </summary>
    public static bool IsModalDialogOpen { get; set; } = false;

    /// <summary>
    /// Primary constructor that accepts the DI container from Program.cs.
    /// </summary>
    public App(IServiceProvider serviceProvider)
    {
        // Use the DI container created by Program.cs
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger>();
    }

    /// <summary>
    /// Parameterless constructor required by WPF generated code.
    /// At runtime, Program.Main creates the App with serviceProvider, so this is only used by the designer.
    /// </summary>
    public App()
    {
        // Fallback: Initialize DI container here for WPF designer compatibility
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        _logger = _serviceProvider.GetRequiredService<ILogger>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _logger?.Log("Application Starting...");
        base.OnStartup(e);

        // Global exception handling
        this.DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                _logger?.LogError("CRASH", args.Exception);
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

        // Start Diagnostics Service (Investigation)
        var diagService = _serviceProvider.GetRequiredService<MemoryDiagnosticsService>();
        diagService.StartAsync(new System.Threading.CancellationToken());

        // Apply theme immediately
        themeService.LoadCurrentTheme();

        // Handle MSI installer startup flag - if /enablestartup was passed, enable Windows startup
        if (EnableStartupOnFirstRun)
        {
            _logger?.Log("EnableStartupOnFirstRun flag detected - enabling Windows startup");
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
        var showHideItem = new ToolStripMenuItem("Show / Hide", null, (s, args) => ToggleMainWindow());
        showHideItem.Font = new Font(showHideItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(showHideItem);
        contextMenu.Items.Add("Settings", null, (s, args) => OpenSettings());
        contextMenu.Items.Add("-"); // Separator
        contextMenu.Items.Add("Exit", null, (s, args) => Shutdown());
        _trayIcon.ContextMenuStrip = contextMenu;

        // Double Click to Toggle
        _trayIcon.DoubleClick += (s, args) => ToggleMainWindow();

        // Create MainWindow with injected dependencies
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Only show the main window if not starting minimized
        if (!StartMinimized)
        {
            _mainWindow.Show();
        }
        else
        {
            _logger?.Log("Starting minimized - MainWindow hidden until hotkey is pressed");
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        // Suppress hotkey when a modal dialog (e.g., Settings) is open
        if (IsModalDialogOpen) return;

        // If window is visible, hide it
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            // Show the window
            _mainWindow.Opacity = 0;
            _mainWindow.Show();

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.ForceOpen();
        }
    }

    private Icon GetIcon()
    {
        try
        {
            // Load from Embedded Resource
            var uri = new Uri("pack://application:,,,/Resources/icon.png");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                using var bitmap = new Bitmap(stream);

                // Properly handle GDI resource cleanup
                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    using var iconWrapper = System.Drawing.Icon.FromHandle(hIcon);
                    return (System.Drawing.Icon)iconWrapper.Clone(); // Return a clone that owns its handle
                }
                finally
                {
                    NativeInterop.DestroyIcon(hIcon); // Destroy the original from GetHicon
                }
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
        var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };

        // Block global hotkey while settings is open (proper modal behavior)
        IsModalDialogOpen = true;
        try
        {
            // Use ShowDialog to make the settings window modal
            settingsWindow.ShowDialog();
        }
        finally
        {
            IsModalDialogOpen = false;
        }
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
