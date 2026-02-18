using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    /// <summary>
    /// Unit tests for the FuzzyMatcher service.
    /// Tests cover delimiter handling, subsequence matching, scoring, and edge cases.
    /// </summary>
    public class FuzzyMatcherTests
    {
        #region Basic Matching

        [Fact]
        public void Score_ExactMatch_ReturnsHighScore()
        {
            int score = FuzzyMatcher.Score("Chrome", "Chrome");
            Assert.True(score > 0, "Exact match should return positive score");
        }

        [Fact]
        public void Score_NoMatch_ReturnsZero()
        {
            int score = FuzzyMatcher.Score("Chrome", "xyz");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_EmptyQuery_ReturnsZero()
        {
            int score = FuzzyMatcher.Score("Chrome", "");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_EmptyTitle_ReturnsZero()
        {
            int score = FuzzyMatcher.Score("", "test");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_NullQuery_ReturnsZero()
        {
            int score = FuzzyMatcher.Score("Chrome", null!);
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_NullTitle_ReturnsZero()
        {
            int score = FuzzyMatcher.Score(null!, "test");
            Assert.Equal(0, score);
        }

        #endregion

        #region Delimiter Equivalence

        [Fact]
        public void Score_SpaceMatchesUnderscore_ReturnsPositive()
        {
            // Key feature: "hello there" should match "hello_there"
            int score = FuzzyMatcher.Score("hello_there", "hello there");
            Assert.True(score > 0, "Space in query should match underscore in title");
        }

        [Fact]
        public void Score_UnderscoreMatchesSpace_ReturnsPositive()
        {
            int score = FuzzyMatcher.Score("hello there", "hello_there");
            Assert.True(score > 0, "Underscore in query should match space in title");
        }

        [Fact]
        public void Score_DashMatchesUnderscore_ReturnsPositive()
        {
            int score = FuzzyMatcher.Score("hello_there", "hello-there");
            Assert.True(score > 0, "Dash in query should match underscore in title");
        }

        [Fact]
        public void Score_DashMatchesSpace_ReturnsPositive()
        {
            int score = FuzzyMatcher.Score("hello there", "hello-there");
            Assert.True(score > 0, "Dash in query should match space in title");
        }

        [Fact]
        public void Score_MixedDelimiters_ReturnsPositive()
        {
            int score = FuzzyMatcher.Score("my_long-file name.txt", "my long file name");
            Assert.True(score > 0, "Mixed delimiters should all be treated as equivalent");
        }

        #endregion

        #region Case Insensitivity

        [Fact]
        public void Score_CaseInsensitive_UpperToLower()
        {
            int score = FuzzyMatcher.Score("chrome", "CHROME");
            Assert.True(score > 0, "Should match regardless of case");
        }

        [Fact]
        public void Score_CaseInsensitive_LowerToUpper()
        {
            int score = FuzzyMatcher.Score("CHROME", "chrome");
            Assert.True(score > 0, "Should match regardless of case");
        }

        [Fact]
        public void Score_CaseInsensitive_MixedCase()
        {
            int score = FuzzyMatcher.Score("GoOgLe ChRoMe", "google chrome");
            Assert.True(score > 0, "Should match regardless of case");
        }

        #endregion

        #region Subsequence Matching

        [Fact]
        public void Score_Subsequence_ShortQuery()
        {
            // "gc" should match "Google Chrome"
            int score = FuzzyMatcher.Score("Google Chrome", "gc");
            Assert.True(score > 0, "Subsequence 'gc' should match 'Google Chrome'");
        }

        [Fact]
        public void Score_Subsequence_NonContiguous()
        {
            // "gchm" should match "Google Chrome"
            int score = FuzzyMatcher.Score("Google Chrome", "gchm");
            Assert.True(score > 0, "Subsequence 'gchm' should match 'Google Chrome'");
        }

        [Fact]
        public void Score_Subsequence_AllCharactersRequired()
        {
            // "gcz" should NOT match "Google Chrome" (z not present)
            int score = FuzzyMatcher.Score("Google Chrome", "gcz");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_Subsequence_OrderMatters()
        {
            // "gc" should match "Google Chrome" - g at 0, c at 6 in normalized "googlechrome"
            int scoreGc = FuzzyMatcher.Score("Google Chrome", "gc");
            Assert.True(scoreGc > 0, "Subsequence 'gc' should match - g before c in 'googlechrome'");

            // "cg" should NOT match "Google Chrome" - there's no 'g' after 'c' in "googlechrome"
            int scoreCg = FuzzyMatcher.Score("Google Chrome", "cg");
            Assert.Equal(0, scoreCg);
        }

        #endregion

        #region Scoring Priority

        [Fact]
        public void Score_ContiguousHigherThanNonContiguous()
        {
            // "chr" in "Chrome" (contiguous) should score higher than "chr" finding c...h...r
            int contiguousScore = FuzzyMatcher.Score("Chrome", "chr");
            int nonContiguousScore = FuzzyMatcher.Score("Catch Her", "chr");

            Assert.True(contiguousScore > nonContiguousScore,
                "Contiguous matches should score higher than non-contiguous");
        }

        [Fact]
        public void Score_StartsWithHigherThanContains()
        {
            // "chr" at start of "Chrome" should score higher than "chr" in "unchrome"
            int startsWithScore = FuzzyMatcher.Score("Chrome", "chr");
            int containsScore = FuzzyMatcher.Score("unchrome", "chr");

            Assert.True(startsWithScore > containsScore,
                "Starts-with matches should score higher than contains");
        }

        [Fact]
        public void Score_ExactMatchHighestScore()
        {
            // Exact matches should score highest
            int exactScore = FuzzyMatcher.Score("Chrome", "Chrome");
            int partialScore = FuzzyMatcher.Score("Chrome", "chro");
            int subsequenceScore = FuzzyMatcher.Score("Chrome", "crm");

            Assert.True(exactScore > partialScore, "Exact should beat partial");
            Assert.True(partialScore > subsequenceScore, "Partial should beat subsequence");
        }

        #endregion

        #region IsMatch Helper

        [Fact]
        public void IsMatch_ReturnsTrue_WhenScorePositive()
        {
            bool result = FuzzyMatcher.IsMatch("Chrome", "chr");
            Assert.True(result);
        }

        [Fact]
        public void IsMatch_ReturnsFalse_WhenNoMatch()
        {
            bool result = FuzzyMatcher.IsMatch("Chrome", "xyz");
            Assert.False(result);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public void Score_BrowserTab_PartialMatch()
        {
            int score = FuzzyMatcher.Score("Inbox (5) - user@gmail.com - Gmail", "inbox gmail");
            Assert.True(score > 0, "Should match browser tab with partial query");
        }

        [Fact]
        public void Score_CodeFile_PartialMatch()
        {
            int score = FuzzyMatcher.Score("main_controller.cs", "main con");
            Assert.True(score > 0, "Should match code file with partial query");
        }

        [Fact]
        public void Score_Terminal_PartialMatch()
        {
            int score = FuzzyMatcher.Score("PowerShell - [Admin]", "ps admin");
            Assert.True(score > 0, "Should match terminal with partial query");
        }

        [Fact]
        public void Score_VsCode_WindowTitle()
        {
            int score = FuzzyMatcher.Score("MainViewModel.cs - SwitchBlade - Visual Studio Code", "mainview");
            Assert.True(score > 0, "Should match VS Code window");
        }

        [Fact]
        public void Score_WindowsExplorer_Path()
        {
            int score = FuzzyMatcher.Score("Downloads - File Explorer", "down exp");
            Assert.True(score > 0, "Should match Explorer window");
        }

        #endregion

        #region Edge Cases - Coverage Extension

        [Fact]
        public void Score_TitleLongerThan256_UsesHeapAllocation()
        {
            string longTitle = new string('a', 300) + "target";
            int score = FuzzyMatcher.Score(longTitle, "target");
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_QueryLongerThan64_UsesHeapAllocation()
        {
            string longQuery = new string('a', 70) + "target";
            string title = new string('a', 100) + "target";
            int score = FuzzyMatcher.Score(title, longQuery);
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_NormalizedQueryEmpty_ReturnsZero()
        {
            // Normalize skips spaces, underscores, and dashes
            int score = FuzzyMatcher.Score("title", " _- ");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_NormalizedQueryLongerThanTitle_ReturnsZero()
        {
            // Normalized "abc" is length 3, "ab" is length 2
            int score = FuzzyMatcher.Score("ab", "abc");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_NormalizedQueryLongerThanTitleWithDelimiters_ReturnsZero()
        {
            // Normalized title "a" (len 1), normalized query "ab" (len 2)
            int score = FuzzyMatcher.Score("a-_ ", "a b");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_MaximizeNormalizedLengths_DoesNotCrash()
        {
            // Hits the logic where length is truncated to MaxNormalizedLength (512)
            string superLongTitle = new string('a', 600);
            string superLongQuery = new string('a', 600);
            int score = FuzzyMatcher.Score(superLongTitle, superLongQuery);
            Assert.True(score > 0);
        }

        #endregion
    }
}
