using System.IO;
using System.Threading.Tasks;
using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class FileSystemWrapperTests
    {
        [Fact]
        public void FileExists_ShouldCallFileExists()
        {
            var fs = new FileSystemWrapper();
            var path = "nonexistent.txt";
            Assert.False(fs.FileExists(path));
        }

        [Fact]
        public async Task ReadAllTextAsync_ShouldReadFileContent()
        {
            var fs = new FileSystemWrapper();
            var path = Path.GetTempFileName();
            var content = "test content";
            File.WriteAllText(path, content);
            try
            {
                var result = await fs.ReadAllTextAsync(path);
                Assert.Equal(content, result);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ReadAllText_ShouldReadFileContent()
        {
            var fs = new FileSystemWrapper();
            var path = Path.GetTempFileName();
            var content = "test content";
            File.WriteAllText(path, content);
            try
            {
                var result = fs.ReadAllText(path);
                Assert.Equal(content, result);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void OpenRead_ShouldReturnStream()
        {
            var fs = new FileSystemWrapper();
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "test");
            try
            {
                using var stream = fs.OpenRead(path);
                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
