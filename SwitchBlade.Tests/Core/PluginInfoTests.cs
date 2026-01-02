using Xunit;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class PluginInfoTests
    {
        [Fact]
        public void PluginInfo_DefaultValues_AreEmptyStrings()
        {
            var pluginInfo = new PluginInfo();

            Assert.Equal(string.Empty, pluginInfo.Name);
            Assert.Equal(string.Empty, pluginInfo.Version);
            Assert.Equal(string.Empty, pluginInfo.AssemblyName);
            Assert.Equal(string.Empty, pluginInfo.TypeName);
            Assert.False(pluginInfo.IsInternal);
        }

        [Fact]
        public void PluginInfo_SetName_ReturnsCorrectValue()
        {
            var pluginInfo = new PluginInfo { Name = "TestPlugin" };

            Assert.Equal("TestPlugin", pluginInfo.Name);
        }

        [Fact]
        public void PluginInfo_SetVersion_ReturnsCorrectValue()
        {
            var pluginInfo = new PluginInfo { Version = "1.0.0" };

            Assert.Equal("1.0.0", pluginInfo.Version);
        }

        [Fact]
        public void PluginInfo_SetAssemblyName_ReturnsCorrectValue()
        {
            var pluginInfo = new PluginInfo { AssemblyName = "TestAssembly.dll" };

            Assert.Equal("TestAssembly.dll", pluginInfo.AssemblyName);
        }

        [Fact]
        public void PluginInfo_SetTypeName_ReturnsCorrectValue()
        {
            var pluginInfo = new PluginInfo { TypeName = "TestNamespace.TestType" };

            Assert.Equal("TestNamespace.TestType", pluginInfo.TypeName);
        }

        [Fact]
        public void PluginInfo_SetIsInternal_ReturnsCorrectValue()
        {
            var pluginInfo = new PluginInfo { IsInternal = true };

            Assert.True(pluginInfo.IsInternal);
        }
    }
}
