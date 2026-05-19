using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for FragmentValidator and ImmutableLayerGuard (memory-fragment S003).
///
/// Covers 5 of 6 acceptance criteria (AC-6 TableReference requires Unity localization runtime):
///   AC-1: Cross-fragment same-chapter target passes validation
///   AC-2: Cross-chapter target + nonexistent target produce errors
///   AC-3: Duplicate FragmentId in same chapter produces error
///   AC-4: Object count >5 and ChoiceGroup count >2 produce warnings
///   AC-5: Immutable layer protection rejects ToggleVisualLayer changes
/// </summary>
public class EditorValidationTest
{
    // =========================================================================
    // Mock IFragmentValidationTarget for testing
    // =========================================================================

    private class MockFragment : IFragmentValidationTarget
    {
        public string FragmentId { get; set; }
        public string ChapterKey { get; set; }
        public InteractiveObject[] InteractiveObjects { get; set; }
        public ChoiceGroup[] ChoiceGroups { get; set; }

        public MockFragment(string fragmentId, string chapterKey)
        {
            FragmentId = fragmentId;
            ChapterKey = chapterKey;
            InteractiveObjects = System.Array.Empty<InteractiveObject>();
            ChoiceGroups = System.Array.Empty<ChoiceGroup>();
        }
    }

    private static InteractiveObject CreateDummyObject(string objectId)
    {
        return new InteractiveObject { ObjectId = objectId };
    }

    private static ChoiceGroup CreateChoiceGroup(string groupId, params ContentChange[] changes)
    {
        var choice = new Choice
        {
            ChoiceId = "choice_01",
            ContentChanges = new List<ContentChange>(changes ?? System.Array.Empty<ContentChange>())
        };
        return new ChoiceGroup
        {
            GroupId = groupId,
            Choices = new Choice[] { choice }
        };
    }

    // =========================================================================
    // AC-1: Cross-fragment same-chapter target passes
    // =========================================================================

