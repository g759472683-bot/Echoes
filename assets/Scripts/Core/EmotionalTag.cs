using System;
using UnityEngine;

/// <summary>
/// Emotional tag attached to a MemoryFragment (ADR-0007 Category 4).
///
/// TagId references the emotional tag vocabulary defined by the Emotional Tag System (#10).
/// BaseWeight is mutable (wet ink) — player choices can modify it via ModifyTagWeight ContentChange.
/// IsPrimary marks the fragment's dominant emotional tone (used by the web association engine for rhythm pacing).
/// </summary>
[Serializable]
public struct EmotionalTag
{
    /// <summary>Tag identifier from the emotional tag vocabulary (e.g., "sorrow", "hope", "fear").</summary>
    [field: SerializeField]
    public string TagId { get; private set; }

    /// <summary>
    /// Default weight [0.0, 1.0] — wet ink.
    /// Player choices can modify this via ModifyTagWeight ContentChange.
    /// </summary>
    [field: SerializeField]
    public float BaseWeight { get; private set; }

    /// <summary>
    /// Whether this is the fragment's primary emotional tag.
    /// Used by the web association engine (ADR-0009) for emotional rhythm pacing.
    /// Default false. Only one tag per fragment should be marked IsPrimary.
    /// </summary>
    [field: SerializeField]
    public bool IsPrimary { get; private set; }

    public EmotionalTag(string tagId, float baseWeight, bool isPrimary = false)
    {
        TagId = tagId;
        BaseWeight = Mathf.Clamp01(baseWeight);
        IsPrimary = isPrimary;
    }
}
