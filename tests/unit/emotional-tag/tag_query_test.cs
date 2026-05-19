using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Unit tests for TagQueryEngine (emotional-tag S002).
///
/// Covers 3 acceptance criteria:
///   AC-1: GetTagsForFragment + GetPrimaryTag
///   AC-2: QueryFragmentsByTag hierarchy (parent includes children)
///   AC-3: Invalid TagId → empty/default + warning
/// </summary>
public class TagQueryTest
{
    // =========================================================================
    // Mock Dependencies
    // =========================================================================

    private class MockFragmentTagProvider : IFragmentTagProvider
    {
        private readonly Dictionary<string, EmotionalTag[]> _fragments = new();

        public void RegisterFragment(string fragmentId, EmotionalTag[] tags)
        {
            _fragments[fragmentId] = tags;
        }

        public EmotionalTag[] GetFragmentTags(string fragmentId)
        {
            _fragments.TryGetValue(fragmentId, out var tags);
            return tags; // null if not found
        }

        public string[] GetAllFragmentIds()
        {
            return _fragments.Keys.ToArray();
        }
    }

    private static EmotionalTagData MakeTag(
        string tagId, string displayName, EmotionCategory category,
        string parentTagId = null, string[] incompatibleWith = null)
    {
        return new EmotionalTagData(
            tagId: tagId,
            displayName: displayName,
            category: category,
            parentTagId: parentTagId,
            incompatibleWith: incompatibleWith,
            associatedColors: new ColorAssociation("#AAA", "#BBB"),
            description: $"Desc: {tagId}"
        );
    }

    private static EmotionalTag MakeEmotionalTag(string tagId, float weight, bool isPrimary = false)
    {
        return new EmotionalTag(tagId, weight, isPrimary);
    }

    private EmotionalTagCatalogData CreateCatalog(List<EmotionalTagData> tags)
    {
        return EmotionalTagCatalogData.CreateFromTags(tags);
    }

    // =========================================================================
    // Setup / Teardown
    // =========================================================================

    private MockFragmentTagProvider _fragmentProvider;
    private TagQueryEngine _engine;

    [SetUp]
    public void SetUp()
    {
        _fragmentProvider = new MockFragmentTagProvider();
    }

    [TearDown]
    public void TearDown()
    {
        TagQueryEngine.ResetStaticEvents();
    }

    private void CreateEngine(List<EmotionalTagData> catalogTags)
    {
        var catalog = CreateCatalog(catalogTags);
        _engine = new TagQueryEngine(catalog, _fragmentProvider);
    }

    // =========================================================================
    // AC-1: GetTagsForFragment + GetPrimaryTag
    // =========================================================================

