using System.Collections.Generic;
using Xunit;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class NavigationServiceTests
    {
        private readonly NavigationService _service = new();

        #region ResolveSelection Tests

        [Fact]
        public void ResolveSelection_EmptyList_ReturnsNull()
        {
            var result = _service.ResolveSelection(
                new List<WindowItem>(), null, null, 0, RefreshBehavior.PreserveScroll, false);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveSelection_ResetSelection_ReturnsFirstItem()
        {
            var items = new List<WindowItem>
            {
                new() { Title = "First", Hwnd = (System.IntPtr)1 },
                new() { Title = "Second", Hwnd = (System.IntPtr)2 }
            };

            var result = _service.ResolveSelection(
                items, (System.IntPtr)2, "Second", 1, RefreshBehavior.PreserveScroll, resetSelection: true);

            Assert.Same(items[0], result);
        }

        [Fact]
        public void ResolveSelection_PreserveIdentity_FindsMatchingWindow()
        {
            var items = new List<WindowItem>
            {
                new() { Title = "First", Hwnd = (System.IntPtr)1 },
                new() { Title = "Second", Hwnd = (System.IntPtr)2 },
                new() { Title = "Third", Hwnd = (System.IntPtr)3 }
            };

            var result = _service.ResolveSelection(
                items, (System.IntPtr)2, "Second", 1, RefreshBehavior.PreserveIdentity, false);

            Assert.Same(items[1], result);
        }

        [Fact]
        public void ResolveSelection_PreserveIdentity_NoMatch_ReturnsFirst()
        {
            var items = new List<WindowItem>
            {
                new() { Title = "First", Hwnd = (System.IntPtr)1 },
                new() { Title = "Second", Hwnd = (System.IntPtr)2 }
            };

            var result = _service.ResolveSelection(
                items, (System.IntPtr)999, "Gone", 0, RefreshBehavior.PreserveIdentity, false);

            Assert.Same(items[0], result);
        }

        [Fact]
        public void ResolveSelection_PreserveIndex_ClampsToValidRange()
        {
            var items = new List<WindowItem>
            {
                new() { Title = "First", Hwnd = (System.IntPtr)1 },
                new() { Title = "Second", Hwnd = (System.IntPtr)2 }
            };

            var result = _service.ResolveSelection(
                items, null, null, 10, RefreshBehavior.PreserveIndex, false);

            Assert.Same(items[1], result); // Clamped to last
        }

        [Fact]
        public void ResolveSelection_PreserveScroll_NoPrevious_ReturnsFirst()
        {
            var items = new List<WindowItem>
            {
                new() { Title = "First", Hwnd = (System.IntPtr)1 }
            };

            var result = _service.ResolveSelection(
                items, null, null, -1, RefreshBehavior.PreserveScroll, false);

            Assert.Same(items[0], result);
        }

        #endregion

        #region CalculateMoveIndex Tests

        [Fact]
        public void CalculateMoveIndex_EmptyList_ReturnsMinusOne()
        {
            int result = _service.CalculateMoveIndex(0, 1, 0);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void CalculateMoveIndex_NoSelection_DownReturnsFirst()
        {
            int result = _service.CalculateMoveIndex(-1, 1, 5);
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculateMoveIndex_NoSelection_UpReturnsLast()
        {
            int result = _service.CalculateMoveIndex(-1, -1, 5);
            Assert.Equal(4, result);
        }

        [Fact]
        public void CalculateMoveIndex_Down_IncrementsIndex()
        {
            int result = _service.CalculateMoveIndex(2, 1, 5);
            Assert.Equal(3, result);
        }

        [Fact]
        public void CalculateMoveIndex_Up_DecrementsIndex()
        {
            int result = _service.CalculateMoveIndex(2, -1, 5);
            Assert.Equal(1, result);
        }

        [Fact]
        public void CalculateMoveIndex_AtEnd_ClampsToLast()
        {
            int result = _service.CalculateMoveIndex(4, 1, 5);
            Assert.Equal(4, result);
        }

        [Fact]
        public void CalculateMoveIndex_AtStart_ClampsToFirst()
        {
            int result = _service.CalculateMoveIndex(0, -1, 5);
            Assert.Equal(0, result);
        }

        #endregion

        #region CalculatePageMoveIndex Tests

        [Fact]
        public void CalculatePageMoveIndex_EmptyList_ReturnsMinusOne()
        {
            int result = _service.CalculatePageMoveIndex(0, 1, 5, 0);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void CalculatePageMoveIndex_PageDown_MovesCorrectly()
        {
            int result = _service.CalculatePageMoveIndex(0, 1, 5, 20);
            Assert.Equal(5, result);
        }

        [Fact]
        public void CalculatePageMoveIndex_PageUp_MovesCorrectly()
        {
            int result = _service.CalculatePageMoveIndex(10, -1, 5, 20);
            Assert.Equal(5, result);
        }

        [Fact]
        public void CalculatePageMoveIndex_ClampsToEnd()
        {
            int result = _service.CalculatePageMoveIndex(18, 1, 5, 20);
            Assert.Equal(19, result);
        }

        [Fact]
        public void ResolveSelection_NullList_ReturnsNull()
        {
            var result = _service.ResolveSelection(
                null!, null, null, 0, RefreshBehavior.PreserveScroll, false);

            Assert.Null(result);
        }

        [Fact]
        public void CalculatePageMoveIndex_PageSizeZero_ReturnsMinusOne()
        {
            int result = _service.CalculatePageMoveIndex(0, 1, 0, 10);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void CalculatePageMoveIndex_NegativeIndex_TreatsAsZero()
        {
            int result = _service.CalculatePageMoveIndex(-1, 1, 5, 20);
            Assert.Equal(5, result);
        }

        #endregion
    }
}
