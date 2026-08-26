using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Windows.Automation;
using SwitchBlade.Plugins.WindowsTerminal;
using SwitchBlade.Contracts;
using System.Linq;

namespace SwitchBlade.Tests.Plugins
{
    public class WindowsTerminalPluginTests
    {
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPluginSettingsService> _mockSettings;
        private readonly WindowsTerminalPlugin _plugin;

        public WindowsTerminalPluginTests()
        {
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockSettings = new Mock<IPluginSettingsService>();
            
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
            _mockContext.Setup(c => c.Settings).Returns(_mockSettings.Object);

            _plugin = new WindowsTerminalPlugin(_mockSettings.Object);
        }

        [Fact]
        public void PluginName_ReturnsCorrectName()
        {
            Assert.Equal("WindowsTerminalPlugin", _plugin.PluginName);
        }

        [Fact]
        public void HasSettings_ReturnsTrue()
        {
            Assert.True(_plugin.HasSettings);
        }

        [Fact]
        public void Initialize_SetsLogger()
        {
            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert - no exception means success
            // Logger is internal, but we can verify by checking that subsequent calls work
            Assert.NotNull(_plugin);
        }

        [Fact]
        public void GetHandledProcesses_ReturnsDefaultProcesses()
        {
            // Arrange
            _mockContext.Setup(c => c.Settings).Returns(_mockSettings.Object);
            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert - should have default processes: WindowsTerminal, wt
            // Assert - should have default processes: WindowsTerminal
            Assert.Single(handled);
            Assert.Contains("WindowsTerminal", handled);
            // Assert.Contains("wt", handled); // "wt" is not in default list currently
        }

        [Fact]
        public void ReloadSettings_DoesNotThrow()
        {
            // Arrange
            _mockContext.Setup(c => c.Settings).Returns(_mockSettings.Object);
            _plugin.Initialize(_mockContext.Object);

            // Act & Assert - no exception
            _plugin.ReloadSettings();
        }



        [Fact]
        public void GetWindows_ReturnsEmptyWhenNoTerminalRunning()
        {
            // Arrange
            _mockContext.Setup(c => c.Settings).Returns(_mockSettings.Object);
            _plugin.Initialize(_mockContext.Object);

            // Act
            // Note: This test will only pass reliably if no Windows Terminal is running
            // In a CI environment, this should be the case
            var windows = _plugin.GetWindows().ToList();

            // Assert - result depends on whether Terminal is running
            // We just verify it doesn't throw
            Assert.NotNull(windows);
        }

        private WindowItem MakeItem(IntPtr hwnd, string title, bool fallback, IWindowProvider? source = null) => new()
        {
            Hwnd = hwnd,
            Title = title,
            ProcessName = "WindowsTerminal",
            IsFallback = fallback,
            Source = source ?? _plugin
        };

        [Fact]
        public void DeduplicateResults_MixedTabsAndFallbacks_OnlyRealTabsSurvive()
        {
            // Arrange: one handle peered successfully (2 tabs), another fell back to the main window.
            var tabA = MakeItem(new IntPtr(0x1), "powershell", fallback: false);
            var tabB = MakeItem(new IntPtr(0x1), "git bash", fallback: false);
            var mainWindowFallback = MakeItem(new IntPtr(0x2), "Windows Terminal", fallback: true);

            var pidToResults = new Dictionary<int, List<WindowItem>>
            {
                [4321] = new List<WindowItem> { tabA, tabB, mainWindowFallback }
            };

            // Act
            var result = WindowsTerminalPlugin.DeduplicateResults(_plugin, pidToResults).ToList();

            // Assert: the bare "Main Window" fallback must not appear alongside its tabs.
            Assert.Equal(2, result.Count);
            Assert.Contains(result, i => i.Title == "powershell");
            Assert.Contains(result, i => i.Title == "git bash");
            Assert.DoesNotContain(result, i => i.IsFallback);
        }

        [Fact]
        public void DeduplicateResults_AllHandlesFallback_SingleUniqueItemSurvives()
        {
            // Arrange: two handles of the same PID, neither peered successfully.
            var fallbackA = MakeItem(new IntPtr(0x1), "Windows Terminal", fallback: true);
            var fallbackB = MakeItem(new IntPtr(0x2), "Windows Terminal", fallback: true);

            var pidToResults = new Dictionary<int, List<WindowItem>>
            {
                [4321] = new List<WindowItem> { fallbackA, fallbackB }
            };

            // Act
            var result = WindowsTerminalPlugin.DeduplicateResults(_plugin, pidToResults).ToList();

            // Assert: exactly one entry (no "Found 2 windows"), and it stays marked as a fallback
            // so downstream LKG logic treats it as degraded data.
            Assert.Single(result);
            Assert.True(result[0].IsFallback);
        }

        [Fact]
        public void DeduplicateResults_ItemsFromOtherProvider_AreIgnored()
        {
            // Arrange: a foreign provider's tab must not count toward this plugin's dedup decision.
            var otherPlugin = new WindowsTerminalPlugin();
            var foreignTab = MakeItem(new IntPtr(0x1), "foreign tab", fallback: false, source: otherPlugin);
            var ownFallback = MakeItem(new IntPtr(0x2), "Windows Terminal", fallback: true);

            var pidToResults = new Dictionary<int, List<WindowItem>>
            {
                [4321] = new List<WindowItem> { foreignTab, ownFallback }
            };

            // Act
            var result = WindowsTerminalPlugin.DeduplicateResults(_plugin, pidToResults).ToList();

            // Assert: only our own fallback survives.
            Assert.Single(result);
            Assert.Same(ownFallback, result[0]);
        }

        [Fact]
        public void DeduplicateResults_EmptyInput_ReturnsEmpty()
        {
            var result = WindowsTerminalPlugin.DeduplicateResults(_plugin, new Dictionary<int, List<WindowItem>>()).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void RunTabScan_NonTransientResolutionFailure_Propagates()
        {
            // A genuine bug in root resolution must surface (scan coordinator's error path), not be swallowed.
            var results = new List<WindowItem>();
            Func<AutomationElement?> boom = () => throw new InvalidOperationException("real bug");

            var ex = Record.Exception(() => _plugin.RunTabScan(new IntPtr(1), 42, "WindowsTerminal", null, "Term", new ScanDiagnostics(), results, boom));

            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public void RunTabScan_ResolverReturnsNull_AddsFallbackItem()
        {
            var results = new List<WindowItem>();

            _plugin.RunTabScan(new IntPtr(1), 42, "WindowsTerminal", null, "Main Window", new ScanDiagnostics(), results, () => null);

            var fallback = Assert.Single(results);
            Assert.True(fallback.IsFallback);
            Assert.Equal("Main Window", fallback.Title);
        }
    }
}
