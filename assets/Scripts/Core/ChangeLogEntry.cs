using System;

/// <summary>
/// A single append-only entry in ChangeTracker._changeLog (ADR-0007).
///
/// Records every ApplyChanges invocation for audit/debug purposes.
/// Log is never cleared — it grows monotonically across the play session.
/// Not persisted to save data (Story 004 handles persistence).
/// </summary>
[Serializable]
public struct ChangeLogEntry
{
    /// <summary>UTC timestamp of the change application.</summary>
    public DateTime Timestamp;

    /// <summary>The fragment this change targets.</summary>
    public string TargetFragmentId;

    /// <summary>The choice that triggered this change.</summary>
    public string ChoiceId;

    /// <summary>Number of ContentChange items in the original batch.</summary>
    public int InputChangeCount;

    /// <summary>Number of valid changes actually applied.</summary>
    public int AppliedChangeCount;

    /// <summary>OverlayVersion after this change was applied.</summary>
    public int ResultingOverlayVersion;

    public ChangeLogEntry(
        DateTime timestamp,
        string targetFragmentId,
        string choiceId,
        int inputChangeCount,
        int appliedChangeCount,
        int resultingOverlayVersion)
    {
        Timestamp = timestamp;
        TargetFragmentId = targetFragmentId;
        ChoiceId = choiceId;
        InputChangeCount = inputChangeCount;
        AppliedChangeCount = appliedChangeCount;
        ResultingOverlayVersion = resultingOverlayVersion;
    }
}
