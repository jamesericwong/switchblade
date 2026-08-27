using Xunit;
using Moq;
using System;
using System.Windows.Automation;
using SwitchBlade.Plugins.NotepadPlusPlus;
using SwitchBlade.Contracts;
using System.Linq;

namespace SwitchBlade.Tests.Plugins
{
    public class NotepadPlusPlusPluginTests
    {
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPluginSettingsService> _mockSettings;
        private readonly NotepadPlusPlusPlugin _plugin;

        public NotepadPlusPlusPluginTests()
        {
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockSettings = new Mock<IPluginSettingsService>();
            
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _mockContext.Setup(c => c.Settings).Returns(_mockSettings.Object);

            // Inject mocked settings service
            _plugin = new NotepadPlusPlusPlugin(_mockSettings.Object);
        }

        [Fact]
        public void PluginName_ReturnsCorrectName()
        {
            Assert.Equal("NotepadPlusPlusPlugin", _plugin.PluginName);
        }

        [Fact]
        public void HasSettings_ReturnsTrue()
        {
            Assert.True(_plugin.HasSettings);
        }

        [Fact]
        public void Initialize_SetsLoggerAndReloadsSettings()
        {
            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert
            Assert.NotNull(_plugin);
            // Verify settings were checked
            _mockSettings.Verify(s => s.KeyExists("NppProcesses"), Times.Once);
        }

        [Fact]
        public void GetHandledProcesses_ReturnsDefaultProcesses_WhenSettingsEmpty()
        {
            // Arrange
            _mockSettings.Setup(s => s.KeyExists("NppProcesses")).Returns(false);
            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert
            Assert.Single(handled);
            Assert.Contains("notepad++", handled);
        }

        [Fact]
        public void GetHandledProcesses_ReturnsCustomProcesses_WhenSettingsExist()
        {
            // Arrange
            _mockSettings.Setup(s => s.KeyExists("NppProcesses")).Returns(true);
            _mockSettings.Setup(s => s.GetStringList("NppProcesses", It.IsAny<List<string>>()))
                         .Returns(new List<string> { "notepad++", "npp_custom" });
            
            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert
            Assert.Equal(2, handled.Count);
            Assert.Contains("notepad++", handled);
            Assert.Contains("npp_custom", handled);
        }

        [Fact]
        public void GetWindows_ReturnsEmptyWhenNoNotepadRunning()
        {
            // Arrange
            _plugin.Initialize(_mockContext.Object);

            // Act
            var windows = _plugin.GetWindows().ToList();

            // Assert
            Assert.NotNull(windows);
            // Should be empty in a test environment (unless user has NPP open, but we can't control that)
            // Main check is that it doesn't throw E_FAIL or other exceptions
        }

        [Fact]
        public void RunTabScan_ResolutionThrows_DoesNotPropagate()
        {
            // Per-window isolation (TeamsPlugin parity / v1.9.16 behavior): a faulting window must not abort the
            // whole run and discard sibling windows' results. With this dead HWND there is no title to fall back
            // on, so nothing escapes AND nothing is added for this window.
            var results = new List<WindowItem>();
            Func<AutomationElement?> boom = () => throw new InvalidOperationException("real bug");

            var ex = Record.Exception(() => _plugin.RunTabScan(new IntPtr(1), 42, "notepad++", null, new ScanDiagnostics(), results, boom));

            Assert.Null(ex);
            Assert.Empty(results);
        }

        [Fact]
        public void RunTabScan_ResolverReturnsNull_DeadHwnd_AddsNothing()
        {
            var results = new List<WindowItem>();

            _plugin.RunTabScan(new IntPtr(0x12345678), 42, "notepad++", null, new ScanDiagnostics(), results, () => null);

            // Fallback reads the (nonexistent) window title via Win32 and adds nothing for a dead HWND
            Assert.Empty(results);
        }
    }
}
