using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract base class for all content change types (ADR-0007 Category 6).
///
/// ContentChanges are the mechanism by which player choices modify fragment state.
/// They are stored on ChoiceOption.ContentChanges and applied via ChangeTracker.ApplyChanges()
/// to populate the _overlay Dictionary.
///
/// All 6 subclasses use [Serializable] for Unity serialization and must be listed
/// in link.xml for IL2CPP AOT preservation (ADR-0007).
///
/// Cross-fragment targeting: TargetFragmentId can reference any fragment in the same
/// chapter. Cross-chapter changes are indirect (via SetFlag + ConditionGroup).
/// </summary>
[Serializable]
public abstract class ContentChange
{
    /// <summary>The fragment this change targets. May differ from the fragment that defines the choice.</summary>
    [field: SerializeField]
    public string TargetFragmentId { get; private set; }

    /// <summary>Human-readable change type identifier (for editor display only).</summary>
    public virtual string ChangeType => GetType().Name;

    protected ContentChange() { }
    protected ContentChange(string targetFragmentId) => TargetFragmentId = targetFragmentId;
}

// ---------------------------------------------------------------------------
// 6 ContentChange Subclasses (GDD memory-fragment-data-model §Rule 3, Category 6)
// ---------------------------------------------------------------------------

/// <summary>
/// Toggles a VisualLayer on/off on the target fragment.
/// If the layer's IsMutable is false, the overlay system logs a warning and skips the change.
/// </summary>
[Serializable]
public class ToggleVisualLayer : ContentChange
{
    [field: SerializeField] public string LayerId { get; private set; }
    [field: SerializeField] public bool Visible { get; private set; }

    public ToggleVisualLayer() { }
    public ToggleVisualLayer(string targetFragmentId, string layerId, bool visible)
        : base(targetFragmentId)
    {
        LayerId = layerId;
        Visible = visible;
    }
}

/// <summary>
/// Changes an InteractiveObject's state (Active / Hidden / Disabled) on the target fragment.
/// </summary>
[Serializable]
public class SetObjectState : ContentChange
{
    [field: SerializeField] public string ObjectId { get; private set; }
    [field: SerializeField] public ObjectState NewState { get; private set; }

    public SetObjectState() { }
    public SetObjectState(string targetFragmentId, string objectId, ObjectState newState)
        : base(targetFragmentId)
    {
        ObjectId = objectId;
        NewState = newState;
    }
}

/// <summary>
/// Replaces a text field's content on the target fragment.
/// TextFieldId identifies the text slot; NewText is the replacement string.
/// Uses string for now — will become TableReference once com.unity.localization is configured.
/// </summary>
[Serializable]
public class SetTextContent : ContentChange
{
    [field: SerializeField] public string TextFieldId { get; private set; }

    /// <summary>
    /// Replacement text. Uses string until com.unity.localization package is installed,
    /// then migrate to TableReference. LocalizationManager (#4) is ready.
    /// </summary>
    [field: SerializeField] public string NewText { get; private set; }

    public SetTextContent() { }
    public SetTextContent(string targetFragmentId, string textFieldId, string newText)
        : base(targetFragmentId)
    {
        TextFieldId = textFieldId;
        NewText = newText;
    }
}

/// <summary>
/// Modifies an emotional tag's weight on the target fragment.
/// Operation determines how Delta is applied: Add, Multiply, or Set.
/// The resolved weight is clamped to [0.0, 1.0] after the operation.
/// </summary>
[Serializable]
public class ModifyTagWeight : ContentChange
{
    [field: SerializeField] public string TagId { get; private set; }

    /// <summary>Delta value [-1.0, 1.0]. Meaning depends on Operation.</summary>
    [field: SerializeField] public float Delta { get; private set; }

    /// <summary>How Delta is applied to the tag's current weight.</summary>
    [field: SerializeField] public WeightOperation Operation { get; private set; }

    public ModifyTagWeight() { }
    public ModifyTagWeight(string targetFragmentId, string tagId, float delta,
        WeightOperation operation = WeightOperation.Add)
        : base(targetFragmentId)
    {
        TagId = tagId;
        Delta = delta;
        Operation = operation;
    }
}

/// <summary>How ModifyTagWeight.Delta is applied to a tag's current weight.</summary>
public enum WeightOperation
{
    /// <summary>newWeight = currentWeight + Delta, clamped to [0, 1].</summary>
    Add,

    /// <summary>newWeight = currentWeight * Delta, clamped to [0, 1].</summary>
    Multiply,

    /// <summary>newWeight = Delta (clamped directly to [0, 1]). Ignores current weight.</summary>
    Set
}

/// <summary>
/// Reveals a hidden association between two fragments.
/// The association is always bidirectional — both fragments gain the link.
/// </summary>
[Serializable]
public class UnlockAssociation : ContentChange
{
    /// <summary>The fragment to create a bidirectional association with.</summary>
    [field: SerializeField] public string AssociationTargetId { get; private set; }

    public UnlockAssociation() { }
    public UnlockAssociation(string targetFragmentId, string associationTargetId)
        : base(targetFragmentId)
    {
        AssociationTargetId = associationTargetId;
    }
}

/// <summary>
/// Sets a global narrative flag to a boolean value.
/// Flags are cross-chapter persistent — SetFlag is the indirect mechanism for
/// cross-chapter state propagation (ADR-0011).
/// </summary>
[Serializable]
public class SetFlag : ContentChange
{
    /// <summary>Globally unique flag identifier.</summary>
    [field: SerializeField] public string FlagId { get; private set; }

    /// <summary>The boolean value to set.</summary>
    [field: SerializeField] public bool Value { get; private set; }

    public SetFlag() { }
    public SetFlag(string flagId, bool value)
    {
        FlagId = flagId;
        Value = value;
    }

    // Note: SetFlag does not use TargetFragmentId — it operates globally.
}
