using Xunit;
using Moq;
using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using SwitchBlade.Handlers;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Handlers
{
    public class KeyboardInputHandlerTests
    {
        private readonly Mock<IWindowListViewModel> _mockViewModel;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<INumberShortcutService> _mockNumberShortcutService;
        private readonly Mock<Action> _mockHideWindow;
        private readonly Mock<Action<WindowItem?>> _mockActivateWindow;
        private readonly Mock<Func<double>> _mockGetListBoxHeight;
        private readonly UserSettings _settings;
        private readonly KeyboardInputHandler _handler;

        public KeyboardInputHandlerTests()
        {
            _mockViewModel = new Mock<IWindowListViewModel>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockNumberShortcutService = new Mock<INumberShortcutService>();
            _mockHideWindow = new Mock<Action>();
            _mockActivateWindow = new Mock<Action<WindowItem?>>();
            _mockGetListBoxHeight = new Mock<Func<double>>();

            _settings = new UserSettings();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            _mockGetListBoxHeight.Setup(f => f()).Returns(500); // Default height

            _handler = new KeyboardInputHandler(
                _mockViewModel.Object,
                _mockSettingsService.Object,
                _mockNumberShortcutService.Object,
                _mockHideWindow.Object,
                _mockActivateWindow.Object,
                _mockGetListBoxHeight.Object);
        }

        [Fact]
        public void HandleKeyInput_Escape_HidesWindow()
        {
            // Act
            bool result = _handler.HandleKeyInput(Key.Escape, ModifierKeys.None);

            // Assert
            _mockHideWindow.Verify(x => x(), Times.Once);
            Assert.False(result); // Escape logic returns false in current impl
        }

        [Fact]
        public void HandleKeyInput_Down_MovesSelectionNext()
        {
            // Act
            bool result = _handler.HandleKeyInput(Key.Down, ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelection(1), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_Up_MovesSelectionPrevious()
        {
            // Act
            bool result = _handler.HandleKeyInput(Key.Up, ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelection(-1), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_Enter_ActivatesSelectedWindow()
        {
            // Arrange
            var window = new WindowItem { Hwnd = IntPtr.Zero, Title = "Test", ProcessName = "Test.exe" };
            _mockViewModel.Setup(vm => vm.SelectedWindow).Returns(window);

            // Act
            bool result = _handler.HandleKeyInput(Key.Enter, ModifierKeys.None);

            // Assert
            _mockActivateWindow.Verify(x => x(window), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_CtrlHome_MovesToFirst()
        {
            // Act
            bool result = _handler.HandleKeyInput(Key.Home, ModifierKeys.Control);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionToFirst(), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_CtrlEnd_MovesToLast()
        {
            // Act
            bool result = _handler.HandleKeyInput(Key.End, ModifierKeys.Control);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionToLast(), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_PageUp_MovesByPage()
        {
            // Arrange
            _settings.ItemHeight = 50;
            _mockGetListBoxHeight.Setup(f => f()).Returns(500); // 10 items
            
            // Act
            bool result = _handler.HandleKeyInput(Key.PageUp, ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionByPage(-1, 10), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_PageDown_MovesByPage()
        {
            // Arrange
            _settings.ItemHeight = 50;
            _mockGetListBoxHeight.Setup(f => f()).Returns(500); // 10 items

            // Act
            bool result = _handler.HandleKeyInput(Key.PageDown, ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionByPage(1, 10), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_DelegatesToNumberShortcutService()
        {
            // Arrange
            _mockNumberShortcutService
                .Setup(s => s.HandleShortcut(It.IsAny<Key>(), It.IsAny<ModifierKeys>(), It.IsAny<IWindowListViewModel>(), It.IsAny<Action<WindowItem?>>()))
                .Returns(true);

            // Act
            bool result = _handler.HandleKeyInput(Key.D1, ModifierKeys.Alt);

            // Assert
            Assert.True(result);
            _mockNumberShortcutService.Verify(s => s.HandleShortcut(
                Key.D1, 
                ModifierKeys.Alt, 
                _mockViewModel.Object, 
                _mockActivateWindow.Object), Times.Once);
        }

        [Fact]
        public void CalculatePageSize_HandlesZeroHeights_DefaultsSafely()
        {
            _settings.ItemHeight = 0; // Should trigger default 64
            _mockGetListBoxHeight.Setup(f => f()).Returns(500);
            
            // Page size = 500 / 64 = 7.8 -> 7
            _handler.HandleKeyInput(Key.PageDown, ModifierKeys.None);
            
            _mockViewModel.Verify(vm => vm.MoveSelectionByPage(1, 7), Times.Once);
        }

        [Fact]
        public void CalculatePageSize_HandlesZeroListBoxHeight()
        {
            _settings.ItemHeight = 100;
            _mockGetListBoxHeight.Setup(f => f()).Returns(0); // Should trigger default 400
            
            // Page size = 400 / 100 = 4
            _handler.HandleKeyInput(Key.PageDown, ModifierKeys.None);
            
            _mockViewModel.Verify(vm => vm.MoveSelectionByPage(1, 4), Times.Once);
        }
        
        [Fact]
        public void CalculatePageSize_EnsuresMinimumPageSizeOfOne()
        {
             _settings.ItemHeight = 500;
             _mockGetListBoxHeight.Setup(f => f()).Returns(100); 
             
             // 100 / 500 = 0 -> Max(1, 0) -> 1
             _handler.HandleKeyInput(Key.PageDown, ModifierKeys.None);
             
             _mockViewModel.Verify(vm => vm.MoveSelectionByPage(1, 1), Times.Once);
        }

        [Fact]
        public void HandleKeyInput_UnhandledKeys_ReturnFalse()
        {
            Assert.False(_handler.HandleKeyInput(Key.A, ModifierKeys.None));
            Assert.False(_handler.HandleKeyInput(Key.F1, ModifierKeys.None));
        }
    }
}
