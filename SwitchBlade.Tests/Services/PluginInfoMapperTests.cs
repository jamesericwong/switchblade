using System;
using System.Collections.Generic;
using System.Reflection;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class PluginInfoMapperTests
    {
        [Fact]
        public void GetTypeName_ReturnsFullName_WhenAvailable()
        {
            Assert.Equal("System.String", PluginInfoMapper.GetTypeName(typeof(string)));
        }

        [Fact]
        public void GetTypeName_FallsBackToName_WhenFullNameIsNull()
        {
            // Generic type parameters have null FullName
            var genericParam = typeof(List<>).GetGenericArguments()[0];
            Assert.Equal("T", PluginInfoMapper.GetTypeName(genericParam));
        }

        [Fact]
        public void GetAssemblyName_ReturnsName_WhenAvailable()
        {
            var name = new AssemblyName("TestAssembly");
            Assert.Equal("TestAssembly", PluginInfoMapper.GetAssemblyName(name));
        }

        [Fact]
        public void GetAssemblyName_ReturnsUnknown_WhenNameIsNull()
        {
            var name = new AssemblyName();
            Assert.Equal("Unknown", PluginInfoMapper.GetAssemblyName(name));
        }

        [Fact]
        public void GetVersion_ReturnsVersionString_WhenAvailable()
        {
            var name = new AssemblyName("Test") { Version = new Version(1, 2, 3) };
            Assert.Equal("1.2.3", PluginInfoMapper.GetVersion(name));
        }

        [Fact]
        public void GetVersion_ReturnsFallback_WhenVersionIsNull()
        {
            var name = new AssemblyName("Test") { Version = null };
            Assert.Equal("0.0.0", PluginInfoMapper.GetVersion(name));
        }

        [Fact]
        public void IsInternalProvider_ReturnsTrue_WhenSameAssembly()
        {
            var assembly = typeof(PluginService).Assembly;
            Assert.True(PluginInfoMapper.IsInternalProvider(assembly, assembly.GetName()));
        }

        [Fact]
        public void IsInternalProvider_ReturnsTrue_WhenNameIsSwitchBlade()
        {
            var externalAssembly = typeof(object).Assembly;
            var fakeName = new AssemblyName("SwitchBlade");
            Assert.True(PluginInfoMapper.IsInternalProvider(externalAssembly, fakeName));
        }

        [Fact]
        public void IsInternalProvider_ReturnsFalse_WhenExternalAssemblyAndDifferentName()
        {
            var externalAssembly = typeof(object).Assembly;
            var otherName = new AssemblyName("ExternalPlugin");
            Assert.False(PluginInfoMapper.IsInternalProvider(externalAssembly, otherName));
        }

        [Fact]
        public void MapToInfo_WithExplicitParams_MapsAllFields()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("TestPlugin");
            mockProvider.Setup(p => p.HasSettings).Returns(true);

            var info = PluginInfoMapper.MapToInfo(mockProvider.Object, "TypeA", "AssemblyA", "1.0.0", true);

            Assert.Equal("TestPlugin", info.Name);
            Assert.Equal("TypeA", info.TypeName);
            Assert.Equal("AssemblyA", info.AssemblyName);
            Assert.Equal("1.0.0", info.Version);
            Assert.True(info.IsInternal);
            Assert.True(info.HasSettings);
            Assert.Same(mockProvider.Object, info.Provider);
            Assert.True(info.IsEnabled);
        }

        [Fact]
        public void MapToInfo_ConvenienceOverload_ResolvesMetadataFromProvider()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("TestPlugin");

            var info = PluginInfoMapper.MapToInfo(mockProvider.Object);

            Assert.Equal("TestPlugin", info.Name);
            Assert.NotNull(info.TypeName);
            Assert.NotNull(info.AssemblyName);
            Assert.NotNull(info.Version);
        }
    }
}
