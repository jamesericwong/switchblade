using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        [Fact]
        public void GetTypeName_HitsAllBranches()
        {
            // FullName is not null
            Assert.Equal("System.String", PluginService.GetTypeName(typeof(string)));
            
            // FullName is null for generic parameters
            var genericParam = typeof(List<>).GetGenericArguments()[0];
            Assert.Equal("T", PluginService.GetTypeName(genericParam));
        }

        [Fact]
        public void GetAssemblyName_HitsAllBranches()
        {
            // Name is not null
            var name1 = new AssemblyName("Test");
            Assert.Equal("Test", PluginService.GetAssemblyName(name1));
            
            // Name is null
            var name2 = new AssemblyName();
            Assert.Equal("Unknown", PluginService.GetAssemblyName(name2));
        }

        [Fact]
        public void GetVersion_HitsAllBranches()
        {
            // Version is not null
            var name1 = new AssemblyName("Test") { Version = new Version(1, 0) };
            Assert.Equal("1.0", PluginService.GetVersion(name1));
            
            // Version is null
            var name2 = new AssemblyName("Test") { Version = null };
            Assert.Equal("0.0.0", PluginService.GetVersion(name2));
        }

        [Fact]
        public void IsInternalProvider_HitsAllBranches()
        {
            var internalAssembly = typeof(PluginService).Assembly;
            var externalAssembly = typeof(object).Assembly;
            
            // Branch 1: Is internal assembly
            Assert.True(PluginService.IsInternalProvider(internalAssembly, internalAssembly.GetName()));
            
            // Branch 2: Not internal assembly but name is "SwitchBlade"
            var fakeName = new AssemblyName("SwitchBlade");
            Assert.True(PluginService.IsInternalProvider(externalAssembly, fakeName));
            
            // Branch 3: Not internal and name is not "SwitchBlade"
            var otherName = new AssemblyName("Other");
            Assert.False(PluginService.IsInternalProvider(externalAssembly, otherName));
        }

        [Fact]
        public void MapToInfo_HitsBasicBranches()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("Test");
            mockProvider.Setup(p => p.HasSettings).Returns(true);
            
            var info = PluginService.MapToInfo(mockProvider.Object, "T", "A", "V", true);
            Assert.Equal("Test", info.Name);
            Assert.Equal("T", info.TypeName);
            Assert.Equal("A", info.AssemblyName);
            Assert.Equal("V", info.Version);
            Assert.True(info.IsInternal);
            Assert.True(info.HasSettings);
            Assert.Same(mockProvider.Object, info.Provider);
        }

        [Fact]
        public void MapToInfo_Wrapper_Works()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("Test");
            
            var info = PluginService.MapToInfo(mockProvider.Object);
            Assert.NotNull(info.TypeName);
            Assert.NotNull(info.AssemblyName);
            Assert.NotNull(info.Version);
        }
    }
}
