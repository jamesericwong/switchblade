using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    public class UiaProtocolTests
    {
        [Theory]
        [InlineData(0, true)]   // Legacy peer (no version field) — accepted for compatibility
        [InlineData(1, true)]   // Current revision
        [InlineData(2, false)]  // Newer than this build — must be rejected
        [InlineData(-1, false)] // Invalid value
        public void IsCompatibleVersion_WithinSupportedRange_ReturnsExpected(int version, bool expected)
        {
            Assert.Equal(expected, UiaProtocol.IsCompatibleVersion(version));
        }

        [Fact]
        public void CurrentVersion_IsAboveLegacy()
        {
            Assert.True(UiaProtocol.CurrentVersion > UiaProtocol.LegacyVersion);
        }
    }
}
