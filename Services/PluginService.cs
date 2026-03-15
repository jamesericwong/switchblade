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
        private readonly IRegistryService _registryService;
        private readonly IPluginLoader _pluginLoader;
        private readonly WindowFinder _windowFinder;
        private readonly List<IWindowProvider> _providers = new();

        public IReadOnlyList<IWindowProvider> Providers => _providers;

        public PluginService(IPluginContext context, ISettingsService settingsService, IRegistryService registryService, ILogger? logger, IPluginLoader pluginLoader, WindowFinder windowFinder)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logger = logger;
            _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
            _windowFinder = windowFinder ?? throw new ArgumentNullException(nameof(windowFinder));

            LoadProviders();
        }

        private void LoadProviders()
        {
            _providers.Clear();

            // 1. Internal provider: WindowFinder
            _windowFinder.Initialize(_context);
            _providers.Add(_windowFinder);

            // 2. External plugins — discover and then initialize each exactly once
            try
            {
                var plugins = _pluginLoader.LoadPlugins();
                
                foreach (var plugin in plugins)
                {
                    // Create per-plugin context with settings and initialize once
                    var pluginSettings = new PluginSettingsService(plugin.PluginName, _registryService, _context.Logger);
                    var pluginContext = new PluginContext(_context.Logger, _context.Interop, _registryService, pluginSettings);
                    plugin.Initialize(pluginContext);
                    _providers.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError("Error loading plugins", ex);
                }
            }
        }

        public void ReloadPlugins()
        {
            LoadProviders();
        }

        public IEnumerable<PluginInfo> GetPluginInfos()
        {
            return _providers.Select(PluginInfoMapper.MapToInfo);
        }
    }
}
