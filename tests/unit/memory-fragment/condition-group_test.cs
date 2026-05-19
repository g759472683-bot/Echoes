using NUnit.Framework;
using System.Collections.Generic;

/// <summary>
/// Unit tests for ConditionGroup evaluation engine — covers S002 acceptance criteria.
///
/// Tests All/Any/Not combinators, short-circuit evaluation, depth validation,
/// composite conditions (AC-6), and pure-function guarantees.
/// </summary>
public class ConditionGroupTest
{
    // =========================================================================
    // Mock IChangeTracker
    // =========================================================================

    private class MockChangeTracker : IChangeTracker
    {
        public Dictionary<string, bool> Flags = new Dictionary<string, bool>();
        public Dictionary<string, bool> Choices = new Dictionary<string, bool>();
        public Dictionary<string, ObjectState> ObjectStates = new Dictionary<string, ObjectState>();
        public HashSet<string> VisitedFragments = new HashSet<string>();
        public HashSet<string> CompletedChapters = new HashSet<string>();
        public Dictionary<string, float> TagWeights = new Dictionary<string, float>();

        // Track calls for short-circuit verification
        public int GetFlagCallCount;
        public int GetTagWeightCallCount;
        public int HasChoiceMadeCallCount;

        public bool GetFlag(string flagId)
        {
            GetFlagCallCount++;
            return Flags.TryGetValue(flagId, out var v) && v;
        }

        public bool HasChoiceMade(string fragmentId, string choiceId)
        {
            HasChoiceMadeCallCount++;
            return Choices.TryGetValue($"{fragmentId}:{choiceId}", out var v) && v;
        }

        public ObjectState GetObjectState(string fragmentId, string objectId)
        {
            return ObjectStates.TryGetValue($"{fragmentId}:{objectId}", out var s)
                ? s : ObjectState.Hidden;
        }

        public bool HasVisited(string fragmentId) => VisitedFragments.Contains(fragmentId);

        public bool IsChapterCompleted(string chapterId) => CompletedChapters.Contains(chapterId);

        public float GetTagWeight(string tagId)
        {
            GetTagWeightCallCount++;
            return TagWeights.TryGetValue(tagId, out var w) ? w : 0f;
        }
    }

    // =========================================================================
    // Test Fixture
    // =========================================================================

