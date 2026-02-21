using System.Reflection;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
namespace SwitchBlade.Services
{
    /// <summary>
    /// Pure, stateless mapper that converts <see cref="IWindowProvider"/> instances
    /// into <see cref="PluginInfo"/> DTOs for UI display.
    /// </summary>
    public static class PluginInfoMapper
    {
        /// <summary>
        /// Maps a provider to its <see cref="PluginInfo"/> using reflection metadata.
        /// </summary>
        public static PluginInfo MapToInfo(IWindowProvider provider)
        {
            var type = provider.GetType();
            var assembly = type.Assembly;
            var assemblyName = assembly.GetName();

            return MapToInfo(
                provider,
                GetTypeName(type),
                GetAssemblyName(assemblyName),
                GetVersion(assemblyName),
                IsInternalProvider(assembly, assemblyName));
        }

        /// <summary>
        /// Maps a provider to its <see cref="PluginInfo"/> from pre-resolved metadata.
        /// </summary>
        public static PluginInfo MapToInfo(
            IWindowProvider provider,
            string typeName,
            string assemblyName,
            string version,
            bool isInternal)
        {
            return new PluginInfo
            {
                Name = provider.PluginName,
                TypeName = typeName,
                AssemblyName = assemblyName,
                Version = version,
                IsInternal = isInternal,
                HasSettings = provider.HasSettings,
                Provider = provider,
                IsEnabled = true
            };
        }

        public static string GetTypeName(Type type) => type.FullName ?? type.Name;

        public static string GetAssemblyName(AssemblyName name) => name.Name ?? "Unknown";

        public static string GetVersion(AssemblyName name) => name.Version?.ToString() ?? "0.0.0";

        public static bool IsInternalProvider(Assembly assembly, AssemblyName name)
            => assembly == typeof(PluginService).Assembly || name.Name == "SwitchBlade";
    }
}
