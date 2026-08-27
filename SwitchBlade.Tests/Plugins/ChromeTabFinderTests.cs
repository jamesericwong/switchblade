using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Windows.Automation;
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
            _mockSettingsService = new Mock<IPluginSettingsService>();
            
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _mockContext.Setup(c => c.Settings).Returns(_mockSettingsService.Object);

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
            _mockContext.Setup(c => c.Settings).Returns(_mockSettingsService.Object);
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

            _mockContext.Setup(c => c.Settings).Returns(_mockSettingsService.Object);
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

            _mockContext.Setup(c => c.Settings).Returns(_mockSettingsService.Object);
            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert - HashSet with OrdinalIgnoreCase should dedupe
            Assert.Single(handled);
        }

        [Fact]
        public void RunTabScan_ResolutionThrows_IsolatesErrorAndAddsFallback()
        {
            // Per-window isolation (TeamsPlugin parity / v1.9.16 behavior): a window whose scan throws must not
            // abort the whole run and discard sibling windows' results — it is logged, and the main-window
            // fallback for this window still runs so LKG keeps the PID alive.
            var results = new List<WindowItem>();
            Func<AutomationElement?> boom = () => throw new InvalidOperationException("real bug");

            var ex = Record.Exception(() => _plugin.RunTabScan(new IntPtr(1), 42, "chrome", null, "Win", new ScanDiagnostics(), results, boom));

            Assert.Null(ex);
            var fallback = Assert.Single(results);
            Assert.True(fallback.IsFallback);
        }

        [Fact]
        public void RunTabScan_ResolverReturnsNull_AddsFallbackItem()
        {
            var results = new List<WindowItem>();

            _plugin.RunTabScan(new IntPtr(1), 42, "chrome", null, "Main Window", new ScanDiagnostics(), results, () => null);

            var fallback = Assert.Single(results);
            Assert.True(fallback.IsFallback);
            Assert.Equal("Main Window", fallback.Title);
        }

        [Fact]
        public void ActivateWindow_DeadHwnd_ReturnsGracefully()
        {
            // A window that no longer exists must not throw out of activation (resolver degrades to null).
            var item = new WindowItem { Hwnd = new IntPtr(0x12345678), Title = "Ghost Tab", Source = _plugin };

            var ex = Record.Exception(() => _plugin.ActivateWindow(item));

            Assert.Null(ex);
        }
    }
}

