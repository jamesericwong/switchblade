using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    public class NativeInteropWrapper : INativeInteropWrapper
    {
        public void ClearProcessCache()
        {
            NativeInterop.ClearProcessCache();
        }
    }
}
