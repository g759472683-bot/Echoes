/// <summary>
/// A single weight modification overlay entry (ADR-0007 S003).
///
/// Applied to a specific tag on a specific fragment. Multiple overlays
/// for the same tag are applied in OrderIndex ascending order.
///
/// Stored in ChangeTracker._overlay dictionary and serialized to save data.
/// </summary>
public readonly struct TagWeightOverlay
{
    /// <summary>The tag this overlay modifies.</summary>
    public readonly string TagId;

    /// <summary>The math operation to apply.</summary>
    public readonly ModOp Operation;

    /// <summary>The operand value for the operation.</summary>
    public readonly float Value;

    /// <summary>
    /// Application order (ascending). When multiple overlays affect
    /// the same tag, lower OrderIndex values are applied first.
    /// </summary>
    public readonly int OrderIndex;

    public TagWeightOverlay(string tagId, ModOp operation, float value, int orderIndex = 0)
    {
        TagId = tagId;
        Operation = operation;
        Value = value;
        OrderIndex = orderIndex;
    }
}
