namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Wrapper for static NativeInterop calls to enable unit testing.
    /// </summary>
    public interface INativeInteropWrapper
    {
        void ClearProcessCache();
    }
}
