using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class LruRegexCacheTests
    {
        [Fact]
        public void GetOrCreate_ValidPattern_ReturnsRegex()
        {
            var cache = new LruRegexCache(10);

            var regex = cache.GetOrCreate("test.*");

            Assert.NotNull(regex);
            Assert.Matches(regex, "test123");
        }

        [Fact]
        public void GetOrCreate_SamePattern_ReturnsSameInstance()
        {
            var cache = new LruRegexCache(10);

            var regex1 = cache.GetOrCreate("pattern");
            var regex2 = cache.GetOrCreate("pattern");

            Assert.Same(regex1, regex2);
        }

        [Fact]
        public void GetOrCreate_InvalidPattern_ReturnsNull()
        {
            var cache = new LruRegexCache(10);

            // Unbalanced parenthesis is invalid regex
            var regex = cache.GetOrCreate("[invalid");

            Assert.Null(regex);
        }

        [Fact]
        public void GetOrCreate_NullPattern_ReturnsNull()
        {
            var cache = new LruRegexCache(10);

            var regex = cache.GetOrCreate(null!);

            Assert.Null(regex);
        }

        [Fact]
        public void GetOrCreate_EmptyPattern_ReturnsNull()
        {
            var cache = new LruRegexCache(10);

            var regex = cache.GetOrCreate("");

            Assert.Null(regex);
        }

        [Fact]
        public void GetOrCreate_ExceedsCapacity_EvictsOldest()
        {
            var cache = new LruRegexCache(2);

            cache.GetOrCreate("first");
            cache.GetOrCreate("second");
            cache.GetOrCreate("third"); // Should evict "first"

            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void GetOrCreate_AccessUpdatesLru_PreservesRecentlyUsed()
        {
            var cache = new LruRegexCache(2);

            var first = cache.GetOrCreate("first");
            cache.GetOrCreate("second");

            // Access "first" again to make it recently used
            cache.GetOrCreate("first");

            // Add third - should evict "second", not "first"
            cache.GetOrCreate("third");

            // First should still be cached
            var firstAgain = cache.GetOrCreate("first");
            Assert.Same(first, firstAgain);
        }

        [Fact]
        public void Clear_RemovesAllPatterns()
        {
            var cache = new LruRegexCache(10);
            cache.GetOrCreate("one");
            cache.GetOrCreate("two");

            cache.Clear();

            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void Constructor_InvalidMaxSize_ThrowsException()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new LruRegexCache(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new LruRegexCache(-1));
        }

        [Fact]
        public void GetOrCreate_CaseInsensitive_MatchesIgnoringCase()
        {
            var cache = new LruRegexCache(10);

            var regex = cache.GetOrCreate("test");

            Assert.NotNull(regex);
            Assert.Matches(regex, "TEST");
            Assert.Matches(regex, "Test");
        }
    }
}
