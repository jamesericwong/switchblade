using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class NullUiaWorkerClientTests
    {
        [Fact]
        public async Task ScanAsync_ReturnsEmptyList()
        {
            var client = new NullUiaWorkerClient();
            var result = await client.ScanAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task ScanStreamingAsync_ReturnsEmptyEnumerable()
        {
            var client = new NullUiaWorkerClient();
            var results = new List<SwitchBlade.Contracts.UiaPluginResult>();
            await foreach (var result in client.ScanStreamingAsync())
            {
                results.Add(result);
            }
            Assert.Empty(results);
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var client = new NullUiaWorkerClient();
            client.Dispose();
        }
    }
}
