using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Unit tests for EmotionalTagCatalogData (emotional-tag S001).
///
/// Covers the single acceptance criterion:
///   AC-1: Catalog loads with 18 tags across 8 categories —
///         all TagId, Category, DisplayName queryable; load failure → error
/// </summary>
public class CatalogLoadingTest
{
    // =========================================================================
    // Test Data Factory
    // =========================================================================

    /// <summary>Creates a minimal valid tag for testing.</summary>
    private static EmotionalTagData MakeTag(
        string tagId,
        string displayName,
        EmotionCategory category,
        string parentTagId = null,
        string[] incompatibleWith = null)
    {
        return new EmotionalTagData(
            tagId: tagId,
            displayName: displayName,
            category: category,
            parentTagId: parentTagId,
            incompatibleWith: incompatibleWith,
            associatedColors: new ColorAssociation("#AAAAAA", "#BBBBBB"),
            description: $"Description for {tagId}"
        );
    }

    /// <summary>Creates the full MVP catalog with 18 tags across all 8 categories.</summary>
    private static List<EmotionalTagData> CreateFullMvpCatalog()
    {
        return new List<EmotionalTagData>
        {
            // Joy (3 tags)
            MakeTag("happiness", "欢乐", EmotionCategory.Joy, incompatibleWith: new[] { "sorrow" }),
            MakeTag("contentment", "满足", EmotionCategory.Joy),
            MakeTag("innocence", "童真", EmotionCategory.Joy),

            // Sadness (3 tags)
            MakeTag("loss", "失去", EmotionCategory.Sadness),
            MakeTag("regret", "遗憾", EmotionCategory.Sadness),
            MakeTag("loneliness", "孤独", EmotionCategory.Sadness, incompatibleWith: new[] { "belonging" }),

            // Love (3 tags)
            MakeTag("love", "爱", EmotionCategory.Love),
            MakeTag("nostalgia", "怀念", EmotionCategory.Love, parentTagId: "love"),
            MakeTag("tenderness", "温柔", EmotionCategory.Love, parentTagId: "love"),

            // Fear (2 tags)
            MakeTag("unease", "不安", EmotionCategory.Fear),
            MakeTag("anxiety", "焦虑", EmotionCategory.Fear, parentTagId: "unease"),

            // Anger (2 tags)
            MakeTag("resentment", "怨恨", EmotionCategory.Anger),
            MakeTag("resignation", "不甘", EmotionCategory.Anger),

            // Wonder (2 tags)
            MakeTag("curiosity", "好奇", EmotionCategory.Wonder),
            MakeTag("dreaminess", "梦幻", EmotionCategory.Wonder, parentTagId: "curiosity"),

            // Melancholy (2 tags)
            MakeTag("homesickness", "乡愁", EmotionCategory.Melancholy),
            MakeTag("wistfulness", "怀旧", EmotionCategory.Melancholy),

            // Peace (1 tag)
            MakeTag("acceptance", "接纳", EmotionCategory.Peace),
        };
    }

    // =========================================================================
    // AC-1: Catalog loading — success path
    // =========================================================================

    [Test]
    public void test_catalog_creates_with_18_tags_8_categories()
    {
        // Arrange
        var tags = CreateFullMvpCatalog();

        // Act
        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        // Assert
        Assert.That(catalog.IsLoaded, Is.True);
        Assert.That(catalog.TagCount, Is.EqualTo(18));
        Assert.That(catalog.CategoryCount, Is.EqualTo(8));
    }

    [Test]
    public void test_catalog_all_tags_queryable_by_tag_id()
    {
        // Arrange
        var tags = CreateFullMvpCatalog();
        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        // Act & Assert — every tag should be findable
        foreach (var tag in tags)
        {
            var found = catalog.GetTag(tag.TagId);
            Assert.That(found.HasValue, Is.True, $"Tag '{tag.TagId}' should be queryable");
            Assert.That(found.Value.TagId, Is.EqualTo(tag.TagId));
            Assert.That(found.Value.DisplayName, Is.EqualTo(tag.DisplayName));
            Assert.That(found.Value.Category, Is.EqualTo(tag.Category));
        }
    }

