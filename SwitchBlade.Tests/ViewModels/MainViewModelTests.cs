using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Tests.ViewModels
{
    public class MainViewModelTests
    {
        private static Mock<IWindowProvider> CreateMockProvider(params WindowItem[] windows)
        {
            var mock = new Mock<IWindowProvider>();
            mock.Setup(p => p.GetWindows()).Returns(windows);
            return mock;
        }

        private MainViewModel CreateViewModel(IEnumerable<IWindowProvider>? providers = null!)
        {
            var pList = (providers ?? Enumerable.Empty<IWindowProvider>()).ToList();
            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(() => pList.SelectMany(p => p.GetWindows()).ToList());
            mockOrch.Setup(o => o.RefreshAsync(It.IsAny<ISet<string>>()))
                .Returns(Task.CompletedTask)
                .Raises(o => o.WindowListUpdated += null, new WindowListUpdatedEventArgs(null!, true));
            
            var mockSearch = new Mock<IWindowSearchService>();
            mockSearch.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((IEnumerable<WindowItem> windows, string text, bool fuzzy) => windows.ToList());

            var mockNav = new Mock<INavigationService>();
            mockNav.Setup(n => n.ResolveSelection(It.IsAny<IList<WindowItem>>(), It.IsAny<IntPtr?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<RefreshBehavior>(), It.IsAny<bool>()))
                .Returns((IList<WindowItem> windows, IntPtr? hwnd, string title, int index, RefreshBehavior behavior, bool reset) => 
                    windows.FirstOrDefault());

            mockNav.Setup(n => n.CalculateMoveIndex(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((int current, int direction, int count) => 
                {
                    if (count == 0) return -1;
                    int next = current + direction;
                    return Math.Clamp(next, 0, count - 1);
                });

            return new MainViewModel(
                mockOrch.Object,
                mockSearch.Object,
                mockNav.Object,
                null,
                new SynchronousDispatcherService());
        }

        [Fact]
        public void Constructor_WithEmptyProviders_CreatesEmptyFilteredWindows()
        {
            var vm = CreateViewModel();

            Assert.Empty(vm.FilteredWindows);
        }

        [Fact]
        public void Constructor_SetsDefaultEnablePreviews_ToTrue()
        {
            var vm = CreateViewModel();

            Assert.True(vm.EnablePreviews);
        }

        [Fact]
        public void SearchText_DefaultValue_IsEmptyString()
        {
            var vm = CreateViewModel();

            Assert.Equal(string.Empty, vm.SearchText);
        }

        [Fact]
        public void SelectedWindow_DefaultValue_IsNull()
        {
            var vm = CreateViewModel();

            Assert.Null(vm.SelectedWindow);
        }

        [Fact]
        public void SearchText_SetValue_UpdatesProperty()
        {
            var vm = CreateViewModel();

            vm.SearchText = "test";

            Assert.Equal("test", vm.SearchText);
        }

        [Fact]
        public void SearchText_Change_RaisesPropertyChanged()
        {
            var vm = CreateViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SearchText))
                    propertyChangedRaised = true;
            }; // Closing the event handler here

            vm.SearchText = "test";

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void EnablePreviews_SetValue_UpdatesProperty()
        {
            var vm = CreateViewModel();

            vm.EnablePreviews = false;

            Assert.False(vm.EnablePreviews);
        }

        [Fact]
        public void EnablePreviews_Change_RaisesPropertyChanged()
        {
            var vm = CreateViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.EnablePreviews))
                    propertyChangedRaised = true;
            };

            vm.EnablePreviews = false;

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void MoveSelection_WithEmptyList_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelection(1));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelection_WithNullSelectedWindow_DoesNotThrow()
        {
            var vm = CreateViewModel();
            vm.SelectedWindow = null;

            var exception = Record.Exception(() => vm.MoveSelection(1));

            Assert.Null(exception);
        }

        [Fact]
        public void FilteredWindows_ImplementsINotifyPropertyChanged()
        {
            var vm = CreateViewModel();

            Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        }

        [Fact]
        public void ShowInTaskbar_WithNullSettingsService_ReturnsTrue()
        {
            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, null);

            Assert.True(vm.ShowInTaskbar);
        }

        [Fact]
        public void MoveSelectionToFirst_WithEmptyList_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelectionToFirst());

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionToLast_WithEmptyList_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelectionToLast());

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithEmptyList_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, 10));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithZeroPageSize_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, 0));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithNegativePageSize_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, -5));

            Assert.Null(exception);
        }
        [Fact]
        public void MoveSelectionToFirst_WithItems_SelectsFirstItem()
        {
            var item1 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "1", ProcessName = "exe", Source = null };
            var item2 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "2", ProcessName = "exe", Source = null };
            // MainViewModel constructor expects IEnumerable<IWindowProvider>
            var vm = CreateViewModel();
            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1, item2 };
            vm.SelectedWindow = item2;
            vm.MoveSelectionToFirst();

            Assert.Equal(item1, vm.SelectedWindow);
        }

        [Fact]
        public void MoveSelectionToLast_WithItems_SelectsLastItem()
        {
            var item1 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "1", ProcessName = "exe", Source = null };
            var item2 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "2", ProcessName = "exe", Source = null };
            var vm = CreateViewModel();

            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1, item2 };
            vm.SelectedWindow = item1;

            vm.MoveSelectionToLast();

            Assert.Equal(item2, vm.SelectedWindow);
        }

        [Fact]
        public void MoveSelection_ClampsAtEndOfList()
        {
            var item1 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "1", ProcessName = "exe", Source = null };
            var item2 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "2", ProcessName = "exe", Source = null };
            var vm = CreateViewModel();

            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1, item2 };
            vm.SelectedWindow = item2;

            vm.MoveSelection(1);

            // Should stay at last item (clamp behavior)
            Assert.Equal(item2, vm.SelectedWindow);
        }

        [Fact]
        public void MoveSelection_ClampsAtStartOfList()
        {
            var item1 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "1", ProcessName = "exe", Source = null };
            var item2 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "2", ProcessName = "exe", Source = null };
            var vm = CreateViewModel();

            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1, item2 };
            vm.SelectedWindow = item1;

            vm.MoveSelection(-1);

            // Should stay at first item (clamp behavior)
            Assert.Equal(item1, vm.SelectedWindow);
        }

        [Fact]
        public void WindowProviders_DirectOrchestration_ReturnsDistinctSources()
        {
            var p1 = new Mock<IWindowProvider>().Object;
            var items = new List<WindowItem> { new() { Source = p1 }, new() { Source = p1 } };
            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(items);

            var vm = new MainViewModel(mockOrch.Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object);

            // DIP fix: WindowProviders now works against the interface, returning distinct sources
            Assert.Single(vm.WindowProviders);
            Assert.Contains(p1, vm.WindowProviders);
        }

        [Fact]
        public void SettingsChanged_UpdatesProperties()
        {
            var mockSettings = new Mock<ISettingsService>();
            var settings = new UserSettings { DisabledPlugins = new List<string> { "P1" }, EnablePreviews = true };
            mockSettings.Setup(s => s.Settings).Returns(settings);

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            settings.EnablePreviews = false;
            mockSettings.Raise(s => s.SettingsChanged += null);

            Assert.False(vm.EnablePreviews);
        }

        [Fact]
        public void SyncCollection_EdgeCases()
        {
            var vm = CreateViewModel();
            var i1 = new WindowItem { Title = "1" };
            var i2 = new WindowItem { Title = "2" };
            var i3 = new WindowItem { Title = "3" };

            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { i1, i2 };

            // Simulate SyncCollection through UpdateSearch 
            // We need to mock SearchService to return [i3, i1] (insert i3, keep i1, remove i2)
            var mockSearch = new Mock<IWindowSearchService>();
            mockSearch.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                      .Returns(new List<WindowItem> { i3, i1 });

            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(new List<WindowItem>());

            var nav = new Mock<INavigationService>();

            var vm2 = new MainViewModel(mockOrch.Object, mockSearch.Object, nav.Object, null, new Mock<IDispatcherService>().Object);
            vm2.FilteredWindows.Add(i1);
            vm2.FilteredWindows.Add(i2);

            // Private UpdateSearch call via SearchText setter
            vm2.SearchText = "update";

            Assert.Equal(2, vm2.FilteredWindows.Count);
            Assert.Equal(i3, vm2.FilteredWindows[0]);
            Assert.Equal(i1, vm2.FilteredWindows[1]);
        }

        [Fact]
        public void MoveSelectionByPage_WithValidPageSize_CallsNavigationService()
        {
            var nav = new Mock<INavigationService>();
            var items = new List<WindowItem> { new() };
            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, nav.Object);
            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem>(items);
            vm.SelectedWindow = items[0];

            vm.MoveSelectionByPage(1, 5);

            nav.Verify(n => n.CalculatePageMoveIndex(0, 1, 5, 1), Times.Once);
        }

        [Fact]
        public void ShortcutModifierText_ReturnsFormattedString()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { NumberShortcutModifier = ModifierKeyFlags.Ctrl | ModifierKeyFlags.Shift });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.Equal("Ctrl+Shift", vm.ShortcutModifierText);
        }

        [Fact]
        public void FilteredWindows_SetToNull_DoesNothing()
        {
            var vm = CreateViewModel();
            var initial = vm.FilteredWindows;

            vm.FilteredWindows = null!;

            Assert.NotNull(vm.FilteredWindows);
            Assert.Same(initial, vm.FilteredWindows);
        }

        [Fact]
        public void SyncCollection_HandlesMovesAndInserts()
        {
            // Setup strict mock for SearchService to control order
            // Start with [A, B]
            var itemA = new WindowItem { Title = "A" };
            var itemB = new WindowItem { Title = "B" };
            var itemC = new WindowItem { Title = "C" };

            var vm = CreateViewModel();
            vm.FilteredWindows.Add(itemA);
            vm.FilteredWindows.Add(itemB);

            // Simulation 1: Swap to [B, A]
            var mockSearchSwap = new Mock<IWindowSearchService>();
            mockSearchSwap.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                          .Returns(new List<WindowItem> { itemB, itemA }); // Reversed order

            // Inject new dependencies via constructor is hard, so we'll just test SyncCollection indirectly via subclass or reflection? 
            // Or just trust the previous SyncCollection_EdgeCases test covered "moves" well enough?
            // "Move" happens when item is found later in list. 
            // [A, B] -> want [B, A]
            // ptr=0. source[0]=B. collection[0]=A != B. scan forward. found B at 1. Move(1, 0).
            // collection is now [B, A]. ptr=1. source[1]=A. collection[1]=A. match. ptr++.
            // Done.

            // Let's verify this specific swap behavior logic with a new test instance
            var vm2 = new MainViewModel(
                new Mock<IWindowOrchestrationService>().Object,
                mockSearchSwap.Object,
                new Mock<INavigationService>().Object,
                null,
                new Mock<IDispatcherService>().Object);

            vm2.FilteredWindows.Add(itemA);
            vm2.FilteredWindows.Add(itemB);

            vm2.SearchText = "force_update"; // triggers UpdateSearch -> SyncCollection

            Assert.Equal(itemB, vm2.FilteredWindows[0]);
            Assert.Equal(itemA, vm2.FilteredWindows[1]);
        }

        [Fact]
        public void SearchText_Change_RaisesSearchTextChanged()
        {
            var vm = CreateViewModel();
            var eventRaised = false;
            vm.SearchTextChanged += (s, e) => eventRaised = true;

            vm.SearchText = "new";

            Assert.True(eventRaised);
        }

        [Fact]
        public void UpdateSearch_RaisesResultsUpdated()
        {
            var vm = CreateViewModel();
            var eventRaised = false;
            vm.ResultsUpdated += (s, e) => eventRaised = true;

            // SearchText change triggers UpdateSearch
            vm.SearchText = "update";

            Assert.True(eventRaised);
        }

        // ===== Coverage Gap Tests =====

        [Fact]
        public void Constructor_NullOrchestrationService_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MainViewModel(null!, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object));
        }

        [Fact]
        public void Constructor_NullSearchService_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MainViewModel(new Mock<IWindowOrchestrationService>().Object, null!, new Mock<INavigationService>().Object));
        }

        [Fact]
        public void Constructor_NullNavigationService_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, null!));
        }

        [Fact]
        public void ItemHeight_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { ItemHeight = 100.0 });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.Equal(100.0, vm.ItemHeight);
        }

        [Fact]
        public void ItemHeight_WithNullSettings_ReturnsDefault()
        {
            var vm = CreateViewModel();

            Assert.Equal(64.0, vm.ItemHeight);
        }

        [Fact]
        public void EnableNumberShortcuts_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { EnableNumberShortcuts = false });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.False(vm.EnableNumberShortcuts);
        }

        [Fact]
        public void EnableNumberShortcuts_WithNullSettings_ReturnsDefault()
        {
            var vm = CreateViewModel();

            Assert.True(vm.EnableNumberShortcuts);
        }

        [Fact]
        public void ShortcutModifierText_WithNullSettings_ReturnsAlt()
        {
            var vm = CreateViewModel();

            Assert.Equal("Alt", vm.ShortcutModifierText);
        }

        [Fact]
        public void ShowInTaskbar_WithSettings_ReturnsFalseWhenHidden()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { HideTaskbarIcon = true });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.False(vm.ShowInTaskbar);
        }

        [Fact]
        public void ShowIcons_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { ShowIcons = false });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.False(vm.ShowIcons);
        }

        [Fact]
        public void ShowIcons_WithNullSettings_ReturnsDefault()
        {
            var vm = CreateViewModel();

            Assert.True(vm.ShowIcons);
        }

        [Fact]
        public void UpdateSearch_MoreThan10Items_SetsMinusOneShortcut()
        {
            var items = Enumerable.Range(0, 12).Select(i => new WindowItem { Title = $"W{i}" }).ToList();
            var mockSearch = new Mock<IWindowSearchService>();
            mockSearch.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                      .Returns(items);
            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(items);

            var vm = new MainViewModel(mockOrch.Object, mockSearch.Object, new Mock<INavigationService>().Object, null, new Mock<IDispatcherService>().Object);

            // Trigger UpdateSearch
            vm.SearchText = "trigger";

            // Items 0-9 should have ShortcutIndex 0-9, items 10+ should be -1
            Assert.Equal(0, vm.FilteredWindows[0].ShortcutIndex);
            Assert.Equal(9, vm.FilteredWindows[9].ShortcutIndex);
            Assert.Equal(-1, vm.FilteredWindows[10].ShortcutIndex);
            Assert.Equal(-1, vm.FilteredWindows[11].ShortcutIndex);
        }

        [Fact]
        public void MoveSelection_NullSelectedWindow_WithItems_UsesMinusOneIndex()
        {
            var item1 = new WindowItem { Title = "1" };
            var nav = new Mock<INavigationService>();
            nav.Setup(n => n.CalculateMoveIndex(-1, 1, 1)).Returns(0);

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, nav.Object);
            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1 };
            vm.SelectedWindow = null;

            vm.MoveSelection(1);

            nav.Verify(n => n.CalculateMoveIndex(-1, 1, 1), Times.Once);
            Assert.Equal(item1, vm.SelectedWindow);
        }

        [Fact]
        public void MoveSelection_OutOfBoundsIndex_DoesNotChangeSelection()
        {
            var item1 = new WindowItem { Title = "1" };
            var nav = new Mock<INavigationService>();
            // Return -1 to simulate out-of-bounds result
            nav.Setup(n => n.CalculateMoveIndex(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(-1);

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, nav.Object);
            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1 };
            vm.SelectedWindow = item1;

            vm.MoveSelection(1);

            // Selection should not change because newIndex is -1
            Assert.Equal(item1, vm.SelectedWindow);
        }

        [Fact]
        public void MoveSelectionByPage_NullSelectedWindow_WithItems_UsesZeroIndex()
        {
            var item1 = new WindowItem { Title = "1" };
            var nav = new Mock<INavigationService>();
            nav.Setup(n => n.CalculatePageMoveIndex(0, 1, 5, 1)).Returns(0);

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, nav.Object);
            vm.FilteredWindows = new System.Collections.ObjectModel.ObservableCollection<WindowItem> { item1 };
            vm.SelectedWindow = null;

            vm.MoveSelectionByPage(1, 5);

            nav.Verify(n => n.CalculatePageMoveIndex(0, 1, 5, 1), Times.Once);
        }

        [Fact]
        public async System.Threading.Tasks.Task WindowProviders_WithConcreteOrchestrationService_ReturnsSources()
        {
            var provider = new Mock<IWindowProvider>();
            provider.Setup(p => p.GetWindows()).Returns(new[] { new WindowItem { Title = "Win", Source = provider.Object } });

            // Use the legacy constructor which creates a real WindowOrchestrationService
            var mockRunner = new Mock<IProviderRunner>();
            mockRunner.Setup(r => r.RunAsync(
                It.IsAny<IList<IWindowProvider>>(),
                It.IsAny<ISet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<Action<IWindowProvider, List<WindowItem>>>()))
                .Returns((IList<IWindowProvider> providers, ISet<string> disabled, HashSet<string> excluded, Action<IWindowProvider, List<WindowItem>> callback) =>
                {
                    foreach (var p in providers)
                    {
                        var results = p.GetWindows().ToList();
                        callback(p, results);
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                });
            var runner = mockRunner.Object;
            var orch = new WindowOrchestrationService(
                new[] { provider.Object },
                new WindowReconciler(null),
                new NullUiaWorkerClient(),
                new Mock<INativeInteropWrapper>().Object,
                runner,
                runner);

            var vm = new MainViewModel(orch, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object);

            // Force a refresh so AllWindows has data
            await vm.RefreshWindows();

            var providers = vm.WindowProviders;
            Assert.NotEmpty(providers);
            Assert.Contains(provider.Object, providers);
        }

        [Fact]
        public void SyncCollection_InnerScanPassesMismatch()
        {
            // Scenario: inner scan loop iterates past a non-matching item before finding target
            // Collection: [A, D, C, B] → Source: [A, B, D, C]
            // When syncing B, inner scan passes C (false branch of collection[j]==item),
            // then finds B at j=3.
            var itemA = new WindowItem { Title = "A" };
            var itemB = new WindowItem { Title = "B" };
            var itemC = new WindowItem { Title = "C" };
            var itemD = new WindowItem { Title = "D" };

            var mockSearch = new Mock<IWindowSearchService>();
            mockSearch.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                      .Returns(new List<WindowItem> { itemA, itemB, itemD, itemC });
            var mockOrch = new Mock<IWindowOrchestrationService>();
            mockOrch.Setup(o => o.AllWindows).Returns(new List<WindowItem>());

            var vm = new MainViewModel(mockOrch.Object, mockSearch.Object, new Mock<INavigationService>().Object, null, new Mock<IDispatcherService>().Object);
            vm.FilteredWindows.Add(itemA);
            vm.FilteredWindows.Add(itemD);
            vm.FilteredWindows.Add(itemC);
            vm.FilteredWindows.Add(itemB);

            vm.SearchText = "trigger";

            Assert.Equal(4, vm.FilteredWindows.Count);
            Assert.Equal(itemA, vm.FilteredWindows[0]);
            Assert.Equal(itemB, vm.FilteredWindows[1]);
            Assert.Equal(itemD, vm.FilteredWindows[2]);
            Assert.Equal(itemC, vm.FilteredWindows[3]);
        }

        [Fact]
        public void EnableSearchHighlighting_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { EnableSearchHighlighting = false });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.False(vm.EnableSearchHighlighting);
        }

        [Fact]
        public void EnableSearchHighlighting_WithNullSettings_ReturnsDefaultTrue()
        {
            var vm = CreateViewModel();

            Assert.True(vm.EnableSearchHighlighting);
        }

        [Fact]
        public void EnableFuzzySearch_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { EnableFuzzySearch = false });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.False(vm.EnableFuzzySearch);
        }

        [Fact]
        public void EnableFuzzySearch_WithNullSettings_ReturnsDefaultTrue()
        {
            var vm = CreateViewModel();

            Assert.True(vm.EnableFuzzySearch);
        }
        [Fact]
        public void SearchHighlightColor_WithSettings_ReturnsSettingsValue()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings { SearchHighlightColor = "#FF0000" });

            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, mockSettings.Object);

            Assert.Equal("#FF0000", vm.SearchHighlightColor);
        }

        [Fact]
        public void SearchHighlightColor_WithNullSettings_ReturnsDefault()
        {
            var vm = new MainViewModel(new Mock<IWindowOrchestrationService>().Object, new Mock<IWindowSearchService>().Object, new Mock<INavigationService>().Object, null);

            Assert.Equal("#FF0078D4", vm.SearchHighlightColor);
        }

        [Fact]
        public void OpenSettingsCommand_ExecutesAndRaisesEvent()
        {
            var vm = CreateViewModel();
            var eventRaised = false;
            vm.OpenSettingsRequested += (s, e) => eventRaised = true;

            vm.OpenSettingsCommand.Execute(null);

            Assert.True(eventRaised);
        }

        [Fact]
        public void OpenSettingsCommand_CanExecute_WithoutSubscriber_DoesNotThrow()
        {
            var vm = CreateViewModel();

            var exception = Record.Exception(() => vm.OpenSettingsCommand.Execute(null));

            Assert.Null(exception);
            Assert.True(vm.OpenSettingsCommand.CanExecute(null));
        }
    }
}


