using System;
using System.Collections.Generic;

/// <summary>
/// Serialization container for ChangeTrackerCore state (ADR-0007 S004).
///
/// Persists the mutable overlay layer, global flags, and tracking sets
/// so game state can be saved and restored. Base SO data is NOT serialized —
/// it is reloaded from Addressables on game start.
///
/// Serialized with System.Text.Json by SaveManager (#7).
/// Inner ContentOverrides uses Unity JsonUtility ([Serializable]).
/// </summary>
[Serializable]
public struct ChangeTrackerSaveData
{
    /// <summary>Serialized _overlay entries (fragmentId + choiceId → ContentOverrides).</summary>
    public List<OverlayEntry> OverlayEntries;

    /// <summary>Serialized _flags entries.</summary>
    public List<FlagEntry> Flags;

    /// <summary>IDs of fragments the player has visited.</summary>
    public string[] VisitedFragments;

    /// <summary>IDs of chapters the player has completed.</summary>
    public string[] CompletedChapters;

    /// <summary>Current overlay version counter.</summary>
    public int OverlayVersion;

    /// <summary>Returns true if this save data has any content (non-empty overlay or flags).</summary>
    public bool IsEmpty =>
        (OverlayEntries == null || OverlayEntries.Count == 0) &&
        (Flags == null || Flags.Count == 0) &&
        (VisitedFragments == null || VisitedFragments.Length == 0) &&
        (CompletedChapters == null || CompletedChapters.Length == 0);
}

/// <summary>
/// A single serialized overlay entry — flattens the (fragmentId, choiceId) Dictionary key.
/// </summary>
[Serializable]
public struct OverlayEntry
{
    /// <summary>The fragment this overlay targets.</summary>
    public string TargetFragmentId;

    /// <summary>The choice that produced this overlay.</summary>
    public string ChoiceId;

    /// <summary>The content overrides applied by this choice.</summary>
    public ContentOverrides Overrides;
}

/// <summary>
/// A single serialized flag entry (flagId → bool).
/// </summary>
[Serializable]
public struct FlagEntry
{
    /// <summary>The global narrative flag identifier.</summary>
    public string FlagId;

    /// <summary>The flag's boolean value.</summary>
    public bool Value;

    public FlagEntry(string flagId, bool value)
    {
        FlagId = flagId;
        Value = value;
    }
}
