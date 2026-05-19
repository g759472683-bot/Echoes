using System;
using UnityEngine;

/// <summary>
/// Ending trigger condition defined on a MemoryFragment (ADR-0007 Category 8).
///
/// Fragments contribute ending triggers that the multi-ending system (ADR-0010)
/// collects and evaluates. Each trigger specifies an ending, a condition that
/// must be met, a contribution weight, and whether it is essential.
///
/// Ending resolution logic (collect → gate → accumulate → threshold → tie-break)
/// is owned by the multi-ending system (#14) — fragments only define the conditions.
/// </summary>
[Serializable]
public struct EndingTrigger
{
    /// <summary>Identifier for the ending this trigger contributes to.</summary>
    [field: SerializeField]
    public string EndingId { get; private set; }

    /// <summary>
    /// Condition that must be met for this trigger to fire.
    /// Example: ChoiceMade("ch1_frag_07", "keep_letter") AND ChapterCompleted("ch1").
    /// </summary>
    [field: SerializeField]
    public ConditionGroup TriggerCondition { get; private set; }

    /// <summary>
    /// Contribution weight [0.0, 1.0] — accumulated during ending resolution.
    /// Higher weights make this trigger more influential in the ending calculation.
    /// </summary>
    [field: SerializeField]
    public float ContributionWeight { get; private set; }

    /// <summary>
    /// If true, this trigger is essential — the ending cannot fire without it.
    /// Non-essential triggers add weight but are not gating conditions.
    /// </summary>
    [field: SerializeField]
    public bool IsEssential { get; private set; }

    public EndingTrigger(string endingId, ConditionGroup triggerCondition,
        float contributionWeight, bool isEssential)
    {
        EndingId = endingId;
        TriggerCondition = triggerCondition;
        ContributionWeight = Mathf.Clamp01(contributionWeight);
        IsEssential = isEssential;
    }
}
