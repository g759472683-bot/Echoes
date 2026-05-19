using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for WebAssociationEngine Factors B, C, D (S003).
///
/// Covers 5 acceptance criteria:
///   AC-1: B = -1.0 → designer exclusion
///   AC-2: Bidirectional bonus (+0.15)
///   AC-3: Rhythm penalty with sliding window K=4
///   AC-4: Discovery boost (unvisited 1.30 vs visited 0.70)
///   AC-5: Cold start (C = 1.0, D = 1.30 for all)
/// </summary>
public class FactorBCDTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private MockDataManager _dataManager;
    private MockEmotionalTagSystem _tagSystem;
    private MockChangeTracker _changeTracker;
    private WebAssociationEngine _engine;

    [SetUp]
    public void SetUp()
    {
        _dataManager = new MockDataManager();
        _tagSystem = new MockEmotionalTagSystem();
        _changeTracker = new MockChangeTracker();
        _engine = new WebAssociationEngine(_dataManager, _tagSystem, _changeTracker);
    }

    /// <summary>
    /// Creates a bare MemoryFragment with ID, tags, and optional explicit associations.
    /// </summary>
    private static MemoryFragment CreateFragment(
        string fragmentId,
        List<EmotionalTag> tags = null,
        List<FragmentAssociation> associations = null)
    {
        var frag = UnityEngine.ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = fragmentId;
        frag.ChapterKey = "Ch01";
        frag.EmotionalTags = tags ?? new List<EmotionalTag>();
        frag.ExplicitAssociations = associations ?? new List<FragmentAssociation>();
        return frag;
    }

    private static List<EmotionalTag> MakeTags(params (string, float)[] pairs)
    {
        var list = new List<EmotionalTag>();
        foreach (var (id, weight) in pairs)
            list.Add(new EmotionalTag(id, weight));
        return list;
    }

    // =========================================================================
    // AC-1: Factor B — B = -1.0 exclusion
    // =========================================================================

    [Test]
    public void test_factor_b_exclusion_returns_negative_one()
    {
        // Arrange: current has explicit association to candidate A with Weight = -1.0
        var exclusionAssoc = new FragmentAssociation("frag_X", AssociationType.Narrative,
            -1.0f, false, null, "Designer-excluded");

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { exclusionAssoc });
        var candidate = CreateFragment("frag_X");

        // Act
        float b = _engine.ComputeFactorB(current, candidate);

        // Assert: B = -1.0 sentinel (designer exclusion)
        Assert.That(b, Is.LessThanOrEqualTo(WebAssociationEngine.B_EXCLUSION + 0.001f),
            "B should be -1.0 for designer-excluded association");
    }

    [Test]
    public void test_factor_b_exclusion_in_compute_associations_removes_candidate()
    {
        // Integration test: candidate with B=-1.0 should not appear in final result
        var exclusionAssoc = new FragmentAssociation("frag_BAD", AssociationType.Narrative,
            -1.0f, false, null, "Excluded");

        var current = CreateFragment("frag_current",
            tags: MakeTags(("Nostalgia", 0.9f)),
            associations: new List<FragmentAssociation> { exclusionAssoc });

        var candidateA = CreateFragment("frag_A", tags: MakeTags(("Nostalgia", 0.5f)));

        // Build pool: current, A, BAD
        _dataManager.FragmentsByChapter["Ch01"] = new List<MemoryFragment>
            { current, candidateA, CreateFragment("frag_BAD", tags: MakeTags(("Nostalgia", 0.9f))) };
        _tagSystem.DominantCategories["frag_current"] = "Sadness";
        _tagSystem.DominantCategories["frag_A"] = "Sadness";
        _tagSystem.DominantCategories["frag_BAD"] = "Sadness";

        // Act
        var result = _engine.ComputeAssociations("frag_current", "Ch01",
            new List<string>(), new HashSet<string>());

        // Assert: frag_BAD excluded
        foreach (var c in result)
            Assert.That(c.FragmentId, Is.Not.EqualTo("frag_BAD"),
                "B=-1.0 candidate should be excluded from final results");
    }

    [Test]
    public void test_factor_b_no_explicit_association_returns_zero()
    {
        var current = CreateFragment("frag_current");
        var candidate = CreateFragment("frag_other");

        float b = _engine.ComputeFactorB(current, candidate);

        Assert.That(b, Is.EqualTo(0f).Within(0.001f),
            "No explicit association should give B = 0");
    }

    [Test]
    public void test_factor_b_current_null_returns_zero()
    {
        var candidate = CreateFragment("frag_X");

        float b = _engine.ComputeFactorB(null, candidate);

        Assert.That(b, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_b_visibility_condition_hides_association()
    {
        // Association has a VisibilityCondition requiring flag "revealed" = true
        // Flag is false → association hidden → B = 0 (not exclusion)
        var hiddenAssoc = new FragmentAssociation("frag_X", AssociationType.Narrative,
            0.8f, false,
            new ConditionGroup(ConditionCombinator.All,
                new System.Collections.Generic.List<Condition> { new ConditionFlagSet("revealed", true) }));

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { hiddenAssoc });
        var candidate = CreateFragment("frag_X");

        // Flag "revealed" is NOT set
        Assert.That(_changeTracker.GetFlag("revealed"), Is.False);

        float b = _engine.ComputeFactorB(current, candidate);

        // Hidden association → B = 0 (not found / not visible)
        Assert.That(b, Is.EqualTo(0f).Within(0.001f),
            "Hidden association (visibility condition not met) should give B = 0");
    }

    [Test]
    public void test_factor_b_visibility_condition_met_shows_association()
    {
        // Set flag so visibility condition passes
        _changeTracker.SetFlag("revealed", true);

        var visibleAssoc = new FragmentAssociation("frag_X", AssociationType.Narrative,
            0.8f, false,
            new ConditionGroup(ConditionCombinator.All,
                new System.Collections.Generic.List<Condition> { new ConditionFlagSet("revealed", true) }));

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { visibleAssoc });
        var candidate = CreateFragment("frag_X");

        float b = _engine.ComputeFactorB(current, candidate);

        Assert.That(b, Is.EqualTo(0.8f).Within(0.001f),
            "Visible association should return its BaseWeight");
    }

    // =========================================================================
    // AC-2: Factor B — bidirectional bonus
    // =========================================================================

    [Test]
    public void test_factor_b_bidirectional_bonus_adds_015()
    {
        // AC-2: current→Z Weight=0.8, Z→current bidirectional → B = 0.95
        var forwardAssoc = new FragmentAssociation("frag_Z", AssociationType.Narrative,
            0.8f, false);

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { forwardAssoc });

        var reverseAssoc = new FragmentAssociation("frag_current", AssociationType.Narrative,
            0.9f, true); // IsBidirectional = true

        var candidate = CreateFragment("frag_Z",
            associations: new List<FragmentAssociation> { reverseAssoc });

        // Act
        float b = _engine.ComputeFactorB(current, candidate);

        // Assert: 0.8 + 0.15 = 0.95
        Assert.That(b, Is.EqualTo(0.95f).Within(0.001f),
            "B should be 0.8 + 0.15 = 0.95 with bidirectional bonus");
    }

    [Test]
    public void test_factor_b_bidirectional_clamped_to_one()
    {
        // CurrentWeight 0.9 + 0.15 = 1.05 → clamped to 1.0
        var forwardAssoc = new FragmentAssociation("frag_Z", AssociationType.Narrative,
            0.9f, false);

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { forwardAssoc });

        var reverseAssoc = new FragmentAssociation("frag_current", AssociationType.Narrative,
            0.9f, true);

        var candidate = CreateFragment("frag_Z",
            associations: new List<FragmentAssociation> { reverseAssoc });

        float b = _engine.ComputeFactorB(current, candidate);

        Assert.That(b, Is.EqualTo(1.0f).Within(0.001f),
            "B should be clamped to 1.0 (0.9 + 0.15 = 1.05)");
    }

    [Test]
    public void test_factor_b_one_way_association_no_bonus()
    {
        // Only forward association exists, no reverse → no bonus
        var forwardAssoc = new FragmentAssociation("frag_Z", AssociationType.Narrative,
            0.7f, false);

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { forwardAssoc });
        var candidate = CreateFragment("frag_Z"); // No reverse

        float b = _engine.ComputeFactorB(current, candidate);

        Assert.That(b, Is.EqualTo(0.7f).Within(0.001f),
            "Unidirectional association should have no bonus");
    }

    [Test]
    public void test_factor_b_reverse_not_bidirectional_no_bonus()
    {
        // Reverse exists but IsBidirectional = false → no bonus
        var forwardAssoc = new FragmentAssociation("frag_Z", AssociationType.Narrative,
            0.7f, false);

        var current = CreateFragment("frag_current",
            associations: new List<FragmentAssociation> { forwardAssoc });

        var reverseAssoc = new FragmentAssociation("frag_current", AssociationType.Narrative,
            0.5f, false); // NOT bidirectional

        var candidate = CreateFragment("frag_Z",
            associations: new List<FragmentAssociation> { reverseAssoc });

        float b = _engine.ComputeFactorB(current, candidate);

        Assert.That(b, Is.EqualTo(0.7f).Within(0.001f),
            "Reverse with IsBidirectional=false should NOT add bonus");
    }

    // =========================================================================
    // AC-3: Factor C — rhythm penalty
    // =========================================================================

    [Test]
    public void test_factor_c_rhythm_penalty_calculation()
    {
        // AC-3: recentHistory = [A(Sadness), B(Sadness), C(Sadness)]
        // Each matches candidate's dominant category (Sadness)
        // Position 0 penalty = 0.70, position 1 penalty = 0.55
        // C = 0.70 × 0.55 = 0.385

        var history = new List<string> { "frag_A", "frag_B", "frag_C" };
        _tagSystem.SetFragmentCategory("frag_A", "Sadness");
        _tagSystem.SetFragmentCategory("frag_B", "Sadness");
        _tagSystem.SetFragmentCategory("frag_C", "Sadness");

        var candidate = CreateFragment("frag_candidate",
            tags: MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        // Act
        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        // Assert: C = 0.70 × 0.55 = 0.385
        Assert.That(c, Is.EqualTo(0.385f).Within(0.01f));
    }

    [Test]
    public void test_factor_c_four_same_category_clamped_to_min()
    {
        // 4 consecutive same-category → C = 0.70×0.55×0.40×0.25 = 0.0385 → clamped to C_MIN 0.10
        var history = new List<string> { "frag_A", "frag_B", "frag_C", "frag_D" };
        _tagSystem.SetFragmentCategory("frag_A", "Sadness");
        _tagSystem.SetFragmentCategory("frag_B", "Sadness");
        _tagSystem.SetFragmentCategory("frag_C", "Sadness");
        _tagSystem.SetFragmentCategory("frag_D", "Sadness");

        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(WebAssociationEngine.C_MIN).Within(0.001f),
            "4 consecutive same-category should be clamped to C_MIN (0.10)");
    }

    [Test]
    public void test_factor_c_empty_history_returns_one()
    {
        var history = new List<string>();
        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(1.0f).Within(0.001f),
            "Empty history should give C = 1.0 (no penalty)");
    }

    [Test]
    public void test_factor_c_different_category_no_penalty()
    {
        // History fragments have different dominant category than candidate → no penalty
        var history = new List<string> { "frag_A", "frag_B" };
        _tagSystem.SetFragmentCategory("frag_A", "Joy");
        _tagSystem.SetFragmentCategory("frag_B", "Joy");

        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness"; // Different from Joy

        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(1.0f).Within(0.001f),
            "Different category history should give C = 1.0 (no penalty)");
    }

    [Test]
    public void test_factor_c_peace_bonus()
    {
        // Peace category gets ×1.30 bonus
        var history = new List<string>(); // No history → base C = 1.0
        var candidate = CreateFragment("frag_X", MakeTags(("Calm", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Calm"] = "Peace";

        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(WebAssociationEngine.PEACE_BONUS).Within(0.001f),
            "Peace category should get ×1.30 bonus → C = 1.30");
    }

    [Test]
    public void test_factor_c_adaptive_halving_small_pool()
    {
        // Pool ≤ ADAPTIVE_POOL_THRESHOLD (5) → penalties halved
        // Position 0 penalty: 1.0 - (1.0 - 0.70) * 0.5 = 1.0 - 0.15 = 0.85

        var history = new List<string> { "frag_A" };
        _tagSystem.SetFragmentCategory("frag_A", "Sadness");

        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        float cSmall = _engine.ComputeFactorC(history, candidate, poolSize: 4);  // ≤ 5 → adaptive
        float cLarge = _engine.ComputeFactorC(history, candidate, poolSize: 10); // > 5 → normal

        Assert.That(cSmall, Is.GreaterThan(cLarge),
            $"Adaptive (pool=4, C={cSmall}) should be > normal (pool=10, C={cLarge})");
        // Small pool: penalty = 0.85 (halved from 0.70)
        Assert.That(cSmall, Is.EqualTo(0.85f).Within(0.01f));
        // Large pool: penalty = 0.70
        Assert.That(cLarge, Is.EqualTo(0.70f).Within(0.01f));
    }

    [Test]
    public void test_factor_c_null_candidate_returns_one()
    {
        float c = _engine.ComputeFactorC(new List<string>(), null, poolSize: 10);
        Assert.That(c, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void test_factor_c_null_history_returns_base()
    {
        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        float c = _engine.ComputeFactorC(null, candidate, poolSize: 10);

        // Null history → no penalty
        Assert.That(c, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void test_factor_c_empty_dominant_category_returns_one()
    {
        // Candidate has tags but no dominant category registered
        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        // Don't set DominantCategoriesByTags → GetDominantCategory returns null

        float c = _engine.ComputeFactorC(new List<string>(), candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(1.0f).Within(0.001f),
            "Null dominant category should give C = 1.0");
    }

    [Test]
    public void test_factor_c_mixed_history_penalty_only_for_matching()
    {
        // History: [A(Joy), B(Sadness), C(Joy)], candidate = Sadness
        // Only B matches → position 1 penalty = 0.55
        // C = 0.55
        var history = new List<string> { "frag_A", "frag_B", "frag_C" };
        _tagSystem.SetFragmentCategory("frag_A", "Joy");
        _tagSystem.SetFragmentCategory("frag_B", "Sadness");
        _tagSystem.SetFragmentCategory("frag_C", "Joy");

        var candidate = CreateFragment("frag_X", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        float c = _engine.ComputeFactorC(history, candidate, poolSize: 10);

        Assert.That(c, Is.EqualTo(0.55f).Within(0.01f),
            "Only matching category at position 1 should apply penalty 0.55");
    }

    // =========================================================================
    // AC-4: Factor D — discovery boost
    // =========================================================================

    [Test]
    public void test_factor_d_unvisited_returns_130()
    {
        var visited = new HashSet<string>();

        float d = _engine.ComputeFactorD("frag_new", visited);

        Assert.That(d, Is.EqualTo(WebAssociationEngine.D_UNVISITED).Within(0.001f),
            "Unvisited fragment should get D = 1.30");
    }

    [Test]
    public void test_factor_d_visited_returns_070()
    {
        var visited = new HashSet<string> { "frag_old" };

        float d = _engine.ComputeFactorD("frag_old", visited);

        // Visited with no pending changes: max(0.3, 1.0 - 0.3) = 0.7
        // But if HasVisited returns false, no pending changes floor → stays at 0.7
        Assert.That(d, Is.EqualTo(0.70f).Within(0.01f),
            "Single-visit fragment should get D = 0.70");
    }

    [Test]
    public void test_factor_d_unvisited_is_higher_than_visited()
    {
        // AC-4: X unvisited, Y visited → D(X) > D(Y)
        var visited = new HashSet<string> { "frag_Y" };

        float dX = _engine.ComputeFactorD("frag_X", visited);
        float dY = _engine.ComputeFactorD("frag_Y", visited);

        Assert.That(dX, Is.GreaterThan(dY),
            $"D(unvisited)={dX} should be > D(visited)={dY}");
        Assert.That(dX, Is.EqualTo(1.30f).Within(0.001f));
        Assert.That(dY, Is.EqualTo(0.70f).Within(0.001f));
    }

    [Test]
    public void test_factor_d_pending_changes_floor()
    {
        // Fragment has been visited AND has pending changes → floor at 0.70
        var visited = new HashSet<string> { "frag_revisit" };
        _changeTracker.MarkVisited("frag_revisit");

        float d = _engine.ComputeFactorD("frag_revisit", visited);

        // Already at 0.70, pending changes floor is also 0.70 → stays 0.70
        Assert.That(d, Is.GreaterThanOrEqualTo(WebAssociationEngine.D_PENDING_CHANGES_FLOOR),
            "Visited fragment with pending changes should floor at 0.70");
    }

    [Test]
    public void test_factor_d_null_visited_set_returns_unvisited()
    {
        float d = _engine.ComputeFactorD("frag_any", null);

        Assert.That(d, Is.EqualTo(WebAssociationEngine.D_UNVISITED).Within(0.001f),
            "Null visited set → unvisited → D = 1.30");
    }

    [Test]
    public void test_factor_d_clamped_to_min()
    {
        // All scenarios should stay ≥ D_MIN (0.30)
        var visited = new HashSet<string> { "frag_X" };

        float d = _engine.ComputeFactorD("frag_X", visited);

        Assert.That(d, Is.GreaterThanOrEqualTo(WebAssociationEngine.D_MIN),
            "D should never drop below D_MIN (0.30)");
    }

    // =========================================================================
    // AC-5: Cold start
    // =========================================================================

    [Test]
    public void test_cold_start_all_c_is_one()
    {
        // Empty history → all candidates have C = 1.0
        var history = new List<string>();

        var candidate1 = CreateFragment("frag_1", MakeTags(("Sorrow", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Sorrow"] = "Sadness";

        var candidate2 = CreateFragment("frag_2", MakeTags(("Joy", 0.5f)));
        _tagSystem.DominantCategoriesByTags["Joy"] = "Joy";

        float c1 = _engine.ComputeFactorC(history, candidate1, poolSize: 10);
        float c2 = _engine.ComputeFactorC(history, candidate2, poolSize: 10);

        Assert.That(c1, Is.EqualTo(1.0f).Within(0.001f));
        Assert.That(c2, Is.EqualTo(1.0f).Within(0.001f));
    }

    [Test]
    public void test_cold_start_all_d_is_unvisited()
    {
        var visited = new HashSet<string>();

        float d1 = _engine.ComputeFactorD("frag_1", visited);
        float d2 = _engine.ComputeFactorD("frag_2", visited);

        Assert.That(d1, Is.EqualTo(WebAssociationEngine.D_UNVISITED).Within(0.001f));
        Assert.That(d2, Is.EqualTo(WebAssociationEngine.D_UNVISITED).Within(0.001f));
    }

    [Test]
    public void test_cold_start_only_a_and_b_differentiate()
    {
        // AC-5: With empty history and visited, only A and B produce differentiation
        // C = 1.0, D = 1.30 → Score = (A×0.6 + B×0.4) × 1.0 × 1.30
        // Verify by checking C and D constants
        var history = new List<string>();
        var visited = new HashSet<string>();

        var fragments = new List<MemoryFragment>();
        var current = CreateFragment("frag_current",
            tags: MakeTags(("Nostalgia", 0.9f)));

        // frag_A shares tag, frag_B doesn't
        fragments.Add(current);
        fragments.Add(CreateFragment("frag_A", tags: MakeTags(("Nostalgia", 0.5f))));
        fragments.Add(CreateFragment("frag_B", tags: MakeTags(("Joy", 0.5f))));

        _dataManager.FragmentsByChapter["Ch01"] = fragments;
        _tagSystem.DominantCategories["frag_current"] = "Sadness";
        _tagSystem.DominantCategories["frag_A"] = "Sadness";
        _tagSystem.DominantCategories["frag_B"] = "Sadness";

        var result = _engine.ComputeAssociations("frag_current", "Ch01", history, visited);

        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));

        // All should have C=1.0 and D=1.30 (cold start)
        foreach (var c in result)
        {
            Assert.That(c.FactorC, Is.EqualTo(1.0f).Within(0.001f),
                $"Cold start: C should be 1.0 for {c.FragmentId}");
            Assert.That(c.FactorD, Is.EqualTo(WebAssociationEngine.D_UNVISITED).Within(0.001f),
                $"Cold start: D should be {WebAssociationEngine.D_UNVISITED} for {c.FragmentId}");
        }

        // A differentiation: frag_A (shared tag) should score higher than frag_B (unrelated)
        var aScore = result.Find(r => r.FragmentId == "frag_A").CompositeScore;
        var bScore = result.Find(r => r.FragmentId == "frag_B").CompositeScore;
        Assert.That(aScore, Is.GreaterThan(bScore),
            "With same C and D, only A and B differentiate. Shared tag should score higher.");
    }

    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockDataManager : IDataManager
    {
        public readonly Dictionary<string, List<MemoryFragment>> FragmentsByChapter = new();

        public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
        {
            FragmentsByChapter.TryGetValue(chapterKey, out var list);
            return list ?? new List<MemoryFragment>();
        }

        public System.Threading.Tasks.Task<ChapterDefinition> GetChapterAsync(string chapterKey)
            => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)
            => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<UnityEngine.Sprite> GetIllustrationAsync(string illustrationKey)
            => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<UnityEngine.Sprite> GetIllustrationAsync(string illustrationKey, string fragmentId)
            => throw new System.NotImplementedException();
        public bool IsReady(string assetKey) => false;
        public MemoryFragment GetCachedFragment(string chapterKey, string fragmentId) => null;
        public void SetCurrentChapter(string chapterKey) { }
        public void CheckAndTriggerPreload(string currentChapterKey, int remainingFragments) { }
        public void UnloadChapter(string chapterKey) { }
        public void ReleaseFragment(string fragmentId) { }
        public System.Threading.Tasks.Task PreloadChapterAsync(string chapterKey)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task PreloadFragmentAsync(string chapterKey, string fragmentId)
            => System.Threading.Tasks.Task.CompletedTask;
        public string SerializeState<T>(T state) where T : class => "{}";
        public T DeserializeState<T>(string json) where T : class => default;
    }

    private class MockEmotionalTagSystem : IEmotionalTagSystem
    {
        private readonly Dictionary<string, string> _fragmentCategories = new();
        /// <summary>tagId → dominant category (for GetDominantCategory)</summary>
        public readonly Dictionary<string, string> DominantCategoriesByTags = new();

        public void SetFragmentCategory(string fragmentId, string category)
            => _fragmentCategories[fragmentId] = category;

        public string GetDominantCategory(List<EmotionalTag> tags)
        {
            if (tags == null || tags.Count == 0) return null;
            DominantCategoriesByTags.TryGetValue(tags[0].TagId, out var cat);
            return cat;
        }
        public string GetParentTag(string tagId) => null;
        public string GetTagCategory(string tagId) => null;
        public float GetTagSimilarity(string tagIdA, string tagIdB) => 0f;
        public string GetFragmentDominantCategory(string fragmentId)
        {
            _fragmentCategories.TryGetValue(fragmentId, out var cat);
            return cat;
        }
        public List<EmotionalTag> GetFragmentTags(string fragmentId) => new List<EmotionalTag>();
    }

    private class MockChangeTracker : IChangeTracker
    {
        private readonly Dictionary<string, bool> _flags = new();
        private readonly HashSet<string> _visited = new();

        public void SetFlag(string id, bool value) => _flags[id] = value;
        public bool GetFlag(string flagId) => _flags.TryGetValue(flagId, out bool v) && v;
        public bool HasChoiceMade(string fragmentId, string choiceId) => false;
        public ObjectState GetObjectState(string fragmentId, string objectId) => ObjectState.Hidden;
        public bool HasVisited(string fragmentId) => _visited.Contains(fragmentId);
        public bool IsChapterCompleted(string chapterId) => false;
        public float GetTagWeight(string tagId) => 0f;

        public void MarkVisited(string fragmentId) => _visited.Add(fragmentId);
    }
}
