using System;
using System.IO;
using System.Windows.Media;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class IconServiceTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly UserSettings _settings;
        private readonly IconService _service;

        public IconServiceTests()
        {
            _settings = new UserSettings { IconCacheSize = 10 };
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            _service = new IconService(_mockSettingsService.Object);
        }

        [Fact]
        public void GetIcon_NullOrEmptyPath_ReturnsNull()
        {
            Assert.Null(_service.GetIcon(null));
            Assert.Null(_service.GetIcon(""));
        }

        [Fact]
        public void GetIcon_ValidExe_ReturnsIconAndCachesIt()
        {
            string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (!File.Exists(explorerPath)) return; // Skip if not on Windows or path different

            var icon1 = _service.GetIcon(explorerPath);
            Assert.NotNull(icon1);
            Assert.Equal(1, _service.CacheCount);

            var icon2 = _service.GetIcon(explorerPath);
            Assert.Same(icon1, icon2); // Should be same instance from cache
        }

        [Fact]
        public void GetIcon_InvalidPath_ReturnsNull()
        {
            var result = _service.GetIcon(@"C:\this\file\does\not\exist.exe");
            Assert.Null(result);
        }

        [Fact]
        public void ClearCache_Works()
        {
            string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (File.Exists(explorerPath)) _service.GetIcon(explorerPath);

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
    }
}
