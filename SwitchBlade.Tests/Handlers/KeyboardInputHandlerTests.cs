using Xunit;
using Moq;
using System;
using Windows.System;
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
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<Action<WindowItem?>> _mockActivateWindow;
        private readonly UserSettings _settings;
        private readonly KeyboardInputHandler _handler;

        public KeyboardInputHandlerTests()
        {
            _mockViewModel = new Mock<IWindowListViewModel>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILogger>();
            _mockActivateWindow = new Mock<Action<WindowItem?>>();

            _settings = new UserSettings();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);

            _handler = new KeyboardInputHandler(
                _mockViewModel.Object,
                _mockLogger.Object,
                _mockSettingsService.Object,
                _mockActivateWindow.Object);
        }

        [Fact]
        public void HandleKeyInput_Escape_ReturnsFalse()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.Escape, SwitchBlade.Handlers.ModifierKeys.None);

            // Assert
            Assert.False(result); // Escape handled by caller now
        }

        [Fact]
        public void HandleKeyInput_Down_MovesSelectionNext()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.Down, SwitchBlade.Handlers.ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelection(1), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_Up_MovesSelectionPrevious()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.Up, SwitchBlade.Handlers.ModifierKeys.None);

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
            bool result = _handler.HandleKeyInput(VirtualKey.Enter, SwitchBlade.Handlers.ModifierKeys.None);

            // Assert
            _mockActivateWindow.Verify(x => x(window), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_CtrlHome_MovesToFirst()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.Home, SwitchBlade.Handlers.ModifierKeys.Control);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionToFirst(), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_CtrlEnd_MovesToLast()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.End, SwitchBlade.Handlers.ModifierKeys.Control);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionToLast(), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_PageUp_MovesByPage()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.PageUp, SwitchBlade.Handlers.ModifierKeys.None);

            // Assert
            _mockViewModel.Verify(x => x.MoveSelectionByPage(-1, 10), Times.Once);
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_PageDown_MovesByPage()
        {
            // Act
            bool result = _handler.HandleKeyInput(VirtualKey.PageDown, SwitchBlade.Handlers.ModifierKeys.None);

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
            bool result = _handler.HandleKeyInput(VirtualKey.Number2, SwitchBlade.Handlers.ModifierKeys.Alt);

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
            bool result = _handler.HandleKeyInput(VirtualKey.Number2, SwitchBlade.Handlers.ModifierKeys.Alt);

            // Assert
            _mockActivateWindow.Verify(x => x(It.IsAny<WindowItem>()), Times.Never);
            Assert.False(result);
        }
    }
}
