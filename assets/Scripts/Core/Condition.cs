using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Abstract base class for all conditions in the unified condition system (ADR-0008).
///
/// Subclasses define the condition type and its parameters. Evaluate() is a pure
/// function — same inputs always produce same outputs, no side effects.
///
/// All subclasses must be listed in link.xml for IL2CPP AOT preservation (ADR-0007).
/// </summary>
[Serializable]
public abstract class Condition
{
    /// <summary>Human-readable condition type identifier (for editor display only).</summary>
    public virtual string ConditionType => GetType().Name;

    /// <summary>
    /// Evaluates this condition against the provided context.
    /// Pure function — does not modify ctx state.
    /// </summary>
    public abstract bool Evaluate(IChangeTracker ctx);
}

// ---------------------------------------------------------------------------
// 6 Leaf Conditions (GDD memory-fragment-data-model §Rule 5)
// ---------------------------------------------------------------------------

/// <summary>Always satisfied — used as a default/placeholder condition.</summary>
[Serializable]
public class ConditionAlways : Condition
{
    public override bool Evaluate(IChangeTracker ctx) => true;
}

/// <summary>
/// Satisfied when the player has made a specific choice in a specific fragment.
/// </summary>
[Serializable]
public class ConditionChoiceMade : Condition
{
    [field: SerializeField] public string FragmentId { get; private set; }
    [field: SerializeField] public string ChoiceId { get; private set; }

    public ConditionChoiceMade() { }
    public ConditionChoiceMade(string fragmentId, string choiceId)
    {
        FragmentId = fragmentId;
        ChoiceId = choiceId;
    }

    public override bool Evaluate(IChangeTracker ctx) => ctx.HasChoiceMade(FragmentId, ChoiceId);
}

/// <summary>
/// Satisfied when a global flag matches the expected boolean value.
/// </summary>
[Serializable]
public class ConditionFlagSet : Condition
{
    [field: SerializeField] public string FlagId { get; private set; }
    [field: SerializeField] public bool Value { get; private set; }

    public ConditionFlagSet() { }
    public ConditionFlagSet(string flagId, bool value)
    {
        FlagId = flagId;
        Value = value;
    }

    public override bool Evaluate(IChangeTracker ctx) => ctx.GetFlag(FlagId) == Value;
}

/// <summary>
/// Satisfied when a specific object on a specific fragment is in the given state.
/// </summary>
[Serializable]
public class ConditionObjectStateIs : Condition
{
    [field: SerializeField] public string FragmentId { get; private set; }
    [field: SerializeField] public string ObjectId { get; private set; }
    [field: SerializeField] public ObjectState State { get; private set; }

    public ConditionObjectStateIs() { }
    public ConditionObjectStateIs(string fragmentId, string objectId, ObjectState state)
    {
        FragmentId = fragmentId;
        ObjectId = objectId;
        State = state;
    }

    public override bool Evaluate(IChangeTracker ctx) => ctx.GetObjectState(FragmentId, ObjectId) == State;
}

/// <summary>
/// Satisfied when the player has visited the specified fragment at least once.
/// </summary>
[Serializable]
public class ConditionVisitedFragment : Condition
{
    [field: SerializeField] public string FragmentId { get; private set; }

    public ConditionVisitedFragment() { }
    public ConditionVisitedFragment(string fragmentId) => FragmentId = fragmentId;

    public override bool Evaluate(IChangeTracker ctx) => ctx.HasVisited(FragmentId);
}

/// <summary>
/// Satisfied when the specified chapter has been completed.
/// </summary>
[Serializable]
public class ConditionChapterCompleted : Condition
{
    [field: SerializeField] public string ChapterId { get; private set; }

    public ConditionChapterCompleted() { }
    public ConditionChapterCompleted(string chapterId) => ChapterId = chapterId;

    public override bool Evaluate(IChangeTracker ctx) => ctx.IsChapterCompleted(ChapterId);
}

/// <summary>
/// Satisfied when an emotional tag's current weight meets a threshold comparison.
/// The weight is the resolved value after all overlay modifications are applied.
/// </summary>
[Serializable]
public class ConditionTagWeight : Condition
{
    [field: SerializeField] public string TagId { get; private set; }
    [field: SerializeField] public float Threshold { get; private set; }

    /// <summary>Comparison operator for the threshold check.</summary>
    [field: SerializeField] public WeightComparison Comparison { get; private set; }

    public ConditionTagWeight() { }
    public ConditionTagWeight(string tagId, float threshold, WeightComparison comparison = WeightComparison.GreaterOrEqual)
    {
        TagId = tagId;
        Threshold = threshold;
        Comparison = comparison;
    }

    public override bool Evaluate(IChangeTracker ctx)
    {
        var weight = ctx.GetTagWeight(TagId);
        return Comparison switch
        {
            WeightComparison.GreaterOrEqual => weight >= Threshold,
            WeightComparison.LessOrEqual => weight <= Threshold,
            WeightComparison.Greater => weight > Threshold,
            WeightComparison.Less => weight < Threshold,
            WeightComparison.Equal => Math.Abs(weight - Threshold) < 0.001f,
            _ => false
        };
    }
}

/// <summary>Comparison operators for ConditionTagWeight threshold checks.</summary>
public enum WeightComparison
{
    GreaterOrEqual,
    LessOrEqual,
    Greater,
    Less,
    Equal
}

// ---------------------------------------------------------------------------
// 3 Combinator Conditions (ADR-0008)
// ---------------------------------------------------------------------------

/// <summary>
/// All combinator — all child conditions must be satisfied.
/// Short-circuits on the first false result.
/// </summary>
[Serializable]
public class AllCondition : Condition
{
    [SerializeReference]
    [field: SerializeField]
    public Condition[] Children { get; private set; } = Array.Empty<Condition>();

    public AllCondition() { }
    public AllCondition(params Condition[] children) => Children = children;

    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var child in Children)
            if (!child.Evaluate(ctx)) return false;
        return true;
    }
}

/// <summary>
/// Any combinator — at least one child condition must be satisfied.
/// Short-circuits on the first true result.
/// </summary>
[Serializable]
public class AnyCondition : Condition
{
    [SerializeReference]
    [field: SerializeField]
    public Condition[] Children { get; private set; } = Array.Empty<Condition>();

    public AnyCondition() { }
    public AnyCondition(params Condition[] children) => Children = children;

    public override bool Evaluate(IChangeTracker ctx)
    {
        foreach (var child in Children)
            if (child.Evaluate(ctx)) return true;
        return false;
    }
}

/// <summary>
/// Not combinator — inverts the child condition's result.
/// </summary>
[Serializable]
public class NotCondition : Condition
{
    [SerializeReference]
    [field: SerializeField]
    public Condition Child { get; private set; }

    public NotCondition() { }
    public NotCondition(Condition child) => Child = child;

    public override bool Evaluate(IChangeTracker ctx) => !Child.Evaluate(ctx);
}
