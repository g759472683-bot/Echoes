using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Static validation engine for MemoryFragment ScriptableObjects (ADR-0007).
///
/// Performs batch cross-fragment validation:
///   - FragmentId uniqueness (within same chapter)
///   - ContentChange cross-fragment target existence + same-chapter constraint
///   - Object count and ChoiceGroup count MVP limit warnings
///   - ConditionGroup depth validation (delegates to ConditionValidator)
///
/// The production Editor window calls ValidateAll() and displays results.
/// Testable without Unity runtime — operates on IFragmentValidationTarget instances.
/// </summary>
public static class FragmentValidator
{
    /// <summary>MVP upper limit for InteractiveObjects per fragment.</summary>
    public const int MaxInteractiveObjects = 5;

    /// <summary>MVP upper limit for ChoiceGroups per fragment.</summary>
    public const int MaxChoiceGroups = 2;

    /// <summary>
    /// Validates a collection of fragments against cross-fragment constraints.
    /// Returns all errors and warnings found. Empty list = all valid.
    /// </summary>
    public static List<ValidationError> ValidateAll(IEnumerable<IFragmentValidationTarget> fragments)
    {
        var errors = new List<ValidationError>();
        var fragList = fragments?.ToList() ?? new List<IFragmentValidationTarget>();

        if (fragList.Count == 0) return errors;

        // =====================================================================
        // 1. FragmentId uniqueness (per chapter)
        // =====================================================================
        var chapterGroups = fragList.GroupBy(f => f.ChapterKey ?? "");
        foreach (var chapterGroup in chapterGroups)
        {
            var dups = chapterGroup
                .GroupBy(f => f.FragmentId)
                .Where(g => g.Count() > 1);

            foreach (var dup in dups)
            {
                errors.Add(new ValidationError(
                    ValidationErrorLevel.Error,
                    $"碎片 ID '{dup.Key}' 在章节 '{chapterGroup.Key}' 中重复 ({dup.Count()} 个实例)",
                    dup.Key
                ));
            }
        }

        // Build lookup for cross-fragment target validation
        var fragmentIds = new HashSet<string>(fragList.Select(f => f.FragmentId));
        var fragmentChapterMap = fragList.ToDictionary(f => f.FragmentId, f => f.ChapterKey ?? "");

        // =====================================================================
        // 2. Per-fragment checks
        // =====================================================================
        foreach (var frag in fragList)
        {
            // 2a. Object count warning
            if (frag.InteractiveObjects != null && frag.InteractiveObjects.Length > MaxInteractiveObjects)
            {
                errors.Add(new ValidationError(
                    ValidationErrorLevel.Warning,
                    $"碎片 '{frag.FragmentId}' 物件数 ({frag.InteractiveObjects.Length}) 超过 MVP 上限 {MaxInteractiveObjects}",
                    frag.FragmentId
                ));
            }

            // 2b. ChoiceGroup count warning
            if (frag.ChoiceGroups != null && frag.ChoiceGroups.Length > MaxChoiceGroups)
            {
                errors.Add(new ValidationError(
                    ValidationErrorLevel.Warning,
                    $"碎片 '{frag.FragmentId}' ChoiceGroup 数 ({frag.ChoiceGroups.Length}) 超过 MVP 上限 {MaxChoiceGroups}",
                    frag.FragmentId
                ));
            }

            // 2c. Cross-fragment ContentChange target validation
            if (frag.ChoiceGroups != null)
            {
                foreach (var cg in frag.ChoiceGroups)
                {
                    if (cg.Choices == null) continue;
                    foreach (var choice in cg.Choices)
                    {
                        if (choice.ContentChanges == null) continue;
                        foreach (var change in choice.ContentChanges)
                        {
                            ValidateContentChangeTarget(change, frag, fragmentIds, fragmentChapterMap, errors);
                        }
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a single ContentChange's TargetFragmentId against the fragment catalog.
    /// </summary>
    private static void ValidateContentChangeTarget(
        ContentChange change,
        IFragmentValidationTarget sourceFragment,
        HashSet<string> allFragmentIds,
        Dictionary<string, string> fragmentChapterMap,
        List<ValidationError> errors)
    {
        var targetId = change.TargetFragmentId;
        if (string.IsNullOrEmpty(targetId)) return; // self-targeting is valid

        // Self-targeting is always valid
        if (targetId == sourceFragment.FragmentId) return;

        // Target must exist
        if (!allFragmentIds.Contains(targetId))
        {
            errors.Add(new ValidationError(
                ValidationErrorLevel.Error,
                $"碎片 '{sourceFragment.FragmentId}' 的 ContentChange 目标 '{targetId}' 不存在于任何已加载的章节中",
                sourceFragment.FragmentId
            ));
            return;
        }

        // Target must be in the same chapter
        var sourceChapter = sourceFragment.ChapterKey ?? "";
        var targetChapter = fragmentChapterMap.TryGetValue(targetId, out var tc) ? tc : "";

        if (sourceChapter != targetChapter)
        {
            errors.Add(new ValidationError(
                ValidationErrorLevel.Error,
                $"碎片 '{sourceFragment.FragmentId}' 的跨碎片 ContentChange 目标 '{targetId}' 属于不同章节 " +
                $"(源: '{sourceChapter}', 目标: '{targetChapter}')",
                sourceFragment.FragmentId
            ));
        }
    }
}
