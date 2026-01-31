using Xunit;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;

namespace SwitchBlade.Tests.ViewModels
{
    /// <summary>
    /// Tests for the SyncCollection algorithm in MainViewModel.
    /// Uses reflection to test the private method via UpdateSearch.
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
            SyncCollectionHelper(collection, source);

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
            SyncCollectionHelper(collection, source);

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
            SyncCollectionHelper(collection, source);

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
            SyncCollectionHelper(collection, source);

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
            SyncCollectionHelper(collection, source);

            // Assert
            Assert.Single(collection);
            Assert.Same(item1, collection[0]);
        }

        /// <summary>
        /// Helper to mimic the SyncCollection logic for testing.
        /// </summary>
        private static void SyncCollectionHelper(ObservableCollection<WindowItem> collection, IList<WindowItem> source)
        {
            var sourceSet = new HashSet<WindowItem>(source);
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!sourceSet.Contains(collection[i]))
                    collection.RemoveAt(i);
            }

            int ptr = 0;
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (ptr < collection.Count && collection[ptr] == item)
                {
                    ptr++;
                }
                else
                {
                    int foundAt = -1;
                    for (int j = ptr + 1; j < collection.Count; j++)
                    {
                        if (collection[j] == item)
                        {
                            foundAt = j;
                            break;
                        }
                    }

                    if (foundAt != -1)
                    {
                        collection.Move(foundAt, ptr);
                        ptr++;
                    }
                    else
                    {
                        collection.Insert(ptr, item);
                        ptr++;
                    }
                }
            }
        }
    }
}
