using Xunit;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class ThemeInfoTests
    {
        [Fact]
        public void ThemeInfo_DefaultName_IsEmptyString()
        {
            var themeInfo = new ThemeInfo();

            Assert.Equal(string.Empty, themeInfo.Name);
        }

        [Fact]
        public void ThemeInfo_DefaultResources_IsNotNull()
        {
            var themeInfo = new ThemeInfo();

            Assert.NotNull(themeInfo.Resources);
        }

        [Fact]
        public void ThemeInfo_SetName_ReturnsCorrectValue()
        {
            var themeInfo = new ThemeInfo { Name = "Cyberpunk" };

            Assert.Equal("Cyberpunk", themeInfo.Name);
        }
    }
}
