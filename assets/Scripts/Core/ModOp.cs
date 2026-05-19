/// <summary>
/// Operation type for ModifyTagWeight overlay (ADR-0007).
///
/// Determines how a tag weight overlay modifies the base weight:
///   Add      → baseWeight + value
///   Multiply → baseWeight * value
///   Set      → value (replaces base entirely)
/// </summary>
public enum ModOp
{
    Add,
    Multiply,
    Set
}
