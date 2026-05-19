using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Integration tests for TagWeightResolver + FragmentTagValidator (emotional-tag S003).
///
/// Covers 4 acceptance criteria:
///   AC-1: ModifyTagWeight overlay merge (Add/Multiply/Set, OrderIndex ordering)
///   AC-2: IsPrimary incompatible pair validation
///   AC-3: Empty tags / invalid TagId validation
///   AC-4: Weight clamping [0.0, 1.0]
/// </summary>
public class WeightOverlayTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private static EmotionalTagData MakeCatalogTag(
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

    private static EmotionalTag MakeTag(string tagId, float weight, bool isPrimary = false)
    {
        return new EmotionalTag(tagId, weight, isPrimary);
    }

    // =========================================================================
    // AC-1: ModifyTagWeight overlay merge
    // =========================================================================

    private class MockOverlayProvider : IOverlayProvider
    {
        private readonly Dictionary<string, List<TagWeightOverlay>> _overlays = new();

        public void AddOverlay(string fragmentId, TagWeightOverlay overlay)
        {
            if (!_overlays.ContainsKey(fragmentId))
                _overlays[fragmentId] = new List<TagWeightOverlay>();
            _overlays[fragmentId].Add(overlay);
            // Keep sorted by OrderIndex
            _overlays[fragmentId] = _overlays[fragmentId].OrderBy(o => o.OrderIndex).ToList();
        }

        public List<TagWeightOverlay> GetWeightOverlays(string fragmentId)
        {
            _overlays.TryGetValue(fragmentId, out var list);
            return list ?? new List<TagWeightOverlay>();
        }
    }

    [Test]
    public void test_add_overlay_increases_weight()
    {
        // AC-1: BaseWeight=0.5 + Add 0.2 → 0.7
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_C", new TagWeightOverlay("nostalgia", ModOp.Add, 0.2f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_C", "nostalgia", 0.5f);

        Assert.That(result, Is.EqualTo(0.7f).Within(0.001f));
    }

    [Test]
    public void test_multiply_overlay_scales_weight()
    {
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_A", new TagWeightOverlay("hope", ModOp.Multiply, 0.5f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_A", "hope", 0.8f);

        Assert.That(result, Is.EqualTo(0.4f).Within(0.001f));
    }

    [Test]
    public void test_set_overlay_replaces_weight()
    {
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_A", new TagWeightOverlay("fear", ModOp.Set, 0.99f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_A", "fear", 0.2f);

        Assert.That(result, Is.EqualTo(0.99f).Within(0.001f));
    }

    [Test]
    public void test_multiple_overlays_applied_in_order_index_order()
    {
        // AC-1: Multiple overlays applied by OrderIndex ascending
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_C", new TagWeightOverlay("nostalgia", ModOp.Add, 0.2f, orderIndex: 2));
        overlayProvider.AddOverlay("frag_C", new TagWeightOverlay("nostalgia", ModOp.Multiply, 0.5f, orderIndex: 1));
        // Order: Multiply 0.5 first → 0.5*0.5=0.25, then Add 0.2 → 0.45
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_C", "nostalgia", 0.5f);

        Assert.That(result, Is.EqualTo(0.45f).Within(0.001f));
    }

    [Test]
    public void test_overlay_only_applies_to_matching_tag_id()
    {
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_A", new TagWeightOverlay("hope", ModOp.Add, 0.5f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_A", "fear", 0.5f);

        Assert.That(result, Is.EqualTo(0.5f),
            "Overlay for 'hope' should not affect 'fear'");
    }

    [Test]
    public void test_no_overlays_returns_base_weight()
    {
        var overlayProvider = new MockOverlayProvider();
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("any", "any", 0.75f);

        Assert.That(result, Is.EqualTo(0.75f));
    }

    [Test]
    public void test_null_fragment_id_returns_clamped_base_weight()
    {
        var overlayProvider = new MockOverlayProvider();
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight(null, "hope", 0.5f);

        Assert.That(result, Is.EqualTo(0.5f));
    }

    // =========================================================================
    // AC-2: IsPrimary incompatible pair validation
    // =========================================================================

    private EmotionalTagCatalogData CreateCatalog(List<EmotionalTagData> tags)
    {
        return EmotionalTagCatalogData.CreateFromTags(tags);
    }

    [Test]
    public void test_incompatible_primary_tags_validation_error()
    {
        // AC-2: hope and despair both IsPrimary, hope.IncompatibleWith contains despair
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy, incompatibleWith: new[] { "despair" }),
            MakeCatalogTag("despair", "绝望", EmotionCategory.Sadness, incompatibleWith: new[] { "hope" }),
        });
        var tags = new[]
        {
            MakeTag("hope", 0.8f, true),
            MakeTag("despair", 0.6f, true),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_B", tags, catalog);

        Assert.That(errors.Count, Is.GreaterThan(0));
        Assert.That(errors.Any(e => e.Message.Contains("hope") && e.Message.Contains("despair") && e.Message.Contains("互斥")),
            Is.True);
    }

    [Test]
    public void test_incompatible_tags_as_non_primary_no_error()
    {
        // Edge case: incompatible pair as non-primary tags → no error
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy, incompatibleWith: new[] { "despair" }),
            MakeCatalogTag("despair", "绝望", EmotionCategory.Sadness),
        });
        var tags = new[]
        {
            MakeTag("hope", 0.8f, false),
            MakeTag("despair", 0.6f, false),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_B", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("互斥")), Is.False,
            "Incompatible tags as non-primary should not trigger error");
    }

    // =========================================================================
    // AC-3: Empty tags / invalid TagId validation
    // =========================================================================

    [Test]
    public void test_empty_tags_validation_error()
    {
        // AC-3: Empty EmotionalTags list
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
        });

        var errors = FragmentTagValidator.ValidateFragment("frag_D", new EmotionalTag[0], catalog);

        Assert.That(errors.Count, Is.GreaterThan(0));
        Assert.That(errors[0].Message, Does.Contain("无情感标签"));
        Assert.That(errors[0].Message, Does.Contain("frag_D"));
    }

    [Test]
    public void test_null_tags_validation_error()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
        });

        var errors = FragmentTagValidator.ValidateFragment("frag_D", null, catalog);

        Assert.That(errors.Count, Is.GreaterThan(0));
        Assert.That(errors[0].Message, Does.Contain("无情感标签"));
    }

    [Test]
    public void test_invalid_tag_id_validation_error()
    {
        // AC-3: TagId not in catalog
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
        });
        var tags = new[]
        {
            MakeTag("hope", 0.5f),
            MakeTag("phantom", 0.8f), // Not in catalog
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_E", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("phantom") && e.Message.Contains("不存在于")), Is.True);
    }

    [Test]
    public void test_empty_tag_id_in_fragment_validation_error()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
        });
        var tags = new[]
        {
            new EmotionalTag("", 0.5f), // Empty TagId
            MakeTag("hope", 0.3f),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_F", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("空 TagId")), Is.True);
    }

    // =========================================================================
    // AC-4: Weight clamping
    // =========================================================================

    [Test]
    public void test_weight_clamped_to_one_on_overflow()
    {
        // AC-4: BaseWeight=0.9 + Add 0.3 → 1.0
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_C", new TagWeightOverlay("nostalgia", ModOp.Add, 0.3f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_C", "nostalgia", 0.9f);

        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void test_weight_clamped_to_zero_on_underflow()
    {
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_X", new TagWeightOverlay("hope", ModOp.Add, -1.0f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", 0.3f);

        Assert.That(result, Is.EqualTo(0.0f));
    }

    [Test]
    public void test_base_weight_above_one_clamped_before_overlay()
    {
        // BaseWeight=1.2 → clamped to 1.0, then +0.2 → still 1.0
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_X", new TagWeightOverlay("hope", ModOp.Add, 0.2f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", 1.2f);

        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void test_base_weight_below_zero_clamped_before_overlay()
    {
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_X", new TagWeightOverlay("hope", ModOp.Add, 0.3f));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", -0.5f);

        Assert.That(result, Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void test_nan_base_weight_clamped_to_zero()
    {
        var overlayProvider = new MockOverlayProvider();
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", float.NaN);

        Assert.That(result, Is.EqualTo(0.0f));
    }

    [Test]
    public void test_negative_infinity_clamped_to_zero()
    {
        var overlayProvider = new MockOverlayProvider();
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", float.NegativeInfinity);

        Assert.That(result, Is.EqualTo(0.0f));
    }

    [Test]
    public void test_positive_infinity_clamped_to_one()
    {
        var overlayProvider = new MockOverlayProvider();
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", float.PositiveInfinity);

        Assert.That(result, Is.EqualTo(1.0f));
    }

    [Test]
    public void test_intermediate_result_clamped_between_operations()
    {
        // Multiply 0.8 by 2.0 → 1.6, clamped to 1.0, then Add 0.5 → still 1.0
        var overlayProvider = new MockOverlayProvider();
        overlayProvider.AddOverlay("frag_X", new TagWeightOverlay("hope", ModOp.Multiply, 2.0f, orderIndex: 1));
        overlayProvider.AddOverlay("frag_X", new TagWeightOverlay("hope", ModOp.Add, 0.5f, orderIndex: 2));
        var resolver = new TagWeightResolver(overlayProvider);

        float result = resolver.GetEffectiveWeight("frag_X", "hope", 0.8f);

        Assert.That(result, Is.EqualTo(1.0f),
            "Intermediate clamping should prevent runaway values");
    }

    // =========================================================================
    // Edge cases — constructor
    // =========================================================================

    [Test]
    public void test_resolver_constructor_rejects_null_provider()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new TagWeightResolver(null));
    }

    // =========================================================================
    // Validator edge cases
    // =========================================================================

    [Test]
    public void test_null_catalog_returns_error()
    {
        var tags = new[] { MakeTag("hope", 0.5f) };
        var errors = FragmentTagValidator.ValidateFragment("frag_X", tags, null);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0].Message, Does.Contain("Catalog is null"));
    }

    [Test]
    public void test_unloaded_catalog_returns_error()
    {
        var catalog = EmotionalTagCatalogData.CreateError("not loaded");
        var tags = new[] { MakeTag("hope", 0.5f) };
        var errors = FragmentTagValidator.ValidateFragment("frag_X", tags, catalog);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0].Message, Does.Contain("not loaded"));
    }

    [Test]
    public void test_valid_fragment_produces_no_errors()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
            MakeCatalogTag("peace", "平静", EmotionCategory.Peace),
        });
        var tags = new[]
        {
            MakeTag("hope", 0.8f, true),
            MakeTag("peace", 0.4f, false),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_ok", tags, catalog);

        Assert.That(errors, Is.Empty, "Valid fragment should produce no validation errors");
    }

    [Test]
    public void test_multiple_is_primary_validation_error()
    {
        // Rule 6: Max 1 IsPrimary
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
            MakeCatalogTag("peace", "平静", EmotionCategory.Peace),
            MakeCatalogTag("loss", "失去", EmotionCategory.Sadness),
        });
        var tags = new[]
        {
            MakeTag("hope", 0.5f, true),
            MakeTag("peace", 0.3f, true),
            MakeTag("loss", 0.2f, false),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_G", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("主标签") && e.Message.Contains("最多允许 1 个")), Is.True);
    }

    [Test]
    public void test_max_tags_exceeded_validation_error()
    {
        // Rule 5: Max 5 tags
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("a", "A", EmotionCategory.Joy),
            MakeCatalogTag("b", "B", EmotionCategory.Sadness),
            MakeCatalogTag("c", "C", EmotionCategory.Love),
            MakeCatalogTag("d", "D", EmotionCategory.Fear),
            MakeCatalogTag("e", "E", EmotionCategory.Anger),
            MakeCatalogTag("f", "F", EmotionCategory.Wonder),
        });
        var tags = new[]
        {
            MakeTag("a", 0.5f), MakeTag("b", 0.5f), MakeTag("c", 0.5f),
            MakeTag("d", 0.5f), MakeTag("e", 0.5f), MakeTag("f", 0.5f),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_H", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("6") && e.Message.Contains("5") && e.Message.Contains("超过最大限制")), Is.True);
    }

    [Test]
    public void test_parent_tag_cycle_detection()
    {
        // A's parent is B, B's parent is A → cycle
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("a", "A", EmotionCategory.Joy, parentTagId: "b"),
            MakeCatalogTag("b", "B", EmotionCategory.Joy, parentTagId: "a"),
        });
        var tags = new[]
        {
            MakeTag("a", 0.5f),
        };

        var errors = FragmentTagValidator.ValidateFragment("frag_I", tags, catalog);

        Assert.That(errors.Any(e => e.Message.Contains("循环")), Is.True);
    }

    [Test]
    public void test_batch_validation_validates_all_fragments()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
            MakeCatalogTag("peace", "平静", EmotionCategory.Peace),
        });
        var fragments = new (string, EmotionalTag[])[]
        {
            ("frag_good", new[] { MakeTag("hope", 0.5f) }),
            ("frag_bad", new EmotionalTag[0]), // Empty — should error
            ("frag_also_good", new[] { MakeTag("peace", 0.7f) }),
        };

        var errors = FragmentTagValidator.ValidateFragments(fragments, catalog);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0].FragmentId, Is.EqualTo("frag_bad"));
    }

    [Test]
    public void test_max_tags_per_fragment_constant_is_5()
    {
        Assert.That(FragmentTagValidator.MaxTagsPerFragment, Is.EqualTo(5));
    }

    [Test]
    public void test_null_fragment_id_uses_unknown()
    {
        var catalog = CreateCatalog(new List<EmotionalTagData>
        {
            MakeCatalogTag("hope", "希望", EmotionCategory.Joy),
        });

        var errors = FragmentTagValidator.ValidateFragment(null, new EmotionalTag[0], catalog);

        Assert.That(errors[0].Message, Does.Contain("unknown"));
    }
}
