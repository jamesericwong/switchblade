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
        private readonly Mock<Action> _mockHideWindow;
        private readonly Mock<Action<WindowItem?>> _mockActivateWindow;
        private readonly Mock<Func<double>> _mockGetListBoxHeight;
        private readonly UserSettings _settings;
        private readonly KeyboardInputHandler _handler;

        public KeyboardInputHandlerTests()
        {
            _mockViewModel = new Mock<IWindowListViewModel>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockHideWindow = new Mock<Action>();
            _mockActivateWindow = new Mock<Action<WindowItem?>>();
            _mockGetListBoxHeight = new Mock<Func<double>>();

            _settings = new UserSettings();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            _mockGetListBoxHeight.Setup(f => f()).Returns(500); // Default height

            _handler = new KeyboardInputHandler(
                _mockViewModel.Object,
                _mockSettingsService.Object,
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
            Assert.False(result); // Escape logic returns false in current impl (bubble up?)
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
            // Page size should be 10

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
        public void HandleKeyInput_NumberShortcut_ActivatedIfEnabled()
        {
            // Arrange
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = 1; // Alt

            var windows = new ObservableCollection<WindowItem>
            {
                new WindowItem { Hwnd = IntPtr.Zero, Title = "One" },
                new WindowItem { Hwnd = IntPtr.Zero, Title = "Two" },
                new WindowItem { Hwnd = IntPtr.Zero, Title = "Three" }
            };
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(windows);

            // Act
            // Alt+2 -> Index 1 ("Two")
            bool result = _handler.HandleKeyInput(Key.D2, ModifierKeys.Alt);

            // Assert
            _mockActivateWindow.Verify(x => x(windows[1]), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_NumberShortcut_IgnoredIfDisabled()
        {
            // Arrange
            _settings.EnableNumberShortcuts = false;

            // Act
            bool result = _handler.HandleKeyInput(Key.D2, ModifierKeys.Alt);

            // Assert
            _mockActivateWindow.Verify(x => x(It.IsAny<WindowItem>()), Times.Never);
            Assert.False(result);
        }
    }
}
