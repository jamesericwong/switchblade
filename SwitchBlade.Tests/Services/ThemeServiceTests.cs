using Xunit;
using Moq;
using System.Windows;
using System.Collections.Generic;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class ThemeServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IApplicationResourceHandler> _mockResourceHandler;
        private readonly UserSettings _settings;
        private readonly ThemeService _service;

        public ThemeServiceTests()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockResourceHandler = new Mock<IApplicationResourceHandler>();
            _settings = new UserSettings();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);

            _service = new ThemeService(_mockSettingsService.Object, _mockResourceHandler.Object);
        }

        [Fact]
        public void LoadCurrentTheme_AppliesThemeFromSettings()
        {
            // Arrange
            _settings.CurrentTheme = "Cyberpunk";

            // Act
            _service.LoadCurrentTheme();

            // Assert
            // Verify AddMergedDictionary was called.
            _mockResourceHandler.Verify(x => x.AddMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Once);
        }

        [Fact]
        public void ApplyTheme_UpdatesSettingsAndResources()
        {
            // Arrange
            string newTheme = "Deep Ocean";

            // Act
            _service.ApplyTheme(newTheme);

            // Assert
            Assert.Equal(newTheme, _settings.CurrentTheme);
            _mockResourceHandler.Verify(x => x.AddMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Once);
            _mockSettingsService.Verify(x => x.SaveSettings(), Times.Once);
        }

        [Fact]
        public void ApplyTheme_RemovesOldTheme()
        {
            // Arrange
            // Apply first theme
            _service.ApplyTheme("Dark");

            // Act
            _service.ApplyTheme("Light");

            // Assert
            // Should have removed the first one
            _mockResourceHandler.Verify(x => x.RemoveMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Once);
            // And added the second one
            _mockResourceHandler.Verify(x => x.AddMergedDictionary(It.IsAny<ResourceDictionary>()), Times.Exactly(2));
        }

        [Fact]
        public void InitializeThemes_PopulatesAvailableThemes()
        {
            // Assert
            Assert.NotEmpty(_service.AvailableThemes);
            Assert.Contains(_service.AvailableThemes, t => t.Name == "Dark");
            Assert.Contains(_service.AvailableThemes, t => t.Name == "Light");
        }
    }
}
