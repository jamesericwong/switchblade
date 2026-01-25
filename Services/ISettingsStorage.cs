namespace SwitchBlade.Services
{
    /// <summary>
    /// Abstraction for settings persistence.
    /// Allows decoupling SettingsService from the Windows Registry.
    /// </summary>
    public interface ISettingsStorage
    {
        /// <summary>
        /// Checks if a setting exists in storage.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>True if the key exists.</returns>
        bool HasKey(string key);

        /// <summary>
        /// Retrieves a value from storage.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">Default value if the key is not found or conversion fails.</param>
        /// <returns>The stored value, or <paramref name="defaultValue"/> if not found.</returns>
        T GetValue<T>(string key, T defaultValue);

        /// <summary>
        /// Stores a value.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The value to store.</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// Gets a JSON-serialized list of strings.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The list, or an empty list if not found.</returns>
        List<string> GetStringList(string key);

        /// <summary>
        /// Stores a list of strings as JSON.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The list to store.</param>
        void SetStringList(string key, List<string> value);

        /// <summary>
        /// Ensures all pending writes are flushed to the underlying storage.
        /// </summary>
        void Flush();
    }
}
