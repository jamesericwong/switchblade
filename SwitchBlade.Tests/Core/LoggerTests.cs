using System;
using System.IO;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class LoggerTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly string _originalLogPath;
        private readonly bool _originalDebugState;

        public LoggerTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"switchblade_test_{Guid.NewGuid()}.log");
            _originalLogPath = Logger.LogFilePath;
            _originalDebugState = Logger.IsDebugEnabled;
            Logger.LogFilePath = _tempFile;
            Logger.IsDebugEnabled = true;
        }

        public void Dispose()
        {
            Logger.LogFilePath = _originalLogPath;
            Logger.IsDebugEnabled = _originalDebugState;
            if (File.Exists(_tempFile))
            {
                try { File.Delete(_tempFile); } catch { }
            }
        }

        [Fact]
        public void Log_WhenDebugEnabled_WritesToFile()
        {
            Logger.IsDebugEnabled = true;
            Logger.Log("Test message");

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("Test message", content);
        }

        [Fact]
        public void Log_WhenDebugDisabled_DoesNotWriteToFile()
        {
            Logger.IsDebugEnabled = false;
            if (File.Exists(_tempFile)) File.Delete(_tempFile);

            Logger.Log("Should not be logged");

            Assert.False(File.Exists(_tempFile));
        }

        [Fact]
        public void LogError_String_WritesErrorToFile()
        {
            Logger.LogError("Test error");

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR: Test error", content);
        }

        [Fact]
        public void LogError_ContextAndException_WritesDetailsToFile()
        {
            var ex = new Exception("Inner exception");
            Logger.LogError("TestContext", ex);

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR [TestContext]: Inner exception", content);
            Assert.Contains("Stack:", content);
        }

        [Fact]
        public void ILogger_Log_WritesToFile()
        {
            ILogger logger = Logger.Instance;
            logger.Log("Interface log");

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("Interface log", content);
        }

        [Fact]
        public void ILogger_LogError_WritesToFile()
        {
            ILogger logger = Logger.Instance;
            logger.LogError("InterfaceContext", new Exception("InterfaceEx"));

            var content = File.ReadAllText(_tempFile);
            Assert.Contains("ERROR [InterfaceContext]: InterfaceEx", content);
        }

        [Fact]
        public void Log_WhenPathIsInvalid_SilentlyFails()
        {
            // Set an invalid path (e.g., a path with invalid characters)
            Logger.LogFilePath = "Z:\\invalid\\path\\that\\does\\not\\exist\\log.txt";
            
            // This should not throw
            Logger.Log("Invalid path log");
            Logger.LogError("Invalid path error");
            Logger.LogError("Context", new Exception("Ex"));
        }
    }
}
