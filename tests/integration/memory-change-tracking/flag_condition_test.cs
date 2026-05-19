using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Integration tests for Flag system + Condition evaluation (memory-change-tracking S003).
///
/// Covers 5 acceptance criteria:
///   AC-1: SetFlag + ConditionFlagSet evaluation
///   AC-2: VisitedFragment condition
///   AC-3: ChapterCompleted condition
///   AC-4: Unset flag default behavior
///   AC-5: Depth validation (ConditionValidator)
/// </summary>
public class FlagConditionTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private MockFragmentRegistry _registry;
    private MockFragmentStateProvider _stateProvider;
    private ChangeTrackerCore _core;
    private List<string> _warnings;

    [SetUp]
    public void SetUp()
    {
        _registry = new MockFragmentRegistry();
        _stateProvider = new MockFragmentStateProvider();

        _registry.ExistingFragments.Add("frag_A");
        _registry.ExistingFragments.Add("frag_B");
        _registry.ExistingFragments.Add("frag_C");
        _registry.ExistingFragments.Add("frag_D");
        _registry.ExistingFragments.Add("frag_E");

        _registry.LayerMutability[("frag_B", "layer_door")] = true;
        _registry.ExistingObjects.Add(("frag_B", "obj_letter"));
        _registry.ExistingTags.Add("hope");
        _registry.ExistingTags.Add("fear");

        _stateProvider.ExistingFragments.Add("frag_B");
        _stateProvider.VisualLayers["frag_B"] = new List<VisualLayer>();
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("hope", 0.8f),
        };
        _stateProvider.InteractiveObjects["frag_B"] = new List<InteractiveObject>
        {
            new InteractiveObject { ObjectId = "obj_letter", DefaultState = ObjectState.Hidden },
        };

        _core = new ChangeTrackerCore(_registry)
        {
            StateProvider = _stateProvider
        };

        _warnings = new List<string>();
        ChangeTrackerCore.OnWarning += msg => _warnings?.Add(msg);
    }

    [TearDown]
    public void TearDown()
    {
        ChangeTrackerCore.ResetStaticEvents();
    }

    // =========================================================================
    // AC-1: SetFlag + ConditionFlagSet
    // =========================================================================

    [Test]
    public void test_set_flag_then_flag_set_condition_returns_true()
    {
        // AC-1: SetFlag("ch1_letter_kept", true) → FlagSet condition returns true
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("ch1_letter_kept", true),
        });

        Assert.That(_core.GetFlag("ch1_letter_kept"), Is.True);

        var condition = new ConditionFlagSet("ch1_letter_kept", true);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_set_flag_false_then_flag_set_condition_false_returns_true()
    {
        // Flag is false, condition expects false → true
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("ch1_secret_revealed", false),
        });

        var condition = new ConditionFlagSet("ch1_secret_revealed", false);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True,
            "Evaluating FlagSet(false) when flag is false should return true");
    }

    [Test]
    public void test_set_flag_value_mismatch_returns_false()
    {
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("flag_test", true),
        });

        var condition = new ConditionFlagSet("flag_test", false);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False,
            "Evaluating FlagSet(false) when flag is true should return false");
    }

    [Test]
    public void test_set_flag_idempotent_does_not_increment_version()
    {
        _core.ApplyChanges("frag_A", "choice_1", new ContentChange[]
        {
            new SetFlag("flag_idem", true),
        });

        int versionAfterFirst = _core.OverlayVersion;

        // Same value — idempotent
        _core.SetFlag("flag_idem", true);
        Assert.That(_core.OverlayVersion, Is.EqualTo(versionAfterFirst),
            "Setting same flag value should not increment version");

        Assert.That(_core.GetFlag("flag_idem"), Is.True);
    }

    [Test]
    public void test_set_flag_different_value_overwrites()
    {
        _core.SetFlag("flag_change", true);
        Assert.That(_core.GetFlag("flag_change"), Is.True);

        _core.SetFlag("flag_change", false);
        Assert.That(_core.GetFlag("flag_change"), Is.False);

        _core.SetFlag("flag_change", true);
        Assert.That(_core.GetFlag("flag_change"), Is.True);
    }

    // =========================================================================
    // AC-2: VisitedFragment condition
    // =========================================================================

    [Test]
    public void test_visited_fragment_condition_returns_true_after_visit()
    {
        // AC-2: Record visit → HasVisited → ConditionVisitedFragment returns true
        _core.RecordVisit("frag_E");

        Assert.That(_core.HasVisited("frag_E"), Is.True);

        var condition = new ConditionVisitedFragment("frag_E");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_unvisited_fragment_condition_returns_false()
    {
        var condition = new ConditionVisitedFragment("frag_E");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    [Test]
    public void test_empty_fragment_id_visited_returns_false()
    {
        Assert.That(_core.HasVisited(""), Is.False);
        Assert.That(_core.HasVisited(null), Is.False);
    }

    [Test]
    public void test_record_visit_is_idempotent()
    {
        _core.RecordVisit("frag_E");
        int versionAfterFirst = _core.OverlayVersion;

        _core.RecordVisit("frag_E"); // Duplicate
        Assert.That(_core.OverlayVersion, Is.EqualTo(versionAfterFirst),
            "Duplicate visit should not increment version");
    }

    // =========================================================================
    // AC-3: ChapterCompleted condition
    // =========================================================================

    [Test]
    public void test_chapter_completed_condition_returns_true_after_completion()
    {
        // AC-3: Record completion → IsChapterCompleted → ConditionChapterCompleted returns true
        _core.RecordChapterCompleted("ch1");

        Assert.That(_core.IsChapterCompleted("ch1"), Is.True);

        var condition = new ConditionChapterCompleted("ch1");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_uncompleted_chapter_condition_returns_false()
    {
        var condition = new ConditionChapterCompleted("ch2");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    [Test]
    public void test_empty_chapter_id_completed_returns_false()
    {
        Assert.That(_core.IsChapterCompleted(""), Is.False);
        Assert.That(_core.IsChapterCompleted(null), Is.False);
    }

    [Test]
    public void test_record_chapter_completed_is_idempotent()
    {
        _core.RecordChapterCompleted("ch1");
        int versionAfterFirst = _core.OverlayVersion;

        _core.RecordChapterCompleted("ch1"); // Duplicate
        Assert.That(_core.OverlayVersion, Is.EqualTo(versionAfterFirst));
    }

    // =========================================================================
    // AC-4: Unset flag default behavior
    // =========================================================================

    [Test]
    public void test_unset_flag_returns_false_from_get_flag()
    {
        // AC-4: Unset flag → GetFlag returns false
        Assert.That(_core.GetFlag("never_set_flag"), Is.False);
    }

    [Test]
    public void test_unset_flag_with_flag_set_false_condition_returns_true()
    {
        // Unset flag = false, evaluating FlagSet(id, false) → false == false → true
        var condition = new ConditionFlagSet("never_set_flag", false);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True,
            "Unset flag is false, so FlagSet(id, false) should be satisfied");
    }

    [Test]
    public void test_unset_flag_with_flag_set_true_condition_returns_false()
    {
        var condition = new ConditionFlagSet("never_set_flag", true);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    // =========================================================================
    // AC-5: Depth validation
    // =========================================================================

    [Test]
    public void test_depth_4_condition_group_reports_error()
    {
        // AC-5: ConditionGroup depth 4 > max 3
        // Build: All(Any(Not(FlagSet("x", true)))) → depth 4
        var deep = new AllCondition(
            new AnyCondition(
                new NotCondition(
                    new ConditionFlagSet("x", true)
                )
            )
        );

        int depth = ConditionValidator.GetDepth(deep);
        Assert.That(depth, Is.EqualTo(4));

        var report = ConditionValidator.GetDepthReport(deep);
        Assert.That(report, Does.Contain("深度超限"));
    }

    [Test]
    public void test_depth_3_does_not_error()
    {
        // Depth 3: All(Any(FlagSet)) → leaf=0, any=1, all=2 → depth 3
        var valid = new AllCondition(
            new AnyCondition(
                new ConditionFlagSet("x", true),
                new ConditionFlagSet("y", false)
            )
        );

        int depth = ConditionValidator.GetDepth(valid);
        Assert.That(depth, Is.EqualTo(3));

        var report = ConditionValidator.GetDepthReport(valid);
        Assert.That(report, Does.Not.Contain("深度超限"));
    }

    [Test]
    public void test_depth_1_leaf_condition_is_valid()
    {
        int depth = ConditionValidator.GetDepth(new ConditionFlagSet("x", true));
        Assert.That(depth, Is.EqualTo(1));
    }

    // =========================================================================
    // Integration: HasChoiceMade via Overlay
    // =========================================================================

    [Test]
    public void test_has_choice_made_returns_true_after_apply()
    {
        _core.ApplyChanges("frag_A", "choice_42", new ContentChange[]
        {
            new SetFlag("flag_made", true),
        });

        Assert.That(_core.HasChoiceMade("frag_A", "choice_42"), Is.True);

        var condition = new ConditionChoiceMade("frag_A", "choice_42");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_has_choice_made_returns_false_for_unmade_choice()
    {
        Assert.That(_core.HasChoiceMade("frag_A", "never_made"), Is.False);

        var condition = new ConditionChoiceMade("frag_A", "never_made");
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    // =========================================================================
    // Integration: GetObjectState via GetCurrentState
    // =========================================================================

    [Test]
    public void test_get_object_state_returns_resolved_state()
    {
        // Apply SetObjectState overlay
        _core.ApplyChanges("frag_B", "choice_reveal", new ContentChange[]
        {
            new SetObjectState("frag_B", "obj_letter", ObjectState.Active),
        });

        var state = _core.GetObjectState("frag_B", "obj_letter");
        Assert.That(state, Is.EqualTo(ObjectState.Active));
    }

    [Test]
    public void test_get_object_state_returns_hidden_when_no_state_provider()
    {
        var coreNoProvider = new ChangeTrackerCore(_registry); // No StateProvider
        Assert.That(coreNoProvider.GetObjectState("frag_B", "obj_letter"),
            Is.EqualTo(ObjectState.Hidden));
    }

    // =========================================================================
    // Integration: GetTagWeight via GetCurrentState
    // =========================================================================

    [Test]
    public void test_get_tag_weight_returns_resolved_weight()
    {
        _core.ApplyChanges("frag_B", "choice_boost", new ContentChange[]
        {
            new ModifyTagWeight("frag_B", "hope", 0.1f, WeightOperation.Add),
        });

        float weight = _core.GetTagWeight("frag_B", "hope");
        Assert.That(weight, Is.EqualTo(0.9f).Within(0.001f),
            "BaseWeight 0.8 + Add 0.1 = 0.9");
    }

    [Test]
    public void test_condition_tag_weight_evaluates_correctly()
    {
        _core.ApplyChanges("frag_B", "choice_boost", new ContentChange[]
        {
            new ModifyTagWeight("frag_B", "hope", 0.1f, WeightOperation.Add),
        });

        var condition = new ConditionTagWeight("hope", 0.85f, WeightComparison.GreaterOrEqual);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True,
            "hope weight = 0.9 >= 0.85 → true");
    }

    [Test]
    public void test_condition_tag_weight_less_than_threshold()
    {
        var condition = new ConditionTagWeight("hope", 0.9f, WeightComparison.Less);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False,
            "hope weight = 0.8, 0.8 < 0.9 is false");
    }

    // =========================================================================
    // Integration: ConditionGroup.Evaluate (combinator logic)
    // =========================================================================

    [Test]
    public void test_all_combinator_short_circuits_on_first_false()
    {
        _core.SetFlag("flag_a", true);

        var group = new AllCondition(
            new ConditionFlagSet("flag_a", true),   // true
            new ConditionFlagSet("flag_b", true)    // false (unset)
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    [Test]
    public void test_all_combinator_all_true_returns_true()
    {
        _core.SetFlag("flag_a", true);
        _core.SetFlag("flag_b", true);

        var group = new AllCondition(
            new ConditionFlagSet("flag_a", true),
            new ConditionFlagSet("flag_b", true)
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_any_combinator_short_circuits_on_first_true()
    {
        _core.SetFlag("flag_a", true);

        var group = new AnyCondition(
            new ConditionFlagSet("flag_a", true),   // true → short-circuit
            new ConditionFlagSet("flag_b", true)    // would be false but not evaluated
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    [Test]
    public void test_any_combinator_all_false_returns_false()
    {
        var group = new AnyCondition(
            new ConditionFlagSet("flag_a", true),
            new ConditionFlagSet("flag_b", true)
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.False);
    }

    [Test]
    public void test_not_combinator_inverts_result()
    {
        _core.SetFlag("flag_a", true);

        var group = new NotCondition(
            new ConditionFlagSet("flag_a", true)
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.False,
            "Not(true) → false");
    }

    [Test]
    public void test_not_combinator_inverts_false_to_true()
    {
        var group = new NotCondition(
            new ConditionFlagSet("flag_a", true)
        );

        Assert.That(group.Evaluate(new ChangeTrackerAdapter(_core)), Is.True,
            "Not(false) → true");
    }

    [Test]
    public void test_condition_always_returns_true()
    {
        var condition = new ConditionAlways();
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    // =========================================================================
    // Integration: WeightComparison variants
    // =========================================================================

    [Test]
    public void test_weight_comparison_equal_within_tolerance()
    {
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("fear", 0.5001f),
        };

        var condition = new ConditionTagWeight("fear", 0.5f, WeightComparison.Equal);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True,
            "0.5001 ≈ 0.5 within 0.001 tolerance");
    }

    [Test]
    public void test_weight_comparison_greater()
    {
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("fear", 0.5f),
        };

        var condition = new ConditionTagWeight("fear", 0.5f, WeightComparison.Greater);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.False,
            "0.5 is not greater than 0.5");
    }

    [Test]
    public void test_weight_comparison_less()
    {
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("fear", 0.3f),
        };

        var condition = new ConditionTagWeight("fear", 0.5f, WeightComparison.Less);
        Assert.That(condition.Evaluate(new ChangeTrackerAdapter(_core)), Is.True);
    }

    // =========================================================================
    // Edge cases: null/empty inputs
    // =========================================================================

    [Test]
    public void test_get_flag_empty_string_returns_false()
    {
        Assert.That(_core.GetFlag(""), Is.False);
        Assert.That(_core.GetFlag(null), Is.False);
    }

    [Test]
    public void test_set_flag_empty_string_warns_and_skips()
    {
        _core.SetFlag("", true);
        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_core.Flags.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_record_visit_null_does_not_throw()
    {
        Assert.DoesNotThrow(() => _core.RecordVisit(null));
        Assert.That(_core.HasVisited(null), Is.False);
    }

    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockFragmentRegistry : IFragmentRegistry
    {
        public readonly HashSet<string> ExistingFragments = new();
        public readonly Dictionary<(string, string), bool> LayerMutability = new();
        public readonly HashSet<(string, string)> ExistingObjects = new();
        public readonly HashSet<string> ExistingTags = new();

        public bool HasFragment(string fId)
            => !string.IsNullOrEmpty(fId) && ExistingFragments.Contains(fId);

        public bool IsLayerMutable(string fId, string lId)
        {
            if (string.IsNullOrEmpty(fId) || string.IsNullOrEmpty(lId)) return false;
            return LayerMutability.TryGetValue((fId, lId), out bool m) && m;
        }

        public bool HasObject(string fId, string oId)
        {
            if (string.IsNullOrEmpty(fId) || string.IsNullOrEmpty(oId)) return false;
            return ExistingObjects.Contains((fId, oId));
        }

        public bool HasTag(string tId)
            => !string.IsNullOrEmpty(tId) && ExistingTags.Contains(tId);
    }

    private class MockFragmentStateProvider : IFragmentStateProvider
    {
        public readonly HashSet<string> ExistingFragments = new();
        public readonly Dictionary<string, List<VisualLayer>> VisualLayers = new();
        public readonly Dictionary<string, List<EmotionalTag>> EmotionalTags = new();
        public readonly Dictionary<string, List<InteractiveObject>> InteractiveObjects = new();

        public bool HasFragment(string fId)
            => !string.IsNullOrEmpty(fId) && ExistingFragments.Contains(fId);

        public IReadOnlyList<VisualLayer> GetBaseVisualLayers(string fId)
        {
            VisualLayers.TryGetValue(fId, out var list);
            return list;
        }

        public IReadOnlyList<EmotionalTag> GetBaseEmotionalTags(string fId)
        {
            EmotionalTags.TryGetValue(fId, out var list);
            return list;
        }

        public IReadOnlyList<InteractiveObject> GetBaseInteractiveObjects(string fId)
        {
            InteractiveObjects.TryGetValue(fId, out var list);
            return list;
        }
    }

    /// <summary>
    /// Adapts ChangeTrackerCore to IChangeTracker for Condition.Evaluate().
    /// </summary>
    private class ChangeTrackerAdapter : IChangeTracker
    {
        private readonly ChangeTrackerCore _core;
        private string _currentFragmentId;

        public ChangeTrackerAdapter(ChangeTrackerCore core)
        {
            _core = core;
        }

        public bool GetFlag(string flagId) => _core.GetFlag(flagId);
        public bool HasChoiceMade(string fragmentId, string choiceId) => _core.HasChoiceMade(fragmentId, choiceId);

        public ObjectState GetObjectState(string fragmentId, string objectId)
        {
            _currentFragmentId = fragmentId;
            return _core.GetObjectState(fragmentId, objectId);
        }

        public bool HasVisited(string fragmentId) => _core.HasVisited(fragmentId);
        public bool IsChapterCompleted(string chapterId) => _core.IsChapterCompleted(chapterId);

        public float GetTagWeight(string tagId)
        {
            return _core.GetTagWeight(_currentFragmentId ?? "", tagId);
        }
    }
}
