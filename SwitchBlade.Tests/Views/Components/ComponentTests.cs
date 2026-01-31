using Xunit;
using SwitchBlade.Views.Components;
using SwitchBlade.Contracts;
using System;
using System.Threading;
using System.Windows.Controls;

namespace SwitchBlade.Tests.Views.Components
{
    public class SearchBarTests
    {
        [Fact]
        public void SearchBar_CanBeInstantiated()
        {
            RunOnStaThread(() =>
            {
                var searchBar = new SearchBar();
                Assert.NotNull(searchBar);
            });
        }

        [Fact]
        public void SearchBar_FocusInput_DoesNotThrow()
        {
            RunOnStaThread(() =>
            {
                var searchBar = new SearchBar();
                var exception = Record.Exception(() => searchBar.FocusInput());
                Assert.Null(exception);
            });
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? threadException = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { threadException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadException != null)
                throw threadException;
        }
    }

    public class ResultListTests
    {
        [Fact]
        public void ResultList_CanBeInstantiated()
        {
            RunOnStaThread(() =>
            {
                var resultList = new ResultList();
                Assert.NotNull(resultList);
            });
        }

        [Fact]
        public void ResultList_ListActualHeight_ReturnsZeroInitially()
        {
            RunOnStaThread(() =>
            {
                var resultList = new ResultList();
                Assert.Equal(0, resultList.ListActualHeight);
            });
        }

        [Fact]
        public void ResultList_ScrollIntoView_DoesNotThrowWithNull()
        {
            RunOnStaThread(() =>
            {
                var resultList = new ResultList();
                var exception = Record.Exception(() => resultList.ScrollIntoView(null!));
                Assert.Null(exception);
            });
        }

        [Fact]
        public void ResultList_ActivateItemRequested_CanBeSubscribed()
        {
            RunOnStaThread(() =>
            {
                var resultList = new ResultList();
                bool eventFired = false;
                resultList.ActivateItemRequested += (s, e) => eventFired = true;
                Assert.False(eventFired);
            });
        }

        [Fact]
        public void ResultList_PreviewItemRequested_CanBeSubscribed()
        {
            RunOnStaThread(() =>
            {
                var resultList = new ResultList();
                bool eventFired = false;
                resultList.PreviewItemRequested += (s, e) => eventFired = true;
                Assert.False(eventFired);
            });
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? threadException = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { threadException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadException != null)
                throw threadException;
        }
    }

    public class PreviewPanelTests
    {
        [Fact]
        public void PreviewPanel_CanBeInstantiated()
        {
            RunOnStaThread(() =>
            {
                var previewPanel = new PreviewPanel();
                Assert.NotNull(previewPanel);
            });
        }

        [Fact]
        public void PreviewPanel_PreviewCanvas_IsNotNull()
        {
            RunOnStaThread(() =>
            {
                var previewPanel = new PreviewPanel();
                Assert.NotNull(previewPanel.PreviewCanvas);
            });
        }

        [Fact]
        public void PreviewPanel_PreviewCanvas_IsCanvas()
        {
            RunOnStaThread(() =>
            {
                var previewPanel = new PreviewPanel();
                Assert.IsType<Canvas>(previewPanel.PreviewCanvas);
            });
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? threadException = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { threadException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadException != null)
                throw threadException;
        }
    }
}
