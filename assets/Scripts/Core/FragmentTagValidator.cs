using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# fragment tag validation (ADR-0007 S003).
///
/// Validates fragment EmotionalTags against the catalog rules:
///   1. Each fragment must have at least 1 tag
///   2. IncompatibleWith pairs cannot both be IsPrimary
///   3. All TagIds must exist in the catalog
///   4. ParentTagId must not form cycles (A→B→A)
///   5. Fragment must not exceed max tags (5)
///   6. Max 1 tag can be IsPrimary
///
/// Designed for both Editor-time validation (MenuItem/AssetPostprocessor)
/// and runtime integrity checks. All rules are pure functions —
/// no Unity dependencies.
/// </summary>
public static class FragmentTagValidator
{
    /// <summary>Maximum number of emotional tags per fragment.</summary>
    public const int MaxTagsPerFragment = 5;

    /// <summary>
    /// Single validation error result.
    /// </summary>
    public readonly struct ValidationError
    {
        public readonly string FragmentId;
        public readonly string Message;
        public readonly bool IsBlocking;

        public ValidationError(string fragmentId, string message, bool isBlocking = true)
        {
            FragmentId = fragmentId;
            Message = message;
            IsBlocking = isBlocking;
        }
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Validates a single fragment's tags against the catalog.
    /// Returns a list of validation errors (empty = valid).
    /// </summary>
    public static List<ValidationError> ValidateFragment(
        string fragmentId,
        EmotionalTag[] tags,
        EmotionalTagCatalogData catalog)
    {
        if (catalog == null)
        {
            return new List<ValidationError>
            {
                new ValidationError(fragmentId ?? "unknown", "Catalog is null", true)
            };
        }

        if (!catalog.IsLoaded)
        {
            return new List<ValidationError>
            {
                new ValidationError(fragmentId ?? "unknown", "Catalog is not loaded", true)
            };
        }

        var errors = new List<ValidationError>();
        string fragId = fragmentId ?? "unknown";

        // Rule 1: At least 1 tag
        if (tags == null || tags.Length == 0)
        {
            errors.Add(new ValidationError(fragId,
                $"碎片 [{fragId}] 无情感标签", true));
            return errors; // No tags → skip remaining checks
        }

        // Rule 5: Max tags per fragment
        if (tags.Length > MaxTagsPerFragment)
        {
            errors.Add(new ValidationError(fragId,
                $"碎片 [{fragId}] 有 {tags.Length} 个标签，超过最大限制 {MaxTagsPerFragment}", true));
        }

        // Rule 6: Max 1 IsPrimary
        int primaryCount = 0;
        string firstPrimaryId = null;
        foreach (var tag in tags)
        {
            if (tag.IsPrimary)
            {
                primaryCount++;
                if (firstPrimaryId == null) firstPrimaryId = tag.TagId;
            }
        }
        if (primaryCount > 1)
        {
            errors.Add(new ValidationError(fragId,
                $"碎片 [{fragId}] 有 {primaryCount} 个主标签（IsPrimary），最多允许 1 个", true));
        }

        // Collect valid tag IDs for later checks
        var tagIdsInFragment = new HashSet<string>();
        var catalogTagIds = new HashSet<string>();

        // Rule 3: All TagIds must exist in catalog
        foreach (var tag in tags)
        {
            if (string.IsNullOrEmpty(tag.TagId))
            {
                errors.Add(new ValidationError(fragId,
                    $"碎片 [{fragId}] 包含空 TagId 的标签", true));
                continue;
            }

            if (!catalog.HasTag(tag.TagId))
            {
                errors.Add(new ValidationError(fragId,
                    $"标签 '{tag.TagId}' 不存在于 EmotionalTagCatalog", true));
                continue;
            }

            tagIdsInFragment.Add(tag.TagId);

            // Collect catalog data for hierarchy checks
            var catalogTag = catalog.GetTag(tag.TagId);
            if (catalogTag.HasValue)
            {
                catalogTagIds.Add(tag.TagId);
            }
        }

        // Rule 2: IncompatibleWith pairs cannot both be IsPrimary
        // Only check IsPrimary tags against each other's IncompatibleWith lists
        foreach (var tag in tags)
        {
            if (!tag.IsPrimary) continue;
            if (string.IsNullOrEmpty(tag.TagId)) continue;

            var catalogTag = catalog.GetTag(tag.TagId);
            if (!catalogTag.HasValue) continue;

            var incompatibleWith = catalogTag.Value.IncompatibleWith;
            if (incompatibleWith == null || incompatibleWith.Length == 0) continue;

            foreach (var incompatibleId in incompatibleWith)
            {
                if (string.IsNullOrEmpty(incompatibleId)) continue;

                // Check if the incompatible tag is also IsPrimary on this fragment
                foreach (var otherTag in tags)
                {
                    if (otherTag.TagId == incompatibleId && otherTag.IsPrimary)
                    {
                        errors.Add(new ValidationError(fragId,
                            $"标签 {tag.TagId} 和 {incompatibleId} 互斥，不能同时为主标签", true));
                    }
                }
            }
        }

        // Rule 4: ParentTagId cycle detection (A→B→A, max 2 levels)
        foreach (var tag in tags)
        {
            if (string.IsNullOrEmpty(tag.TagId)) continue;
            var catalogTag = catalog.GetTag(tag.TagId);
            if (!catalogTag.HasValue) continue;

            string parentId = catalogTag.Value.ParentTagId;
            if (string.IsNullOrEmpty(parentId)) continue;

            // Check if parent exists
            var parentTag = catalog.GetTag(parentId);
            if (!parentTag.HasValue) continue; // Validated elsewhere

            // Check if parent's parent points back to this tag (cycle: A→B→A)
            string grandparentId = parentTag.Value.ParentTagId;
            if (!string.IsNullOrEmpty(grandparentId) && grandparentId == tag.TagId)
            {
                errors.Add(new ValidationError(fragId,
                    $"标签层级存在循环: '{tag.TagId}' 的父标签是 '{parentId}'，" +
                    $"而 '{parentId}' 的父标签又是 '{tag.TagId}'", true));
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates multiple fragments in batch. Returns all errors across all fragments.
    /// </summary>
    public static List<ValidationError> ValidateFragments(
        (string fragmentId, EmotionalTag[] tags)[] fragments,
        EmotionalTagCatalogData catalog)
    {
        var allErrors = new List<ValidationError>();
        foreach (var (fragmentId, tags) in fragments)
        {
            allErrors.AddRange(ValidateFragment(fragmentId, tags, catalog));
        }
        return allErrors;
    }
}
