using System;
using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Default implementation of IMemoryInfoProvider using System.GC.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemMemoryInfoProvider : IMemoryInfoProvider
    {
        public long GetTotalMemory(bool forceFullCollection)
        {
            return GC.GetTotalMemory(forceFullCollection);
        }
    }
}
