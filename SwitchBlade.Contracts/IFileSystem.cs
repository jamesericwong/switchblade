using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchBlade.Contracts
{
    public interface IFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
        Stream OpenRead(string path);
    }
}
