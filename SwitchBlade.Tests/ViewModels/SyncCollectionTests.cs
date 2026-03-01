using Xunit;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;

namespace SwitchBlade.Tests.ViewModels
{
    /// <summary>
    /// Tests for the <see cref="ObservableCollectionSync"/> helper.
    /// </summary>
    public class SyncCollectionTests
    {
        [Fact]
        public void SyncCollection_PreservesOrderFromSource()
        {
            // Arrange
            var collection = new ObservableCollection<WindowItem>();
            var item1 = new WindowItem { Hwnd = new IntPtr(1), Title = "A" };
            var item2 = new WindowItem { Hwnd = new IntPtr(2), Title = "B" };
            var item3 = new WindowItem { Hwnd = new IntPtr(3), Title = "C" };
            collection.Add(item3);
            collection.Add(item1);
            collection.Add(item2);

            var source = new List<WindowItem> { item1, item2, item3 };

            // Act
            ObservableCollectionSync.Sync(collection, source);

            // Assert
            Assert.Equal(3, collection.Count);
            Assert.Same(item1, collection[0]);
            Assert.Same(item2, collection[1]);
            Assert.Same(item3, collection[2]);
        }

        [Fact]
        public void SyncCollection_RemovesStaleItems()
        {
            // Arrange
            var collection = new ObservableCollection<WindowItem>();
            var item1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Keep" };
            var item2 = new WindowItem { Hwnd = new IntPtr(2), Title = "Remove" };
            collection.Add(item1);
            collection.Add(item2);

            var source = new List<WindowItem> { item1 };

            // Act
            ObservableCollectionSync.Sync(collection, source);

            // Assert
            Assert.Single(collection);
            Assert.Same(item1, collection[0]);
        }

        [Fact]
        public void SyncCollection_InsertsNewItems()
        {
            // Arrange
            var collection = new ObservableCollection<WindowItem>();
            var item1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Existing" };
            collection.Add(item1);

            var item2 = new WindowItem { Hwnd = new IntPtr(2), Title = "New" };
            var source = new List<WindowItem> { item1, item2 };

            // Act
            ObservableCollectionSync.Sync(collection, source);

            // Assert
            Assert.Equal(2, collection.Count);
            Assert.Same(item2, collection[1]);
        }

        [Fact]
        public void SyncCollection_HandlesEmptySource()
        {
            // Arrange
            var collection = new ObservableCollection<WindowItem>();
            collection.Add(new WindowItem { Hwnd = new IntPtr(1), Title = "A" });
            collection.Add(new WindowItem { Hwnd = new IntPtr(2), Title = "B" });

            var source = new List<WindowItem>();

            // Act
            ObservableCollectionSync.Sync(collection, source);

            // Assert
            Assert.Empty(collection);
        }

        [Fact]
        public void SyncCollection_HandlesEmptyCollection()
        {
            // Arrange
            var collection = new ObservableCollection<WindowItem>();
            var item1 = new WindowItem { Hwnd = new IntPtr(1), Title = "A" };
            var source = new List<WindowItem> { item1 };

            // Act
            ObservableCollectionSync.Sync(collection, source);

            // Assert
            Assert.Single(collection);
            Assert.Same(item1, collection[0]);
        }
    }
}
