using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    public class FileSystemWrapper : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) 
            => File.ReadAllTextAsync(path, cancellationToken);

        public Stream OpenRead(string path) => File.OpenRead(path);
    }
}
