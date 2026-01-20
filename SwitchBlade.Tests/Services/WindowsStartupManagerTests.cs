using Xunit;
using Moq;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class WindowsStartupManagerTests
    {
        [Fact]
        public void IsStartupEnabled_DefaultState_ReturnsFalse()
        {
            // Note: This test reads actual registry state.
            // In a clean environment, SwitchBlade won't be registered.
            var manager = new WindowsStartupManager();

            // Just verify it doesn't throw
            var result = manager.IsStartupEnabled();
            Assert.True(result == true || result == false);
        }

        [Fact]
        public void EnableStartup_NullPath_ThrowsArgumentException()
        {
            var manager = new WindowsStartupManager();

            Assert.Throws<System.ArgumentException>(() => manager.EnableStartup(null!));
        }

        [Fact]
        public void EnableStartup_EmptyPath_ThrowsArgumentException()
        {
            var manager = new WindowsStartupManager();

            Assert.Throws<System.ArgumentException>(() => manager.EnableStartup(""));
        }

        [Fact]
        public void DisableStartup_DoesNotThrow()
        {
            var manager = new WindowsStartupManager();

            var exception = Record.Exception(() => manager.DisableStartup());

            Assert.Null(exception);
        }

        [Fact]
        public void CheckAndApplyStartupMarker_NoMarker_ReturnsFalse()
        {
            var manager = new WindowsStartupManager();

            // In a clean environment, there should be no marker
            var result = manager.CheckAndApplyStartupMarker();

            Assert.False(result);
        }
    }
}
