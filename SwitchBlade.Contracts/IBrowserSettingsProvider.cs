using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    public interface IBrowserSettingsProvider
    {
        List<string> BrowserProcesses { get; }
    }
}
