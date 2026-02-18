using System.Diagnostics.CodeAnalysis;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
    public class WorkstationService : SwitchBlade.Contracts.IWorkstationService
    {
        public bool IsWorkstationLocked()
        {
            return SwitchBlade.Contracts.NativeInterop.IsWorkstationLocked();
        }
    }
}
