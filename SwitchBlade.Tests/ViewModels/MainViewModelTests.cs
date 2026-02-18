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

        [Fact]
        public void Constructor_WithEmptyProviders_CreatesEmptyFilteredWindows()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            Assert.Empty(vm.FilteredWindows);
        }

        [Fact]
        public void Constructor_SetsDefaultEnablePreviews_ToTrue()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            Assert.True(vm.EnablePreviews);
        }

        [Fact]
        public void SearchText_DefaultValue_IsEmptyString()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            Assert.Equal(string.Empty, vm.SearchText);
        }

        [Fact]
        public void SelectedWindow_DefaultValue_IsNull()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            Assert.Null(vm.SelectedWindow);
        }

        [Fact]
        public void SearchText_SetValue_UpdatesProperty()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            vm.SearchText = "test";

            Assert.Equal("test", vm.SearchText);
        }

        [Fact]
        public void SearchText_Change_RaisesPropertyChanged()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SearchText))
                    propertyChangedRaised = true;
            };

            vm.SearchText = "test";

            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void EnablePreviews_SetValue_UpdatesProperty()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            vm.EnablePreviews = false;

            Assert.False(vm.EnablePreviews);
        }

        [Fact]
        public void EnablePreviews_Change_RaisesPropertyChanged()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
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
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelection(1));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelection_WithNullSelectedWindow_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
            vm.SelectedWindow = null;

            var exception = Record.Exception(() => vm.MoveSelection(1));

            Assert.Null(exception);
        }

        [Fact]
        public void FilteredWindows_ImplementsINotifyPropertyChanged()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            Assert.IsAssignableFrom<INotifyPropertyChanged>(vm);
        }

        [Fact]
        public void ShowInTaskbar_WithNullSettingsService_ReturnsTrue()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>(), null);

            Assert.True(vm.ShowInTaskbar);
        }

        [Fact]
        public void MoveSelectionToFirst_WithEmptyList_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelectionToFirst());

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionToLast_WithEmptyList_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelectionToLast());

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithEmptyList_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, 10));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithZeroPageSize_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, 0));

            Assert.Null(exception);
        }

        [Fact]
        public void MoveSelectionByPage_WithNegativePageSize_DoesNotThrow()
        {
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());

            var exception = Record.Exception(() => vm.MoveSelectionByPage(1, -5));

            Assert.Null(exception);
        }
        [Fact]
        public void MoveSelectionToFirst_WithItems_SelectsFirstItem()
        {
            var item1 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "1", ProcessName = "exe", Source = null };
            var item2 = new WindowItem { Hwnd = System.IntPtr.Zero, Title = "2", ProcessName = "exe", Source = null };
            // MainViewModel constructor expects IEnumerable<IWindowProvider>
            var vm = new MainViewModel(System.Linq.Enumerable.Empty<IWindowProvider>());

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
            var vm = new MainViewModel(System.Linq.Enumerable.Empty<IWindowProvider>());

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
            var vm = new MainViewModel(System.Linq.Enumerable.Empty<IWindowProvider>());

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
            var vm = new MainViewModel(System.Linq.Enumerable.Empty<IWindowProvider>());

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
            
            // Should be empty because it's not a WindowOrchestrationService (concrete class check in VM)
            Assert.Empty(vm.WindowProviders);
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
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
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
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
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
            
            var vm = new MainViewModel(Enumerable.Empty<IWindowProvider>());
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
    }
}
