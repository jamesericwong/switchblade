using System;
using System.IO;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    /// <summary>
    /// Each test gets its own Logger instance writing to a unique temp file, so no global logger
    /// state is ever mutated and these tests are safe to run in parallel with any other class.
    /// </summary>
    public class LoggerTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly Logger _logger;

        public LoggerTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"switchblade_test_{Guid.NewGuid()}.log");
            _logger = new Logger { LogFilePath = _tempFile };
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                try { File.Delete(_tempFile); } catch { }
            }
        }

        [Fact]
        public void Log_WhenDebugEnabled_WritesToFile()
        {
            _logger.IsDebugEnabled = true;
            _logger.Log("Test message");

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("Test message", content);
        }

        [Fact]
        public void Log_WhenDebugDisabled_DoesNotWriteToFile()
        {
            // Debug gate is off by default on a fresh instance.
            _logger.Log("Should not be logged");

            Assert.False(File.Exists(_tempFile));
        }

        [Fact]
        public void LogError_ContextAndException_WritesDetailsToFile()
        {
            var ex = new Exception("Inner exception");
            _logger.LogError("TestContext", ex);

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR [TestContext]: Inner exception", content);
            Assert.Contains("Stack:", content);
        }

        [Fact]
        public void LogError_WhenDebugDisabled_WritesToFile()
        {
            // Errors bypass the debug gate (preserved behavior).
            _logger.LogError("Ctx", new Exception("Always written"));

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR [Ctx]: Always written", content);
        }

        [Fact]
        public void ILogger_Log_WritesToFile()
        {
            ILogger logger = _logger;
            logger.IsDebugEnabled = true;
            logger.Log("Interface log");

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("Interface log", content);
        }

        [Fact]
        public void ILogger_LogError_WritesToFile()
        {
            ILogger logger = _logger;
            logger.LogError("InterfaceContext", new Exception("InterfaceEx"));

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR [InterfaceContext]: InterfaceEx", content);
        }

        [Fact]
        public void Log_WhenPathIsInvalid_SilentlyFails()
        {
            // Invalid path is scoped to this instance only — no global state touched.
            _logger.LogFilePath = "Z:\\invalid\\path\\that\\does\\not\\exist\\log.txt";
            _logger.IsDebugEnabled = true;

            _logger.Log("Invalid path log");       // must not throw
            _logger.LogError("Context", new Exception("Ex"));
        }

        [Fact]
        public void IsDebugEnabled_RoundTrips()
        {
            ILogger logger = _logger;

            logger.IsDebugEnabled = true;
            Assert.True(logger.IsDebugEnabled);

            logger.IsDebugEnabled = false;
            Assert.False(logger.IsDebugEnabled);
        }

        [Fact]
        public void Instance_ReturnsSameReference()
        {
            Assert.Same(Logger.Instance, Logger.Instance);
        }
    }
}
