using System;
using System.Collections.Generic;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Services
{
    // A fake provider in the Test assembly (which is external to SwitchBlade main assembly)
    public class FakeGlobalProvider : IWindowProvider
    {
        public string PluginName => "Fake External Plugin";
        public bool HasSettings => false;

        public void Initialize(IPluginContext context) { }
        public void ReloadSettings() { }
        public IEnumerable<WindowItem> GetWindows() => new List<WindowItem>();
        public void ActivateWindow(WindowItem item) { }
    }
}