    [Test]
    public void test_CrossFragmentSameChapter_NoError()
    {
        var fragA = new MockFragment("frag_01", "ch01");
        var fragB = new MockFragment("frag_02", "ch01");
        fragA.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer("frag_02", "layer_01", true))
        };

        var errors = FragmentValidator.ValidateAll(new[] { fragA, fragB });

        Assert.AreEqual(0, errors.Count, "Same-chapter cross-fragment target should not produce errors");
    }

    [Test]
    public void test_SelfTarget_NoError()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer("frag_01", "layer_01", true))
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Self-targeting ContentChange should be valid");
    }

    [Test]
    public void test_NullOrEmptyTarget_NoError()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer(null, "layer_01", true))
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Null/empty TargetFragmentId should be treated as self-target (valid)");
    }

    // =========================================================================
    // AC-2: Cross-chapter target + nonexistent target produce errors
    // =========================================================================

    [Test]
    public void test_CrossChapterTarget_Error()
    {
        var fragA = new MockFragment("frag_01", "ch01");
        var fragB = new MockFragment("frag_05", "ch02");
        fragA.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer("frag_05", "layer_01", true))
        };

        var errors = FragmentValidator.ValidateAll(new[] { fragA, fragB });

        Assert.AreEqual(1, errors.Count, "Cross-chapter target should produce 1 error");
        Assert.AreEqual(ValidationErrorLevel.Error, errors[0].Level);
        StringAssert.Contains("不同章节", errors[0].Message);
        StringAssert.Contains("frag_01", errors[0].Message);
        StringAssert.Contains("frag_05", errors[0].Message);
    }

    [Test]
    public void test_NonexistentTarget_Error()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer("frag_99", "layer_01", true))
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(1, errors.Count, "Nonexistent target should produce 1 error");
        Assert.AreEqual(ValidationErrorLevel.Error, errors[0].Level);
        StringAssert.Contains("不存在", errors[0].Message);
        StringAssert.Contains("frag_99", errors[0].Message);
    }

    // =========================================================================
    // AC-3: Duplicate FragmentId detection
    // =========================================================================

    [Test]
    public void test_DuplicateFragmentIdSameChapter_Error()
    {
        var frag1 = new MockFragment("frag_03", "ch01");
        var frag2 = new MockFragment("frag_03", "ch01");

        var errors = FragmentValidator.ValidateAll(new[] { frag1, frag2 });

        Assert.AreEqual(1, errors.Count, "Duplicate FragmentId in same chapter should produce error");
        Assert.AreEqual(ValidationErrorLevel.Error, errors[0].Level);
        StringAssert.Contains("frag_03", errors[0].Message);
        StringAssert.Contains("重复", errors[0].Message);
    }

    [Test]
    public void test_DuplicateFragmentIdDifferentChapter_NoError()
    {
        var frag1 = new MockFragment("frag_03", "ch01");
        var frag2 = new MockFragment("frag_03", "ch02");

        var errors = FragmentValidator.ValidateAll(new[] { frag1, frag2 });

        // FragmentId uniqueness is per chapter — different chapters is fine
        Assert.AreEqual(0, errors.Count,
            "Same FragmentId in different chapters should be valid (uniqueness is per chapter)");
    }

    [Test]
    public void test_TripleDuplicate_Error()
    {
        var frag1 = new MockFragment("frag_01", "ch01");
        var frag2 = new MockFragment("frag_01", "ch01");
        var frag3 = new MockFragment("frag_01", "ch01");

        var errors = FragmentValidator.ValidateAll(new[] { frag1, frag2, frag3 });

        Assert.AreEqual(1, errors.Count, "3 duplicates should produce 1 error (one per duplicate group)");
        StringAssert.Contains("3 个实例", errors[0].Message);
    }

    // =========================================================================
    // AC-4: Object count and ChoiceGroup count warnings
    // =========================================================================

    [Test]
    public void test_ObjectCountAtLimit_NoWarning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.InteractiveObjects = new InteractiveObject[]
        {
            CreateDummyObject("obj_1"), CreateDummyObject("obj_2"),
            CreateDummyObject("obj_3"), CreateDummyObject("obj_4"),
            CreateDummyObject("obj_5")
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Exactly 5 interactive objects should not produce warning");
    }

    [Test]
    public void test_ObjectCountExceedsLimit_Warning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.InteractiveObjects = new InteractiveObject[]
        {
            CreateDummyObject("obj_1"), CreateDummyObject("obj_2"),
            CreateDummyObject("obj_3"), CreateDummyObject("obj_4"),
            CreateDummyObject("obj_5"), CreateDummyObject("obj_6")
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(ValidationErrorLevel.Warning, errors[0].Level);
        StringAssert.Contains("物件数", errors[0].Message);
        StringAssert.Contains("6", errors[0].Message);
        StringAssert.Contains("5", errors[0].Message);
    }

    [Test]
    public void test_ChoiceGroupCountAtLimit_NoWarning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            new ChoiceGroup { GroupId = "cg1" },
            new ChoiceGroup { GroupId = "cg2" }
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Exactly 2 ChoiceGroups should not produce warning");
    }

    [Test]
    public void test_ChoiceGroupCountExceedsLimit_Warning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            new ChoiceGroup { GroupId = "cg1" },
            new ChoiceGroup { GroupId = "cg2" },
            new ChoiceGroup { GroupId = "cg3" }
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(ValidationErrorLevel.Warning, errors[0].Level);
        StringAssert.Contains("ChoiceGroup", errors[0].Message);
        StringAssert.Contains("3", errors[0].Message);
        StringAssert.Contains("2", errors[0].Message);
    }

    [Test]
    public void test_BothCountsExceeded_TwoWarnings()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.InteractiveObjects = new InteractiveObject[]
        {
            CreateDummyObject("o1"), CreateDummyObject("o2"), CreateDummyObject("o3"),
            CreateDummyObject("o4"), CreateDummyObject("o5"), CreateDummyObject("o6")
        };
        frag.ChoiceGroups = new ChoiceGroup[]
        {
            new ChoiceGroup { GroupId = "cg1" },
            new ChoiceGroup { GroupId = "cg2" },
            new ChoiceGroup { GroupId = "cg3" }
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(2, errors.Count, "Both limits exceeded should produce 2 warnings");
        Assert.IsTrue(errors.TrueForAll(e => e.Level == ValidationErrorLevel.Warning));
    }

    // =========================================================================
    // FragmentValidator — edge cases
    // =========================================================================

    [Test]
    public void test_EmptyList_ReturnsEmpty()
    {
        var errors = FragmentValidator.ValidateAll(System.Array.Empty<IFragmentValidationTarget>());
        Assert.AreEqual(0, errors.Count);
    }

    [Test]
    public void test_NullList_ReturnsEmpty()
    {
        var errors = FragmentValidator.ValidateAll(null);
        Assert.AreEqual(0, errors.Count);
    }

    [Test]
    public void test_NullInteractiveObjects_NoWarning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.InteractiveObjects = null;

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Null InteractiveObjects should not produce warning");
    }

    [Test]
    public void test_NullChoiceGroups_NoWarning()
    {
        var frag = new MockFragment("frag_01", "ch01");
        frag.ChoiceGroups = null;

        var errors = FragmentValidator.ValidateAll(new[] { frag });

        Assert.AreEqual(0, errors.Count, "Null ChoiceGroups should not produce warning");
    }

    [Test]
    public void test_MultipleFragmentsAllValid()
    {
        var fragments = new IFragmentValidationTarget[]
        {
            new MockFragment("frag_01", "ch01"),
            new MockFragment("frag_02", "ch01"),
            new MockFragment("frag_03", "ch01"),
        };

        var errors = FragmentValidator.ValidateAll(fragments);

        Assert.AreEqual(0, errors.Count, "All-valid fragment set should produce no errors");
    }

    [Test]
    public void test_MultipleErrorsAggregated()
    {
        var frag1 = new MockFragment("frag_01", "ch01");
        var frag2 = new MockFragment("frag_01", "ch01"); // duplicate
        frag1.ChoiceGroups = new ChoiceGroup[]
        {
            CreateChoiceGroup("cg1", new ToggleVisualLayer("frag_99", "layer_01", true)) // nonexistent target
        };

        var errors = FragmentValidator.ValidateAll(new[] { frag1, frag2 });

        Assert.GreaterOrEqual(errors.Count, 2, "Should aggregate duplicate error + nonexistent target error");
    }

    // =========================================================================
    // AC-5: ImmutableLayerGuard — Immutable layer protection
    // =========================================================================

    private static List<VisualLayer> CreateLayers(params (string id, bool mutable)[] layers)
    {
        var result = new List<VisualLayer>();
        foreach (var (id, mutable) in layers)
        {
            result.Add(new VisualLayer(id, "sprite_ref", true, 0, default, mutable));
        }
        return result;
    }

    [Test]
    public void test_MutableLayer_ToggleAllowed()
    {
        var layers = CreateLayers(("rain_layer", true));
        var change = new ToggleVisualLayer("frag_01", "rain_layer", false);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, layers, out string reason);

        Assert.IsTrue(allowed);
        Assert.IsNull(reason);
    }

    [Test]
    public void test_ImmutableLayer_ToggleRejected()
    {
        var layers = CreateLayers(("background", false));
        var change = new ToggleVisualLayer("frag_01", "background", false);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, layers, out string reason);

        Assert.IsFalse(allowed);
        Assert.IsNotNull(reason);
        StringAssert.Contains("不可变", reason);
        StringAssert.Contains("background", reason);
    }

    [Test]
    public void test_LayerNotFound_Rejected()
    {
        var layers = CreateLayers(("rain_layer", true));
        var change = new ToggleVisualLayer("frag_01", "nonexistent_layer", false);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, layers, out string reason);

        Assert.IsFalse(allowed);
        Assert.IsNotNull(reason);
        StringAssert.Contains("不存在", reason);
        StringAssert.Contains("nonexistent_layer", reason);
    }

    [Test]
    public void test_NullChange_Rejected()
    {
        var layers = CreateLayers(("layer_01", true));

        bool allowed = ImmutableLayerGuard.CanApplyChange(null, layers, out string reason);

        Assert.IsFalse(allowed);
        Assert.IsNotNull(reason);
        StringAssert.Contains("null", reason);
    }

    [Test]
    public void test_NullVisualLayers_Rejected()
    {
        var change = new ToggleVisualLayer("frag_01", "layer_01", true);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, null, out string reason);

        Assert.IsFalse(allowed);
        Assert.IsNotNull(reason);
        StringAssert.Contains("null", reason);
    }

    [Test]
    public void test_MultipleLayers_ImmutableOneRejected()
    {
        var layers = CreateLayers(
            ("mutable_a", true),
            ("immutable_b", false),
            ("mutable_c", true)
        );
        var change = new ToggleVisualLayer("frag_01", "immutable_b", false);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, layers, out string reason);

        Assert.IsFalse(allowed);
        StringAssert.Contains("不可变", reason);
    }

    [Test]
    public void test_MultipleLayers_MutableOneAllowed()
    {
        var layers = CreateLayers(
            ("mutable_a", true),
            ("immutable_b", false),
            ("mutable_c", true)
        );
        var change = new ToggleVisualLayer("frag_01", "mutable_c", true);

        bool allowed = ImmutableLayerGuard.CanApplyChange(change, layers, out string reason);

        Assert.IsTrue(allowed);
        Assert.IsNull(reason);
    }

    // =========================================================================
    // AC-6: TableReference validation — DEFERRED
    // Requires Unity localization runtime (com.unity.localization) to resolve
    // string table keys. Implemented as a placeholder check when the
    // localization package is integrated.
    // =========================================================================
}
