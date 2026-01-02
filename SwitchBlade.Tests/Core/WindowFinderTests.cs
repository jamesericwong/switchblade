using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class WindowFinderTests
    {
        [Fact]
        public void Constructor_Default_CreatesInstance()
        {
            var finder = new WindowFinder();

            Assert.NotNull(finder);
        }

        [Fact]
        public void GetWindows_WithNullSettingsService_ReturnsEmptyList()
        {
            var finder = new WindowFinder();

            var result = finder.GetWindows();

            Assert.Empty(result);
        }

        [Fact]
        public void Initialize_WithNonSettingsServiceObject_DoesNotThrow()
        {
            var finder = new WindowFinder();

            // Should handle invalid type gracefully
            finder.Initialize(new object(), new LoggerBridge());

            // GetWindows should still return empty since settings weren't set
            var result = finder.GetWindows();
            Assert.Empty(result);
        }
    }
}
