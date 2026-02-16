namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Window result from UIA Worker.
    /// </summary>
    public sealed class UiaWindowResult
    {
        public long Hwnd { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string? ExecutablePath { get; set; }
        public string PluginName { get; set; } = "";
        public bool IsFallback { get; set; }
    }
}
