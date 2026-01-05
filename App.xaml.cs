using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Views;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

namespace SwitchBlade;

/// <summary>
/// WinUI 3 Application class
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
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
    /// Primary constructor that accepts the DI container from Program.cs.
    /// </summary>
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger>();
    }

    /// <summary>
    /// Parameterless constructor required by WinUI generated code.
    /// At runtime, Program.Main creates the App with serviceProvider, so this is only used by the designer.
    /// </summary>
    public App()
    {
        InitializeComponent();
        // Fallback: Initialize DI container here for WinUI designer compatibility
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        _logger = _serviceProvider.GetRequiredService<ILogger>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger?.Log("Application Starting (WinUI)...");

        // Global exception handling for unhandled exceptions
        UnhandledException += (s, e) =>
        {
            try
            {
                _logger?.LogError("CRASH", e.Exception);
                System.Windows.Forms.MessageBox.Show(
                    $"SwitchBlade Crashed!\n\nReason: {e.Exception.Message}\n\nLog saved to %TEMP%\\switchblade_debug.log",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception lastResort)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Double Crash: {lastResort.Message}\nOrig: {e.Exception.Message}",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            e.Handled = true;
            Exit();
        };

        // Get services from DI container
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();

        // Apply theme immediately
        themeService.LoadCurrentTheme();

        // Handle MSI/MSIX installer startup flag - if /enablestartup was passed, enable Windows startup
        if (EnableStartupOnFirstRun)
        {
            _logger?.Log("EnableStartupOnFirstRun flag detected - enabling Windows startup");
            settingsService.Settings.LaunchOnStartup = true;
            settingsService.SaveSettings(); // This writes to Windows Run registry
        }

        // Setup Tray Icon (using WinForms interop)
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
        contextMenu.Items.Add("Exit", null, (s, args) => Exit());
        _trayIcon.ContextMenuStrip = contextMenu;

        // Double Click to Toggle
        _trayIcon.DoubleClick += (s, args) => ToggleMainWindow();

        // Create MainWindow with injected dependencies
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Only show the main window if not starting minimized
        if (!StartMinimized)
        {
            _mainWindow.Activate();
        }
        else
        {
            _logger?.Log("Starting minimized - MainWindow hidden until hotkey is pressed");
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        // If window is visible, hide it
        if (_mainWindow.Visible)
        {
            _mainWindow.Hide();
        }
        else
        {
            // Show the window
            _mainWindow.Activate();
            _mainWindow.ForceOpen();
        }
    }

    private Icon GetIcon()
    {
        try
        {
            // Load from embedded resource or file
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icon.png");
            if (System.IO.File.Exists(iconPath))
            {
                using var bitmap = new Bitmap(iconPath);
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch (Exception ex)
        {
            _logger?.Log($"Icon Error: {ex.Message}");
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
                    Name = provider.PluginName,
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
        var settingsWindow = new SettingsWindow();
        settingsWindow.ViewModel = settingsVm;
        // WinUI doesn't have ShowDialog in the same way, use Activate for now
        settingsWindow.Activate();
    }

    /// <summary>
    /// Gets the DI service provider for dependency resolution.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Clean shutdown of the application
    /// </summary>
    private void Cleanup()
    {
        _trayIcon?.Dispose();
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
