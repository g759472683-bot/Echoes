using System.Collections.Generic;

/// <summary>
/// Immutable snapshot of a fragment's resolved state after applying all overlays (ADR-0007).
///
/// Created by ChangeTrackerCore.GetCurrentState(). Consumers receive a copy —
/// modifications to this struct do NOT propagate back to the base SO or overlays.
///
/// All fields are read-only. Constructed via Builder pattern in GetCurrentState.
/// </summary>
public readonly struct ResolvedFragmentState
{
    /// <summary>The fragment this snapshot represents.</summary>
    public readonly string FragmentId;

    /// <summary>Resolved visual layer visibility after applying ToggleVisualLayer overlays.</summary>
    public readonly IReadOnlyList<ResolvedLayerState> VisualLayers;

    /// <summary>Resolved emotional tag weights after applying ModifyTagWeight overlays, clamped [0, 1].</summary>
    public readonly IReadOnlyList<ResolvedTagWeight> TagWeights;

    /// <summary>Resolved interactive object states after applying SetObjectState overlays.</summary>
    public readonly IReadOnlyList<ResolvedObjectStateEntry> ObjectStates;

    /// <summary>Resolved text content overrides from SetTextContent (only modified fields).</summary>
    public readonly IReadOnlyDictionary<string, string> TextContents;

    /// <summary>Association targets unlocked by UnlockAssociation overlays (set union, no duplicates).</summary>
    public readonly IReadOnlyCollection<string> UnlockedAssociations;

    public ResolvedFragmentState(
        string fragmentId,
        IReadOnlyList<ResolvedLayerState> visualLayers,
        IReadOnlyList<ResolvedTagWeight> tagWeights,
        IReadOnlyList<ResolvedObjectStateEntry> objectStates,
        IReadOnlyDictionary<string, string> textContents,
        IReadOnlyCollection<string> unlockedAssociations)
    {
        FragmentId = fragmentId;
        VisualLayers = visualLayers;
        TagWeights = tagWeights;
        ObjectStates = objectStates;
        TextContents = textContents;
        UnlockedAssociations = unlockedAssociations;
    }
}

// =========================================================================
// Sub-types
// =========================================================================

/// <summary>Resolved visibility state for a single visual layer.</summary>
public readonly struct ResolvedLayerState
{
    public readonly string LayerId;
    public readonly bool Visible;

    public ResolvedLayerState(string layerId, bool visible)
    {
        LayerId = layerId;
        Visible = visible;
    }
}

/// <summary>Resolved emotional tag weight (clamped [0.0, 1.0]).</summary>
public readonly struct ResolvedTagWeight
{
    public readonly string TagId;
    public readonly float Weight;

    public ResolvedTagWeight(string tagId, float weight)
    {
        TagId = tagId;
        Weight = weight;
    }
}

/// <summary>Resolved interactive object state.</summary>
public readonly struct ResolvedObjectStateEntry
{
    public readonly string ObjectId;
    public readonly ObjectState State;

    public ResolvedObjectStateEntry(string objectId, ObjectState state)
    {
        ObjectId = objectId;
        State = state;
    }
}
