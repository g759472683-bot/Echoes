using System;

/// <summary>
/// Explicitly defined association from this fragment to another (ADR-0007 Category 7).
///
/// Associations feed into the web association engine (ADR-0009) as explicit edges
/// in the fragment graph. Each association carries a BaseWeight (wet ink — player
/// choices can modify it) and an optional VisibilityCondition that hides the
/// association until the condition is met.
///
/// If IsBidirectional is true, the target fragment implicitly gains a reverse
/// association back to this fragment.
/// </summary>
[Serializable]
public struct FragmentAssociation
{
    /// <summary>The target fragment this association points to.</summary>
    [field: UnityEngine.SerializeField]
    public string TargetFragmentId { get; private set; }

    /// <summary>
    /// Classification of this association's type (Thematic, Narrative, Visual, Emotional, Causal).
    /// </summary>
    [field: UnityEngine.SerializeField]
    public AssociationType AssociationType { get; private set; }

    /// <summary>
    /// Default edge weight [0.0, 1.0] — wet ink.
    /// Feeds into the web association engine's four-factor formula.
    /// </summary>
    [field: UnityEngine.SerializeField]
    public float BaseWeight { get; private set; }

    /// <summary>
    /// If true, the target fragment implicitly gains a reverse association
    /// back to this fragment with the same BaseWeight.
    /// </summary>
    [field: UnityEngine.SerializeField]
    public bool IsBidirectional { get; private set; }

    /// <summary>
    /// Optional condition controlling when this association becomes visible.
    /// Hidden associations do not appear in the association web for the player.
    /// </summary>
    [field: UnityEngine.SerializeField]
    public ConditionGroup VisibilityCondition { get; private set; }

    /// <summary>Designer notes — not used at runtime.</summary>
    [field: UnityEngine.SerializeField]
    public string Description { get; private set; }

    public FragmentAssociation(string targetFragmentId, AssociationType associationType,
        float baseWeight, bool isBidirectional = false,
        ConditionGroup visibilityCondition = null, string description = null)
    {
        TargetFragmentId = targetFragmentId;
        AssociationType = associationType;
        // Clamp to [-1.0, 1.0] range (ADR-0007).
        // -1.0f is the designer exclusion sentinel used by the web association engine (ADR-0009).
        BaseWeight = Math.Max(-1.0f, Math.Min(1.0f, baseWeight));
        IsBidirectional = isBidirectional;
        VisibilityCondition = visibilityCondition;
        Description = description;
    }
}