    [Test]
    public void test_get_tags_for_fragment_returns_all_tags_with_weights()
    {
        // Arrange
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("nostalgia", "怀念", EmotionCategory.Love),
            MakeTag("peace", "平静", EmotionCategory.Peace),
            MakeTag("loss", "失去", EmotionCategory.Sadness),
        });
        _fragmentProvider.RegisterFragment("frag_A", new[]
        {
            MakeEmotionalTag("nostalgia", 0.8f, true),
            MakeEmotionalTag("peace", 0.4f, false),
            MakeEmotionalTag("loss", 0.6f, false),
        });

        // Act
        var tags = _engine.GetTagsForFragment("frag_A");

        // Assert
        Assert.That(tags.Count, Is.EqualTo(3));
        Assert.That(tags.Any(t => t.TagId == "nostalgia" && Mathf.Approximately(t.BaseWeight, 0.8f)), Is.True);
        Assert.That(tags.Any(t => t.TagId == "peace" && Mathf.Approximately(t.BaseWeight, 0.4f)), Is.True);
        Assert.That(tags.Any(t => t.TagId == "loss" && Mathf.Approximately(t.BaseWeight, 0.6f)), Is.True);
    }

    [Test]
    public void test_get_primary_tag_returns_is_primary_tag()
    {
        // Arrange
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("nostalgia", "怀念", EmotionCategory.Love),
            MakeTag("peace", "平静", EmotionCategory.Peace),
        });
        _fragmentProvider.RegisterFragment("frag_A", new[]
        {
            MakeEmotionalTag("nostalgia", 0.8f, true),
            MakeEmotionalTag("peace", 0.4f, false),
        });

        // Act
        var primary = _engine.GetPrimaryTag("frag_A");

        // Assert
        Assert.That(primary.HasValue, Is.True);
        Assert.That(primary.Value.TagId, Is.EqualTo("nostalgia"));
        Assert.That(primary.Value.IsPrimary, Is.True);
    }

    [Test]
    public void test_get_primary_tag_returns_null_when_no_primary()
    {
        // Arrange
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("peace", "平静", EmotionCategory.Peace),
            MakeTag("loss", "失去", EmotionCategory.Sadness),
        });
        _fragmentProvider.RegisterFragment("frag_B", new[]
        {
            MakeEmotionalTag("peace", 0.4f, false),
            MakeEmotionalTag("loss", 0.6f, false),
        });

        // Act
        var primary = _engine.GetPrimaryTag("frag_B");

        // Assert
        Assert.That(primary.HasValue, Is.False,
            "No IsPrimary=true tag → should return null");
    }

    [Test]
    public void test_get_tags_for_nonexistent_fragment_returns_empty()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });

        var tags = _engine.GetTagsForFragment("nonexistent");

        Assert.That(tags, Is.Empty);
    }

    [Test]
    public void test_get_primary_tag_for_nonexistent_fragment_returns_null()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });

        var primary = _engine.GetPrimaryTag("nonexistent");

        Assert.That(primary.HasValue, Is.False);
    }

    [Test]
    public void test_get_tags_filters_out_tags_not_in_catalog()
    {
        // Fragment has a tag that doesn't exist in the catalog
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("peace", "平静", EmotionCategory.Peace),
        });
        _fragmentProvider.RegisterFragment("frag_C", new[]
        {
            MakeEmotionalTag("peace", 0.5f),
            MakeEmotionalTag("phantom", 1.0f), // Not in catalog
        });

        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        var tags = _engine.GetTagsForFragment("frag_C");

        Assert.That(tags.Count, Is.EqualTo(1));
        Assert.That(tags[0].TagId, Is.EqualTo("peace"));
        Assert.That(warnings.Count, Is.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("phantom"));
    }

    // =========================================================================
    // AC-1: GetPrimaryTag edge cases
    // =========================================================================

    [Test]
    public void test_get_primary_tag_returns_first_when_multiple_primary()
    {
        // Edge case: multiple IsPrimary tags (editor validation should catch this,
        // but runtime should handle gracefully — return the first found)
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("fear", "恐惧", EmotionCategory.Fear),
        });
        _fragmentProvider.RegisterFragment("frag_D", new[]
        {
            MakeEmotionalTag("love", 0.8f, true),
            MakeEmotionalTag("fear", 0.6f, true),
        });

        var primary = _engine.GetPrimaryTag("frag_D");

        Assert.That(primary.HasValue, Is.True);
        Assert.That(primary.Value.IsPrimary, Is.True);
        // Returns the first IsPrimary found
    }

    [Test]
    public void test_get_primary_tag_skips_primary_tag_not_in_catalog()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("peace", "平静", EmotionCategory.Peace),
        });
        _fragmentProvider.RegisterFragment("frag_E", new[]
        {
            MakeEmotionalTag("phantom", 0.8f, true), // Not in catalog
            MakeEmotionalTag("peace", 0.4f, false),
        });

        var primary = _engine.GetPrimaryTag("frag_E");

        Assert.That(primary.HasValue, Is.False,
            "Primary tag not in catalog should be skipped → no valid primary found");
    }

    // =========================================================================
    // AC-2: QueryFragmentsByTag hierarchy
    // =========================================================================

    [Test]
    public void test_query_by_parent_tag_includes_child_tag_fragments()
    {
        // Arrange
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("nostalgia", "怀念", EmotionCategory.Love, parentTagId: "love"),
            MakeTag("tenderness", "温柔", EmotionCategory.Love, parentTagId: "love"),
            MakeTag("fear", "恐惧", EmotionCategory.Fear),
        });
        _fragmentProvider.RegisterFragment("frag_X", new[]
        {
            MakeEmotionalTag("love", 0.9f), // Direct match
        });
        _fragmentProvider.RegisterFragment("frag_Y", new[]
        {
            MakeEmotionalTag("nostalgia", 0.7f), // Child match
        });
        _fragmentProvider.RegisterFragment("frag_Z", new[]
        {
            MakeEmotionalTag("fear", 0.5f), // Unrelated — should NOT match
        });

        // Act
        var result = _engine.QueryFragmentsByTag("love");

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result, Contains.Item("frag_X"), "Direct parent tag match");
        Assert.That(result, Contains.Item("frag_Y"), "Child tag match (nostalgia)");
        Assert.That(result, Does.Not.Contain("frag_Z"), "Unrelated tag should not match");
    }

    [Test]
    public void test_query_by_child_tag_does_not_include_parent_fragments()
    {
        // Querying "nostalgia" should NOT automatically include "love" fragments
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("nostalgia", "怀念", EmotionCategory.Love, parentTagId: "love"),
        });
        _fragmentProvider.RegisterFragment("frag_X", new[]
        {
            MakeEmotionalTag("love", 0.9f),
        });
        _fragmentProvider.RegisterFragment("frag_Y", new[]
        {
            MakeEmotionalTag("nostalgia", 0.7f),
        });

        var result = _engine.QueryFragmentsByTag("nostalgia");

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("frag_Y"),
            "Querying child tag should only return child-tagged fragments, not parent-tagged");
    }

    [Test]
    public void test_query_by_tag_child_weight_not_affected_by_parent()
    {
        // Arrange
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("nostalgia", "怀念", EmotionCategory.Love, parentTagId: "love"),
        });
        _fragmentProvider.RegisterFragment("frag_A", new[]
        {
            MakeEmotionalTag("love", 0.9f),
        });
        _fragmentProvider.RegisterFragment("frag_B", new[]
        {
            MakeEmotionalTag("nostalgia", 0.3f),
        });
        _fragmentProvider.RegisterFragment("frag_C", new[]
        {
            MakeEmotionalTag("nostalgia", 0.7f),
        });

        // Act — query with minWeight = 0.5
        var result = _engine.QueryFragmentsByTag("love", minWeight: 0.5f);

        // Assert
        Assert.That(result, Contains.Item("frag_A"), "love w=0.9 >= 0.5 → included");
        Assert.That(result, Does.Not.Contain("frag_B"), "nostalgia w=0.3 < 0.5 → excluded");
        Assert.That(result, Contains.Item("frag_C"), "nostalgia w=0.7 >= 0.5 → included");
    }

    [Test]
    public void test_query_by_tag_min_weight_default_zero()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        _fragmentProvider.RegisterFragment("frag_A", new[]
        {
            MakeEmotionalTag("hope", 0.001f),
        });

        var result = _engine.QueryFragmentsByTag("hope");

        Assert.That(result, Contains.Item("frag_A"),
            "Default minWeight=0 should include all matching fragments");
    }

    [Test]
    public void test_query_by_tag_min_weight_filters_correctly()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        _fragmentProvider.RegisterFragment("frag_high", new[]
        {
            MakeEmotionalTag("hope", 0.9f),
        });
        _fragmentProvider.RegisterFragment("frag_low", new[]
        {
            MakeEmotionalTag("hope", 0.2f),
        });

        var result = _engine.QueryFragmentsByTag("hope", minWeight: 0.5f);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("frag_high"));
    }

    [Test]
    public void test_query_by_tag_multiple_tags_on_same_fragment_not_duplicated()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("peace", "平静", EmotionCategory.Peace),
        });
        _fragmentProvider.RegisterFragment("frag_X", new[]
        {
            MakeEmotionalTag("love", 0.8f),
            MakeEmotionalTag("peace", 0.4f),
        });

        var result = _engine.QueryFragmentsByTag("love");

        Assert.That(result.Count, Is.EqualTo(1), "Fragment should appear only once");
        Assert.That(result[0], Is.EqualTo("frag_X"));
    }

    // =========================================================================
    // AC-3: Invalid TagId → empty/default + warning
    // =========================================================================

    [Test]
    public void test_get_tag_category_nonexistent_returns_null_and_warns()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        var category = _engine.GetTagCategory("nonexistent");

        Assert.That(category.HasValue, Is.False);
        Assert.That(warnings.Count, Is.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("nonexistent"));
    }

    [Test]
    public void test_query_fragments_by_nonexistent_tag_returns_empty()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        var result = _engine.QueryFragmentsByTag("nonexistent");

        Assert.That(result, Is.Empty);
        Assert.That(warnings.Count, Is.EqualTo(1));
    }

    [Test]
    public void test_get_related_tags_nonexistent_returns_empty()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        var result = _engine.GetRelatedTags("nonexistent");

        Assert.That(result, Is.Empty);
        Assert.That(warnings.Count, Is.EqualTo(1));
    }

    [Test]
    public void test_null_tag_id_returns_empty_and_warns()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        Assert.That(_engine.QueryFragmentsByTag(null), Is.Empty);
        Assert.That(_engine.GetTagCategory(null).HasValue, Is.False);
        Assert.That(_engine.GetRelatedTags(null), Is.Empty);
        Assert.That(warnings.Count, Is.EqualTo(3));
    }

    [Test]
    public void test_empty_tag_id_returns_empty_and_warns()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        Assert.That(_engine.QueryFragmentsByTag(""), Is.Empty);
        Assert.That(_engine.GetTagCategory("").HasValue, Is.False);
        Assert.That(warnings.Count, Is.EqualTo(2));
    }

    // =========================================================================
    // Query 4: GetTagCategory — valid path
    // =========================================================================

    [Test]
    public void test_get_tag_category_returns_correct_category()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("nostalgia", "怀念", EmotionCategory.Love),
            MakeTag("fear", "恐惧", EmotionCategory.Fear),
            MakeTag("joy", "欢乐", EmotionCategory.Joy),
        });

        Assert.That(_engine.GetTagCategory("nostalgia"), Is.EqualTo(EmotionCategory.Love));
        Assert.That(_engine.GetTagCategory("fear"), Is.EqualTo(EmotionCategory.Fear));
        Assert.That(_engine.GetTagCategory("joy"), Is.EqualTo(EmotionCategory.Joy));
    }

    // =========================================================================
    // Query 5: GetRelatedTags
    // =========================================================================

    [Test]
    public void test_get_related_tags_returns_same_category_siblings()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("nostalgia", "怀念", EmotionCategory.Love, parentTagId: "love"),
            MakeTag("tenderness", "温柔", EmotionCategory.Love, parentTagId: "love"),
            MakeTag("fear", "恐惧", EmotionCategory.Fear), // Different category
        });

        var related = _engine.GetRelatedTags("nostalgia");

        // nostalgia is in Love category + has parent "love"
        // love is same category (Love) and same parent (null, but nostalgia's parent=love)
        // tenderness is same category AND same parent
        // fear is different category → excluded
        Assert.That(related.Count, Is.EqualTo(2));
        Assert.That(related, Contains.Item("love"), "Same category sibling");
        Assert.That(related, Contains.Item("tenderness"), "Same category + same parent");
        Assert.That(related, Does.Not.Contain("nostalgia"), "Should NOT include self");
        Assert.That(related, Does.Not.Contain("fear"), "Different category → excluded");
    }

    [Test]
    public void test_get_related_tags_includes_same_parent_only()
    {
        // Tags with same parent but different category (should still be included)
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("root", "根", EmotionCategory.Peace),
            MakeTag("child_a", "子A", EmotionCategory.Joy, parentTagId: "root"),
            MakeTag("child_b", "子B", EmotionCategory.Sadness, parentTagId: "root"),
            MakeTag("other", "其他", EmotionCategory.Peace),
        });

        var related = _engine.GetRelatedTags("child_a");

        // child_a: category=Joy, parent=root
        // child_b: same parent (root) → included
        // root: different category (Peace) but... wait, root is the parent of child_a
        //   Is "same category" for root? root.Category=Peace, child_a.Category=Joy → no
        //   Is "same parent"? root.ParentTagId=null, child_a.ParentTagId="root" → no
        // other: category=Peace (same as root but child_a is Joy) → no
        Assert.That(related, Contains.Item("child_b"), "Same parent → related");
        Assert.That(related.Count, Is.EqualTo(1));
    }

    [Test]
    public void test_get_related_tags_root_tag_returns_same_category_siblings()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("joy_a", "喜悦A", EmotionCategory.Joy),
            MakeTag("joy_b", "喜悦B", EmotionCategory.Joy),
            MakeTag("joy_c", "喜悦C", EmotionCategory.Joy),
            MakeTag("sad", "悲伤", EmotionCategory.Sadness),
        });

        var related = _engine.GetRelatedTags("joy_a");

        Assert.That(related.Count, Is.EqualTo(2));
        Assert.That(related, Contains.Item("joy_b"));
        Assert.That(related, Contains.Item("joy_c"));
        Assert.That(related, Does.Not.Contain("joy_a"), "No self");
        Assert.That(related, Does.Not.Contain("sad"), "Different category");
    }

    // =========================================================================
    // Edge cases — catalog not loaded
    // =========================================================================

    [Test]
    public void test_all_queries_return_empty_when_catalog_not_loaded()
    {
        var catalog = EmotionalTagCatalogData.CreateError("not loaded");
        var engine = new TagQueryEngine(catalog, _fragmentProvider);

        Assert.That(engine.GetTagsForFragment("any"), Is.Empty);
        Assert.That(engine.GetPrimaryTag("any").HasValue, Is.False);
        Assert.That(engine.QueryFragmentsByTag("any"), Is.Empty);
        Assert.That(engine.GetTagCategory("any").HasValue, Is.False);
        Assert.That(engine.GetRelatedTags("any"), Is.Empty);
    }

    [Test]
    public void test_null_fragment_id_returns_empty_and_warns()
    {
        CreateEngine(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        var warnings = new List<string>();
        TagQueryEngine.OnWarning += warnings.Add;

        Assert.That(_engine.GetTagsForFragment(null), Is.Empty);
        Assert.That(_engine.GetPrimaryTag(null).HasValue, Is.False);
        Assert.That(warnings.Count, Is.EqualTo(2));
    }

    // =========================================================================
    // Constructor validation
    // =========================================================================

    [Test]
    public void test_constructor_rejects_null_catalog()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new TagQueryEngine(null, _fragmentProvider));
    }

    [Test]
    public void test_constructor_rejects_null_fragment_provider()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
        });
        Assert.Throws<System.ArgumentNullException>(() =>
            new TagQueryEngine(catalog, null));
    }
}

/// <summary>
/// Standalone Mathf.Approximately for pure C# tests (no UnityEngine dependency).
/// </summary>
internal static class Mathf
{
    public static bool Approximately(float a, float b)
    {
        return System.Math.Abs(a - b) < 0.0001f;
    }
}
