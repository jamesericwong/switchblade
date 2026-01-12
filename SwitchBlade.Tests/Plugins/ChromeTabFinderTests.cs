using Xunit;
using Moq;
using System.Collections.Generic;
using SwitchBlade.Plugins.Chrome;
using SwitchBlade.Contracts;
using System.Linq;

namespace SwitchBlade.Tests.Plugins
{
    public class ChromeTabFinderTests
    {
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPluginSettingsService> _mockSettingsService;
        private readonly ChromeTabFinder _plugin;

        public ChromeTabFinderTests()
        {
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);

            _mockSettingsService = new Mock<IPluginSettingsService>();

            // Inject mock settings service using the new constructor
            _plugin = new ChromeTabFinder(_mockSettingsService.Object);
        }

        [Fact]
        public void Initialize_LoadsSettings_IfExist()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.KeyExists("BrowserProcesses")).Returns(true);
            var expectedProcesses = new List<string> { "custom_browser" };
            _mockSettingsService.Setup(s => s.GetStringList("BrowserProcesses", It.IsAny<List<string>>()))
                .Returns(expectedProcesses);

            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert
            var handled = _plugin.GetHandledProcesses().ToList();
            Assert.Contains("custom_browser", handled);
            Assert.Single(handled);
        }

        [Fact]
        public void Initialize_SetsDefaults_IfSettingsMissing()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.KeyExists("BrowserProcesses")).Returns(false);

            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert
            // Should save defaults
            _mockSettingsService.Verify(s => s.SetStringList("BrowserProcesses", It.IsAny<List<string>>()), Times.Once);

            var handled = _plugin.GetHandledProcesses().ToList();
            Assert.Contains("chrome", handled); // Default
        }

        [Fact]
        public void ReloadSettings_RefreshesProcesses()
        {
            // Arrange
            _plugin.Initialize(_mockContext.Object);

            // Now mock a change
            _mockSettingsService.Setup(s => s.KeyExists("BrowserProcesses")).Returns(true);
            _mockSettingsService.Setup(s => s.GetStringList("BrowserProcesses", It.IsAny<List<string>>()))
                .Returns(new List<string> { "new_browser" });

            // Act
            _plugin.ReloadSettings();

            // Assert
            var handled = _plugin.GetHandledProcesses().ToList();
            Assert.Contains("new_browser", handled);
            Assert.Single(handled);
        }

        [Fact]
        public void GetHandledProcesses_IsCaseInsensitive()
        {
            // Arrange - Tests HashSet with StringComparer.OrdinalIgnoreCase
            _mockSettingsService.Setup(s => s.KeyExists("BrowserProcesses")).Returns(true);
            _mockSettingsService.Setup(s => s.GetStringList("BrowserProcesses", It.IsAny<List<string>>()))
                .Returns(new List<string> { "Chrome", "MSEDGE", "brave" });

            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert - All should be included regardless of case
            Assert.Equal(3, handled.Count);
            // The HashSet stores them as provided but lookups are case-insensitive
            Assert.Contains("Chrome", handled);
            Assert.Contains("MSEDGE", handled);
            Assert.Contains("brave", handled);
        }

        [Fact]
        public void ReloadSettings_DuplicateProcesses_AreDeduped()
        {
            // Arrange - HashSet should dedupe identical entries (case-insensitive)
            _mockSettingsService.Setup(s => s.KeyExists("BrowserProcesses")).Returns(true);
            _mockSettingsService.Setup(s => s.GetStringList("BrowserProcesses", It.IsAny<List<string>>()))
                .Returns(new List<string> { "chrome", "Chrome", "CHROME" });

            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert - HashSet with OrdinalIgnoreCase should dedupe
            Assert.Single(handled);
        }
    }
}

