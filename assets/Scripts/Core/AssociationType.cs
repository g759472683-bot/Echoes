/// <summary>
/// Classification for explicit fragment-to-fragment associations (ADR-0007 Category 7).
/// Used by the web association engine (ADR-0009) to weight and categorize association edges.
/// </summary>
public enum AssociationType
{
    /// <summary>Shared theme or motif (e.g., both fragments reference "loss").</summary>
    Thematic,

    /// <summary>Direct narrative connection (e.g., character appears in both).</summary>
    Narrative,

    /// <summary>Visual similarity (e.g., same color palette, composition).</summary>
    Visual,

    /// <summary>Shared emotional tone (e.g., both fragments carry "sorrow" tag).</summary>
    Emotional,

    /// <summary>Cause-and-effect relationship (e.g., choice in A unlocks B).</summary>
    Causal
}
