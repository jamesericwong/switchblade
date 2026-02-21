using System.Diagnostics;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class RestartLogicTests
    {
        [Fact]
        public void BuildRestartStartInfo_NonElevated_ReturnsCorrectInfo()
        {
            var path = @"C:\Program Files\App\App.exe";
            var workingDir = @"C:\Program Files\App";
            int pid = 1234;
            bool elevated = false;

            var info = RestartLogic.BuildRestartStartInfo(path, workingDir, pid, elevated);

            Assert.Equal("powershell.exe", info.FileName);
            Assert.Contains($"Wait-Process -Id {pid}", info.Arguments);
            Assert.Contains($"Start-Process '{path}'", info.Arguments);
            Assert.Contains($"-WorkingDirectory '{workingDir}'", info.Arguments);
        }

        [Fact]
        public void BuildRestartStartInfo_Elevated_ReturnsDeElevationCommand()
        {
            var path = @"C:\App\App.exe";
            var workingDir = @"C:\App";
            int pid = 5678;
            bool elevated = true;

            var info = RestartLogic.BuildRestartStartInfo(path, workingDir, pid, elevated);

            Assert.Equal("powershell.exe", info.FileName);
            Assert.Contains("Start-Process explorer.exe", info.Arguments);
            // Verify argument escaping for explorer
            Assert.Contains("-ArgumentList '\"C:\\App\\App.exe\"'", info.Arguments);
        }

        [Fact]
        public void BuildRestartStartInfo_EscapesQuotesInPath()
        {
            var path = @"C:\My ""Cool"" App\App.exe";
            var workingDir = @"C:\My ""Cool"" App";
            int pid = 999;
            bool elevated = true;

            var info = RestartLogic.BuildRestartStartInfo(path, workingDir, pid, elevated);

            // " should become `"
            Assert.Contains(@"`""Cool`""", info.Arguments); 
        }
    }
}
