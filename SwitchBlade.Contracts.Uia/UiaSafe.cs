using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Centralized safe access for UI Automation calls against live COM objects owned by other processes.
    /// Encodes the verified transient-exception whitelist (element invalidation, provider-process death)
    /// so every plugin and helper shares one definition of "transient". Anything outside the whitelist
    /// propagates unchanged so genuine bugs surface in tests and logs instead of being swallowed.
    /// </summary>
    /// <remarks>
    /// Whitelist verified against Windows SDK 10.0.22000.0 headers (see CODEBASE_ASSESSMENT.md §11):
    /// type + HRESULT only — exception messages are localizable and never used for control flow.
    /// </remarks>
    public static class UiaSafe
    {
        // Transient HRESULT whitelist (named constants per the no-magic-numbers rule).
        private const uint HResult_UiaElementNotEnabled = 0x80040200;   // UIA_E_ELEMENTNOTENABLED — UIAutomationCoreApi.h:30
        private const uint HResult_UiaElementNotAvailable = 0x80040201; // UIA_E_ELEMENTNOTAVAILABLE — UIAutomationCoreApi.h:31
        private const uint HResult_RpcServerUnavailable = 0x800706BA;   // RPC_S_SERVER_UNAVAILABLE (win32 1722) — winerror.h:10796
        private const uint HResult_RpcDisconnected = 0x80010108;        // RPC_E_DISCONNECTED — winerror.h:35046
        private const uint HResult_RpcServerDied = 0x80010007;          // RPC_E_SERVER_DIED — winerror.h:34866

        /// <summary>
        /// Determines whether the exception (or any exception nested in its InnerException chain) is a
        /// known transient UIA failure. Framework wrappers such as <see cref="ElementNotAvailableException"/>
        /// frequently nest the raw <see cref="COMException"/>, so the whole chain is inspected.
        /// </summary>
        public static bool IsTransient(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is ElementNotAvailableException || current is TimeoutException)
                {
                    return true;
                }

                if (current is COMException com && IsTransientHResult((uint)com.HResult))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Runs a UIA access and returns its value. Transient failures yield <c>false</c> with
        /// <see cref="T"/> default; non-transient exceptions propagate unchanged.
        /// </summary>
        public static bool TryGet<T>(Func<T> access, out T value) => TryGet(access, null, out value);

        /// <summary>
        /// Runs a UIA access and returns its value, recording probe/invalidation counts in <paramref name="diagnostics"/>.
        /// Transient failures yield <c>false</c> with <see cref="T"/> default; non-transient exceptions propagate unchanged.
        /// </summary>
        public static bool TryGet<T>(Func<T> access, ScanDiagnostics? diagnostics, out T value)
        {
            ArgumentNullException.ThrowIfNull(access);

            if (diagnostics != null)
            {
                diagnostics.RecordProbe();
            }

            try
            {
                value = access();
                return true;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (diagnostics != null)
                {
                    diagnostics.RecordInvalidation();
                    diagnostics.RecordObservation(ex);
                }

                value = default!;
                return false;
            }
        }

        /// <summary>
        /// Runs a UIA action tolerating transient failures. Returns <c>true</c> when the action completed,
        /// <c>false</c> on a transient failure (recorded in <paramref name="diagnostics"/> when provided);
        /// non-transient exceptions propagate unchanged.
        /// </summary>
        public static bool TryRun(Action action, ScanDiagnostics? diagnostics = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (diagnostics != null)
            {
                diagnostics.RecordProbe();
            }

            try
            {
                action();
                return true;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                if (diagnostics != null)
                {
                    diagnostics.RecordInvalidation();
                    diagnostics.RecordObservation(ex);
                }

                return false;
            }
        }

        private static bool IsTransientHResult(uint hresult) =>
            hresult == HResult_UiaElementNotEnabled ||
            hresult == HResult_UiaElementNotAvailable ||
            hresult == HResult_RpcServerUnavailable ||
            hresult == HResult_RpcDisconnected ||
            hresult == HResult_RpcServerDied;
    }
}
