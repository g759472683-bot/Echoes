using System.Linq;

/// <summary>
/// Validates Condition trees for structural constraints (ADR-0008).
///
/// Max nesting depth of 3 is enforced — combinatorial nodes (All/Any/Not)
/// increment depth by 1. Leaf conditions are terminal (depth unchanged).
/// </summary>
public static class ConditionValidator
{
    /// <summary>Maximum allowed nesting depth for Condition trees.</summary>
    public const int MaxDepth = 3;

    /// <summary>
    /// Returns true if the condition tree's nesting depth does not exceed maxDepth.
    /// </summary>
    public static bool ValidateDepth(Condition condition, int maxDepth = MaxDepth)
    {
        return GetDepth(condition, 0) <= maxDepth;
    }

    /// <summary>
    /// Returns the maximum nesting depth of a condition tree.
    /// Leaf = current depth. Combinator = max child depth + 1.
    /// </summary>
    public static int GetDepth(Condition condition, int current = 0)
    {
        return condition switch
        {
            AllCondition all => all.Children.Length > 0
                ? all.Children.Max(child => GetDepth(child, current + 1))
                : current,
            AnyCondition any => any.Children.Length > 0
                ? any.Children.Max(child => GetDepth(child, current + 1))
                : current,
            NotCondition not => not.Child != null
                ? GetDepth(not.Child, current + 1)
                : current,
            _ => current
        };
    }

    /// <summary>
    /// Returns a human-readable description of the depth validation result.
    /// "深度 N — 通过" or "深度 N — 超出最大深度 M".
    /// </summary>
    public static string GetDepthReport(Condition condition, int maxDepth = MaxDepth)
    {
        var depth = GetDepth(condition, 0);
        if (depth <= maxDepth)
            return $"深度 {depth} — 通过";
        return $"深度 {depth} — 超出最大深度 {maxDepth}";
    }
}
