using System.Collections.Generic;

namespace SwitchBlade.Core
{
    public interface IWindowProvider
    {
        IEnumerable<WindowItem> GetWindows();
    }
}
