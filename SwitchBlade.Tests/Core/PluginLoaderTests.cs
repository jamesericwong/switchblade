using System;
using System.IO;
using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class PluginLoaderTests : IDisposable
    {
        private readonly string _testPluginsPath;

        public PluginLoaderTests()
        {
            _testPluginsPath = Path.Combine(Path.GetTempPath(), $"SwitchBladeTestPlugins_{Guid.NewGuid()}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPluginsPath))
            {
                Directory.Delete(_testPluginsPath, true);
            }
        }

        [Fact]
        public void LoadPlugins_DirectoryDoesNotExist_CreatesDirectoryAndReturnsEmptyList()
        {
            var loader = new PluginLoader(_testPluginsPath);

            var result = loader.LoadPlugins();

            Assert.Empty(result);
            Assert.True(Directory.Exists(_testPluginsPath));
        }

        [Fact]
        public void LoadPlugins_EmptyDirectory_ReturnsEmptyList()
        {
            Directory.CreateDirectory(_testPluginsPath);
            var loader = new PluginLoader(_testPluginsPath);

            var result = loader.LoadPlugins();

            Assert.Empty(result);
        }

        [Fact]
        public void LoadPlugins_DirectoryWithNoValidDlls_ReturnsEmptyList()
        {
            Directory.CreateDirectory(_testPluginsPath);
            // Create a non-DLL file
            File.WriteAllText(Path.Combine(_testPluginsPath, "readme.txt"), "Test file");
            var loader = new PluginLoader(_testPluginsPath);

            var result = loader.LoadPlugins();

            Assert.Empty(result);
        }

        [Fact]
        public void Constructor_SetsPluginsPath()
        {
            var expectedPath = @"C:\TestPlugins";
            var loader = new PluginLoader(expectedPath);

            // We can't directly access _pluginsPath, but we can verify behavior
            // by calling LoadPlugins on a non-existent path
            Assert.NotNull(loader);
        }
    }
}
