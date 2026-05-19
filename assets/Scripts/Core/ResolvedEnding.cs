using System;
using System.Collections.Generic;

/// <summary>
/// Result of a multi-ending resolution (ADR-0010, GDD Rule 11).
///
/// Returned by MultiEndingSystem.ResolveEnding(). Carries the winning ending's
/// identity, score, and metadata for downstream consumers (ending presentation,
/// gallery, achievements).
/// </summary>
public struct ResolvedEnding
{
    /// <summary>The winning ending's ID.</summary>
    public string EndingId;

    /// <summary>ChapterEnding or HiddenEnding.</summary>
    public EndingType EndingType;

    /// <summary>Final score after accumulation and path bonus.</summary>
    public float Score;

    /// <summary>True if the fallback default ending was used.</summary>
    public bool IsDefault;

    /// <summary>True if this EndingId is newly unlocked (not in UnlockedEndingIds).</summary>
    public bool IsNewUnlock;

    /// <summary>
    /// All endings that met their MinimumScore threshold during this resolution.
    /// Includes the winner. Useful for debugging and gallery previews.
    /// </summary>
    public List<(string EndingId, float Score)> QualifiedEndings;

    /// <summary>
    /// The dominant emotional category across all visited fragments in the chapter.
    /// Computed by ComputeDominantPathEmotion. Null if unavailable.
    /// </summary>
    public string DominantPathEmotion;

    public ResolvedEnding(string endingId, EndingType endingType, float score,
        bool isDefault, bool isNewUnlock,
        List<(string, float)> qualifiedEndings = null,
        string dominantPathEmotion = null)
    {
        EndingId = endingId;
        EndingType = endingType;
        Score = score;
        IsDefault = isDefault;
        IsNewUnlock = isNewUnlock;
        QualifiedEndings = qualifiedEndings ?? new List<(string, float)>();
        DominantPathEmotion = dominantPathEmotion;
    }
}

/// <summary>
/// Save/load container for the multi-ending system's persistent state (ADR-0010).
///
/// Only UnlockedEndingIds is persisted — the resolution itself is re-evaluated
/// each time ResolveEnding is called (no caching).
/// </summary>
[Serializable]
public struct MultiEndingSaveData
{
    /// <summary>All ending IDs the player has ever unlocked (union semantics).</summary>
    public string[] UnlockedEndingIds;
}
