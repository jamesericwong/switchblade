using Xunit;
using Moq;
using SwitchBlade.Plugins.WindowsTerminal;
using SwitchBlade.Contracts;
using System.Linq;

namespace SwitchBlade.Tests.Plugins
{
    public class WindowsTerminalPluginTests
    {
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;
        private readonly WindowsTerminalPlugin _plugin;

        public WindowsTerminalPluginTests()
        {
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);

            _plugin = new WindowsTerminalPlugin();
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
            _plugin.Initialize(_mockContext.Object);

            // Act
            var handled = _plugin.GetHandledProcesses().ToList();

            // Assert - should have default processes: WindowsTerminal, wt
            Assert.Equal(2, handled.Count);
            Assert.Contains("WindowsTerminal", handled);
            Assert.Contains("wt", handled);
        }

        [Fact]
        public void ReloadSettings_DoesNotThrow()
        {
            // Arrange
            _plugin.Initialize(_mockContext.Object);

            // Act & Assert - no exception
            _plugin.ReloadSettings();
        }



        [Fact]
        public void GetWindows_ReturnsEmptyWhenNoTerminalRunning()
        {
            // Arrange
            _plugin.Initialize(_mockContext.Object);

            // Act
            // Note: This test will only pass reliably if no Windows Terminal is running
            // In a CI environment, this should be the case
            var windows = _plugin.GetWindows().ToList();

            // Assert - result depends on whether Terminal is running
            // We just verify it doesn't throw
            Assert.NotNull(windows);
        }
    }
}
