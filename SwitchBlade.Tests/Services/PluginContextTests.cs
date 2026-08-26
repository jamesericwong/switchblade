using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class PluginContextTests
    {
        [Fact]
        public void Ctor_WithAllDependencies_ExposesEachOnItsProperty()
        {
            var logger = Mock.Of<ILogger>();
            var interop = Mock.Of<IWindowInterop>();
            var registry = Mock.Of<IRegistryService>();
            var settings = Mock.Of<IPluginSettingsService>();

            var context = new PluginContext(logger, interop, registry, settings);

            Assert.Same(logger, context.Logger);
            Assert.Same(interop, context.Interop);
            Assert.Same(registry, context.Registry);
            Assert.Same(settings, context.Settings);
        }

        [Fact]
        public void Ctor_WithoutSettings_AllowsOptionalOmission()
        {
            var context = new PluginContext(Mock.Of<ILogger>(), Mock.Of<IWindowInterop>(), Mock.Of<IRegistryService>());

            Assert.Null(context.Settings);
        }
    }
}
