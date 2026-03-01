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

            // Assert
            Assert.True(score > 0);
        }

        [Fact]
        public void IsMatch_CallsFuzzyMatcher()
        {
            // Act
            bool isMatch = _adapter.IsMatch("Visual Studio Code", "vsc");

            // Assert
            Assert.True(isMatch);
        }
    }
}
