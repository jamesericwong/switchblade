using System.Diagnostics;

namespace SwitchBlade.Services
{
    public static class RestartLogic
    {
        public static ProcessStartInfo BuildRestartStartInfo(string processPath, string currentWorkingDirectory, int currentPid, bool isElevated)
        {
            // Determine if we're de-elevating (currently admin, turning it off)
            // If we are currently elevated (isElevated = true), we assume the intention of a generic restart 
            // is to return to the logical "default" state or just restart. 
            // However, the original logic specifically had a check: bool isDeElevating = Program.IsRunningAsAdmin();
            // And then it had two branches.
            // Branch 1 (isDeElevating == true): Uses mixed mode with explorer.exe to de-elevate? 
            // Actually, let's look at the original code carefully:
            // "bool isDeElevating = Program.IsRunningAsAdmin();"
            // This variable name implies that if we ARE admin, we are treating this restart as a "de-elevation" attempt,
            // or at least a restart that might need to handle the admin context carefully.
            //
            // Original Code:
            // if (isDeElevating) {
            //    command = Wait-Process... Start-Process explorer.exe -ArgumentList 'escapedPath'
            // } else {
            //    command = Wait-Process... Start-Process 'processPath'
            // }
            // 
            // The logic seems to be: If we are admin, launch via explorer to essentially de-elevate (since explorer is usually user-level).
            
            if (isElevated)
            {
                var escapedPath = processPath.Replace("\"", "`\"");
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process explorer.exe -ArgumentList '\"{escapedPath}\"'";

                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = currentWorkingDirectory
                };
            }
            else
            {
                var command = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Process '{processPath}' -WorkingDirectory '{currentWorkingDirectory}'";

                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = currentWorkingDirectory
                };
            }
        }
    }
}
