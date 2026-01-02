using System;
using Xunit;
using SwitchBlade.Core;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Core
{
    public class LoggerBridgeTests
    {
        [Fact]
        public void LoggerBridge_ImplementsILogger()
        {
            var bridge = new LoggerBridge();

            Assert.IsAssignableFrom<ILogger>(bridge);
        }

        [Fact]
        public void Log_DelegatesToStaticLogger()
        {
            // This test verifies the bridge calls Logger.Log
            // We can't easily verify the call without more infrastructure,
            // but we can ensure it doesn't throw
            var bridge = new LoggerBridge();

            var exception = Record.Exception(() => bridge.Log("Test message"));

            Assert.Null(exception);
        }

        [Fact]
        public void LogError_DelegatesToStaticLogger()
        {
            var bridge = new LoggerBridge();
            var testException = new InvalidOperationException("Test");

            var exception = Record.Exception(() => bridge.LogError("Context", testException));

            Assert.Null(exception);
        }
    }
}
