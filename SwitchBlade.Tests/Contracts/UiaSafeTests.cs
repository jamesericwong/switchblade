using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    /// <summary>
    /// Covers the §11 locked transient-exception whitelist: type + HRESULT only, full InnerException
    /// chain walked, excluded HRESULTs propagate. No live UIA required — exceptions are constructed directly.
    /// </summary>
    public class UiaSafeTests
    {
        [Fact]
        public void IsTransient_ElementNotAvailableException_ReturnsTrue() =>
            Assert.True(UiaSafe.IsTransient(new ElementNotAvailableException()));

        [Fact]
        public void IsTransient_TimeoutException_ReturnsTrue() =>
            Assert.True(UiaSafe.IsTransient(new TimeoutException()));

        [Theory]
        [InlineData(unchecked((int)0x80040200))] // UIA_E_ELEMENTNOTENABLED
        [InlineData(unchecked((int)0x80040201))] // UIA_E_ELEMENTNOTAVAILABLE
        [InlineData(unchecked((int)0x800706BA))] // RPC_S_SERVER_UNAVAILABLE
        [InlineData(unchecked((int)0x80010108))] // RPC_E_DISCONNECTED
        [InlineData(unchecked((int)0x80010007))] // RPC_E_SERVER_DIED
        public void IsTransient_ComException_WhitelistedHResult_ReturnsTrue(int hresult) =>
            Assert.True(UiaSafe.IsTransient(new COMException("transient", hresult)));

        [Fact]
        public void IsTransient_TransientNestedInInnerChain_ReturnsTrue()
        {
            // Framework wrappers frequently nest the raw COMException.
            var wrapped = new InvalidOperationException("wrapper", new COMException("inner", unchecked((int)0x80040201)));

            Assert.True(UiaSafe.IsTransient(wrapped));
        }

        [Fact]
        public void IsTransient_DeeplyNestedElementNotAvailable_ReturnsTrue()
        {
            var wrapped = new Exception("outer", new InvalidOperationException("mid", new ElementNotAvailableException()));

            Assert.True(UiaSafe.IsTransient(wrapped));
        }

        [Theory]
        [InlineData(unchecked((int)0x80040204))] // UIA_E_NOTSUPPORTED — stable property, must propagate.
        [InlineData(unchecked((int)0x80040202))] // UIA_E_NOCLICKABLEPOINT — element-specific, caller decides.
        public void IsTransient_ComException_ExcludedHResult_ReturnsFalse(int hresult) =>
            Assert.False(UiaSafe.IsTransient(new COMException("not transient", hresult)));

        [Fact]
        public void IsTransient_GenericException_ReturnsFalse() =>
            Assert.False(UiaSafe.IsTransient(new InvalidOperationException("real bug")));

        [Fact]
        public void IsTransient_Null_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => UiaSafe.IsTransient(null!));

        [Fact]
        public void TryGet_Success_ReturnsValueAndTrue()
        {
            var ok = UiaSafe.TryGet(() => 42, out var value);

            Assert.True(ok);
            Assert.Equal(42, value);
        }

        [Fact]
        public void TryGet_TransientFailure_ReturnsFalseAndDefault()
        {
            Exception transient = new ElementNotAvailableException();

            var ok = UiaSafe.TryGet(() => throw transient, out string? value);

            Assert.False(ok);
            Assert.Null(value);
        }

        [Fact]
        public void TryGet_NonTransientFailure_Propagates()
        {
            var bug = new InvalidOperationException("real bug");
            string? value = null;
            Func<string> access = () => throw bug;

            var ex = Assert.Throws<InvalidOperationException>(() => UiaSafe.TryGet(access, out value));

            Assert.Same(bug, ex);
        }

        [Fact]
        public void TryGet_ExcludedHResult_Propagates()
        {
            var notSupported = new COMException("not supported", unchecked((int)0x80040204));
            string? value = null;
            Func<string> access = () => throw notSupported;

            var ex = Assert.Throws<COMException>(() => UiaSafe.TryGet(access, out value));

            Assert.Same(notSupported, ex);
        }

        [Fact]
        public void TryGet_WithDiagnostics_Success_RecordsProbeOnly()
        {
            var diagnostics = new ScanDiagnostics();

            UiaSafe.TryGet(() => "value", diagnostics, out _);

            Assert.Equal(1, diagnostics.ElementsProbed);
            Assert.Equal(0, diagnostics.InvalidatedElements);
        }

        [Fact]
        public void TryGet_WithDiagnostics_TransientFailure_RecordsProbeAndInvalidation()
        {
            var diagnostics = new ScanDiagnostics();

            UiaSafe.TryGet<object>(() => throw new TimeoutException(), diagnostics, out _);

            Assert.Equal(1, diagnostics.ElementsProbed);
            Assert.Equal(1, diagnostics.InvalidatedElements);
        }

        [Fact]
        public void TryGet_NullAccess_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => UiaSafe.TryGet<string>(null!, out _));

        [Fact]
        public void TryRun_Success_ReturnsTrueAndRunsAction()
        {
            var ran = false;

            var ok = UiaSafe.TryRun(() => ran = true);

            Assert.True(ok);
            Assert.True(ran);
        }

        [Fact]
        public void TryRun_TransientFailure_ReturnsFalse() =>
            Assert.False(UiaSafe.TryRun(() => throw new ElementNotAvailableException()));

        [Fact]
        public void TryRun_NonTransientFailure_Propagates()
        {
            var bug = new InvalidOperationException("real bug");
            Action action = () => throw bug;

            var ex = Assert.Throws<InvalidOperationException>(() => UiaSafe.TryRun(action));

            Assert.Same(bug, ex);
        }

        [Fact]
        public void TryRun_WithDiagnostics_TransientFailure_RecordsProbeAndInvalidation()
        {
            var diagnostics = new ScanDiagnostics();

            UiaSafe.TryRun(() => throw new COMException("died", unchecked((int)0x80010007)), diagnostics);

            Assert.Equal(1, diagnostics.ElementsProbed);
            Assert.Equal(1, diagnostics.InvalidatedElements);
        }

        [Fact]
        public void TryRun_NullAction_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => UiaSafe.TryRun(null!));
    }
}
