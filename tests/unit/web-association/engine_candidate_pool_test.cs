using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for WebAssociationEngine — candidate pool and integration (S001).
///
/// Covers 4 acceptance criteria:
///   AC-1: Basic ComputeAssociations returns top 5 sorted by compositeScore
///   AC-2: Empty current tags → Factor A = 0 for all, no exception
///   AC-3: Small pool (≤3) all below 0.05 → returns all candidates with Trace
///   AC-4: Candidate pool filters by UnlockCondition
/// </summary>
public class EngineCandidatePoolTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private const string CHAPTER = "Ch01";
    private const string CURRENT_FRAG = "frag_current";

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

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Creates a MemoryFragment with the given ID and tags.
    /// Tags are specified as (tagId, weight) pairs.
    /// </summary>
    private static MemoryFragment CreateFragment(string fragmentId, params (string, float)[] tags)
    {
        var frag = UnityEngine.ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = fragmentId;
        frag.ChapterKey = CHAPTER;
        frag.UnlockCondition = null;

        if (tags != null && tags.Length > 0)
        {
            var tagList = new List<EmotionalTag>();
            foreach (var (tagId, weight) in tags)
                tagList.Add(new EmotionalTag(tagId, weight));
            frag.EmotionalTags = tagList;
        }

        return frag;
    }

    /// <summary>
    /// Sets up a standard 10-candidate chapter with the current fragment.
    /// Current fragment has Nostalgia:0.9 tag.
    /// Candidates 1-5 have Nostalgia tag (will have non-zero Factor A).
    /// Candidates 6-10 have unrelated tags (Factor A = 0).
    /// </summary>
    private void SetupStandardPool()
    {
        var fragments = new List<MemoryFragment>();

        // Current fragment
        fragments.Add(CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)));

        // Candidates 1-5: share Nostalgia tag (varying weights for score differentiation)
        fragments.Add(CreateFragment("frag_c01", ("Nostalgia", 0.9f)));
        fragments.Add(CreateFragment("frag_c02", ("Nostalgia", 0.7f)));
        fragments.Add(CreateFragment("frag_c03", ("Nostalgia", 0.5f)));
        fragments.Add(CreateFragment("frag_c04", ("Nostalgia", 0.3f)));
        fragments.Add(CreateFragment("frag_c05", ("Nostalgia", 0.1f)));

        // Candidates 6-10: unrelated tags
        fragments.Add(CreateFragment("frag_c06", ("Joy", 0.9f)));
        fragments.Add(CreateFragment("frag_c07", ("Anger", 0.5f)));
        fragments.Add(CreateFragment("frag_c08", ("Fear", 0.5f)));
        fragments.Add(CreateFragment("frag_c09", ("Peace", 0.5f)));
        fragments.Add(CreateFragment("frag_c10", ("Wonder", 0.5f)));

        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        // All fragments "Sadness" dominant category (no rhythm penalty difference)
        _tagSystem.DominantCategories["frag_current"] = "Sadness";
        for (int i = 1; i <= 10; i++)
            _tagSystem.DominantCategories[$"frag_c{i:D2}"] = "Sadness";

        // Tag similarity: same tag = 1.0, others use default rules (all different = 0)
        // Default: GetTagSimilarity returns 0 for all pairs, GetParentTag/GetTagCategory return null
        // So only same-tag matches score 1.0, everything else = 0
    }

    // =========================================================================
    // AC-1: Basic ComputeAssociations returns top 5 sorted by compositeScore
    // =========================================================================

    [Test]
    public void test_compute_associations_returns_top_5_candidates()
    {
        // Arrange
        SetupStandardPool();
        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);

        // Assert
        Assert.That(result.Count, Is.EqualTo(5),
            "Should return exactly 5 candidates (TOP_N = 5)");
    }

    [Test]
    public void test_compute_associations_sorted_descending_by_composite_score()
    {
        // Arrange
        SetupStandardPool();
        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);

        // Assert: sorted descending
        Assert.That(result.Count, Is.GreaterThan(1));
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.That(result[i].CompositeScore, Is.GreaterThanOrEqualTo(result[i + 1].CompositeScore),
                $"result[{i}].CompositeScore should be >= result[{i + 1}].CompositeScore");
        }
    }

    [Test]
    public void test_compute_associations_first_has_highest_score()
    {
        // Arrange
        SetupStandardPool();
        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);

        // Assert: first candidate (frag_c01, Nostalgia 0.9) has highest score
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(result[0].CompositeScore, Is.GreaterThanOrEqualTo(result[result.Count - 1].CompositeScore));
    }

    [Test]
    public void test_compute_associations_exactly_5_candidates_returns_all_5()
    {
        // Arrange: chapter with exactly 5 candidates (6 total including current)
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_A", ("Nostalgia", 0.8f)),
            CreateFragment("frag_B", ("Nostalgia", 0.6f)),
            CreateFragment("frag_C", ("Joy", 0.9f)),
            CreateFragment("frag_D", ("Anger", 0.5f)),
            CreateFragment("frag_E", ("Fear", 0.5f)),
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        foreach (var f in fragments)
            _tagSystem.DominantCategories[f.FragmentId] = "Sadness";

        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);

        // Assert: all 5 candidates returned
        Assert.That(result.Count, Is.EqualTo(5));
    }

    // =========================================================================
    // AC-2: Empty current tags → Factor A = 0, no exception
    // =========================================================================

    [Test]
    public void test_empty_current_tags_factor_a_zero_no_exception()
    {
        // Arrange: current fragment has empty tags
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG), // empty tags
            CreateFragment("frag_A", ("Nostalgia", 0.9f)),
            CreateFragment("frag_B", ("Joy", 0.9f)),
            CreateFragment("frag_C", ("Anger", 0.5f)),
            CreateFragment("frag_D", ("Fear", 0.5f)),
            CreateFragment("frag_E", ("Peace", 0.5f)),
            CreateFragment("frag_F", ("Wonder", 0.5f)),
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        foreach (var f in fragments)
            _tagSystem.DominantCategories[f.FragmentId] = "Sadness";

        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act: should not throw
        List<AssociationCandidate> result = null;
        Assert.DoesNotThrow(() =>
        {
            result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);
        });

        // Assert: all candidates have FactorA = 0
        Assert.That(result, Is.Not.Null);
        foreach (var c in result)
        {
            Assert.That(c.FactorA, Is.EqualTo(0f).Within(0.001f),
                $"Candidate {c.FragmentId} FactorA should be 0 when current has no tags");
        }
    }

    [Test]
    public void test_both_empty_tags_still_runs_no_exception()
    {
        // Arrange: both current and all candidates have empty/null tags
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG), // empty tags
            CreateFragment("frag_A"),     // null/no tags
            CreateFragment("frag_B"),
            CreateFragment("frag_C"),
            CreateFragment("frag_D"),
            CreateFragment("frag_E"),
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER,
            new List<string>(), new HashSet<string>());

        // Assert: runs without exception, all FactorA = 0
        Assert.That(result, Is.Not.Null);
        foreach (var c in result)
            Assert.That(c.FactorA, Is.EqualTo(0f).Within(0.001f));
    }

    // =========================================================================
    // AC-3: Small pool with all scores < 0.05 → returns all with Trace
    // =========================================================================

    [Test]
    public void test_small_pool_all_low_score_returns_all_with_trace()
    {
        // Arrange: 2 candidates, both will have very low scores
        // Use unrelated tags (Factor A = 0), no explicit associations (Factor B = 0)
        // C=1.0 (empty history), D=1.30 (unvisited) → score = (0+0)*1.0*1.30 = 0
        // Score of 0 < 0.05, but pool ≤ 3 → threshold relaxed
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_X", ("Foo", 0.5f)),
            CreateFragment("frag_Y", ("Bar", 0.5f)),
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        foreach (var f in fragments)
            _tagSystem.DominantCategories[f.FragmentId] = "Sadness";

        var history = new List<string>();
        var visited = new HashSet<string>();

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER, history, visited);

        // Assert: returns 2 candidates (both retained despite compositeScore < 0.05)
        Assert.That(result.Count, Is.EqualTo(2),
            "Small pool should return all candidates even if scores are below 0.05");
        foreach (var c in result)
        {
            Assert.That(c.Grade, Is.EqualTo(Strength.Trace),
                $"Candidate {c.FragmentId} with score {c.CompositeScore} should be Trace");
        }
    }

    [Test]
    public void test_single_candidate_pool_returns_one()
    {
        // Edge case: only 1 candidate
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_only", ("Foo", 0.5f)),
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;
        _tagSystem.DominantCategories["frag_current"] = "Sadness";
        _tagSystem.DominantCategories["frag_only"] = "Sadness";

        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER,
            new List<string>(), new HashSet<string>());

        Assert.That(result.Count, Is.EqualTo(1));
    }

    // =========================================================================
    // AC-4: Candidate pool filtering (UnlockCondition)
    // =========================================================================

    [Test]
    public void test_candidate_pool_filters_by_unlock_condition()
    {
        // Arrange: 5 fragments, 1 locked by condition (flag not set)
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_A", ("Nostalgia", 0.5f)),
            CreateFragment("frag_B", ("Nostalgia", 0.5f)),
            CreateFragment("frag_C", ("Nostalgia", 0.5f)),
        };

        // frag_LOCKED requires flag "unlock_d" to be true — it's false
        var fragLocked = CreateFragment("frag_LOCKED", ("Nostalgia", 0.5f));
        fragLocked.UnlockCondition = new ConditionGroup(
            ConditionCombinator.All,
            new System.Collections.Generic.List<Condition> { new ConditionFlagSet("unlock_d", true) }
        );
        fragments.Add(fragLocked);

        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        foreach (var f in fragments)
            _tagSystem.DominantCategories[f.FragmentId] = "Sadness";

        // Prove the flag IS false
        Assert.That(_changeTracker.GetFlag("unlock_d"), Is.False);

        // Act
        var pool = _engine.BuildCandidatePool(CURRENT_FRAG, CHAPTER);

        // Assert: LOCKED fragment excluded
        Assert.That(pool.Count, Is.EqualTo(3),
            "Should exclude locked fragment (4 total - 1 self - 1 locked = 3 candidates, pool excludes self)");

        foreach (var f in pool)
            Assert.That(f.FragmentId, Is.Not.EqualTo("frag_LOCKED"),
                "Locked fragment should not be in candidate pool");
    }

    [Test]
    public void test_unlock_condition_met_fragment_included()
    {
        // Arrange: fragment with unlock condition that IS met
        _changeTracker.SetFlag("unlock_d", true);

        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_A", ("Nostalgia", 0.5f)),
        };

        var fragUnlocked = CreateFragment("frag_unlocked", ("Nostalgia", 0.5f));
        fragUnlocked.UnlockCondition = new ConditionGroup(
            ConditionCombinator.All,
            new System.Collections.Generic.List<Condition> { new ConditionFlagSet("unlock_d", true) }
        );
        fragments.Add(fragUnlocked);

        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        // Act
        var pool = _engine.BuildCandidatePool(CURRENT_FRAG, CHAPTER);

        // Assert
        Assert.That(pool.Exists(f => f.FragmentId == "frag_unlocked"), Is.True,
            "Fragment with met unlock condition should be in pool");
    }

    [Test]
    public void test_all_unlocked_entire_chapter_empty_pool()
    {
        // Arrange: empty chapter
        _dataManager.FragmentsByChapter[CHAPTER] = new List<MemoryFragment>();

        var pool = _engine.BuildCandidatePool("any_frag", CHAPTER);
        Assert.That(pool.Count, Is.EqualTo(0));

        var result = _engine.ComputeAssociations("any_frag", CHAPTER,
            new List<string>(), new HashSet<string>());
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_null_fragments_in_pool_are_skipped()
    {
        // Arrange: fragment list contains null entries
        var fragments = new List<MemoryFragment>
        {
            CreateFragment(CURRENT_FRAG, ("Nostalgia", 0.9f)),
            CreateFragment("frag_A", ("Nostalgia", 0.5f)),
            null, // null entry
            CreateFragment("frag_B", ("Nostalgia", 0.5f)),
            null, // another null
        };
        _dataManager.FragmentsByChapter[CHAPTER] = fragments;

        foreach (var f in fragments)
        {
            if (f != null)
                _tagSystem.DominantCategories[f.FragmentId] = "Sadness";
        }

        // Act
        var result = _engine.ComputeAssociations(CURRENT_FRAG, CHAPTER,
            new List<string>(), new HashSet<string>());

        // Assert: only valid fragments considered
        Assert.That(result.Count, Is.GreaterThan(0));
        Assert.That(result.Count, Is.LessThanOrEqualTo(2));
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

        // Unused methods — throw to catch unexpected calls
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
        /// <summary>fragmentId → dominant category</summary>
        public readonly Dictionary<string, string> DominantCategories = new();
        /// <summary>tagId → parent tag</summary>
        public readonly Dictionary<string, string> ParentTags = new();
        /// <summary>tagId → category</summary>
        public readonly Dictionary<string, string> TagCategories = new();
        /// <summary>(tagA, tagB) → similarity [0,1]</summary>
        public readonly Dictionary<(string, string), float> TagSimilarities = new();
        /// <summary>fragmentId → tags list</summary>
        public readonly Dictionary<string, List<EmotionalTag>> FragmentTags = new();

        public string GetDominantCategory(List<EmotionalTag> tags)
        {
            if (tags == null || tags.Count == 0) return null;
            // Return category of first tag's TagId
            TagCategories.TryGetValue(tags[0].TagId, out var cat);
            return cat;
        }

        public string GetParentTag(string tagId)
        {
            ParentTags.TryGetValue(tagId, out var parent);
            return parent;
        }

        public string GetTagCategory(string tagId)
        {
            TagCategories.TryGetValue(tagId, out var cat);
            return cat;
        }

        public float GetTagSimilarity(string tagIdA, string tagIdB)
        {
            if (TagSimilarities.TryGetValue((tagIdA, tagIdB), out float sim))
                return sim;
            if (TagSimilarities.TryGetValue((tagIdB, tagIdA), out float sim2))
                return sim2;
            return 0f; // No matrix entry → fall through to default rules
        }

        public string GetFragmentDominantCategory(string fragmentId)
        {
            DominantCategories.TryGetValue(fragmentId, out var cat);
            return cat;
        }

        public List<EmotionalTag> GetFragmentTags(string fragmentId)
        {
            FragmentTags.TryGetValue(fragmentId, out var tags);
            return tags ?? new List<EmotionalTag>();
        }
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

        public void MarkVisited(string id) => _visited.Add(id);
    }
}
