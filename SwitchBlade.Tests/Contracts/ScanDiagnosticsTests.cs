using System;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    public class ScanDiagnosticsTests
    {
        [Fact]
        public void Counters_IncrementIndependently()
        {
            var diagnostics = new ScanDiagnostics();
            diagnostics.RecordProbe();
            diagnostics.RecordProbe();
            diagnostics.RecordInvalidation();

            Assert.Equal(2, diagnostics.ElementsProbed);
            Assert.Equal(1, diagnostics.InvalidatedElements);
        }

        [Theory]
        [InlineData(0, 0, false)]   // Nothing probed: never high.
        [InlineData(2, 1, false)]  // Exactly 50% does not exceed the threshold.
        [InlineData(4, 2, false)]  // Exactly 50% again.
        [InlineData(3, 2, true)]   // Two of three probed failed: exceeds 50%.
        [InlineData(10, 6, true)]  // Clear majority invalidated.
        public void IsHighInvalidationRate_Boundary(int probed, int invalidated, bool expected)
        {
            var diagnostics = new ScanDiagnostics();
            for (var i = 0; i < probed; i++)
            {
                diagnostics.RecordProbe();
            }

            for (var i = 0; i < invalidated; i++)
            {
                diagnostics.RecordInvalidation();
            }

            Assert.Equal(expected, diagnostics.IsHighInvalidationRate);
        }

        [Fact]
        public void FormatSummary_ContainsPluginAndCounts()
        {
            var diagnostics = new ScanDiagnostics();
            for (var i = 0; i < 40; i++)
            {
                diagnostics.RecordProbe();
            }

            for (var i = 0; i < 3; i++)
            {
                diagnostics.RecordInvalidation();
            }

            var summary = diagnostics.FormatSummary("ChromeTabFinder", 12);

            Assert.Contains("ChromeTabFinder", summary);
            Assert.Contains("12 items", summary);
            Assert.Contains("40 probed elements", summary);
            Assert.Contains("3 invalidated", summary);
        }

        [Fact]
        public void Report_LowInvalidationRate_LogsInfo()
        {
            var logger = new CapturingLogger();
            var diagnostics = new ScanDiagnostics();
            for (var i = 0; i < 10; i++)
            {
                diagnostics.RecordProbe();
            }

            diagnostics.RecordInvalidation(); // 10% — below threshold.

            diagnostics.Report(logger, "TestPlugin", 5);

            Assert.Single(logger.Messages);
            Assert.Empty(logger.Warnings);
        }

        [Fact]
        public void Report_HighInvalidationRate_LogsWarning()
        {
            var logger = new CapturingLogger();
            var diagnostics = new ScanDiagnostics();
            for (var i = 0; i < 4; i++)
            {
                diagnostics.RecordProbe();
            }

            for (var i = 0; i < 3; i++)
            {
                diagnostics.RecordInvalidation(); // 75% — above threshold.
            }

            diagnostics.Report(logger, "TestPlugin", 0);

            Assert.Empty(logger.Messages);
            Assert.Single(logger.Warnings);
        }

        [Fact]
        public void Report_NullLogger_DoesNotThrow() =>
            new ScanDiagnostics().Report(null, "TestPlugin", 1); // No-op.

        [Fact]
        public void Report_ZeroProbes_DoesNothing()
        {
            var logger = new CapturingLogger();

            new ScanDiagnostics().Report(logger, "TestPlugin", 3);

            Assert.Empty(logger.Messages);
            Assert.Empty(logger.Warnings);
        }

        private sealed class CapturingLogger : ILogger
        {
            public readonly System.Collections.Generic.List<string> Messages = new();
            public readonly System.Collections.Generic.List<string> Warnings = new();

            public bool IsDebugEnabled { get; set; }
            public void Log(string message) => Messages.Add(message);
            public void LogWarning(string message) => Warnings.Add(message);
            public void LogError(string context, Exception ex) { }
        }
    }
}
