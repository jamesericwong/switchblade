namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Versioning for the host↔worker UIA IPC protocol (JSON lines over stdin/stdout).
    /// Both sides are built from this single source of truth, so a version mismatch means one side is an
    /// older build — each side detects it and fails fast with an actionable message instead of silently
    /// garbling results. Bump <see cref="CurrentVersion"/> whenever the wire format changes semantics.
    /// </summary>
    public static class UiaProtocol
    {
        /// <summary>The first explicitly versioned protocol revision.</summary>
        public const int CurrentVersion = 1;

        /// <summary>A peer that sends no version field at all (pre-versioning builds).</summary>
        public const int LegacyVersion = 0;

        /// <summary>Whether a peer's declared protocol version is one this build can speak.</summary>
        public static bool IsCompatibleVersion(int version) =>
            version >= LegacyVersion && version <= CurrentVersion;
    }
}
