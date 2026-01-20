using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class WindowSearchServiceTests
    {
        private static WindowItem CreateWindow(string title, string processName = "test.exe")
        {
            return new WindowItem
            {
                Hwnd = IntPtr.Zero,
                Title = title,
                ProcessName = processName
            };
        }

        private static IWindowSearchService CreateService(IRegexCache? regexCache = null)
        {
            return new WindowSearchService(regexCache ?? new LruRegexCache(10));
        }

        [Fact]
        public void Search_EmptyQuery_ReturnsAllWindowsSorted()
        {
            var service = CreateService();
            var windows = new[]
            {
                CreateWindow("Zebra", "z.exe"),
                CreateWindow("Alpha", "a.exe"),
                CreateWindow("Beta", "b.exe")
            };

            var results = service.Search(windows, "", useFuzzy: false);

            Assert.Equal(3, results.Count);
            Assert.Equal("Alpha", results[0].Title);
            Assert.Equal("Beta", results[1].Title);
            Assert.Equal("Zebra", results[2].Title);
        }

        [Fact]
        public void Search_FuzzyMatch_ReturnsMatchingWindows()
        {
            var service = CreateService();
            var windows = new[]
            {
                CreateWindow("Visual Studio Code"),
                CreateWindow("Notepad"),
                CreateWindow("VS Code")
            };

            var results = service.Search(windows, "vsc", useFuzzy: true);

            // Both "Visual Studio Code" and "VS Code" should match "vsc"
            Assert.True(results.Count >= 1);
            Assert.True(results.All(w => w.Title.Contains("VS", StringComparison.OrdinalIgnoreCase) ||
                                          w.Title.Contains("Visual", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Search_FuzzyMatch_SortsByScore()
        {
            var service = CreateService();
            var windows = new[]
            {
                CreateWindow("ABC - Long Title Here"),
                CreateWindow("abc")
            };

            var results = service.Search(windows, "abc", useFuzzy: true);

            // Exact match should score higher
            Assert.Equal("abc", results[0].Title);
        }

        [Fact]
        public void Search_RegexMatch_FiltersCorrectly()
        {
            var service = CreateService();
            var windows = new[]
            {
                CreateWindow("Notepad - file.txt"),
                CreateWindow("Chrome"),
                CreateWindow("Notepad++ - code.py")
            };

            var results = service.Search(windows, "Notepad.*", useFuzzy: false);

            Assert.Equal(2, results.Count);
            Assert.All(results, w => Assert.Contains("Notepad", w.Title));
        }

        [Fact]
        public void Search_InvalidRegex_FallsBackToSubstring()
        {
            var service = CreateService();
            var windows = new[]
            {
                CreateWindow("Test [brackets]"),
                CreateWindow("No brackets here")
            };

            // Invalid regex pattern (unbalanced bracket)
            var results = service.Search(windows, "[brackets", useFuzzy: false);

            // Should fallback to substring match
            Assert.Single(results);
            Assert.Equal("Test [brackets]", results[0].Title);
        }

        [Fact]
        public void Search_NullWindows_ReturnsEmptyList()
        {
            var service = CreateService();

            var results = service.Search(null!, "test", useFuzzy: false);

            Assert.Empty(results);
        }

        [Fact]
        public void Search_EmptyWindows_ReturnsEmptyList()
        {
            var service = CreateService();

            var results = service.Search(Enumerable.Empty<WindowItem>(), "test", useFuzzy: false);

            Assert.Empty(results);
        }

        [Fact]
        public void Search_NoMatches_ReturnsEmptyList()
        {
            var service = CreateService();
            var windows = new[] { CreateWindow("Chrome"), CreateWindow("Firefox") };

            var results = service.Search(windows, "notepad", useFuzzy: true);

            Assert.Empty(results);
        }

        [Fact]
        public void Search_DuplicateWindows_ReturnsDistinct()
        {
            var service = CreateService();
            var window = CreateWindow("Same Window");
            var windows = new[] { window, window, window };

            var results = service.Search(windows, "", useFuzzy: false);

            Assert.Single(results);
        }

        [Fact]
        public void Search_CaseInsensitive_MatchesIgnoringCase()
        {
            var service = CreateService();
            var windows = new[] { CreateWindow("UPPERCASE"), CreateWindow("lowercase") };

            var results = service.Search(windows, "upper", useFuzzy: false);

            Assert.Single(results);
            Assert.Equal("UPPERCASE", results[0].Title);
        }

        [Fact]
        public void Constructor_NullRegexCache_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new WindowSearchService(null!));
        }
    }
}
