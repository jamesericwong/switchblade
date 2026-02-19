namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Abstraction for retrieving garbage collection and memory information.
    /// </summary>
    public interface IMemoryInfoProvider
    {
        /// <summary>
        /// Retrieves the number of bytes currently thought to be allocated.
        /// </summary>
        /// <param name="forceFullCollection">If true, waits for garbage collection to occur before returning.</param>
        long GetTotalMemory(bool forceFullCollection);
    }
}
