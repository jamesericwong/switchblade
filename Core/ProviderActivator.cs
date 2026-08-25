using System;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Isolates plugin activation exceptions so they can never escape into the WPF message loop.
    /// </summary>
    internal static class ProviderActivator
    {
        public static bool TryActivate(WindowItem item, ILogger? logger)
        {
            if (item.Source == null)
                return false;

            try
            {
                item.Source.ActivateWindow(item);
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Failed to activate window '{item.Title}'", ex);
                return false;
            }
        }
    }
}
