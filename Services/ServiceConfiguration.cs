using System;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Composition root for dependency injection configuration.
    /// All services are registered via factories for 100% constructor-driven initialization.
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configures and returns the service provider with all registered services.
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Configures the service collection with all application services.
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {

            // System Abstractions (v1.9.11 coverage improvements)
            services.AddSingleton<IProcessFactory, ProcessFactory>();
            services.AddSingleton<IFileSystem, FileSystemWrapper>();
            services.AddSingleton<IRegistryService, RegistryServiceWrapper>();
            services.AddSingleton<INativeInteropWrapper, NativeInteropWrapper>();

            // Core Services
            services.AddSingleton<SettingsService>(sp =>
            {
                var registryService = sp.GetRequiredService<IRegistryService>();
                var logger = sp.GetRequiredService<ILogger>();
                return new SettingsService(
                    new RegistrySettingsStorage(@"Software\SwitchBlade", registryService),
                    new WindowsStartupManager(registryService),
                    logger);
            });
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<ThemeService>();
            services.AddSingleton<IDispatcherService, WpfDispatcherService>();
            services.AddSingleton<IIconService>(sp => new IconService(sp.GetRequiredService<ISettingsService>(), sp.GetRequiredService<IIconExtractor>()));
            services.AddSingleton<IIconExtractor, IconExtractor>();

            // Logger & Plugin Context
            services.AddSingleton<ILogger>(Logger.Instance);
            services.AddSingleton<IPluginContext>(sp => new PluginContext(sp.GetRequiredService<ILogger>()));
            services.AddSingleton<IWorkstationService, WorkstationService>();

            // New Services (v1.6.4)
            services.AddSingleton<IPluginLoader>(sp =>
                new PluginLoader(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"), sp.GetRequiredService<ILogger>()));

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IPluginService>(sp => new PluginService(
                sp.GetRequiredService<IPluginContext>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IPluginLoader>()));

            // Matching Algorithm
            services.AddSingleton<IMatcher, FuzzyMatcherAdapter>();

            // Window Search Service (with LRU cache)
            services.AddSingleton<IWindowSearchService>(sp =>
            {
                var settings = sp.GetRequiredService<ISettingsService>();
                int cacheSize = settings.Settings.RegexCacheSize;
                var matcher = sp.GetRequiredService<IMatcher>();
                return new WindowSearchService(new LruRegexCache(cacheSize), matcher);
            });

            services.AddSingleton<INumberShortcutService, NumberShortcutService>();

            // UIA Worker Client (out-of-process UIA scanning)
            services.AddSingleton<IUiaWorkerClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                var settings = sp.GetRequiredService<ISettingsService>();
                var processFactory = sp.GetRequiredService<IProcessFactory>();
                var fileSystem = sp.GetRequiredService<IFileSystem>();
                var timeoutSeconds = settings.Settings.UiaWorkerTimeoutSeconds;
                var timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 60);
                return new UiaWorkerClient(logger, timeout, processFactory, fileSystem);
            });

            // Window Orchestration Service (replaces manual provider coordination)
            services.AddSingleton<IWindowReconciler>(sp =>
                new WindowReconciler(sp.GetRequiredService<IIconService>(), sp.GetRequiredService<ILogger>()));

            services.AddSingleton<IWindowOrchestrationService>(sp =>
            {
                var pluginService = sp.GetRequiredService<IPluginService>();
                var reconciler = sp.GetRequiredService<IWindowReconciler>();
                var uiaWorkerClient = sp.GetRequiredService<IUiaWorkerClient>();
                var nativeInterop = sp.GetRequiredService<INativeInteropWrapper>();
                var logger = sp.GetRequiredService<ILogger>();
                var settingsService = sp.GetRequiredService<ISettingsService>();
                return new WindowOrchestrationService(pluginService.Providers, reconciler, uiaWorkerClient, nativeInterop, logger, settingsService);
            });

            // ViewModels
            services.AddTransient<MainViewModel>(sp => new MainViewModel(
                sp.GetRequiredService<IWindowOrchestrationService>(),
                sp.GetRequiredService<IWindowSearchService>(),
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IDispatcherService>()
            ));

            services.AddSingleton<IUIService, WpfUIService>();
            services.AddSingleton<MainWindow>();
            services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ThemeService>(),
                sp.GetRequiredService<IPluginService>(),
                sp.GetRequiredService<IUIService>()
            ));

            // Diagnostics (Investigation)
            services.AddSingleton<MemoryDiagnosticsService>();
        }
    }
}
