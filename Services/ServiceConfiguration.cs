using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Composition root for dependency injection configuration.
    /// Registers all services and plugins.
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configures and returns the service provider with all registered services.
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core Services - IMPORTANT: Use factory pattern to ensure SINGLE instance
            // for both interface and concrete requests
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<ThemeService>();
            services.AddSingleton<IDispatcherService, WpfDispatcherService>();

            // Logger
            services.AddSingleton<ILogger, LoggerBridge>();
            services.AddSingleton<IPluginContext>(sp => new PluginContext(sp.GetRequiredService<ILogger>()));

            // Window Providers
            services.AddSingleton<WindowFinder>(sp =>
            {
                var finder = new WindowFinder(sp.GetRequiredService<SettingsService>());
                finder.Initialize(sp.GetRequiredService<IPluginContext>());
                return finder;
            });

            // ViewModels
            services.AddTransient<MainViewModel>(sp =>
            {
                var providers = GetAllProviders(sp);
                return new MainViewModel(
                    providers,
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetRequiredService<IDispatcherService>()
                );
            });

            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Gets all window providers including dynamically loaded plugins.
        /// </summary>
        private static List<IWindowProvider> GetAllProviders(IServiceProvider sp)
        {
            var providers = new List<IWindowProvider>();
            var context = sp.GetRequiredService<IPluginContext>();

            // 1. Internal: WindowFinder
            providers.Add(sp.GetRequiredService<WindowFinder>());

            // 2. Load external plugins
            try
            {
                var pluginPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                var loader = new PluginLoader(pluginPath);
                var plugins = loader.LoadPlugins();

                foreach (var plugin in plugins)
                {
                    plugin.Initialize(context);
                    providers.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Error loading plugins", ex);
            }

            return providers;
        }
    }

    /// <summary>
    /// Logger bridge that implements ILogger from Contracts.
    /// </summary>
    public class LoggerBridge : ILogger
    {
        public void Log(string message) => SwitchBlade.Core.Logger.Log(message);
        public void LogError(string message, Exception? ex)
        {
            if (ex != null)
            {
                SwitchBlade.Core.Logger.LogError(message, ex);
            }
            else
            {
                SwitchBlade.Core.Logger.Log($"[Error] {message}");
            }
        }
    }
}
