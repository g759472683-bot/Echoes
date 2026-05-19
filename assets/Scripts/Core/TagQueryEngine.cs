using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure C# query engine for the emotional tag system (ADR-0007 S002).
///
/// Provides 5 query methods over the catalog + fragment tag data:
///   1. GetTagsForFragment — all tags + weights for a fragment
///   2. GetPrimaryTag — the IsPrimary tag, or null
///   3. QueryFragmentsByTag — fragments matching a tag (includes child tags)
///   4. GetTagCategory — the EmotionCategory of a tag
///   5. GetRelatedTags — sibling tags (same category or same parent)
///
/// All queries are pure functions — no state mutation.
/// Invalid tag/fragment IDs return empty results + fire OnWarning (never throw).
/// </summary>
public class TagQueryEngine
{
    // =========================================================================
    // Dependencies (DI)
    // =========================================================================

    private readonly EmotionalTagCatalogData _catalog;
    private readonly IFragmentTagProvider _fragmentProvider;

    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>Fired when a query encounters an invalid tag ID or fragment ID.</summary>
    public static event Action<string> OnWarning;

    // =========================================================================
    // Construction
    // =========================================================================

    public TagQueryEngine(EmotionalTagCatalogData catalog, IFragmentTagProvider fragmentProvider)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _fragmentProvider = fragmentProvider ?? throw new ArgumentNullException(nameof(fragmentProvider));
    }

    // =========================================================================
    // Query 1: GetTagsForFragment
    // =========================================================================

    /// <summary>
    /// Returns all emotional tags assigned to the fragment, with their BaseWeight.
    /// Returns empty list if the fragment doesn't exist or has no tags.
    /// </summary>
    public List<EmotionalTag> GetTagsForFragment(string fragmentId)
    {
        if (!_catalog.IsLoaded)
        {
            OnWarning?.Invoke($"GetTagsForFragment('{fragmentId}'): catalog not loaded");
            return new List<EmotionalTag>();
        }

        if (string.IsNullOrEmpty(fragmentId))
        {
            OnWarning?.Invoke("GetTagsForFragment: fragmentId is null or empty");
            return new List<EmotionalTag>();
        }

        EmotionalTag[] tags = _fragmentProvider.GetFragmentTags(fragmentId);
        if (tags == null || tags.Length == 0)
        {
            return new List<EmotionalTag>();
        }

        // Validate each tag against catalog
        var result = new List<EmotionalTag>();
        foreach (var tag in tags)
        {
            if (_catalog.HasTag(tag.TagId))
            {
                result.Add(tag);
            }
            else
            {
                OnWarning?.Invoke(
                    $"GetTagsForFragment('{fragmentId}'): tag '{tag.TagId}' not found in catalog — skipped");
            }
        }

        return result;
    }

    // =========================================================================
    // Query 2: GetPrimaryTag
    // =========================================================================

    /// <summary>
    /// Returns the fragment's primary tag (IsPrimary=true), or null if
    /// the fragment has no primary tag or doesn't exist.
    /// </summary>
    public EmotionalTag? GetPrimaryTag(string fragmentId)
    {
        if (!_catalog.IsLoaded)
        {
            OnWarning?.Invoke($"GetPrimaryTag('{fragmentId}'): catalog not loaded");
            return null;
        }

        if (string.IsNullOrEmpty(fragmentId))
        {
            OnWarning?.Invoke("GetPrimaryTag: fragmentId is null or empty");
            return null;
        }

        EmotionalTag[] tags = _fragmentProvider.GetFragmentTags(fragmentId);
        if (tags == null)
        {
            return null;
        }

        foreach (var tag in tags)
        {
            if (tag.IsPrimary && _catalog.HasTag(tag.TagId))
            {
                return tag;
            }
        }

        return null; // No primary tag set — valid state
    }

    // =========================================================================
    // Query 3: QueryFragmentsByTag
    // =========================================================================

    /// <summary>
    /// Returns fragment IDs that have the specified tag (or any of its child
    /// tags) with weight >= minWeight.
    ///
    /// Hierarchy: if "love" has child tag "nostalgia", QueryFragmentsByTag("love")
    /// returns fragments tagged with "love" AND fragments tagged with "nostalgia".
    /// Child tag weights are NOT affected by the parent query.
    /// </summary>
    public List<string> QueryFragmentsByTag(string tagId, float minWeight = 0.0f)
    {
        if (!_catalog.IsLoaded)
        {
            OnWarning?.Invoke($"QueryFragmentsByTag('{tagId}'): catalog not loaded");
            return new List<string>();
        }

        if (string.IsNullOrEmpty(tagId))
        {
            OnWarning?.Invoke("QueryFragmentsByTag: tagId is null or empty");
            return new List<string>();
        }

        if (!_catalog.HasTag(tagId))
        {
            OnWarning?.Invoke($"QueryFragmentsByTag: tag '{tagId}' not found in catalog");
            return new List<string>();
        }

        // Collect all tag IDs in the hierarchy (parent + children)
        var matchTagIds = new HashSet<string> { tagId };
        foreach (var tag in _catalog.AllTags)
        {
            if (tag.ParentTagId == tagId)
            {
                matchTagIds.Add(tag.TagId);
            }
        }

        // Scan all fragments for matches
        var result = new List<string>();
        string[] allFragmentIds = _fragmentProvider.GetAllFragmentIds();

        foreach (string fragmentId in allFragmentIds)
        {
            EmotionalTag[] tags = _fragmentProvider.GetFragmentTags(fragmentId);
            if (tags == null) continue;

            foreach (var tag in tags)
            {
                if (matchTagIds.Contains(tag.TagId) && tag.BaseWeight >= minWeight)
                {
                    result.Add(fragmentId);
                    break; // Fragment matched — no need to check remaining tags
                }
            }
        }

        return result;
    }

    // =========================================================================
    // Query 4: GetTagCategory
    // =========================================================================

    /// <summary>
    /// Returns the EmotionCategory for a tag, or null if the tag doesn't exist.
    /// </summary>
    public EmotionCategory? GetTagCategory(string tagId)
    {
        if (!_catalog.IsLoaded)
        {
            OnWarning?.Invoke($"GetTagCategory('{tagId}'): catalog not loaded");
            return null;
        }

        if (string.IsNullOrEmpty(tagId))
        {
            OnWarning?.Invoke("GetTagCategory: tagId is null or empty");
            return null;
        }

        var tag = _catalog.GetTag(tagId);
        if (tag.HasValue)
        {
            return tag.Value.Category;
        }

        OnWarning?.Invoke($"GetTagCategory: tag '{tagId}' not found in catalog");
        return null;
    }

    // =========================================================================
    // Query 5: GetRelatedTags
    // =========================================================================

    /// <summary>
    /// Returns TagIds of related tags:
    ///   - Sibling tags in the same Category
    ///   - Tags that share the same ParentTagId
    ///
    /// The input tag itself is excluded from results.
    /// Returns empty list if the tag doesn't exist.
    /// </summary>
    public List<string> GetRelatedTags(string tagId)
    {
        if (!_catalog.IsLoaded)
        {
            OnWarning?.Invoke($"GetRelatedTags('{tagId}'): catalog not loaded");
            return new List<string>();
        }

        if (string.IsNullOrEmpty(tagId))
        {
            OnWarning?.Invoke("GetRelatedTags: tagId is null or empty");
            return new List<string>();
        }

        var sourceTag = _catalog.GetTag(tagId);
        if (!sourceTag.HasValue)
        {
            OnWarning?.Invoke($"GetRelatedTags: tag '{tagId}' not found in catalog");
            return new List<string>();
        }

        var related = new HashSet<string>();
        EmotionCategory category = sourceTag.Value.Category;
        string parentId = sourceTag.Value.ParentTagId;

        foreach (var tag in _catalog.AllTags)
        {
            if (tag.TagId == tagId) continue; // Skip self

            bool sameCategory = tag.Category == category;
            bool sameParent = !string.IsNullOrEmpty(parentId) && tag.ParentTagId == parentId;

            if (sameCategory || sameParent)
            {
                related.Add(tag.TagId);
            }
        }

        return related.ToList();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Resets static events. Call in test TearDown.</summary>
    public static void ResetStaticEvents()
    {
        OnWarning = null;
    }
}
