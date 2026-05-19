using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-only stub for the ConditionGroup composite pattern (ADR-0008).
///
/// Defines the shape of condition data stored on InteractiveObjects, Choices,
/// and EndingTriggers. Evaluation logic (Evaluate) is implemented in Story 002
/// of the memory-fragment epic — this story only defines the serialized schema.
///
/// Max nesting depth: 3 (enforced by editor validation, Story 003).
/// </summary>
[Serializable]
public class ConditionGroup
{
    /// <summary>Logical combinator for child conditions.</summary>
    [field: SerializeField]
    public ConditionCombinator Combinator { get; private set; }

    /// <summary>
    /// Leaf conditions and/or nested ConditionGroups.
    /// Use [SerializeReference] for polymorphic serialization (ADR-0007).
    /// </summary>
    [SerializeReference]
    [field: SerializeField]
    public List<Condition> Conditions { get; private set; }

    public ConditionGroup()
    {
        Combinator = ConditionCombinator.All;
        Conditions = new List<Condition>();
    }

    public ConditionGroup(ConditionCombinator combinator, List<Condition> conditions)
    {
        Combinator = combinator;
        Conditions = conditions ?? new List<Condition>();
    }

    /// <summary>
    /// Evaluates all conditions in this group using the configured combinator.
    /// Short-circuits: All stops on first false, Any stops on first true.
    /// </summary>
    public bool Evaluate(IChangeTracker ctx)
    {
        if (Conditions == null || Conditions.Count == 0)
            return true; // empty = vacuously satisfied

        return Combinator switch
        {
            ConditionCombinator.All => EvaluateAll(ctx),
            ConditionCombinator.Any => EvaluateAny(ctx),
            ConditionCombinator.Not => EvaluateNot(ctx),
            _ => false
        };
    }

    private bool EvaluateAll(IChangeTracker ctx)
    {
        foreach (var c in Conditions)
            if (!c.Evaluate(ctx)) return false;
        return true;
    }

    private bool EvaluateAny(IChangeTracker ctx)
    {
        foreach (var c in Conditions)
            if (c.Evaluate(ctx)) return true;
        return false;
    }

    private bool EvaluateNot(IChangeTracker ctx)
    {
        // Not combinator: invert the first condition
        return Conditions.Count > 0 && !Conditions[0].Evaluate(ctx);
    }
}

/// <summary>
/// Logical combinator for ConditionGroup evaluation.
/// </summary>
public enum ConditionCombinator
{
    /// <summary>All child conditions must be satisfied.</summary>
    All,

    /// <summary>At least one child condition must be satisfied.</summary>
    Any,

    /// <summary>The child condition must NOT be satisfied (single child only).</summary>
    Not
}
