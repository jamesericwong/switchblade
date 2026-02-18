using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class IconServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IIconExtractor> _mockExtractor;
        private readonly UserSettings _settings;
        private readonly IconService _service;

        public IconServiceTests()
        {
            _settings = new UserSettings { IconCacheSize = 10 };
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            _mockExtractor = new Mock<IIconExtractor>();
            _service = new IconService(_mockSettingsService.Object, _mockExtractor.Object);
        }

        [Fact]
        public void GetIcon_NullOrEmptyPath_ReturnsNull()
        {
            Assert.Null(_service.GetIcon(null));
            Assert.Null(_service.GetIcon(""));
        }

        [Fact]
        public void GetIcon_ValidPath_CallsExtractorAndCachesResult()
        {
            var mockIcon = new BitmapImage(); // Use real instance instead of Mock<ImageSource>
            _mockExtractor.Setup(e => e.ExtractIcon("test.exe")).Returns(mockIcon);

            var icon1 = _service.GetIcon("test.exe");
            Assert.Same(mockIcon, icon1);
            _mockExtractor.Verify(e => e.ExtractIcon("test.exe"), Times.Once);

            var icon2 = _service.GetIcon("test.exe");
            Assert.Same(mockIcon, icon2);
            _mockExtractor.Verify(e => e.ExtractIcon("test.exe"), Times.Once); // Second call from cache
        }

        [Fact]
        public void GetIcon_ExtractorReturnsNull_CachesNull()
        {
            _mockExtractor.Setup(e => e.ExtractIcon("missing.exe")).Returns((ImageSource?)null);

            var icon1 = _service.GetIcon("missing.exe");
            Assert.Null(icon1);
            _mockExtractor.Verify(e => e.ExtractIcon("missing.exe"), Times.Once);

            var icon2 = _service.GetIcon("missing.exe");
            Assert.Null(icon2);
            _mockExtractor.Verify(e => e.ExtractIcon("missing.exe"), Times.Once); 
        }

        [Fact]
        public void ClearCache_Works()
        {
            _service.GetIcon("test.exe");
            _service.ClearCache();
            Assert.Equal(0, _service.CacheCount);
        }

        [Fact]
        public void GetIcon_CacheLimitReached_ClearsCache()
        {
            _settings.IconCacheSize = 2;
            
            _service.GetIcon("fake1.exe");
            _service.GetIcon("fake2.exe");
            Assert.Equal(2, _service.CacheCount);

            // This should trigger clear because it's the 3rd item
            _service.GetIcon("fake3.exe");
            
            // After clear, it adds the new one, so count should be 1
            Assert.Equal(1, _service.CacheCount);
        }

        [Fact]
        public void Constructor_NullSettings_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new IconService(null!));
        }

        [Fact]
        public void DefaultConstructor_UsesRealExtractor()
        {
            var service = new IconService(_mockSettingsService.Object);
            // We can't easily verify the internal extractor, but we verify it doesn't throw
            Assert.NotNull(service);
        }
    }
}
