using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A group of choices presented to the player when a PresentChoice interaction fires
/// (ADR-0007 Category 5).
///
/// Stored on <see cref="MemoryFragment"/> and looked up by GroupId at interaction time.
/// Each ChoiceGroup contains one or more Choice options. The player can select up to
/// MaxSelections choices (MVP: always 1).
/// </summary>
[Serializable]
public class ChoiceGroup
{
    /// <summary>Unique identifier for this choice group within its fragment.</summary>
    [field: SerializeField]
    public string GroupId { get; set; }

    /// <summary>
    /// Localized prompt text displayed above the choices.
    /// Uses string until com.unity.localization package is installed,
    /// then migrate to TableReference. LocalizationManager (#4) is ready.
    /// </summary>
    [field: SerializeField]
    public string GroupLabel { get; set; }

    /// <summary>The available choices in this group.</summary>
    public Choice[] Choices;

    /// <summary>Maximum number of selections allowed (MVP: 1 for single-choice).</summary>
    public int MaxSelections;

    public ChoiceGroup()
    {
        MaxSelections = 1;
        Choices = Array.Empty<Choice>();
    }
}

/// <summary>
/// A single selectable option within a <see cref="ChoiceGroup"/> (ADR-0007 Category 5).
///
/// Each Choice carries display text, an optional availability condition,
/// and a list of ContentChanges that are applied when the choice is selected.
///
/// OnSelect provides backward-compatible immediate-action dispatch (existing
/// InteractionManager flow). ContentChanges provide the ADR-0007 overlay-based
/// state modification path used by ChangeTracker.
/// </summary>
[Serializable]
public class Choice
{
    /// <summary>Unique identifier for this choice within its group.</summary>
    public string ChoiceId;

    /// <summary>
    /// Display text for the choice button (localized key or raw string).
    /// Uses string until com.unity.localization package is installed,
    /// then migrate to TableReference. LocalizationManager (#4) is ready.
    /// </summary>
    public string Text;

    /// <summary>
    /// Optional condition controlling when this choice is available.
    /// If null or ConditionAlways, the choice is always selectable.
    /// </summary>
    [SerializeReference]
    public ConditionGroup ChoiceCondition;

    /// <summary>Whether this choice can be selected more than once. Default false.</summary>
    public bool IsRepeatable;

    /// <summary>
    /// Content changes applied when this choice is selected (ADR-0007 overlay path).
    /// Applied via ChangeTracker.ApplyChanges() to populate the _overlay Dictionary.
    /// </summary>
    [SerializeReference]
    public List<ContentChange> ContentChanges;

    /// <summary>
    /// [Backward compat] Immediate action dispatched when this choice is selected.
    /// Existing InteractionManager.DispatchInteractionResult uses this for
    /// PlayAnimation / ShowText / TransitionToFragment / RevealObject dispatch.
    /// </summary>
    public InteractionResult OnSelect;

    public Choice()
    {
        IsRepeatable = false;
        ContentChanges = new List<ContentChange>();
    }
}
