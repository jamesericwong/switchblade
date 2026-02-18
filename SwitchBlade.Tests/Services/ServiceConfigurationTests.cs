using System;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using SwitchBlade.ViewModels;
using Xunit;
using System.Linq;

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

        [Fact]
        public void ConfigureServices_WithInvalidTimeout_FallsBackToDefault()
        {
            // Arrange
            var services = new ServiceCollection();
            ServiceConfiguration.ConfigureServices(services);

            // Replace ISettingsService with Mock
            var mockSettings = new Moq.Mock<ISettingsService>();
            var userSettings = new SwitchBlade.Services.UserSettings { UiaWorkerTimeoutSeconds = 0 }; // Invalid
            mockSettings.Setup(s => s.Settings).Returns(userSettings);

            // Remove existing registration
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISettingsService));
            if (descriptor != null) services.Remove(descriptor);
            
            services.AddSingleton(mockSettings.Object);

            // Build
            var sp = services.BuildServiceProvider();

            // Act
            var worker = (UiaWorkerClient)sp.GetRequiredService<IUiaWorkerClient>();

            // Assert
            // Use reflection to check private _timeout field in UiaWorkerClient
            var field = typeof(UiaWorkerClient).GetField("_timeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var timeout = (TimeSpan)field.GetValue(worker)!;

            Assert.Equal(TimeSpan.FromSeconds(60), timeout);
        }

        [Fact]
        public void ConfigureServices_WithValidTimeout_UsesValue()
        {
            // Arrange
            var services = new ServiceCollection();
            ServiceConfiguration.ConfigureServices(services);

            // Replace ISettingsService with Mock
            var mockSettings = new Moq.Mock<ISettingsService>();
            var userSettings = new SwitchBlade.Services.UserSettings { UiaWorkerTimeoutSeconds = 30 }; // Valid
            mockSettings.Setup(s => s.Settings).Returns(userSettings);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISettingsService));
            if (descriptor != null) services.Remove(descriptor);
            services.AddSingleton(mockSettings.Object);

            var sp = services.BuildServiceProvider();

            // Act
            var worker = (UiaWorkerClient)sp.GetRequiredService<IUiaWorkerClient>();

            // Assert
            var field = typeof(UiaWorkerClient).GetField("_timeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var timeout = (TimeSpan)field.GetValue(worker)!;

            Assert.Equal(TimeSpan.FromSeconds(30), timeout);
        }
    }
}
