using Microsoft.Win32;
using SwitchBlade.Services;
using Xunit;
using System;

namespace SwitchBlade.Tests.Services
{
    public class RegistryServiceWrapperTests : IDisposable
    {
        private const string TestKeyPath = @"Software\SwitchBladeTests";
        private readonly RegistryServiceWrapper _wrapper = new RegistryServiceWrapper();

        public RegistryServiceWrapperTests()
        {
            // Clean start
            try { Registry.CurrentUser.DeleteSubKeyTree(TestKeyPath, false); } catch { }
        }

        public void Dispose()
        {
            // Clean up
            try { Registry.CurrentUser.DeleteSubKeyTree(TestKeyPath, false); } catch { }
        }

        [Fact]
        public void SetAndGetValue_ShouldWork()
        {
            _wrapper.SetCurrentUserValue(TestKeyPath, "TestValue", "Hello", RegistryValueKind.String);
            var result = _wrapper.GetCurrentUserValue(TestKeyPath, "TestValue");
            
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void GetValue_MissingKey_ReturnsNull()
        {
            var result = _wrapper.GetCurrentUserValue(@"Software\NonExistentKey_12345", "MissingValue");
            Assert.Null(result);
        }

        [Fact]
        public void DeleteValue_ShouldWork()
        {
            _wrapper.SetCurrentUserValue(TestKeyPath, "DeleteMe", 1, RegistryValueKind.DWord);
            _wrapper.DeleteCurrentUserValue(TestKeyPath, "DeleteMe", true);
            
            var result = _wrapper.GetCurrentUserValue(TestKeyPath, "DeleteMe");
            Assert.Null(result);
        }

        [Fact]
        public void DeleteValue_MissingKey_DoesNotThrow()
        {
            _wrapper.DeleteCurrentUserValue(@"Software\NonExistentKey_123456", "MissingValue", false);
        }
    }
}
