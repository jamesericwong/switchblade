using Xunit;
using Moq;
using System;
using System.Windows.Input;
using System.Collections.ObjectModel;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Services
{
    public class NumberShortcutServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IWindowListViewModel> _mockViewModel;
        private readonly Mock<Action<WindowItem?>> _mockActivateWindow;
        private readonly UserSettings _settings;
        private readonly NumberShortcutService _service;

        public NumberShortcutServiceTests()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILogger>();
            _mockViewModel = new Mock<IWindowListViewModel>();
            _mockActivateWindow = new Mock<Action<WindowItem?>>();

            _settings = new UserSettings();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);

            _service = new NumberShortcutService(_mockSettingsService.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_Throws_WhenDependenciesNull()
        {
            Assert.Throws<ArgumentNullException>(() => new NumberShortcutService(null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new NumberShortcutService(_mockSettingsService.Object, null!));
        }

        [Fact]
        public void HandleShortcut_ReturnsFalse_WhenDisabled()
        {
            _settings.EnableNumberShortcuts = false;
            
            bool handled = _service.HandleShortcut(Key.D1, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object);
            
            Assert.False(handled);
            _mockActivateWindow.Verify(a => a(It.IsAny<WindowItem>()), Times.Never);
        }

        [Fact]
        public void HandleShortcut_ReturnsFalse_WhenModifierMissing()
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.Alt;

            bool handled = _service.HandleShortcut(Key.D1, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object);

            Assert.False(handled);
            _mockActivateWindow.Verify(a => a(It.IsAny<WindowItem>()), Times.Never);
        }

        [Fact]
        public void HandleShortcut_ReturnsFalse_WhenKeyNotANumber()
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.None;

            bool handled = _service.HandleShortcut(Key.A, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object);

            Assert.False(handled);
            _mockActivateWindow.Verify(a => a(It.IsAny<WindowItem>()), Times.Never);
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
        [InlineData(Key.NumPad0, 9)]
        public void HandleShortcut_ActivatesCorrectIndex(Key key, int expectedIndex)
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.None;

            var items = new ObservableCollection<WindowItem>();
            for (int i = 0; i < 10; i++) items.Add(new WindowItem { Title = $"Item {i}" });
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(items);

            bool handled = _service.HandleShortcut(key, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object);

            Assert.True(handled);
            _mockActivateWindow.Verify(a => a(items[expectedIndex]), Times.Once);
        }

        [Fact]
        public void HandleShortcut_ReturnsTrueButNoActivation_WhenIndexOutOfRange()
        {
            _settings.EnableNumberShortcuts = true;
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.None;

            // Only 1 item
            var items = new ObservableCollection<WindowItem> { new WindowItem() };
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(items);

            // Key D2 -> Index 1 (out of range)
            bool handled = _service.HandleShortcut(Key.D2, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object);

            Assert.True(handled);
            _mockActivateWindow.Verify(a => a(It.IsAny<WindowItem>()), Times.Never);
        }

        [Fact]
        public void HandleShortcut_WorksWithAllSupportedModifiers()
        {
            _settings.EnableNumberShortcuts = true;
            var items = new ObservableCollection<WindowItem> { new WindowItem() };
            _mockViewModel.Setup(vm => vm.FilteredWindows).Returns(items);

            // Alt
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.Alt;
            Assert.True(_service.HandleShortcut(Key.D1, ModifierKeys.Alt, _mockViewModel.Object, _mockActivateWindow.Object));
            
            // Ctrl
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.Ctrl;
            Assert.True(_service.HandleShortcut(Key.D1, ModifierKeys.Control, _mockViewModel.Object, _mockActivateWindow.Object));

            // Shift
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.Shift;
            Assert.True(_service.HandleShortcut(Key.D1, ModifierKeys.Shift, _mockViewModel.Object, _mockActivateWindow.Object));

            // None
            _settings.NumberShortcutModifier = (uint)ModifierKeyFlags.None;
            Assert.True(_service.HandleShortcut(Key.D1, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object));

            // Invalid modifier value
            _settings.NumberShortcutModifier = 99;
            Assert.False(_service.HandleShortcut(Key.D1, ModifierKeys.None, _mockViewModel.Object, _mockActivateWindow.Object));
        }
    }
}
