using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure C# runtime representation of the EmotionalTagCatalog (ADR-0007 S001).
///
/// Wraps the designtime ScriptableObject data in a queryable, validated
/// container. Built by ICatalogProvider (production: Addressables load;
/// test: direct construction).
///
/// Runtime read-only — all tag data is immutable after loading.
/// Provides basic tag lookup; full query API is in S002.
///
/// Error states:
///   - NotLoaded: catalog hasn't been loaded yet (IsLoaded=false, Tags empty)
///   - Loaded: catalog loaded successfully
///   - Error: catalog failed to load (IsLoaded=false, ErrorMessage set)
/// </summary>
public class EmotionalTagCatalogData
{
    // =========================================================================
    // Internal State
    // =========================================================================

    private readonly Dictionary<string, EmotionalTagData> _tagsById;
    private readonly Dictionary<EmotionCategory, List<string>> _tagsByCategory;
    private readonly List<EmotionalTagData> _allTags;
    private readonly List<string> _validationErrors;

    // =========================================================================
    // Public Properties
    // =========================================================================

    /// <summary>Whether the catalog has been successfully loaded and validated.</summary>
    public bool IsLoaded { get; }

    /// <summary>Error message if IsLoaded is false (null if loaded or not yet attempted).</summary>
    public string ErrorMessage { get; }

    /// <summary>Total number of tags in the catalog.</summary>
    public int TagCount => _allTags?.Count ?? 0;

    /// <summary>Number of distinct categories represented.</summary>
    public int CategoryCount => _tagsByCategory?.Count ?? 0;

    /// <summary>Validation errors found during loading (empty if clean).</summary>
    public IReadOnlyList<string> ValidationErrors => _validationErrors;

    /// <summary>All tags in the catalog (empty if not loaded).</summary>
    public IReadOnlyList<EmotionalTagData> AllTags => _allTags;

    // =========================================================================
    // Construction (internal — use static factories)
    // =========================================================================

    private EmotionalTagCatalogData(
        Dictionary<string, EmotionalTagData> tagsById,
        Dictionary<EmotionCategory, List<string>> tagsByCategory,
        List<EmotionalTagData> allTags,
        List<string> validationErrors,
        string errorMessage)
    {
        _tagsById = tagsById;
        _tagsByCategory = tagsByCategory;
        _allTags = allTags;
        _validationErrors = validationErrors;
        ErrorMessage = errorMessage;
        IsLoaded = errorMessage == null && allTags != null && allTags.Count > 0;
    }

    // =========================================================================
    // Static Factories
    // =========================================================================

    /// <summary>
    /// Creates a catalog from a list of tag data entries.
    /// Validates: duplicate TagIds, empty TagIds, invalid Category values.
    /// Returns a catalog with IsLoaded=true if valid and non-empty.
    /// </summary>
    public static EmotionalTagCatalogData CreateFromTags(List<EmotionalTagData> tags)
    {
        if (tags == null)
        {
            return new EmotionalTagCatalogData(null, null, null, new List<string>(),
                "情感标签数据加载失败 — catalog data is null");
        }

        if (tags.Count == 0)
        {
            return new EmotionalTagCatalogData(null, null, new List<EmotionalTagData>(),
                new List<string>(),
                "情感标签数据加载失败 — catalog is empty");
        }

        var errors = new List<string>();
        var tagsById = new Dictionary<string, EmotionalTagData>();
        var tagsByCategory = new Dictionary<EmotionCategory, List<string>>();
        var validTags = new List<EmotionalTagData>();

        foreach (var tag in tags)
        {
            // Validate TagId
            if (string.IsNullOrWhiteSpace(tag.TagId))
            {
                errors.Add($"标签数据无效: TagId 为空");
                continue;
            }

            // Validate uniqueness
            if (tagsById.ContainsKey(tag.TagId))
            {
                errors.Add($"重复的 TagId: '{tag.TagId}'");
                continue;
            }

            // Validate DisplayName
            if (string.IsNullOrWhiteSpace(tag.DisplayName))
            {
                errors.Add($"标签 '{tag.TagId}' 的 DisplayName 为空");
                // Non-blocking — still add the tag
            }

            tagsById[tag.TagId] = tag;
            validTags.Add(tag);

            // Index by category
            if (!tagsByCategory.ContainsKey(tag.Category))
            {
                tagsByCategory[tag.Category] = new List<string>();
            }
            tagsByCategory[tag.Category].Add(tag.TagId);
        }

        if (validTags.Count == 0)
        {
            return new EmotionalTagCatalogData(null, null, new List<EmotionalTagData>(),
                errors,
                "情感标签数据加载失败 — no valid tags after validation");
        }

        // Validate parent tag references
        foreach (var tag in validTags)
        {
            if (!string.IsNullOrEmpty(tag.ParentTagId) && !tagsById.ContainsKey(tag.ParentTagId))
            {
                errors.Add($"标签 '{tag.TagId}' 引用不存在的 ParentTagId: '{tag.ParentTagId}'");
            }
        }

        // Validate IncompatibleWith references
        foreach (var tag in validTags)
        {
            if (tag.IncompatibleWith != null)
            {
                foreach (var incompatibleId in tag.IncompatibleWith)
                {
                    if (!string.IsNullOrEmpty(incompatibleId) && !tagsById.ContainsKey(incompatibleId))
                    {
                        errors.Add($"标签 '{tag.TagId}' 的 IncompatibleWith 引用不存在的标签: '{incompatibleId}'");
                    }
                }
            }
        }

        return new EmotionalTagCatalogData(tagsById, tagsByCategory, validTags, errors, null);
    }

    /// <summary>
    /// Creates an error-state catalog (Addressables load failed, etc.).
    /// IsLoaded will be false and ErrorMessage will be set.
    /// </summary>
    public static EmotionalTagCatalogData CreateError(string errorMessage)
    {
        return new EmotionalTagCatalogData(null, null, new List<EmotionalTagData>(),
            new List<string>(),
            errorMessage ?? "情感标签数据加载失败");
    }

    // =========================================================================
    // Public Query API (basic — full API in S002)
    // =========================================================================

    /// <summary>Gets a tag by ID, or null if not found or catalog not loaded.</summary>
    public EmotionalTagData? GetTag(string tagId)
    {
        if (!IsLoaded || string.IsNullOrEmpty(tagId))
            return null;

        _tagsById.TryGetValue(tagId, out var tag);
        return tag;
    }

    /// <summary>Whether a tag with the given ID exists in the catalog.</summary>
    public bool HasTag(string tagId)
    {
        return IsLoaded && !string.IsNullOrEmpty(tagId) && _tagsById.ContainsKey(tagId);
    }

    /// <summary>Gets all tag IDs in the specified category.</summary>
    public IReadOnlyList<string> GetTagIdsByCategory(EmotionCategory category)
    {
        if (!IsLoaded || _tagsByCategory == null)
            return new List<string>();

        _tagsByCategory.TryGetValue(category, out var ids);
        return ids ?? (IReadOnlyList<string>)new List<string>();
    }
}
