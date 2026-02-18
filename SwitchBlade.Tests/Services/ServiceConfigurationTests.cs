using System;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using SwitchBlade.ViewModels;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class ServiceConfigurationTests
    {
        [Fact]
        public void ConfigureServices_ResolvesEssentialServices()
        {
            var serviceProvider = ServiceConfiguration.ConfigureServices();

            // Resolve some key services to trigger factory execution
            Assert.NotNull(serviceProvider.GetRequiredService<ISettingsService>());
            Assert.NotNull(serviceProvider.GetRequiredService<ILogger>());
            Assert.NotNull(serviceProvider.GetRequiredService<IWindowOrchestrationService>());
            Assert.NotNull(serviceProvider.GetRequiredService<IPluginService>());
            Assert.NotNull(serviceProvider.GetRequiredService<MainViewModel>());
            Assert.NotNull(serviceProvider.GetRequiredService<SettingsViewModel>());
            Assert.NotNull(serviceProvider.GetRequiredService<IUIService>());
        }
    }
}