    [Test]
    public void test_catalog_has_tag_returns_true_for_existing()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        Assert.That(catalog.HasTag("nostalgia"), Is.True);
        Assert.That(catalog.HasTag("happiness"), Is.True);
        Assert.That(catalog.HasTag("acceptance"), Is.True);
    }

    [Test]
    public void test_catalog_has_tag_returns_false_for_missing()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        Assert.That(catalog.HasTag("nonexistent"), Is.False);
        Assert.That(catalog.HasTag(""), Is.False);
        Assert.That(catalog.HasTag(null), Is.False);
    }

    [Test]
    public void test_catalog_tags_indexed_by_category()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Joy).Count, Is.EqualTo(3));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Sadness).Count, Is.EqualTo(3));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Love).Count, Is.EqualTo(3));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Fear).Count, Is.EqualTo(2));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Anger).Count, Is.EqualTo(2));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Wonder).Count, Is.EqualTo(2));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Melancholy).Count, Is.EqualTo(2));
        Assert.That(catalog.GetTagIdsByCategory(EmotionCategory.Peace).Count, Is.EqualTo(1));
    }

    [Test]
    public void test_catalog_category_index_contains_correct_tags()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        var loveTags = catalog.GetTagIdsByCategory(EmotionCategory.Love);
        Assert.That(loveTags, Contains.Item("love"));
        Assert.That(loveTags, Contains.Item("nostalgia"));
        Assert.That(loveTags, Contains.Item("tenderness"));
    }

    [Test]
    public void test_catalog_get_tag_returns_null_for_missing()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        var result = catalog.GetTag("phantom_tag");
        Assert.That(result.HasValue, Is.False);
    }

    [Test]
    public void test_catalog_get_tag_returns_null_when_not_loaded()
    {
        var catalog = EmotionalTagCatalogData.CreateError("load failed");

        var result = catalog.GetTag("anything");
        Assert.That(result.HasValue, Is.False);
    }

    // =========================================================================
    // AC-1: Load failure → error state
    // =========================================================================

    [Test]
    public void test_create_error_catalog_is_not_loaded()
    {
        var catalog = EmotionalTagCatalogData.CreateError("Connection timeout");

        Assert.That(catalog.IsLoaded, Is.False);
        Assert.That(catalog.TagCount, Is.EqualTo(0));
        Assert.That(catalog.CategoryCount, Is.EqualTo(0));
    }

    [Test]
    public void test_create_error_catalog_has_error_message()
    {
        var catalog = EmotionalTagCatalogData.CreateError("情感标签数据加载失败");

        Assert.That(catalog.ErrorMessage, Is.Not.Null);
        Assert.That(catalog.ErrorMessage, Does.Contain("情感标签数据加载失败"));
    }

    [Test]
    public void test_create_error_null_message_uses_default()
    {
        var catalog = EmotionalTagCatalogData.CreateError(null);

        Assert.That(catalog.ErrorMessage, Does.Contain("情感标签数据加载失败"));
    }

    [Test]
    public void test_create_error_catalog_all_tags_empty()
    {
        var catalog = EmotionalTagCatalogData.CreateError("fail");

        Assert.That(catalog.AllTags, Is.Empty);
        Assert.That(catalog.HasTag("anything"), Is.False);
    }

    // =========================================================================
    // Edge cases — null/empty input
    // =========================================================================

    [Test]
    public void test_null_tags_list_creates_error_catalog()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(null);

        Assert.That(catalog.IsLoaded, Is.False);
        Assert.That(catalog.ErrorMessage, Does.Contain("null"));
    }

    [Test]
    public void test_empty_tags_list_creates_error_catalog()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(new List<EmotionalTagData>());

        Assert.That(catalog.IsLoaded, Is.False);
        Assert.That(catalog.ErrorMessage, Does.Contain("empty"));
        Assert.That(catalog.TagCount, Is.EqualTo(0));
    }

    // =========================================================================
    // Edge cases — validation
    // =========================================================================

    [Test]
    public void test_duplicate_tag_id_is_rejected()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy),
            MakeTag("hope", "希望_v2", EmotionCategory.Peace),
            MakeTag("fear", "恐惧", EmotionCategory.Fear),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.True, "Should still load with the unique tags");
        Assert.That(catalog.TagCount, Is.EqualTo(2), "Duplicate should be excluded");
        Assert.That(catalog.ValidationErrors.Count, Is.GreaterThan(0));
        Assert.That(catalog.ValidationErrors.Any(e => e.Contains("hope") && e.Contains("重复")), Is.True);
    }

    [Test]
    public void test_empty_tag_id_is_rejected()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag("", "空标签", EmotionCategory.Joy),
            MakeTag("valid", "有效", EmotionCategory.Peace),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.True);
        Assert.That(catalog.TagCount, Is.EqualTo(1));
        Assert.That(catalog.HasTag("valid"), Is.True);
    }

    [Test]
    public void test_null_tag_id_is_rejected()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag(null, "无ID", EmotionCategory.Joy),
            MakeTag("ok", "OK", EmotionCategory.Peace),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.TagCount, Is.EqualTo(1));
        Assert.That(catalog.HasTag("ok"), Is.True);
    }

    [Test]
    public void test_missing_parent_tag_id_is_flagged()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag("orphan", "孤儿标签", EmotionCategory.Sadness, parentTagId: "nonexistent_parent"),
            MakeTag("root", "根标签", EmotionCategory.Joy),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.True);
        Assert.That(catalog.TagCount, Is.EqualTo(2), "Both tags should be present — parent reference is advisory");
        Assert.That(catalog.ValidationErrors.Count, Is.GreaterThan(0));
        Assert.That(catalog.ValidationErrors.Any(e => e.Contains("orphan") && e.Contains("nonexistent_parent")), Is.True);
    }

    [Test]
    public void test_missing_incompatible_with_reference_is_flagged()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag("hope", "希望", EmotionCategory.Joy, incompatibleWith: new[] { "nonexistent_tag" }),
            MakeTag("root", "根标签", EmotionCategory.Peace),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.True);
        Assert.That(catalog.TagCount, Is.EqualTo(2));
        Assert.That(catalog.ValidationErrors.Any(e => e.Contains("hope") && e.Contains("nonexistent_tag")), Is.True);
    }

    [Test]
    public void test_all_tags_rejected_produces_error_catalog()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag(null, "A", EmotionCategory.Joy),
            MakeTag("", "B", EmotionCategory.Sadness),
            MakeTag("", "C", EmotionCategory.Love),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.False);
        Assert.That(catalog.ErrorMessage, Does.Contain("no valid tags"));
    }

    [Test]
    public void test_display_name_can_be_queryable_even_when_empty()
    {
        var tags = new List<EmotionalTagData>
        {
            new EmotionalTagData(
                tagId: "missing_name",
                displayName: "",
                category: EmotionCategory.Peace,
                associatedColors: new ColorAssociation("#FFF", "#AAA")
            ),
            new EmotionalTagData(
                tagId: "has_name",
                displayName: "有名",
                category: EmotionCategory.Joy,
                associatedColors: new ColorAssociation("#FFF", "#AAA")
            ),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.IsLoaded, Is.True);
        Assert.That(catalog.TagCount, Is.EqualTo(2));
        // Tag with empty display name should still be loadable
        var tag = catalog.GetTag("missing_name");
        Assert.That(tag.HasValue, Is.True);
        Assert.That(tag.Value.DisplayName, Is.Empty);
    }

    [Test]
    public void test_validation_errors_list_empty_for_clean_catalog()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        Assert.That(catalog.ValidationErrors, Is.Empty,
            "Full MVP catalog should have no validation errors");
    }

    // =========================================================================
    // Catalog properties
    // =========================================================================

    [Test]
    public void test_all_tags_returns_readonly_copy()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());

        var all = catalog.AllTags;
        Assert.That(all.Count, Is.EqualTo(18));
        // All tags should be unique
        var ids = all.Select(t => t.TagId).ToList();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(18));
    }

    [Test]
    public void test_category_count_returns_distinct_categories()
    {
        var tags = new List<EmotionalTagData>
        {
            MakeTag("a", "A", EmotionCategory.Joy),
            MakeTag("b", "B", EmotionCategory.Joy),
            MakeTag("c", "C", EmotionCategory.Sadness),
        };

        var catalog = EmotionalTagCatalogData.CreateFromTags(tags);

        Assert.That(catalog.CategoryCount, Is.EqualTo(2));
    }

    // =========================================================================
    // ICatalogProvider integration
    // =========================================================================

    private class MockCatalogProvider : ICatalogProvider
    {
        private readonly EmotionalTagCatalogData _catalog;
        public MockCatalogProvider(EmotionalTagCatalogData catalog) => _catalog = catalog;
        public EmotionalTagCatalogData Catalog => _catalog;
    }

    [Test]
    public void test_provider_returns_catalog()
    {
        var catalog = EmotionalTagCatalogData.CreateFromTags(CreateFullMvpCatalog());
        var provider = new MockCatalogProvider(catalog);

        Assert.That(provider.Catalog, Is.SameAs(catalog));
        Assert.That(provider.Catalog.IsLoaded, Is.True);
    }

    [Test]
    public void test_provider_returns_error_catalog()
    {
        var catalog = EmotionalTagCatalogData.CreateError("load fail");
        var provider = new MockCatalogProvider(catalog);

        Assert.That(provider.Catalog.IsLoaded, Is.False);
        Assert.That(provider.Catalog.ErrorMessage, Does.Contain("load fail"));
    }
}
