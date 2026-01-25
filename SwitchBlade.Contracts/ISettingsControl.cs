namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Interface for plugins that provide a WPF-based settings UI.
    /// This allows the host application to display plugin settings
    /// without relying on raw HWND manipulation.
    /// </summary>
    public interface ISettingsControl
    {
        /// <summary>
        /// Creates the settings control for this plugin.
        /// The returned object should be a WPF FrameworkElement that will be hosted in a dialog.
        /// </summary>
        /// <returns>A WPF FrameworkElement (as object) containing the settings UI.</returns>
        object CreateSettingsControl();

        /// <summary>
        /// Called when the user clicks Save/OK in the host dialog.
        /// The plugin should persist its settings here.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Called when the user cancels the settings dialog.
        /// The plugin should discard any pending changes.
        /// </summary>
        void CancelSettings();
    }
}
