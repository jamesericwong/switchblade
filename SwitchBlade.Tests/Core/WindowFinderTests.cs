using Xunit;
using Moq;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

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
        public void Initialize_WithPluginContext_DoesNotThrow()
        {
            var finder = new WindowFinder();
            var mockLogger = new Mock<ILogger>();
            var mockContext = new Mock<IPluginContext>();
            mockContext.Setup(c => c.Logger).Returns(mockLogger.Object);

            // Should initialize without throwing
            finder.Initialize(mockContext.Object);

            // GetWindows should still return empty since settings weren't set
            var result = finder.GetWindows();
            Assert.Empty(result);
        }

        [Fact]
        public void SetExclusions_DoesNotThrow()
        {
            var finder = new WindowFinder();
            // This verifies the method exists and runs without exception
            finder.SetExclusions(new System.Collections.Generic.List<string> { "chrome" });
            Assert.NotNull(finder);
        }
    }
}

