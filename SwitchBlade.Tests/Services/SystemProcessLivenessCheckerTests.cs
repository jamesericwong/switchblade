using System;
using System.Diagnostics;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class SystemProcessLivenessCheckerTests
    {
        private readonly SystemProcessLivenessChecker _checker = new();

        [Fact]
        public void IsAnyRunning_CurrentProcessName_ReturnsTrue()
        {
            // The test host process is always running — deterministic positive case.
            var currentName = Process.GetCurrentProcess().ProcessName;

            Assert.True(_checker.IsAnyRunning([currentName]));
        }

        [Fact]
        public void IsAnyRunning_NonExistentProcess_ReturnsFalse()
        {
            Assert.False(_checker.IsAnyRunning(["definitely_not_a_real_process_xyz_123"]));
        }

        [Fact]
        public void IsAnyRunning_MixedNames_FindsLiveOne()
        {
            var currentName = Process.GetCurrentProcess().ProcessName;

            Assert.True(_checker.IsAnyRunning(["definitely_not_a_real_process_xyz_123", currentName]));
        }

        [Fact]
        public void IsAnyRunning_CaseInsensitive_Matches()
        {
            // Process names are matched case-insensitively (OS process table semantics).
            var currentName = Process.GetCurrentProcess().ProcessName;

            Assert.True(_checker.IsAnyRunning([currentName.ToUpperInvariant()]));
        }

        [Fact]
        public void IsAnyRunning_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _checker.IsAnyRunning(null!));
        }

        [Fact]
        public void IsAnyRunning_EmptyList_ReturnsFalse()
        {
            Assert.False(_checker.IsAnyRunning([]));
        }

        [Fact]
        public void IsAnyRunning_AllWhitespaceNames_ReturnsFalse()
        {
            Assert.False(_checker.IsAnyRunning(["", "   "]));
        }
    }
}
