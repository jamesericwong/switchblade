using System;
using System.Collections.Generic;
using System.IO;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Manages plugin discovery and lifecycle.
    /// Encapsulates PluginLoader and provides a clean API for the DI container.
    /// </summary>
    public class PluginService : IPluginService
    {
        private readonly string _pluginPath;
        private readonly IPluginContext _context;
        private readonly ISettingsService _settingsService;
        private readonly List<IWindowProvider> _providers = new();

        public IReadOnlyList<IWindowProvider> Providers => _providers;

        public PluginService(IPluginContext context, ISettingsService settingsService)
            : this(context, settingsService, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"))
        {
        }

        public PluginService(IPluginContext context, ISettingsService settingsService, string pluginPath)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _pluginPath = pluginPath ?? throw new ArgumentNullException(nameof(pluginPath));

            LoadProviders();
        }

        private void LoadProviders()
        {
            _providers.Clear();

            // 1. Internal provider: WindowFinder
            var windowFinder = new WindowFinder(_settingsService);
            windowFinder.Initialize(_context);
            _providers.Add(windowFinder);

            // 2. External plugins
            try
            {
                if (Directory.Exists(_pluginPath))
                {
                    var loader = new PluginLoader(_pluginPath);
                    var plugins = loader.LoadPlugins(_context);
                    _providers.AddRange(plugins);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading plugins", ex);
            }
        }

        public void ReloadPlugins()
        {
            LoadProviders();
        }

        public IEnumerable<PluginInfo> GetPluginInfos()
        {
            return _providers.Select(p =>
            {
                var type = p.GetType();
                var assembly = type.Assembly;
                return new PluginInfo
                {
                    Name = p.PluginName,
                    TypeName = type.FullName ?? type.Name,
                    AssemblyName = assembly.GetName().Name ?? "Unknown",
                    Version = assembly.GetName().Version?.ToString() ?? "0.0.0",
                    IsInternal = assembly == typeof(PluginService).Assembly || assembly.GetName().Name == "SwitchBlade",
                    HasSettings = p.HasSettings,
                    Provider = p,
                    IsEnabled = true
                };
            });
        }
    }
}
