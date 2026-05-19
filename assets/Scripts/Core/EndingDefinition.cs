using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-authored ending configuration stored in ChapterDefinition.Endings[] (ADR-0010).
///
/// Each chapter has 2-5 ending definitions. The multi-ending system evaluates
/// all ending definitions when ResolveEnding is called, applies the three-stage
/// algorithm (collect triggers → IsEssential gate → score accumulation →
/// threshold check → tie-breaking), and returns the winning ResolvedEnding.
///
/// Exactly one ending per chapter must have IsDefault=true, MinimumScore=0.0.
/// </summary>
[Serializable]
public class EndingDefinition
{
    /// <summary>Globally unique ending identifier (e.g., "ch01_ending_sad").</summary>
    [field: SerializeField]
    public string EndingId { get; private set; }

    /// <summary>ChapterEnding (per-chapter) or HiddenEnding (cross-chapter rare).</summary>
    [field: SerializeField]
    public EndingType EndingType { get; private set; }

    /// <summary>The chapter this ending is resolved in.</summary>
    [field: SerializeField]
    public string ChapterId { get; private set; }

    /// <summary>Minimum accumulated score [0.0, 1.0] required to trigger this ending.</summary>
    [field: SerializeField]
    public float MinimumScore { get; private set; }

    /// <summary>
    /// If true, this is the chapter's fallback ending — triggered when no other
    /// ending qualifies. Must have MinimumScore=0.0. Exactly one per chapter.
    /// </summary>
    [field: SerializeField]
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Optional dominant emotional category (e.g., "Sadness").
    /// When set and matching dominantPathEmotion, applies the path bonus
    /// multiplier (MVP default pathBonusWeight=0.0 — hook implemented but disabled).
    /// </summary>
    [field: SerializeField]
    public string EmotionalAffinity { get; private set; }

    /// <summary>Localized display name key for the ending screen/gallery.</summary>
    [field: SerializeField]
    public string DisplayNameKey { get; private set; }

    public EndingDefinition() { }

    public EndingDefinition(string endingId, EndingType endingType, string chapterId,
        float minimumScore, bool isDefault, string emotionalAffinity = null,
        string displayNameKey = null)
    {
        EndingId = endingId;
        EndingType = endingType;
        ChapterId = chapterId;
        MinimumScore = Mathf.Clamp(minimumScore, 0f, 1f);
        IsDefault = isDefault;
        EmotionalAffinity = emotionalAffinity;
        DisplayNameKey = displayNameKey;
    }
}

/// <summary>
/// Classification of ending scope (ADR-0010, GDD Rule 1).
/// </summary>
public enum EndingType
{
    /// <summary>Standard ending resolved when a chapter completes.</summary>
    ChapterEnding,

    /// <summary>Rare ending requiring cross-chapter conditions.</summary>
    HiddenEnding
}
