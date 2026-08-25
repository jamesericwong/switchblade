using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class StartupOptionsTests
    {
        [Fact]
        public void Defaults_AreFalse()
        {
            var options = new StartupOptions();

            Assert.False(options.StartMinimized);
            Assert.False(options.EnableStartupOnFirstRun);
        }

        [Fact]
        public void InitValues_RoundTrip()
        {
            var options = new StartupOptions(StartMinimized: true, EnableStartupOnFirstRun: true);

            Assert.True(options.StartMinimized);
            Assert.True(options.EnableStartupOnFirstRun);
        }
    }
}
