using System.Windows.Automation;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    /// <summary>
    /// Covers the unified tab-detection predicate (PL2 fix): both historical literals — Chrome's "tab"
    /// and Terminal/Notepad++'s "tab item" — must match, case-insensitively. Pure property-value logic,
    /// so no live UIA tree is required.
    /// </summary>
    public class UiaTabScannerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("tab")]
        [InlineData("button")]
        public void IsTabElement_TabItemControlType_ReturnsTrueRegardlessOfLiteral(string? localizedControlType) =>
            Assert.True(UiaTabScanner.IsTabElement(ControlType.TabItem, localizedControlType));

        [Theory]
        [InlineData("tab")]
        [InlineData("TAB")]
        public void IsTabElement_ChromeLiteral_ReturnsTrue(string localizedControlType) =>
            Assert.True(UiaTabScanner.IsTabElement(ControlType.Group, localizedControlType));

        [Theory]
        [InlineData("tab item")]
        [InlineData("TAB ITEM")]
        [InlineData("Tab Item")]
        public void IsTabElement_TerminalNppLiteral_ReturnsTrue(string localizedControlType) =>
            Assert.True(UiaTabScanner.IsTabElement(ControlType.Group, localizedControlType));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("button")]
        [InlineData("tab-ish")]
        [InlineData("Tab ")]  // Trailing whitespace is not a literal match (exact value semantics).
        public void IsTabElement_NonTabControlTypeAndNonMatchingLiteral_ReturnsFalse(string? localizedControlType) =>
            Assert.False(UiaTabScanner.IsTabElement(ControlType.Button, localizedControlType));

        [Fact]
        public void IsTabElement_NullControlType_MatchesOnLiteralOnly()
        {
            Assert.True(UiaTabScanner.IsTabElement(null, "tab"));
            Assert.False(UiaTabScanner.IsTabElement(null, null));
        }

        [Fact]
        public void DefaultMaxContainers_IsTheMaximumHistoricalPerPluginCap() =>
            Assert.Equal(200, UiaTabScanner.DefaultMaxContainers); // Chrome 100 / Terminal 200 / NPP 50.
    }
}
