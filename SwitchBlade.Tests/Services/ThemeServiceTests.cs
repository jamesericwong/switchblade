using System.Windows;
using Moq;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Xunit;
using System.Linq;

namespace SwitchBlade.Tests.Services
{
    public class ThemeServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly Mock<IApplicationResourceHandler> _mockHandler;
        private readonly UserSettings _settings;
        private readonly ThemeService _service;

        public ThemeServiceTests()
        {
            _settings = new UserSettings { CurrentTheme = "Dark" };
            _mockSettings = new Mock<ISettingsService>();
            _mockSettings.Setup(s => s.Settings).Returns(_settings);
            
            _mockHandler = new Mock<IApplicationResourceHandler>();
            _service = new ThemeService(_mockSettings.Object, _mockHandler.Object);
        }

        [Fact]
        public void LoadCurrentTheme_AppliesSetting()
        {
            _service.LoadCurrentTheme();
            _mockHandler.Verify(h => h.AddMergedDictionary(It.IsAny<ResourceDictionary>()), Times.AtLeastOnce());
        }

        [Fact]
        public void ApplyTheme_RepeatedCalls_RemovesPrevious()
        {
            _service.ApplyTheme("Light");
            _service.ApplyTheme("Dark");
            
            _mockHandler.Verify(h => h.RemoveMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Once());
            _mockHandler.Verify(h => h.AddMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Exactly(2)); // Light + Dark
        }

        [Fact]
        public void ApplyTheme_UnknownTheme_FallsBack()
        {
            _service.ApplyTheme("NonExistent");
            Assert.Equal("Dark", _service.AvailableThemes.First().Name);
        }

        [Fact]
        public void ApplyTheme_WhenThemeMatchesSetting_DoesNotSave()
        {
            _settings.CurrentTheme = "Light";
            _service.ApplyTheme("Light");
            
            _mockSettings.Verify(s => s.SaveSettings(), Times.Never());
        }

        [Fact]
        public void Constructor_DefaultHandler_Works()
        {
            var service = new ThemeService(_mockSettings.Object, null);
            Assert.NotNull(service);
        }
    }
}
