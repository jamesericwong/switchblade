using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class PluginServiceTests
    {
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPluginLoader> _mockLoader;

        public PluginServiceTests()
        {
            _mockContext = new Mock<IPluginContext>();
            _mockSettings = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILogger>();
            _mockLoader = new Mock<IPluginLoader>();
            
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_LoadsInternalProviders()
        {
            // Arrange
            _mockLoader.Setup(l => l.LoadPlugins()).Returns(new List<IWindowProvider>());

            // Act
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);

            // Assert
            Assert.Contains(service.Providers, p => p is WindowFinder);
        }

        [Fact]
        public void Constructor_LoadsExternalPlugins_AndInitializesThem()
        {
            // Arrange
            var mockPlugin = new Mock<IWindowProvider>();
            mockPlugin.Setup(p => p.PluginName).Returns("TestPlugin");

            _mockLoader.Setup(l => l.LoadPlugins()).Returns(new List<IWindowProvider> { mockPlugin.Object });

            // Act
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);

            // Assert
            Assert.Contains(service.Providers, p => p == mockPlugin.Object);
            // Verify Initialize was called on the plugin
            mockPlugin.Verify(p => p.Initialize(It.IsAny<IPluginContext>()), Times.Once);
        }

        [Fact]
        public void LoadProviders_LogsError_WhenLoaderThrows()
        {
            // Arrange
            _mockLoader.Setup(l => l.LoadPlugins()).Throws(new Exception("Load failed"));

            // Act
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);

            // Assert
            // Should still have internal provider
            Assert.Contains(service.Providers, p => p is WindowFinder);
            // Should verify logging happened
            _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void LoadProviders_DoesNotCrash_WhenLoaderThrowsAndLoggerIsNull()
        {
            // Arrange
            _mockLoader.Setup(l => l.LoadPlugins()).Throws(new Exception("Load failed"));

            // Act
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, null, _mockLoader.Object);

            // Assert
            Assert.Contains(service.Providers, p => p is WindowFinder);
            // No crash means success
        }

        [Fact]
        public void ReloadPlugins_ReinitializesProviders()
        {
            // Arrange
            _mockLoader.Setup(l => l.LoadPlugins()).Returns(new List<IWindowProvider>());
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);
            int initialCount = service.Providers.Count;

            // Act
            service.ReloadPlugins();

            // Assert
            Assert.Equal(initialCount, service.Providers.Count);
            Assert.Contains(service.Providers, p => p is WindowFinder);
            
            // Verify LoadPlugins called twice (Constructor + Reload)
            _mockLoader.Verify(l => l.LoadPlugins(), Times.Exactly(2));
        }

        [Fact]
        public void GetPluginInfos_ReturnsCorrectInfo()
        {
            // Arrange
            var mockPlugin = new Mock<IWindowProvider>();
            mockPlugin.Setup(p => p.PluginName).Returns("TestPlugin");
            // Setup mock plugin to return true/false for HasSettings if needed, defaults are false.

            _mockLoader.Setup(l => l.LoadPlugins()).Returns(new List<IWindowProvider> { mockPlugin.Object });
            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);

            // Act
            var infos = service.GetPluginInfos().ToList();

            // Assert
            Assert.NotEmpty(infos);
            var pluginInfo = infos.FirstOrDefault(i => i.Name == "TestPlugin");
            Assert.NotNull(pluginInfo);
            Assert.Equal("TestPlugin", pluginInfo.Name);
            Assert.False(pluginInfo.IsInternal);
            
            var internalInfo = infos.FirstOrDefault(i => i.Name == "WindowFinder"); // WindowFinder usually sets Name in constructor or similar
            // Actually WindowFinder name checks might be fragile if hardcoded. 
            // Better to check TypeName or simply IsInternal flag.
            Assert.Contains(infos, i => i.IsInternal);
        }

        [Fact]
        public void GetPluginInfos_CorrectlyIdentifiesExternals()
        {
            // Arrange
            var fakeProvider = new FakeGlobalProvider();
            _mockLoader.Setup(l => l.LoadPlugins()).Returns(new List<IWindowProvider> { fakeProvider });
            
            // Capture errors
            Exception? capturedEx = null;
            _mockLogger.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()))
                       .Callback<string, Exception>((msg, ex) => capturedEx = ex);

            var service = new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object);

            if (capturedEx != null)
                throw new Exception("Caught exception during LoadProviders: " + capturedEx.ToString());

            // Act
            var infos = service.GetPluginInfos().ToList();

            // Assert
            var fakeInfo = infos.FirstOrDefault(i => i.Name == fakeProvider.PluginName);
            Assert.NotNull(fakeInfo);
            Assert.False(fakeInfo!.IsInternal, "Fake provider from Test assembly should be external");
            Assert.Equal("Fake External Plugin", fakeInfo.Name);
            
            // Also check WindowFinder is still internal
            var internalInfo = infos.FirstOrDefault(i => i.Name == "WindowFinder"); 
            Assert.NotNull(internalInfo);
            Assert.True(internalInfo!.IsInternal, "WindowFinder should be internal");
        }

        [Fact]
        public void Constructor_Default_DoesNotThrow()
        {
            // Just verifying the 2-arg constructor doesn't explode immediately.
            // It will try to create a PluginLoader pointing to BaseDirector/Plugins.
            // This directory might not exist, but PluginLoader constructor might not check existence immediately or just accept it.
            // Let's create it.
            var service = new PluginService(_mockContext.Object, _mockSettings.Object);
            Assert.NotNull(service);
            Assert.NotNull(service.Providers);
        }

        [Fact]
        public void Constructor_Throws_WhenArgumentsNull()
        {
             Assert.Throws<ArgumentNullException>(() => new PluginService(null!, _mockSettings.Object, _mockLogger.Object, _mockLoader.Object));
             Assert.Throws<ArgumentNullException>(() => new PluginService(_mockContext.Object, null!, _mockLogger.Object, _mockLoader.Object));
             Assert.Throws<ArgumentNullException>(() => new PluginService(_mockContext.Object, _mockSettings.Object, _mockLogger.Object, (IPluginLoader)null!));
        }

    }
}
