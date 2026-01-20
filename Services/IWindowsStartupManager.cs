namespace SwitchBlade.Services
{
    /// <summary>
    /// Abstraction for Windows startup registry operations.
    /// Separates startup management from general settings persistence.
    /// </summary>
    public interface IWindowsStartupManager
    {
        /// <summary>
        /// Checks if the application is currently set to run at Windows startup.
        /// </summary>
        bool IsStartupEnabled();

        /// <summary>
        /// Enables the application to run at Windows startup.
        /// </summary>
        /// <param name="executablePath">Path to the executable to run at startup.</param>
        void EnableStartup(string executablePath);

        /// <summary>
        /// Disables the application from running at Windows startup.
        /// </summary>
        void DisableStartup();

        /// <summary>
        /// Checks for startup marker set by installer and applies if present.
        /// </summary>
        /// <returns>True if startup was enabled via marker.</returns>
        bool CheckAndApplyStartupMarker();
    }
}
