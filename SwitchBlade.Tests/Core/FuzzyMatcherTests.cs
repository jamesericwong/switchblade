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
            // Must NOT trigger exact match fast path
            string longTitle = new string('a', 300) + "x y z";
            int score = FuzzyMatcher.Score(longTitle, "xyz"); // Fuzzy match 'x', 'y', 'z'
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_QueryLongerThan64_UsesHeapAllocation()
        {
            // Must NOT trigger exact match fast path
            string longQuery = new string('a', 70) + "x y z";
            string title = new string('a', 100) + "x-y-z";
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

        [Fact]
        public void Score_MixedDelimitersAndCase_CalculatesCorrectly()
        {
            // "G_C" should match "google-chrome"
            int score = FuzzyMatcher.Score("google-chrome", "G_C");
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_Subsequence_MatchAtExactEnd_ExercisesCondition()
        {
            // Title "abc", Query "c"
            // lastMatchIndex will be 2, title.Length is 3
            // Exercises: if (matchedAtStart && lastMatchIndex < title.Length)
            // But matchedAtStart is false here.
            int score = FuzzyMatcher.Score("abc", "c");
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_MatchedAtStart_WithLastMatchAtEnd_ExercisesCondition()
        {
            // Title "abc", Query "abc"
            // matchedAtStart = true, lastMatchIndex = 2, title.Length = 3
            // Exercises the branch: if (matchedAtStart && lastMatchIndex < title.Length)
            int score = FuzzyMatcher.Score("abc", "abc");
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_MatchedAtStart_Partial_ExercisesCondition()
        {
            // Title "abcd", Query "ab"
            // matchedAtStart = true, lastMatchIndex = 1, title.Length = 4
            int score = FuzzyMatcher.Score("abcd", "ab");
            Assert.True(score > 0);
        }

        [Fact]
        public void Score_ExactContains_NotStartsWith()
        {
            // Tests the branch where title.Contains(query) is true, but StartsWith is false.
            // "Hello World" contains "World", but doesn't start with it.
            int score = FuzzyMatcher.Score("Hello World", "World");
            Assert.True(score > 0);
            // Should score lower than "World Hello" (starts with) which gets bonus.
            int startsWithScore = FuzzyMatcher.Score("World Hello", "World");
            Assert.True(startsWithScore > score);
        }

        [Fact]
        public void Score_Fuzzy_NotAtStart()
        {
            // Explicitly test fuzzy match that does NOT start at 0 normalized index.
            // "G_oogle", "ogle" -> Norm "google", "ogle".
            // 'o' matches at 1. No match at start.
            // Ensures matchedAtStart remains false.
            int score = FuzzyMatcher.Score("G_oogle", "ogle");
            Assert.True(score > 0);

            // Verify matchedAtStart is false by comparing with a starts-with match
            int startsWithScore = FuzzyMatcher.Score("google", "goog");
            int nonStartScore = FuzzyMatcher.Score("xgoogle", "goog");
            Assert.True(startsWithScore > nonStartScore);
        }

        [Fact]
        public void Score_Normalization_BufferTruncation_ExercisesBranch()
        {
            // This is a bit tricky to hit, but if we have a title that is exactly the length 
            // of the buffer but contains delimiters, Normalize will finish i loop before writeIndex hits buffer end.
            // If it HAS NO delimiters, writeIndex hits buffer end.
            string maxLenTitle = new string('a', 512); 
            int score = FuzzyMatcher.Score(maxLenTitle, "aaaa");
            Assert.True(score > 0);
        }

        #endregion

        #region GetMatchedIndices

        [Fact]
        public void GetMatchedIndices_NullTitle_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices(null!, "abc", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_NullQuery_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("title", null!, true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_EmptyQuery_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("title", "", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_EmptyTitle_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("", "abc", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_ExactSubstring_ReturnsContiguousIndices()
        {
            // "Chrome" contains "rom" at index 2
            var result = FuzzyMatcher.GetMatchedIndices("Chrome", "rom", true);
            Assert.Equal(new[] { 2, 3, 4 }, result);
        }

        [Fact]
        public void GetMatchedIndices_CaseInsensitiveSubstring_ReturnsIndices()
        {
            var result = FuzzyMatcher.GetMatchedIndices("Chrome", "CHR", true);
            Assert.Equal(new[] { 0, 1, 2 }, result);
        }

        [Fact]
        public void GetMatchedIndices_FuzzyMatch_ReturnsNonContiguousIndices()
        {
            // "Google Chrome" normalized -> "googlechrome"
            // query "gc" -> g at 0, c at 6 in normalized
            // Original: G(0) o(1) o(2) g(3) l(4) e(5) (space)(6) C(7)
            // Normalized map: g->0, o->1, o->2, g->3, l->4, e->5, c->7, h->8, r->9, o->10, m->11, e->12
            // So fuzzy 'g' at norm[0] -> orig 0, 'c' at norm[6] -> orig 7
            var result = FuzzyMatcher.GetMatchedIndices("Google Chrome", "gc", true);
            Assert.Equal(new[] { 0, 7 }, result);
        }

        [Fact]
        public void GetMatchedIndices_NoMatch_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("Chrome", "xyz", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_NotFuzzy_SubstringOnly()
        {
            // With fuzzy disabled, only exact substring match works
            var result = FuzzyMatcher.GetMatchedIndices("Chrome", "rom", false);
            Assert.Equal(new[] { 2, 3, 4 }, result);
        }

        [Fact]
        public void GetMatchedIndices_NotFuzzy_NoSubstringMatch_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("Chrome", "gc", false);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_FuzzyWithDelimiters_MapsBackCorrectly()
        {
            // "hello_world" normalized -> "helloworld"
            // query "hw" -> h at norm[0] -> orig 0, w at norm[5] -> orig 6
            var result = FuzzyMatcher.GetMatchedIndices("hello_world", "hw", true);
            Assert.Equal(new[] { 0, 6 }, result);
        }

        [Fact]
        public void GetMatchedIndices_FuzzyQueryLongerThanNormalized_ReturnsEmpty()
        {
            var result = FuzzyMatcher.GetMatchedIndices("ab", "abcdef", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_FuzzyIncompleteMatch_ReturnsEmpty()
        {
            // "abc" normalized -> "abc", query "abz" -> a,b match but z doesn't
            var result = FuzzyMatcher.GetMatchedIndices("abc", "abz", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_FuzzyNormalizedQueryEmpty_ReturnsEmpty()
        {
            // Query " _-" normalizes to empty
            var result = FuzzyMatcher.GetMatchedIndices("title", " _-", true);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_LongTitle_UsesHeapAllocation_ReturnsCorrectIndices()
        {
            // Title > 256 chars (MaxNormalizedLength is 256)
            var longTitle = new string('a', 300) + "bc";
            var query = "bc";
            
            // "bc" will be at the end, far beyond 256 limit. 
            // NormalizeWithMap truncates at MaxNormalizedLength (256).
            // So "bc" at index 300 will NOT be found in first 256 chars.
            var result = FuzzyMatcher.GetMatchedIndices(longTitle, query, true);
            
            // Should match "bc" at the end, using heap-allocated buffers
            Assert.Equal(new[] { 300, 301 }, result);
        }

        [Fact]
        public void GetMatchedIndices_LongTitle_FuzzyMatch_UsesHeapAllocation_ReturnsCorrectIndices()
        {
            // Title > 256, Query > 64 NOT REQUIRED for fuzzy branch, just Title > 256
            // But let's make it long enough to trigger the true branch in GetFuzzyMatchIndices:
            // TERNARY: char* titlePtr = title.Length > 256 ? (char*)Marshal.AllocHGlobal(...) : stackalloc char[256];
            
            var title = new string('a', 300) + "b" + new string('d', 10) + "c";
            var query = "bc";

            // Act
            var result = FuzzyMatcher.GetMatchedIndices(title, query, true);

            // Assert
            // Should match 'b' at 300 and 'c' at 311
            Assert.Equal(new[] { 300, 311 }, result);
        }

        [Fact]
        public void GetMatchedIndices_LongQuery_UsesHeapAllocation_ReturnsEmpty()
        {
            // Query > 64 chars
            var longQuery = new string('a', 100);
            var result = FuzzyMatcher.GetMatchedIndices("aaa", longQuery, true);
            
            Assert.Empty(result);
        }

        [Fact]
        public void GetMatchedIndices_MatchWithinTruncatedTitle_ReturnsIndices()
        {
            // Title is 300 chars, query "xyz" is at index 10 (well within 256 limit)
            var baseTitle = "abcxyzdef";
            var longTitle = baseTitle + new string('a', 300);
            
            var result = FuzzyMatcher.GetMatchedIndices(longTitle, "xyz", true);
            
            Assert.Equal(new[] { 3, 4, 5 }, result);
        }

        #endregion
    }
}
