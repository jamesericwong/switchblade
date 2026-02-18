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
        private readonly IPluginContext _context;
        private readonly ISettingsService _settingsService;
        private readonly ILogger? _logger;
        private readonly IPluginLoader _pluginLoader;
        private readonly List<IWindowProvider> _providers = new();

        public IReadOnlyList<IWindowProvider> Providers => _providers;

        public PluginService(IPluginContext context, ISettingsService settingsService)
            : this(context, settingsService, null, new PluginLoader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins")))
        {
        }

        public PluginService(IPluginContext context, ISettingsService settingsService, ILogger? logger, IPluginLoader pluginLoader)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger;
            _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));

            LoadProviders();
        }

        private void LoadProviders()
        {
            _providers.Clear();

            // 1. Internal provider: WindowFinder
            var windowFinder = new WindowFinder(_settingsService);
            windowFinder.Initialize(_context);
            _providers.Add(windowFinder);

            // 2. External plugins — discover and then initialize each exactly once
            try
            {
                var plugins = _pluginLoader.LoadPlugins();
                
                foreach (var plugin in plugins)
                {
                    // Create per-plugin context with settings and initialize once
                    var pluginSettings = new PluginSettingsService(plugin.PluginName, _context.Logger);
                    var pluginContext = new PluginContext(_context.Logger, pluginSettings);
                    plugin.Initialize(pluginContext);
                    _providers.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error loading plugins", ex);
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
