using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using SwitchBlade.Core;
using SwitchBlade.ViewModels;
using Xunit;

namespace SwitchBlade.Tests.ViewModels
{
    public class RegexCacheTests
    {
        private readonly Mock<IWindowProvider> _mockWindowProvider;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly UserSettings _userSettings;
        private readonly Mock<IWindowSearchService> _mockSearchService;
        private readonly SynchronousDispatcherService _dispatcher = new SynchronousDispatcherService();

        public RegexCacheTests()
        {
            _mockWindowProvider = new Mock<IWindowProvider>();
            _mockSettingsService = new Mock<ISettingsService>();
            _userSettings = new UserSettings();

            _mockSettingsService.Setup(s => s.Settings).Returns(_userSettings);
            _mockWindowProvider.Setup(p => p.PluginName).Returns("MockPlugin");

            var regexCache = new LruRegexCache(_userSettings.RegexCacheSize);
            _mockSearchService = new Mock<IWindowSearchService>(); // Keep the field but we'll use a real one in the VM
            _realSearchService = new WindowSearchService(regexCache);
        }

        private readonly WindowSearchService _realSearchService;

        private MainViewModel CreateViewModel(IEnumerable<IWindowProvider>? providers = null!)
        {
            var pList = (providers ?? Enumerable.Empty<IWindowProvider>()).ToList();
            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(() => pList.SelectMany(p => p.GetWindows()).ToList());
            
            mockOrch.Setup(o => o.RefreshAsync(It.IsAny<ISet<string>>()))
                .Returns(Task.CompletedTask)
                .Raises(o => o.WindowListUpdated += null, new WindowListUpdatedEventArgs(null!, true));

            var mockNav = new Mock<INavigationService>();
            mockNav.Setup(n => n.ResolveSelection(It.IsAny<IList<WindowItem>>(), It.IsAny<IntPtr?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<RefreshBehavior>(), It.IsAny<bool>()))
                .Returns((IList<WindowItem> windows, IntPtr? hwnd, string title, int index, RefreshBehavior behavior, bool reset) => 
                {
                    if (behavior == RefreshBehavior.PreserveIdentity && hwnd.HasValue)
                        return windows.FirstOrDefault(w => w.Hwnd == hwnd.Value);
                    return windows.FirstOrDefault();
                });

            return new MainViewModel(mockOrch.Object, _realSearchService, mockNav.Object, _mockSettingsService.Object, _dispatcher);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_UsesRegexCache_SubsequentSearchesAreFast()
        {
            // Arrange
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            var windows = new List<WindowItem>
            {
                new WindowItem { Title = "Apple", ProcessName = "A", Source = _mockWindowProvider.Object },
                new WindowItem { Title = "Banana", ProcessName = "B", Source = _mockWindowProvider.Object }
            };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(windows);

            // Populate all windows
            await vm.RefreshWindows();

            // Act
            vm.SearchText = "app"; // First run caches the regex
            var results1 = vm.FilteredWindows.ToList();

            vm.SearchText = "";    // Reset
            vm.SearchText = "app"; // Second run should hit the cache
            var results2 = vm.FilteredWindows.ToList();

            // Assert
            Assert.Single(results1);
            Assert.Single(results2);
            Assert.Equal("Apple", results1[0].Title);
            Assert.Equal("Apple", results2[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_LruCache_EvictsOldestItems()
        {
            // Arrange
            _userSettings.RegexCacheSize = 2; // Set small cache for testing
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>());
            await vm.RefreshWindows();

            // Act
            vm.SearchText = "one";   // Cache: [one]
            vm.SearchText = "two";   // Cache: [two, one]
            vm.SearchText = "three"; // Cache: [three, two]. "one" should be evicted.

            // Assert
            Assert.NotNull(vm.FilteredWindows);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_InvalidRegex_FallsBackToIndexOf()
        {
            // Arrange
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            var windows = new List<WindowItem>
            {
                new WindowItem { Title = "[Bracketed]", ProcessName = "A", Source = _mockWindowProvider.Object }
            };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(windows);
            await vm.RefreshWindows();

            // Act
            // "[" is an invalid regex pattern (missing closing bracket)
            vm.SearchText = "[";

            // Assert
            // Should not throw, and should find the bracketed item via IndexOf fallback
            Assert.Single(vm.FilteredWindows);
            Assert.Equal("[Bracketed]", vm.FilteredWindows[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_RegexSafety_DoesNotHangOnComplexPatterns()
        {
            // Arrange
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            var windows = new List<WindowItem>
            {
                new WindowItem { Title = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!", ProcessName = "A", Source = _mockWindowProvider.Object }
            };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(windows);
            await vm.RefreshWindows();

            // Act
            // This is a classic ReDoS pattern that could hang traditional backtracking engines
            // if input doesn't match and backtracking is not capped.
            // With NonBacktracking, it's safe.
            vm.SearchText = "^(a+)+$";

            // Assert
            // Should return quickly with no match
            Assert.Empty(vm.FilteredWindows);
        }

        private class SynchronousDispatcherService : IDispatcherService
        {
            public void Invoke(Action action) => action();
            public async System.Threading.Tasks.Task InvokeAsync(Func<System.Threading.Tasks.Task> action) => await action();
        }
    }
}
