using System.Collections.Generic;

/// <summary>
/// Internal interface for ChangeTracker operations reserved for CrossChapterTracker (#16).
///
/// SetFlagRaw writes directly to _flags without triggering OnOverlayChanged or
/// incrementing OverlayVersion. GetAllFlags returns a shallow copy for persistence.
/// SetImmutableFlagCheck wires the IsImmutable guard into SetFlag.
///
/// NOT part of the public IChangeTracker query interface — only CrossChapterTracker
/// receives this reference at construction time.
/// </summary>
internal interface IChangeTrackerInternal
{
    /// <summary>
    /// Directly sets a flag value in _flags. No validation, no events, no
    /// OverlayVersion increment. Used for initialization, replay reset, and
    /// save restoration — never for player-driven SetFlag.
    /// </summary>
    void SetFlagRaw(string flagId, bool value);

    /// <summary>
    /// Returns a shallow copy of the entire _flags dictionary for persistence.
    /// </summary>
    Dictionary<string, bool> GetAllFlags();

    /// <summary>
    /// Wires an optional immutable-flag check callback. When set, ChangeTrackerCore.SetFlag
    /// calls this before allowing a true→false transition. If the callback returns true,
    /// the SetFlag(false) is rejected with LogWarning.
    ///
    /// Pass null to remove the check (for test teardown).
    /// </summary>
    void SetImmutableFlagCheck(System.Func<string, bool> isImmutableFunc);
}
