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

            // Core Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<ThemeService>();
            services.AddSingleton<IDispatcherService, WpfDispatcherService>();
            services.AddSingleton<IIconService>(sp => new IconService(sp.GetRequiredService<ISettingsService>()));

            // Logger & Plugin Context
            services.AddSingleton<ILogger>(Logger.Instance);
            services.AddSingleton<IPluginContext>(sp => new PluginContext(sp.GetRequiredService<ILogger>()));

            // New Services (v1.6.4)
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IPluginService, PluginService>();

            // Window Search Service (with LRU cache)
            services.AddSingleton<IWindowSearchService>(sp =>
            {
                var settings = sp.GetRequiredService<ISettingsService>();
                int cacheSize = settings.Settings.RegexCacheSize;
                return new WindowSearchService(new LruRegexCache(cacheSize));
            });

            // Window Orchestration Service (replaces manual provider coordination)
            services.AddSingleton<IWindowOrchestrationService>(sp =>
            {
                var pluginService = sp.GetRequiredService<IPluginService>();
                var iconService = sp.GetRequiredService<IIconService>();
                return new WindowOrchestrationService(pluginService.Providers, iconService);
            });

            // ViewModels
            services.AddTransient<MainViewModel>(sp => new MainViewModel(
                sp.GetRequiredService<IWindowOrchestrationService>(),
                sp.GetRequiredService<IWindowSearchService>(),
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IDispatcherService>()
            ));

            services.AddSingleton<MainWindow>();
            services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ThemeService>(),
                sp.GetRequiredService<IPluginService>()
            ));

            // Diagnostics (Investigation)
            services.AddSingleton<MemoryDiagnosticsService>();

            return services.BuildServiceProvider();
        }
    }
}
