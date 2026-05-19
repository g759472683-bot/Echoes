using System;
using System.Collections.Generic;

/// <summary>
/// Accumulated content overrides for a single (fragmentId, choiceId) key (ADR-0007).
///
/// Stored in ChangeTracker._overlay Dictionary. Each field groups one of the
/// 6 ContentChange types. Lists are null when no changes of that type exist,
/// reducing serialized size for typical cases (1-3 changes per choice).
///
/// All entry types are [Serializable] for Unity serialization and must be
/// listed in link.xml for IL2CPP AOT preservation.
/// </summary>
[Serializable]
public struct ContentOverrides
{
    /// <summary>Visual layer toggles applied by this choice.</summary>
    public List<ToggleLayerEntry> ToggledLayers;

    /// <summary>Interactive object state changes applied by this choice.</summary>
    public List<ObjectStateEntry> ObjectStates;

    /// <summary>Text content replacements applied by this choice.</summary>
    public List<TextOverrideEntry> TextOverrides;

    /// <summary>Emotional tag weight modifications applied by this choice.</summary>
    public List<TagWeightModEntry> TagWeightMods;

    /// <summary>Association targets unlocked by this choice.</summary>
    public List<string> UnlockedAssociations;

    /// <summary>Global narrative flags set by this choice.</summary>
    public List<FlagSetEntry> SetFlags;

    /// <summary>
    /// Application order index. Set from ChangeTrackerCore.OverlayVersion at apply time.
    /// Used by GetCurrentState to apply overlays in chronological order.
    /// </summary>
    public int OrderIndex;

    /// <summary>Total number of changes across all categories.</summary>
    public int TotalChanges =>
        (ToggledLayers?.Count ?? 0) +
        (ObjectStates?.Count ?? 0) +
        (TextOverrides?.Count ?? 0) +
        (TagWeightMods?.Count ?? 0) +
        (UnlockedAssociations?.Count ?? 0) +
        (SetFlags?.Count ?? 0);
}

// =========================================================================
// Entry Types
// =========================================================================

/// <summary>A single visual layer toggle stored in ContentOverrides.</summary>
[Serializable]
public struct ToggleLayerEntry
{
    public string LayerId;
    public bool Visible;

    public ToggleLayerEntry(string layerId, bool visible)
    {
        LayerId = layerId;
        Visible = visible;
    }
}

/// <summary>A single object state change stored in ContentOverrides.</summary>
[Serializable]
public struct ObjectStateEntry
{
    public string ObjectId;
    public ObjectState NewState;

    public ObjectStateEntry(string objectId, ObjectState newState)
    {
        ObjectId = objectId;
        NewState = newState;
    }
}

/// <summary>A single text override stored in ContentOverrides.</summary>
[Serializable]
public struct TextOverrideEntry
{
    public string TextFieldId;
    public string NewText;

    public TextOverrideEntry(string textFieldId, string newText)
    {
        TextFieldId = textFieldId;
        NewText = newText;
    }
}

/// <summary>A single tag weight modification stored in ContentOverrides.</summary>
[Serializable]
public struct TagWeightModEntry
{
    public string TagId;
    public ModOp Operation;
    public float Value;

    /// <summary>Per-entry order for sequential tag weight resolution.</summary>
    public int OrderIndex;

    public TagWeightModEntry(string tagId, ModOp operation, float value, int orderIndex = 0)
    {
        TagId = tagId;
        Operation = operation;
        Value = value;
        OrderIndex = orderIndex;
    }
}

/// <summary>A single global flag assignment stored in ContentOverrides.</summary>
[Serializable]
public struct FlagSetEntry
{
    public string FlagId;
    public bool Value;

    public FlagSetEntry(string flagId, bool value)
    {
        FlagId = flagId;
        Value = value;
    }
}
