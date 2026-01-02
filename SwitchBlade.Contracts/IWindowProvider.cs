using System.Collections.Generic;

namespace SwitchBlade.Contracts
{
    public interface IWindowProvider
    {
        // Called after instantiation to pass dependencies (e.g., SettingsService)
        // We use 'object' to avoid hard dependency on the main app's SettingsService,
        // ideally this would be a specific settings interface in Contracts too, but for now 'object' allows loose coupling.
        void Initialize(object settingsService, ILogger logger);

        IEnumerable<WindowItem> GetWindows();

        void ActivateWindow(WindowItem item);
    }
}
