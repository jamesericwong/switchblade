using System;
using System.Collections.Generic;
using Moq;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    public class LastKnownGoodStrategyTests
    {
        private const int TestPid = 1234;

        private readonly Mock<IWindowIntrospection> _windows = new();
        private readonly Mock<ILogger> _logger = new();

        public LastKnownGoodStrategyTests()
        {
            // Defaults: one live process, no valid windows. Individual tests override as needed.
            _windows.Setup(w => w.GetPid(It.IsAny<IntPtr>())).Returns(TestPid);
            _windows.Setup(w => w.GetProcessInfo(It.IsAny<uint>())).Returns(("TestProcess", "test.exe"));
            _windows.Setup(w => w.IsWindowValid(It.IsAny<IntPtr>())).Returns(false);
        }

        private LastKnownGoodStrategy CreateStrategy() => new(() => "TestProvider", () => _logger.Object, _windows.Object);

        [Fact]
        public void Apply_GoodItems_ReturnsThemUnchanged()
        {
            // Arrange
            var strategy = CreateStrategy();
            var items = new List<WindowItem>
            {
                new() { Hwnd = (IntPtr)100, Title = "Tab 1", IsFallback = false },
                new() { Hwnd = (IntPtr)200, Title = "Tab 2", IsFallback = false }
            };

            // Act
            var result = strategy.Apply(items);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Tab 1", result[0].Title);
            Assert.Equal("Tab 2", result[1].Title);
        }

        [Fact]
        public void Apply_FallbackOnlyAfterGoodScan_RestoresLastKnownGood()
        {
            // Arrange - first scan populates LKG with good items
            var strategy = CreateStrategy();
            var goodItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Detailed Tab", IsFallback = false };
            strategy.Apply(new List<WindowItem> { goodItem });

            // Act - transient failure: only fallback items for the same live PID
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };
            var result = strategy.Apply(new List<WindowItem> { fallbackItem });

            // Assert - LKG item restored instead of the fallback
            Assert.Single(result);
            Assert.Equal("Detailed Tab", result[0].Title);
            Assert.False(result[0].IsFallback, "Should return LKG item");
        }

        [Fact]
        public void Apply_FallbackOnlyWithUnknownProcess_AcceptsFallbackAndClearsLkg()
        {
            // Arrange - first scan populates LKG with good items
            var strategy = CreateStrategy();
            var goodItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Detailed Tab", IsFallback = false };
            strategy.Apply(new List<WindowItem> { goodItem });

            // Process is now dead/unknown
            _windows.Setup(w => w.GetProcessInfo(It.IsAny<uint>())).Returns(("Unknown", null));
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };

            // Act
            var result = strategy.Apply(new List<WindowItem> { fallbackItem });

            // Assert - fallback accepted because the process is gone
            Assert.Single(result);
            Assert.Equal("Main Window", result[0].Title);
            Assert.True(result[0].IsFallback);

            // LKG was cleared: a further fallback scan must NOT restore the good item
            var secondResult = strategy.Apply(new List<WindowItem> { new() { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true } });
            Assert.True(secondResult[0].IsFallback, "LKG should have been cleared for a dead process");
        }

        [Fact]
        public void Apply_FallbackOnlyWithSystemProcess_AcceptsFallback()
        {
            // Arrange - first scan populates LKG with good items
            var strategy = CreateStrategy();
            strategy.Apply(new List<WindowItem> { new() { Hwnd = (IntPtr)1234, Title = "Detailed Tab", IsFallback = false } });

            _windows.Setup(w => w.GetProcessInfo(It.IsAny<uint>())).Returns(("System", null));
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };

            // Act
            var result = strategy.Apply(new List<WindowItem> { fallbackItem });

            // Assert - "System" is treated as a dead process: fallback accepted
            Assert.Single(result);
            Assert.True(result[0].IsFallback);
        }

        [Fact]
        public void Apply_NoLkgData_AcceptsFallback()
        {
            // Arrange - no prior good scan, so there is nothing to restore
            var strategy = CreateStrategy();
            var fallbackItem = new WindowItem { Hwnd = (IntPtr)1234, Title = "Main Window", IsFallback = true };

            // Act
            var result = strategy.Apply(new List<WindowItem> { fallbackItem });

            // Assert - fallback passes through unchanged
            Assert.Single(result);
            Assert.Equal("Main Window", result[0].Title);
        }

        [Fact]
        public void Apply_PidZero_IncludedAsIsWithoutLkgTracking()
        {
            // Arrange - all items resolve to an unresolvable PID (0)
            var strategy = CreateStrategy();
            _windows.Setup(w => w.GetPid(It.IsAny<IntPtr>())).Returns(0);

            // Act
            var result = strategy.Apply(new List<WindowItem>
            {
                new() { Hwnd = (IntPtr)100, Title = "Orphan 1", IsFallback = false },
                new() { Hwnd = (IntPtr)200, Title = "Orphan 2", IsFallback = true }
            });

            // Assert - unknown-PID windows are surfaced as-is (previously dropped silently)
            Assert.Equal(2, result.Count);
            Assert.Contains(result, i => i.Title == "Orphan 1");
            Assert.Contains(result, i => i.Title == "Orphan 2");

            // And they never enter LKG: a later scan that misses them must not resurrect them
            var second = strategy.Apply(new List<WindowItem>());
            Assert.Empty(second);
        }

        [Fact]
        public void Apply_MixedGoodAndFallbackForSamePid_StoresWholeGroupAsLkg()
        {
            // Arrange - a group containing both good and fallback items counts as "good"
            var strategy = CreateStrategy();
            var mixedGroup = new List<WindowItem>
            {
                new() { Hwnd = (IntPtr)100, Title = "Real Tab", IsFallback = false },
                new() { Hwnd = (IntPtr)200, Title = "Main Window", IsFallback = true }
            };
            strategy.Apply(mixedGroup);

            // Act - transient failure for the same live PID
            var fallbackOnly = new List<WindowItem> { new() { Hwnd = (IntPtr)100, Title = "Main Window", IsFallback = true } };
            var result = strategy.Apply(fallbackOnly);

            // Assert - the whole stored group is restored (good + fallback items)
            Assert.Equal(2, result.Count);
            Assert.Contains(result, i => i.Title == "Real Tab" && !i.IsFallback);
        }

        [Fact]
        public void Apply_PidMissedButWindowStillValid_PreservesLkgAndAddsToResults()
        {
            // Arrange - first scan populates LKG for a PID whose window stays valid
            var strategy = CreateStrategy();
            var goodItem = new WindowItem { Hwnd = (IntPtr)5678, Title = "Tab 1", IsFallback = false };
            strategy.Apply(new List<WindowItem> { goodItem });

            // Act - scan misses the PID entirely, but its window is still valid
            _windows.Setup(w => w.IsWindowValid((IntPtr)5678)).Returns(true);
            var result = strategy.Apply(new List<WindowItem>());

            // Assert - LKG preserved and surfaced in results
            Assert.Single(result);
            Assert.Equal("Tab 1", result[0].Title);

            // LKG still intact: a later fallback scan for the live PID restores it again
            var restored = strategy.Apply(new List<WindowItem> { new() { Hwnd = (IntPtr)5678, Title = "Main Window", IsFallback = true } });
            Assert.Equal("Tab 1", restored[0].Title);
        }

        [Fact]
        public void Apply_PidMissedAndWindowInvalid_DiscardsLkg()
        {
            // Arrange - first scan populates LKG for a PID whose window is gone
            var strategy = CreateStrategy();
            strategy.Apply(new List<WindowItem> { new() { Hwnd = (IntPtr)5678, Title = "Tab 1", IsFallback = false } });

            // Act - scan misses the PID and its windows are invalid (IsWindowValid defaults to false)
            var result = strategy.Apply(new List<WindowItem>());

            // Assert - nothing surfaced; LKG discarded
            Assert.Empty(result);

            // A later fallback scan for the live PID must NOT restore the discarded item
            var secondResult = strategy.Apply(new List<WindowItem> { new() { Hwnd = (IntPtr)5678, Title = "Main Window", IsFallback = true } });
            Assert.True(secondResult[0].IsFallback, "LKG should have been discarded for invalid windows");
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LastKnownGoodStrategy(null!, () => null, _windows.Object));
        }

        [Fact]
        public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LastKnownGoodStrategy(() => "TestProvider", null!, _windows.Object));
        }

        [Fact]
        public void Constructor_NullWindows_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LastKnownGoodStrategy(() => "TestProvider", () => null, null!));
        }

        [Fact]
        public void Apply_NullResults_ThrowsArgumentNullException()
        {
            var strategy = CreateStrategy();

            Assert.Throws<ArgumentNullException>(() => strategy.Apply(null!));
        }
    }
}
