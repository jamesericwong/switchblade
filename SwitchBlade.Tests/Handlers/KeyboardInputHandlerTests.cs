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

        [Theory]
        [InlineData(Key.D1, 0)]
        [InlineData(Key.D2, 1)]
        [InlineData(Key.D3, 2)]
        [InlineData(Key.D4, 3)]
        [InlineData(Key.D5, 4)]
        [InlineData(Key.D6, 5)]
        [InlineData(Key.D7, 6)]
        [InlineData(Key.D8, 7)]
        [InlineData(Key.D9, 8)]
        [InlineData(Key.D0, 9)]
        [InlineData(Key.NumPad1, 0)]
        [InlineData(Key.NumPad2, 1)]
        [InlineData(Key.NumPad3, 2)]
        [InlineData(Key.NumPad4, 3)]
        [InlineData(Key.NumPad5, 4)]
        [InlineData(Key.NumPad6, 5)]
        [InlineData(Key.NumPad7, 6)]
        [InlineData(Key.NumPad8, 7)]
        [InlineData(Key.NumPad9, 8)]
        [InlineData(Key.NumPad0, 9)]
        public void HandleKeyInput_AllNumberKeys_MapToCorrectIndex(Key key, int expectedIndex)
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = 0; // None
            
            // Setup enough windows
            var windows = new ObservableCollection<WindowItem>();
            for(int i=0; i<=9; i++) windows.Add(new WindowItem { Title = $"Win{i}" });
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(windows);

            bool result = _handler.HandleKeyInput(key, ModifierKeys.None);

            if (result)
            {
               _mockActivateWindow.Verify(x => x(windows[expectedIndex]), Times.Once);
            }
            Assert.True(result);
        }

        [Fact]
        public void HandleKeyInput_NumberShortcut_IndexOutOfRange_StillReturnsTrue()
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = 0; // None
            
            // Only 1 window
            var windows = new ObservableCollection<WindowItem> { new WindowItem() };
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(windows);

            // Try to activate index 1 (Key D2)
            bool result = _handler.HandleKeyInput(Key.D2, ModifierKeys.None);

            _mockActivateWindow.Verify(x => x(It.IsAny<WindowItem>()), Times.Never);
            Assert.True(result); 
        }

        [Fact]
        public void HandleKeyInput_NumberShortcut_Modifiers_CoverAllCases()
        {
             _settings.EnableNumberShortcuts = true;
             
             var windows = new ObservableCollection<WindowItem> { new WindowItem(), new WindowItem() };
             _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(windows);

             // Test Alt
             _settings.NumberShortcutModifier = 1; // Alt
             Assert.True(_handler.HandleKeyInput(Key.D1, ModifierKeys.Alt));
             Assert.False(_handler.HandleKeyInput(Key.D1, ModifierKeys.Control));
             
             // Test Ctrl
             _settings.NumberShortcutModifier = 2; // Ctrl
             Assert.True(_handler.HandleKeyInput(Key.D1, ModifierKeys.Control));
             
             // Test Shift
             _settings.NumberShortcutModifier = 4; // Shift
             Assert.True(_handler.HandleKeyInput(Key.D1, ModifierKeys.Shift));
             
             // Test None
             _settings.NumberShortcutModifier = 0; // None
             Assert.True(_handler.HandleKeyInput(Key.D1, ModifierKeys.None));
             
             // Test Unknown Identifier
             _settings.NumberShortcutModifier = 99;
             Assert.False(_handler.HandleKeyInput(Key.D1, ModifierKeys.None));
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