    private MockChangeTracker _ctx;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MockChangeTracker();
    }

    // =========================================================================
    // AC-1: All Combinator
    // =========================================================================

    [Test]
    public void test_AllCondition_allTrue_returnsTrue()
    {
        // Given: All(FlagSet("met_mentor", true), TagWeight("trust", GT, 0.7))
        _ctx.Flags["met_mentor"] = true;
        _ctx.TagWeights["trust"] = 0.8f;

        var condition = new AllCondition(
            new ConditionFlagSet("met_mentor", true),
            new ConditionTagWeight("trust", 0.7f, WeightComparison.Greater)
        );

        // When
        var result = condition.Evaluate(_ctx);

        // Then
        Assert.IsTrue(result);
    }

    [Test]
    public void test_AllCondition_oneFalse_returnsFalse()
    {
        _ctx.Flags["met_mentor"] = false;
        _ctx.TagWeights["trust"] = 0.8f;

        var condition = new AllCondition(
            new ConditionFlagSet("met_mentor", true),
            new ConditionTagWeight("trust", 0.7f, WeightComparison.Greater)
        );

        var result = condition.Evaluate(_ctx);

        Assert.IsFalse(result);
    }

    [Test]
    public void test_AllCondition_shortCircuitsOnFirstFalse()
    {
        // Given: first is false — short-circuit prevents evaluating the second
        _ctx.Flags["a"] = false;
        var secondEvaluated = false;
        var condition = new AllCondition(
            new ConditionFlagSet("a", true),
            new ConditionAlways() // won't be reached
        );

        // Verify: GetFlag("a") = false, short-circuit prevents second evaluation
        var result = condition.Evaluate(_ctx);

        Assert.IsFalse(result);
        Assert.AreEqual(1, _ctx.GetFlagCallCount);
    }

    [Test]
    public void test_AllCondition_shortCircuit_doesNotCallGetTagWeight()
    {
        // Given: first is FlagSet(false), second is TagWeight
        _ctx.Flags["met_mentor"] = false;
        _ctx.TagWeights["trust"] = 0.8f;

        var condition = new AllCondition(
            new ConditionFlagSet("met_mentor", true),
            new ConditionTagWeight("trust", 0.7f, WeightComparison.Greater)
        );

        condition.Evaluate(_ctx);

        // Then: TagWeight not evaluated (short-circuit)
        Assert.AreEqual(0, _ctx.GetTagWeightCallCount);
    }

    [Test]
    public void test_AllCondition_emptyChildren_returnsTrue()
    {
        var condition = new AllCondition();
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    // =========================================================================
    // AC-2: Any Combinator
    // =========================================================================

    [Test]
    public void test_AnyCondition_oneTrue_returnsTrue()
    {
        // Given: Any(FlagSet("a", true), FlagSet("b", true))
        _ctx.Flags["a"] = true;
        _ctx.Flags["b"] = false;

        var condition = new AnyCondition(
            new ConditionFlagSet("a", true),
            new ConditionFlagSet("b", true)
        );

        var result = condition.Evaluate(_ctx);

        Assert.IsTrue(result);
    }

    [Test]
    public void test_AnyCondition_shortCircuitsOnFirstTrue()
    {
        // Given: first is true — short-circuit prevents evaluating second
        _ctx.Flags["a"] = true;
        _ctx.Flags["b"] = false;

        var condition = new AnyCondition(
            new ConditionFlagSet("a", true),
            new ConditionFlagSet("b", true)
        );

        var result = condition.Evaluate(_ctx);

        Assert.IsTrue(result);
        Assert.AreEqual(1, _ctx.GetFlagCallCount); // "b" never evaluated
    }

    [Test]
    public void test_AnyCondition_allFalse_returnsFalse()
    {
        _ctx.Flags["a"] = false;
        _ctx.Flags["b"] = false;

        var condition = new AnyCondition(
            new ConditionFlagSet("a", true),
            new ConditionFlagSet("b", true)
        );

        var result = condition.Evaluate(_ctx);

        Assert.IsFalse(result);
        Assert.AreEqual(2, _ctx.GetFlagCallCount);
    }

    [Test]
    public void test_AnyCondition_emptyChildren_returnsFalse()
    {
        var condition = new AnyCondition();
        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    // =========================================================================
    // AC-3: Not Combinator
    // =========================================================================

    [Test]
    public void test_NotCondition_falseInput_returnsTrue()
    {
        // Given: Not(FlagSet("hide_letter", true)), flag is false
        _ctx.Flags["hide_letter"] = false;

        var condition = new NotCondition(
            new ConditionFlagSet("hide_letter", true)
        );

        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_NotCondition_trueInput_returnsFalse()
    {
        _ctx.Flags["hide_letter"] = true;

        var condition = new NotCondition(
            new ConditionFlagSet("hide_letter", true)
        );

        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    // =========================================================================
    // AC-4: Depth Validation
    // =========================================================================

    [Test]
    public void test_ValidateDepth_depth3_passes()
    {
        // Depth 3: All(Any(FlagSet("a"), Not(FlagSet("b"))))
        var condition = new AllCondition(
            new AnyCondition(
                new ConditionFlagSet("a", true),
                new NotCondition(
                    new ConditionFlagSet("b", true)
                )
            )
        );

        Assert.IsTrue(ConditionValidator.ValidateDepth(condition, 3));
        Assert.AreEqual(2, ConditionValidator.GetDepth(condition));
    }

    [Test]
    public void test_ValidateDepth_depth4_fails()
    {
        // Depth 4: Not(All(Any(FlagSet("a"), Not(FlagSet("b")))))
        var condition = new NotCondition(
            new AllCondition(
                new AnyCondition(
                    new ConditionFlagSet("a", true),
                    new NotCondition(
                        new ConditionFlagSet("b", true)
                    )
                )
            )
        );

        Assert.IsFalse(ConditionValidator.ValidateDepth(condition, 3));
        Assert.AreEqual(3, ConditionValidator.GetDepth(condition));
    }

    [Test]
    public void test_GetDepth_leafIsZero()
    {
        var condition = new ConditionFlagSet("a", true);
        Assert.AreEqual(0, ConditionValidator.GetDepth(condition));
    }

    [Test]
    public void test_GetDepth_singleCombinator_depth1()
    {
        var condition = new NotCondition(new ConditionFlagSet("a", true));
        Assert.AreEqual(1, ConditionValidator.GetDepth(condition));
    }

    [Test]
    public void test_GetDepthReport_showsCorrectMessage()
    {
        var shallow = new ConditionFlagSet("a", true);
        var report = ConditionValidator.GetDepthReport(shallow);
        Assert.IsTrue(report.Contains("通过"));

        // Depth 4
        var deep = new NotCondition(
            new AllCondition(
                new AnyCondition(
                    new ConditionFlagSet("a", true),
                    new NotCondition(new ConditionFlagSet("b", true))
                )
            )
        );
        var failReport = ConditionValidator.GetDepthReport(deep);
        Assert.IsTrue(failReport.Contains("超出"));
    }

    // =========================================================================
    // AC-5: Pure Function
    // =========================================================================

    [Test]
    public void test_Evaluate_sameInput_sameOutput()
    {
        _ctx.Flags["a"] = true;
        var condition = new ConditionFlagSet("a", true);

        var r1 = condition.Evaluate(_ctx);
        var r2 = condition.Evaluate(_ctx);
        var r3 = condition.Evaluate(_ctx);

        Assert.AreEqual(r1, r2);
        Assert.AreEqual(r2, r3);
    }

    [Test]
    public void test_Evaluate_doesNotMutateTracker()
    {
        _ctx.Flags["a"] = true;
        var beforeCount = _ctx.Flags.Count;

        var condition = new ConditionFlagSet("a", true);
        condition.Evaluate(_ctx);

        // Tracker state unchanged
        Assert.AreEqual(beforeCount, _ctx.Flags.Count);
        Assert.IsTrue(_ctx.Flags["a"]);
    }

    [Test]
    public void test_Evaluate_differentInput_differentOutput()
    {
        var condition = new ConditionFlagSet("a", true);

        _ctx.Flags["a"] = false;
        var r1 = condition.Evaluate(_ctx);

        _ctx.Flags["a"] = true;
        var r2 = condition.Evaluate(_ctx);

        Assert.IsFalse(r1);
        Assert.IsTrue(r2);
    }

    // =========================================================================
    // AC-6: Composite Condition (GDD AC-2)
    // =========================================================================

    [Test]
    public void test_composite_bothConditionsMet_returnsTrue()
    {
        // Given: All(ChoiceMade("ch1_frag_07", "keep_letter"), ChapterCompleted("ch1"))
        _ctx.Choices["ch1_frag_07:keep_letter"] = true;
        _ctx.CompletedChapters.Add("ch1");

        var condition = new AllCondition(
            new ConditionChoiceMade("ch1_frag_07", "keep_letter"),
            new ConditionChapterCompleted("ch1")
        );

        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_composite_onlyChoiceMade_returnsFalse()
    {
        _ctx.Choices["ch1_frag_07:keep_letter"] = true;
        // ch1 NOT completed

        var condition = new AllCondition(
            new ConditionChoiceMade("ch1_frag_07", "keep_letter"),
            new ConditionChapterCompleted("ch1")
        );

        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_composite_onlyChapterComplete_returnsFalse()
    {
        _ctx.CompletedChapters.Add("ch1");
        // Choice NOT made

        var condition = new AllCondition(
            new ConditionChoiceMade("ch1_frag_07", "keep_letter"),
            new ConditionChapterCompleted("ch1")
        );

        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_composite_neitherMet_returnsFalse()
    {
        var condition = new AllCondition(
            new ConditionChoiceMade("ch1_frag_07", "keep_letter"),
            new ConditionChapterCompleted("ch1")
        );

        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    // =========================================================================
    // ConditionGroup wrapper tests
    // =========================================================================

    [Test]
    public void test_ConditionGroup_All_combinator()
    {
        _ctx.Flags["a"] = true;
        _ctx.Flags["b"] = true;

        var group = new ConditionGroup(ConditionCombinator.All, new List<Condition>
        {
            new ConditionFlagSet("a", true),
            new ConditionFlagSet("b", true)
        });

        Assert.IsTrue(group.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionGroup_Any_combinator()
    {
        _ctx.Flags["a"] = false;
        _ctx.Flags["b"] = true;

        var group = new ConditionGroup(ConditionCombinator.Any, new List<Condition>
        {
            new ConditionFlagSet("a", true),
            new ConditionFlagSet("b", true)
        });

        Assert.IsTrue(group.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionGroup_Not_combinator()
    {
        _ctx.Flags["a"] = false;

        var group = new ConditionGroup(ConditionCombinator.Not, new List<Condition>
        {
            new ConditionFlagSet("a", true)
        });

        Assert.IsTrue(group.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionGroup_empty_returnsTrue()
    {
        var group = new ConditionGroup();
        Assert.IsTrue(group.Evaluate(_ctx));
    }

    // =========================================================================
    // Individual leaf condition tests
    // =========================================================================

    [Test]
    public void test_ConditionAlways_alwaysTrue()
    {
        var condition = new ConditionAlways();
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionVisitedFragment_visited_returnsTrue()
    {
        _ctx.VisitedFragments.Add("frag_01");
        var condition = new ConditionVisitedFragment("frag_01");
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionVisitedFragment_notVisited_returnsFalse()
    {
        var condition = new ConditionVisitedFragment("frag_99");
        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionObjectStateIs_matches()
    {
        _ctx.ObjectStates["frag:obj"] = ObjectState.Active;
        var condition = new ConditionObjectStateIs("frag", "obj", ObjectState.Active);
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionObjectStateIs_differs()
    {
        _ctx.ObjectStates["frag:obj"] = ObjectState.Hidden;
        var condition = new ConditionObjectStateIs("frag", "obj", ObjectState.Active);
        Assert.IsFalse(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionTagWeight_greaterOrEqual()
    {
        _ctx.TagWeights["trust"] = 0.8f;
        var condition = new ConditionTagWeight("trust", 0.7f, WeightComparison.GreaterOrEqual);
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionTagWeight_less()
    {
        _ctx.TagWeights["trust"] = 0.3f;
        var condition = new ConditionTagWeight("trust", 0.7f, WeightComparison.Less);
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionTagWeight_equal()
    {
        _ctx.TagWeights["trust"] = 0.700f;
        var condition = new ConditionTagWeight("trust", 0.7f, WeightComparison.Equal);
        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_ConditionTagWeight_equal_closeEnough()
    {
        _ctx.TagWeights["trust"] = 0.70001f;
        var condition = new ConditionTagWeight("trust", 0.7f, WeightComparison.Equal);
        Assert.IsTrue(condition.Evaluate(_ctx)); // within epsilon 0.001
    }

    // =========================================================================
    // Nested composite tests
    // =========================================================================

    [Test]
    public void test_nested_AllOfAny_pattern()
    {
        // All(FlagSet("a", true), Any(FlagSet("b", true), FlagSet("c", true)))
        _ctx.Flags["a"] = true;
        _ctx.Flags["b"] = false;
        _ctx.Flags["c"] = true;

        var condition = new AllCondition(
            new ConditionFlagSet("a", true),
            new AnyCondition(
                new ConditionFlagSet("b", true),
                new ConditionFlagSet("c", true)
            )
        );

        Assert.IsTrue(condition.Evaluate(_ctx));
    }

    [Test]
    public void test_nested_NotOfAll_pattern()
    {
        _ctx.Flags["a"] = true;
        _ctx.Flags["b"] = true;

        // Not(All(FlagSet("a"), FlagSet("b"))) — both true → false
        var condition = new NotCondition(
            new AllCondition(
                new ConditionFlagSet("a", true),
                new ConditionFlagSet("b", true)
            )
        );

        Assert.IsFalse(condition.Evaluate(_ctx));
    }
}
