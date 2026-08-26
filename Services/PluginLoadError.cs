namespace SwitchBlade.Services
{
    /// <summary>
    /// Describes a plugin that was discovered but failed to initialize during load.
    /// Surfaced via <see cref="IPluginService.LoadErrors"/> so the UI can report which plugins are unavailable and why.
    /// </summary>
    public sealed record PluginLoadError(string PluginName, string Message);
}
