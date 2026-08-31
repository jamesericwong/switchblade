using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class FuzzyMatcherAdapterTests
    {
        private readonly FuzzyMatcherAdapter _adapter;

        public FuzzyMatcherAdapterTests()
        {
            _adapter = new FuzzyMatcherAdapter();
        }

        [Fact]
        public void Score_CallsFuzzyMatcher()
        {
            // Act
            int score = _adapter.Score("Visual Studio Code", "vsc");

            // Assert: the adapter is a pure delegation — identical semantics to the underlying matcher
            Assert.Equal(FuzzyMatcher.Score("Visual Studio Code", "vsc"), score);
        }

        [Fact]
        public void IsMatch_CallsFuzzyMatcher()
        {
            // Act
            bool isMatch = _adapter.IsMatch("Visual Studio Code", "vsc");

            // Assert
            Assert.True(isMatch);
        }

        [Fact]
        public void ScoreWithNormalizedTitle_MatchesScoreSemantics()
        {
            string title = "Visual Studio Code";
            string query = "vsc";

            // Act
            int score = _adapter.ScoreWithNormalizedTitle(title, SwitchBlade.Contracts.SearchNormalization.Normalize(title), query);

            // Assert: identical to the raw-title path when given the canonical normalization
            Assert.Equal(FuzzyMatcher.Score(title, query), score);
        }
    }
}
