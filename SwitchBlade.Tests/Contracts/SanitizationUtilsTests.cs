using Xunit;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Contracts
{
    public class SanitizationUtilsTests
    {
        [Theory]
        [InlineData("chrome", "chrome")]
        [InlineData("chrome.exe", "chrome")]
        [InlineData("CHROME.EXE", "chrome")]
        [InlineData("  msedge  ", "msedge")]
        [InlineData("brave.exe  ", "brave")]
        [InlineData("bad/process", "badprocess")]
        [InlineData("process\\name", "processname")]
        [InlineData("file:name", "filename")]
        [InlineData("site*name", "sitename")]
        [InlineData("query?name", "queryname")]
        [InlineData("quoted\"name", "quotedname")]
        [InlineData("bracket<name", "bracketname")]
        [InlineData("bracket>name", "bracketname")]
        [InlineData("pipe|name", "pipename")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        public void SanitizeProcessName_ShouldCleanInputCorrectly(string? input, string expected)
        {
            // Act
            var result = SanitizationUtils.SanitizeProcessName(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
