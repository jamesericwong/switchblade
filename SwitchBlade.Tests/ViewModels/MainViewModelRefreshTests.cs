using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using Xunit;

namespace SwitchBlade.Tests.ViewModels
{
    public class MainViewModelRefreshTests
    {
        private readonly Mock<IWindowProvider> _mockWindowProvider;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly UserSettings _userSettings;

        public MainViewModelRefreshTests()
        {
            _mockWindowProvider = new Mock<IWindowProvider>();
            _mockSettingsService = new Mock<ISettingsService>();
            _userSettings = new UserSettings();

            _mockSettingsService.Setup(s => s.Settings).Returns(_userSettings);
            _mockWindowProvider.Setup(p => p.PluginName).Returns("MockPlugin");
        }

        private readonly SynchronousDispatcherService _dispatcher = new SynchronousDispatcherService();

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_WithPreserveScroll_UpdatesFilteredWindows()
        {
            // Arrange
            _userSettings.RefreshBehavior = RefreshBehavior.PreserveScroll;

            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            // Setup initial windows
            var initialWindows = new List<WindowItem>
            {
                new WindowItem { Hwnd = IntPtr.Zero, Title = "Title 1", ProcessName = "Process 1", Source = _mockWindowProvider.Object },
                new WindowItem { Hwnd = IntPtr.Zero, Title = "Title 2", ProcessName = "Process 2", Source = _mockWindowProvider.Object }
            };

            _mockWindowProvider.Setup(p => p.PluginName).Returns("MockPlugin");
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(initialWindows);

            // Act
            await vm.RefreshWindows();

            // Assert - FilteredWindows should be populated
            Assert.Equal(2, vm.FilteredWindows.Count);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_PreserveIdentity_SelectsSameWindowIfMoves()
        {
            // Arrange
            _userSettings.RefreshBehavior = RefreshBehavior.PreserveIdentity;
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var win1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Target Window", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            var win2 = new WindowItem { Hwnd = new IntPtr(2), Title = "Another Window", ProcessName = "Proc", Source = _mockWindowProvider.Object };

            // Initial: win1 is at index 0, win2 at index 1
            _mockWindowProvider.SetupSequence(p => p.GetWindows())
                .Returns(new[] { win1, win2 }) // First call
                .Returns(new[] { win2, win1 }); // Second call (Swap order)

            await vm.RefreshWindows();

            // User selects Target Window (win1)
            vm.SelectedWindow = vm.FilteredWindows.First(w => w.Hwnd == new IntPtr(1));
            Assert.Equal("Target Window", vm.SelectedWindow.Title);

            // Act: Refresh again, order swaps
            await vm.RefreshWindows();

            // Assert: Selection should still be "Target Window" (Identity preserved)
            Assert.NotNull(vm.SelectedWindow);
            Assert.Equal("Target Window", vm.SelectedWindow.Title);
            Assert.Equal(new IntPtr(1), vm.SelectedWindow.Hwnd);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_PreserveIndex_SelectsSameIndexIfContentChanges()
        {
            // Arrange
            _userSettings.RefreshBehavior = RefreshBehavior.PreserveIndex;
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var win1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Window A", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            var win2 = new WindowItem { Hwnd = new IntPtr(2), Title = "Window B", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            var winNew = new WindowItem { Hwnd = new IntPtr(3), Title = "Window C", ProcessName = "Proc", Source = _mockWindowProvider.Object };

            // Initial: A, B
            _mockWindowProvider.SetupSequence(p => p.GetWindows())
                .Returns(new[] { win1, win2 })
                .Returns(new[] { winNew, win2 }); // A is gone, C is new. Sorted: B, C.

            await vm.RefreshWindows();

            // Select index 1 (Window B)
            vm.SelectedWindow = vm.FilteredWindows[1];
            Assert.Equal("Window B", vm.SelectedWindow.Title);

            // Act: Refresh. List becomes [B, C]. Index 1 is now C.
            await vm.RefreshWindows();

            // Assert: Selection should be at index 1 (Window C)
            Assert.NotNull(vm.SelectedWindow);
            Assert.Equal("Window C", vm.SelectedWindow.Title);
            Assert.NotEqual("Window B", vm.SelectedWindow.Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateSearch_PreserveScroll_DoesNotForceSelectionIfItemGone()
        {
            // Arrange
            _userSettings.RefreshBehavior = RefreshBehavior.PreserveScroll;
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var win1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Window A", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            var win2 = new WindowItem { Hwnd = new IntPtr(2), Title = "Window B", ProcessName = "Proc", Source = _mockWindowProvider.Object };

            _mockWindowProvider.SetupSequence(p => p.GetWindows())
                .Returns(new[] { win1, win2 })
                .Returns(new[] { win2 }); // A is gone

            await vm.RefreshWindows();
            vm.SelectedWindow = vm.FilteredWindows[0]; // Select A

            // Act
            await vm.RefreshWindows();

            // Assert
            // Behavior for SCROLL logic: If identity found, keep it. If not found, select index (fallback).
            // But View suppresses scroll via Pre/Post Update events.
            // Here, A is gone. Logic falls back to index preservation (select index 0, which is B).

            Assert.NotNull(vm.SelectedWindow);
            Assert.Equal("Window B", vm.SelectedWindow.Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_WithDuplicateKeys_DedupesWithoutError()
        {
            // Arrange
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            // Create two identical windows (same Hwnd, same Title)
            var duplicateWin = new WindowItem { Hwnd = new IntPtr(999), Title = "Duplicate Window", ProcessName = "Proc", Source = _mockWindowProvider.Object };

            _mockWindowProvider.Setup(p => p.GetWindows())
                .Returns(new[] { duplicateWin, duplicateWin });

            // Act
            // This should NOT throw ArgumentException
            await vm.RefreshWindows();

            // Assert
            // It should validly populate the list with ONE instance
            Assert.Single(vm.FilteredWindows);
            Assert.Equal("Duplicate Window", vm.FilteredWindows[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_TitleChangeOnly_UpdatesTitleWithoutResetingBadgeAnimation()
        {
            // Arrange - This tests the fix for windows with frequently changing titles
            // (e.g., bandwidth monitors) where we want titles to update but badges to NOT re-animate
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var hwnd = new IntPtr(123);
            var win1 = new WindowItem { Hwnd = hwnd, Title = "Download: 50 KB/s", ProcessName = "NetMonitor", Source = _mockWindowProvider.Object };

            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1 });
            await vm.RefreshWindows();

            // Simulate badge animation has run
            var itemAfterFirstRefresh = vm.FilteredWindows[0];
            itemAfterFirstRefresh.HasBeenAnimated = true;
            itemAfterFirstRefresh.BadgeOpacity = 1;
            itemAfterFirstRefresh.BadgeTranslateX = 0;

            // Act - Same window, different title (simulating frequent title updates)
            var win1Updated = new WindowItem { Hwnd = hwnd, Title = "Download: 75 KB/s", ProcessName = "NetMonitor", Source = _mockWindowProvider.Object };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1Updated });
            await vm.RefreshWindows();

            // Assert
            Assert.Single(vm.FilteredWindows);
            var resultItem = vm.FilteredWindows[0];

            // Title should be updated
            Assert.Equal("Download: 75 KB/s", resultItem.Title);

            // Badge animation state should be preserved (NOT reset)
            Assert.True(resultItem.HasBeenAnimated, "HasBeenAnimated should remain true - badge should not re-animate");
            Assert.Equal(1, resultItem.BadgeOpacity);
            Assert.Equal(0, resultItem.BadgeTranslateX);
        }

        private class SynchronousDispatcherService : IDispatcherService
        {
            public void Invoke(Action action) => action();
            public async System.Threading.Tasks.Task InvokeAsync(Func<System.Threading.Tasks.Task> action) => await action();
        }

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_StructurallyIdentical_DoesNotResetBadgeState()
        {
            // Arrange - Tests the optimized HashSet-based structural diff check
            // When windows are structurally identical (same HWNDs), titles can update
            // without triggering full reconciliation (badge state preserved)
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var hwnd1 = new IntPtr(100);
            var hwnd2 = new IntPtr(200);

            var win1 = new WindowItem { Hwnd = hwnd1, Title = "Window A v1", ProcessName = "App", Source = _mockWindowProvider.Object };
            var win2 = new WindowItem { Hwnd = hwnd2, Title = "Window B v1", ProcessName = "App", Source = _mockWindowProvider.Object };

            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1, win2 });
            await vm.RefreshWindows();

            // Simulate badge animation completed
            foreach (var item in vm.FilteredWindows)
            {
                item.HasBeenAnimated = true;
                item.BadgeOpacity = 1.0;
            }

            // Act - Same HWNDs, different titles (structurally identical)
            var win1Updated = new WindowItem { Hwnd = hwnd1, Title = "Window A v2", ProcessName = "App", Source = _mockWindowProvider.Object };
            var win2Updated = new WindowItem { Hwnd = hwnd2, Title = "Window B v2", ProcessName = "App", Source = _mockWindowProvider.Object };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1Updated, win2Updated });
            await vm.RefreshWindows();

            // Assert - Badge state should be preserved (no full reconciliation)
            Assert.Equal(2, vm.FilteredWindows.Count);
            Assert.All(vm.FilteredWindows, item => Assert.True(item.HasBeenAnimated));
            Assert.All(vm.FilteredWindows, item => Assert.Equal(1.0, item.BadgeOpacity));
        }

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_StructuralChange_TriggersReconciliation()
        {
            // Arrange - When HWNDs change (window added/removed), full reconciliation happens
            var vm = new MainViewModel(new[] { _mockWindowProvider.Object }, _mockSettingsService.Object, _dispatcher);

            var hwnd1 = new IntPtr(100);
            var hwnd2 = new IntPtr(200);
            var hwnd3 = new IntPtr(300);

            var win1 = new WindowItem { Hwnd = hwnd1, Title = "Window A", ProcessName = "App", Source = _mockWindowProvider.Object };
            var win2 = new WindowItem { Hwnd = hwnd2, Title = "Window B", ProcessName = "App", Source = _mockWindowProvider.Object };

            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1, win2 });
            await vm.RefreshWindows();

            Assert.Equal(2, vm.FilteredWindows.Count);

            // Act - Different HWNDs (structural change - one removed, one added)
            var win3 = new WindowItem { Hwnd = hwnd3, Title = "Window C", ProcessName = "App", Source = _mockWindowProvider.Object };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new[] { win1, win3 }); // win2 replaced by win3
            await vm.RefreshWindows();

            // Assert - Count changes and new window appears
            Assert.Equal(2, vm.FilteredWindows.Count);
            Assert.Contains(vm.FilteredWindows, w => w.Hwnd == hwnd1);
            Assert.Contains(vm.FilteredWindows, w => w.Hwnd == hwnd3);
            Assert.DoesNotContain(vm.FilteredWindows, w => w.Hwnd == hwnd2);
        }
    }
}

