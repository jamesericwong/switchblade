using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using SwitchBlade.Contracts;
using SwitchBlade.Tests.Services;

namespace SwitchBlade.Tests.Contracts
{
    public class PluginDiscoveryTests : IDisposable
    {
        private readonly string _tempDir;

        public PluginDiscoveryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "switchblade_plugin_discovery_tests", Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch (Exception)
            {
                // Best-effort cleanup of test fixtures.
            }
        }

        [Theory]
        [InlineData("SwitchBlade.Plugins.Chrome.dll", true)]
        [InlineData("switchblade.plugins.teams.dll", true)]
        [InlineData("SwitchBlade.Plugins.Sub.Foo.dll", true)]
        [InlineData("Foo.dll", false)]
        [InlineData("SwitchBlade.Contracts.dll", false)]
        [InlineData("", false)]
        public void IsPluginAssembly_ConventionCheck_ReturnsExpected(string fileName, bool expected)
        {
            Assert.Equal(expected, PluginDiscovery.IsPluginAssembly(fileName));
        }

        [Fact]
        public void EnumeratePluginDlls_MissingDirectory_ReturnsEmpty()
        {
            var result = PluginDiscovery.EnumeratePluginDlls(Path.Combine(_tempDir, "does-not-exist"));

            Assert.Empty(result);
        }

        [Fact]
        public void EnumeratePluginDlls_NullOrEmptyPath_ReturnsEmpty()
        {
            Assert.Empty(PluginDiscovery.EnumeratePluginDlls(null!));
            Assert.Empty(PluginDiscovery.EnumeratePluginDlls(string.Empty));
        }

        [Fact]
        public void EnumeratePluginDlls_NestedAndNonConforming_FindsOnlyRecursivePlugins()
        {
            // Arrange: conforming plugins at root and in a subfolder, plus files that must be skipped
            string sub = Path.Combine(_tempDir, "Nested");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(_tempDir, "SwitchBlade.Plugins.Root.dll"), "x");
            File.WriteAllText(Path.Combine(sub, "SwitchBlade.Plugins.Nested.dll"), "x");
            File.WriteAllText(Path.Combine(_tempDir, "RandomDependency.dll"), "x");
            File.WriteAllText(Path.Combine(sub, "notes.txt"), "x");

            // Act
            var result = PluginDiscovery.EnumeratePluginDlls(_tempDir);

            // Assert: recursive discovery + naming convention filter
            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => Path.GetFileName(p) == "SwitchBlade.Plugins.Root.dll");
            Assert.Contains(result, p => Path.GetFileName(p) == "SwitchBlade.Plugins.Nested.dll");
        }

        [Fact]
        public void DiscoverProviders_TestAssembly_DiscoversFakeGlobalProvider()
        {
            string testDll = typeof(PluginDiscoveryTests).Assembly.Location;

            var providers = PluginDiscovery.DiscoverProviders(new[] { testDll });

            Assert.Contains(providers, p => p is FakeGlobalProvider);
        }

        [Fact]
        public void DiscoverProviders_UnloadableDll_LogsErrorAndContinues()
        {
            // Arrange: a file that exists but is not a valid .NET assembly, followed by the test assembly
            Directory.CreateDirectory(_tempDir);
            string badDll = Path.Combine(_tempDir, "SwitchBlade.Plugins.Broken.dll");
            File.WriteAllText(badDll, "not a managed assembly");

            var logger = new CapturingLogger();

            // Act
            var providers = PluginDiscovery.DiscoverProviders(
                new[] { badDll, typeof(PluginDiscoveryTests).Assembly.Location },
                logger);

            // Assert: the bad DLL is isolated and the good one still loads
            Assert.Contains(providers, p => p is FakeGlobalProvider);
            Assert.Contains(logger.Errors, e => e.Contains("Failed to load plugin assembly"));
        }

        [Fact]
        public void DiscoverProviders_UninstantiableType_ReportsFailureAndContinues()
        {
            // Arrange: the test assembly contains UninstantiableProvider (no parameterless constructor)
            var logger = new CapturingLogger();
            var failures = new List<string>();

            // Act
            var providers = PluginDiscovery.DiscoverProviders(
                new[] { typeof(PluginDiscoveryTests).Assembly.Location },
                logger,
                failures.Add);

            // Assert: the bad type is isolated and reported; good types still load
            Assert.Contains(providers, p => p is FakeGlobalProvider);
            Assert.Contains(failures, f => f.Contains(nameof(UninstantiableProvider)));
            Assert.Contains(logger.Errors, e => e.Contains(nameof(UninstantiableProvider)));
        }

        /// <summary>
        /// IWindowProvider implementation without a parameterless constructor —
        /// used to exercise the per-type instantiation failure path.
        /// </summary>
        private sealed class UninstantiableProvider : IWindowProvider
        {
            public UninstantiableProvider(int token)
            {
                _ = token;
            }

            public string PluginName => "Uninstantiable";
            public bool HasSettings => false;
            public void Initialize(IPluginContext context) { }
            public void ReloadSettings() { }
            public IEnumerable<WindowItem> GetWindows() => new List<WindowItem>();
            public void ActivateWindow(WindowItem item) { }
        }

        private sealed class CapturingLogger : ILogger
        {
            public readonly List<string> Messages = new();
            public readonly List<string> Warnings = new();
            public readonly List<string> Errors = new();

            public bool IsDebugEnabled { get; set; }
            public void Log(string message) => Messages.Add(message);
            public void LogWarning(string message) => Warnings.Add(message);
            public void LogError(string context, Exception ex) => Errors.Add(context);
        }
    }
}
