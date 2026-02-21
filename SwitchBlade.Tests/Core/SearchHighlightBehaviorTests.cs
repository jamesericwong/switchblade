using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    /// <summary>
    /// Tests for <see cref="SearchHighlightBehavior.BuildSegments"/>.
    /// Uses the internal BuildSegments method (pure data, no WPF dependency).
    /// </summary>
    public class SearchHighlightBehaviorTests
    {
        [Fact]
        public void BuildSegments_NullTitle_ReturnsSingleEmptySegment()
        {
            var segments = SearchHighlightBehavior.BuildSegments(null!, "query", true, true);

            Assert.Single(segments);
            Assert.Equal(string.Empty, segments[0].Text);
            Assert.False(segments[0].IsBold);
        }

        [Fact]
        public void BuildSegments_EmptyTitle_ReturnsSingleEmptySegment()
        {
            var segments = SearchHighlightBehavior.BuildSegments("", "query", true, true);

            Assert.Single(segments);
            Assert.Equal(string.Empty, segments[0].Text);
            Assert.False(segments[0].IsBold);
        }

        [Fact]
        public void BuildSegments_EmptySearch_ReturnsSingleNormalSegment()
        {
            var segments = SearchHighlightBehavior.BuildSegments("My Title", "", true, true);

            Assert.Single(segments);
            Assert.Equal("My Title", segments[0].Text);
            Assert.False(segments[0].IsBold);
        }

        [Fact]
        public void BuildSegments_HighlightingDisabled_ReturnsSingleNormalSegment()
        {
            var segments = SearchHighlightBehavior.BuildSegments("My Title", "My", false, true);

            Assert.Single(segments);
            Assert.Equal("My Title", segments[0].Text);
            Assert.False(segments[0].IsBold);
        }

        [Fact]
        public void BuildSegments_SubstringMatch_BoldsMatchedChars()
        {
            // "Chrome", search "rom" -> Ch(normal), rom(bold), e(normal)
            var segments = SearchHighlightBehavior.BuildSegments("Chrome", "rom", true, true);

            Assert.Equal(3, segments.Count);
            Assert.Equal("Ch", segments[0].Text);
            Assert.False(segments[0].IsBold);
            Assert.Equal("rom", segments[1].Text);
            Assert.True(segments[1].IsBold);
            Assert.Equal("e", segments[2].Text);
            Assert.False(segments[2].IsBold);
        }

        [Fact]
        public void BuildSegments_MatchAtStart_StartsBold()
        {
            // "Chrome", search "Chr" -> Chr(bold), ome(normal)
            var segments = SearchHighlightBehavior.BuildSegments("Chrome", "Chr", true, true);

            Assert.Equal(2, segments.Count);
            Assert.Equal("Chr", segments[0].Text);
            Assert.True(segments[0].IsBold);
            Assert.Equal("ome", segments[1].Text);
            Assert.False(segments[1].IsBold);
        }

        [Fact]
        public void BuildSegments_MatchAtEnd_EndsBold()
        {
            // "Chrome", search "ome" -> Chr(normal), ome(bold)
            var segments = SearchHighlightBehavior.BuildSegments("Chrome", "ome", true, true);

            Assert.Equal(2, segments.Count);
            Assert.Equal("Chr", segments[0].Text);
            Assert.False(segments[0].IsBold);
            Assert.Equal("ome", segments[1].Text);
            Assert.True(segments[1].IsBold);
        }

        [Fact]
        public void BuildSegments_NoMatch_ReturnsSingleNormalSegment()
        {
            var segments = SearchHighlightBehavior.BuildSegments("Chrome", "xyz", true, true);

            Assert.Single(segments);
            Assert.Equal("Chrome", segments[0].Text);
            Assert.False(segments[0].IsBold);
        }

        [Fact]
        public void BuildSegments_FuzzyMatch_BoldsNonContiguousChars()
        {
            // "Google Chrome", query "gc" -> G(bold), oogle (normal), C(bold), hrome(normal)
            var segments = SearchHighlightBehavior.BuildSegments("Google Chrome", "gc", true, true);

            Assert.Equal(4, segments.Count);
            Assert.Equal("G", segments[0].Text);
            Assert.True(segments[0].IsBold);
            Assert.Equal("oogle ", segments[1].Text);
            Assert.False(segments[1].IsBold);
            Assert.Equal("C", segments[2].Text);
            Assert.True(segments[2].IsBold);
            Assert.Equal("hrome", segments[3].Text);
            Assert.False(segments[3].IsBold);
        }

        [Fact]
        public void BuildSegments_FullMatch_AllBold()
        {
            var segments = SearchHighlightBehavior.BuildSegments("abc", "abc", true, true);

            Assert.Single(segments);
            Assert.Equal("abc", segments[0].Text);
            Assert.True(segments[0].IsBold);
        }
    }
}
