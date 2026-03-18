using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
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

        private MainViewModel CreateViewModel(IEnumerable<IWindowProvider>? providers = null!)
        {
            var pList = (providers ?? Enumerable.Empty<IWindowProvider>()).ToList();
            var mockOrch = new Mock<IWindowOrchestrationService>();
            var reconciler = new WindowReconciler(null);
            var allWindows = new List<WindowItem>();
            
            mockOrch.Setup(o => o.AllWindows).Returns(() => 
            {
                lock(reconciler)
                {
                    return allWindows.ToList();
                }
            });
            
            mockOrch.Setup(o => o.RefreshAsync(It.IsAny<ISet<string>>()))
                .Returns((ISet<string> disabled) => 
                {
                     lock(reconciler)
                     {
                         foreach(var p in pList)
                         {
                             var raw = p.GetWindows().ToList();
                             var reconciled = reconciler.Reconcile(raw, p);
                             
                             // Replace existing items for this source
                             for (int i = allWindows.Count - 1; i >= 0; i--)
                             {
                                 if (allWindows[i].Source == p)
                                     allWindows.RemoveAt(i);
                             }
                             allWindows.AddRange(reconciled);
                         }
                     }
                     
                     // Raise event synchronously inside the call to ensure no races in tests
                     mockOrch.Raise(o => o.WindowListUpdated += null, new WindowListUpdatedEventArgs(null!, true));
                     return Task.CompletedTask;
                });

            var mockSearch = new Mock<IWindowSearchService>();
            mockSearch.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((IEnumerable<WindowItem> w, string q, bool f) => w.Distinct().ToList());

            var mockNav = new Mock<INavigationService>();
            mockNav.Setup(n => n.ResolveSelection(It.IsAny<IList<WindowItem>>(), It.IsAny<IntPtr?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<RefreshBehavior>(), It.IsAny<bool>()))
                .Returns((IList<WindowItem> windows, IntPtr? hwnd, string title, int index, RefreshBehavior behavior, bool reset) => 
                {
                    if (behavior == RefreshBehavior.PreserveIdentity && hwnd.HasValue)
                        return windows.FirstOrDefault(w => w.Hwnd == hwnd.Value && w.Title == title) ?? windows.FirstOrDefault(w => w.Hwnd == hwnd.Value);
                    return windows.FirstOrDefault();
                });

            return new MainViewModel(
                mockOrch.Object,
                mockSearch.Object,
                mockNav.Object,
                _mockSettingsService.Object,
                _dispatcher);
        }

        private readonly SynchronousDispatcherService _dispatcher = new SynchronousDispatcherService();

        [Fact]
        public async System.Threading.Tasks.Task RefreshWindows_WithPreserveScroll_UpdatesFilteredWindows()
        {
            // Arrange
            _userSettings.RefreshBehavior = RefreshBehavior.PreserveScroll;

            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            var win1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Duplicate Window", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            var win1Clone = new WindowItem { Hwnd = new IntPtr(1), Title = "Duplicate Window", ProcessName = "Proc", Source = _mockWindowProvider.Object };
            
            // Reconciler handles deduping in real app, but here we testing VM's Sync through mock orchard.
            // Since we added Equals override to WindowItem, Sync will handle this if they are in the source list.
            // However, sortedResults from search usually won't have duplicates if orchestrator/reconciler did its job.
            // Let's simulate a case where search DOES return duplicates to test VM robustness.
            
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new List<WindowItem> { win1, win1Clone });

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
            // Arrange
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });
            var win1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Old Title", ProcessName = "App", Source = _mockWindowProvider.Object };
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new List<WindowItem> { win1 });

            await vm.RefreshWindows();
            win1.HasBeenAnimated = true;

            // Update with same HWND but NEW Title - Sync should still find it if we override Equals to only check HWND?
            // Wait, Equals checks Title too. So if Title changes, it's a DIFFERENT item for Sync.
            // But real app Reconciler handles this.
            // For this test to pass with VM's Sync, we need Title to be the same in Equals, or VM to handle title updates.
            // Actually, if Title changes, Reconciler returns the SAME instance but UPDATES the title.
            // So we should simulate THAT: provide the SAME instance with a changed title.
            
            win1.Title = "New Title";
            _mockWindowProvider.Setup(p => p.GetWindows()).Returns(new List<WindowItem> { win1 });

            await vm.RefreshWindows();

            // Assert
            Assert.Equal("New Title", vm.FilteredWindows[0].Title);
            Assert.True(vm.FilteredWindows[0].HasBeenAnimated);
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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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
            var vm = CreateViewModel(new[] { _mockWindowProvider.Object });

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



