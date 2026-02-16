using Xunit;
using Moq;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Microsoft.Win32;
using System.Collections.Generic;

namespace SwitchBlade.Tests.Services
{
    public class RegistrySettingsStorageTests
    {
        private readonly Mock<IRegistryService> _mockRegistry;
        private readonly RegistrySettingsStorage _storage;
        private const string TestKeyPath = @"Software\TestApp";

        public RegistrySettingsStorageTests()
        {
            _mockRegistry = new Mock<IRegistryService>();
            _storage = new RegistrySettingsStorage(TestKeyPath, _mockRegistry.Object);
        }

        [Fact]
        public void HasKey_ReturnsTrue_WhenValueExists()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyKey")).Returns("Present");
            Assert.True(_storage.HasKey("MyKey"));
        }

        [Fact]
        public void HasKey_ReturnsFalse_WhenValueIsNull()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyKey")).Returns((object?)null);
            Assert.False(_storage.HasKey("MyKey"));
        }

        [Fact]
        public void GetValue_String_ReturnsValue()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyString")).Returns("Hello");
            Assert.Equal("Hello", _storage.GetValue("MyString", "Default"));
        }

        [Fact]
        public void GetValue_Int_ReturnsValue()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyInt")).Returns(42);
            Assert.Equal(42, _storage.GetValue("MyInt", 0));
        }

        [Fact]
        public void GetValue_Bool_ReturnsTrue_FromInt1()
        {
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyBool")).Returns(1);
            Assert.True(_storage.GetValue("MyBool", false));
        }

        [Fact]
        public void SetValue_String_CallsRegistry()
        {
            _storage.SetValue("MyString", "NewValue");
            _mockRegistry.Verify(r => r.SetCurrentUserValue(TestKeyPath, "MyString", "NewValue", RegistryValueKind.String), Times.Once);
        }

        [Fact]
        public void SetValue_Int_CallsRegistry()
        {
            _storage.SetValue("MyInt", 100);
            _mockRegistry.Verify(r => r.SetCurrentUserValue(TestKeyPath, "MyInt", 100, RegistryValueKind.DWord), Times.Once);
        }

        [Fact]
        public void SetValue_Bool_CallsRegistryAsDWord()
        {
            _storage.SetValue("MyBool", true);
            _mockRegistry.Verify(r => r.SetCurrentUserValue(TestKeyPath, "MyBool", 1, RegistryValueKind.DWord), Times.Once);
        }

        [Fact]
        public void GetStringList_ReturnsDeserializedList()
        {
            string json = "[\"Item1\",\"Item2\"]";
            _mockRegistry.Setup(r => r.GetCurrentUserValue(TestKeyPath, "MyList")).Returns(json);

            var list = _storage.GetStringList("MyList");

            Assert.Equal(2, list.Count);
            Assert.Contains("Item1", list);
            Assert.Contains("Item2", list);
        }

        [Fact]
        public void SetStringList_SerializesAndSaves()
        {
            var list = new List<string> { "A", "B" };
            _storage.SetStringList("MyList", list);

            _mockRegistry.Verify(r => r.SetCurrentUserValue(TestKeyPath, "MyList", It.Is<string>(s => s.Contains("A") && s.Contains("B")), RegistryValueKind.String), Times.Once);
        }
    }
}
