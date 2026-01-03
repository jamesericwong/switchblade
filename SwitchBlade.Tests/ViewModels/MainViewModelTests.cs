using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Moq;
using Xunit;
using SwitchBlade.Contracts;
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
    }
}
