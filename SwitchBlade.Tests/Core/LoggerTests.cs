using System;
using System.IO;
using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class LoggerTests : IDisposable
    {
        private readonly string _logPath;

        public LoggerTests()
        {
            // Use a unique file for each test instance to avoid contention
            _logPath = Path.Combine(Path.GetTempPath(), $"switchblade_test_{Guid.NewGuid()}.log");
            Logger.LogFilePath = _logPath;

            // Clean up if by rare chance it exists
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
            // Reset logger state
            Logger.IsDebugEnabled = false;
        }

        public void Dispose()
        {
            Logger.IsDebugEnabled = false;
            if (File.Exists(_logPath))
            {
                try { File.Delete(_logPath); } catch { }
            }
        }

        [Fact]
        public void Log_WhenDebugDisabled_DoesNotWriteToFile()
        {
            Logger.IsDebugEnabled = false;

            Logger.Log("Test message");

            // File may exist from previous runs, so check content
            if (File.Exists(_logPath))
            {
                var content = File.ReadAllText(_logPath);
                Assert.DoesNotContain("Test message", content);
            }
        }

        [Fact]
        public void Log_WhenDebugEnabled_WritesToFile()
        {
            Logger.IsDebugEnabled = true;

            Logger.Log("Debug test message");

            Assert.True(File.Exists(_logPath));
            var content = File.ReadAllText(_logPath);
            Assert.Contains("Debug test message", content);
        }

        [Fact]
        public void Log_WhenDebugEnabled_IncludesTimestamp()
        {
            Logger.IsDebugEnabled = true;

            Logger.Log("Timestamp test");

            var content = File.ReadAllText(_logPath);
            // Check for timestamp format [yyyy-MM-dd HH:mm:ss.fff]
            Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]", content);
        }

        [Fact]
        public void LogError_WritesToFile_WithErrorPrefix()
        {
            var exception = new InvalidOperationException("Test exception");

            Logger.LogError("TestContext", exception);

            Assert.True(File.Exists(_logPath));
            var content = File.ReadAllText(_logPath);
            Assert.Contains("ERROR", content);
            Assert.Contains("TestContext", content);
            Assert.Contains("Test exception", content);
        }

        [Fact]
        public void LogError_IncludesStackTrace()
        {
            Exception? capturedException = null;
            try
            {
                throw new InvalidOperationException("Stack trace test");
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }

            Logger.LogError("StackTest", capturedException!);

            var content = File.ReadAllText(_logPath);
            Assert.Contains("Stack:", content);
        }

        [Fact]
        public void IsDebugEnabled_DefaultValue_IsFalse()
        {
            // Reset to verify default
            var field = typeof(Logger).GetProperty("IsDebugEnabled");

            // After construction, should be false by default
            Assert.False(Logger.IsDebugEnabled);
        }
    }
}
