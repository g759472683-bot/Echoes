using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for WebAssociationEngine Factor A — cosine tag similarity (S002).
///
/// Covers 3 acceptance criteria:
///   AC-1: Tag similarity comparison — related tags score higher than unrelated
///   AC-2: Runtime weight changes reflected in Factor A
///   AC-3: Matrix values used when designer-configured (override default fallback)
/// </summary>
public class FactorATagSimilarityTest
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

    /// <summary>Shorthand to create a tag list.</summary>
    private static List<EmotionalTag> MakeTags(params (string, float)[] pairs)
    {
        var list = new List<EmotionalTag>();
        foreach (var (id, weight) in pairs)
            list.Add(new EmotionalTag(id, weight));
        return list;
    }

    // =========================================================================
    // AC-1: Tag similarity comparison — related tags score higher
    // =========================================================================

    [Test]
    public void test_factor_a_higher_for_related_tags_than_unrelated()
    {
        // Arrange:
        // Current: {Nostalgia:0.9, Rain:0.7}
        // Candidate A: {Rain:0.8, Solitude:0.6} — shares Rain with current
        // Candidate B: {Joy:1.0} — completely unrelated
        //
        // Default similarity rules (no matrix entries):
        //   Rain↔Rain = 1.0 (same tag)
        //   Rain↔Solitude = needs category/parent (both null → 0)
        //   Nostalgia↔Rain = 0, Nostalgia↔Joy = 0, Rain↔Joy = 0
        //
        // Set up parent relationships so Rain and Solitude share parent:
        _tagSystem.ParentTags["Rain"] = "Melancholy";
        _tagSystem.ParentTags["Solitude"] = "Melancholy";
        // Nostalgia and Joy stay without parent (unrelated)

        var currentTags = MakeTags(("Nostalgia", 0.9f), ("Rain", 0.7f));
        var candidateATags = MakeTags(("Rain", 0.8f), ("Solitude", 0.6f));
        var candidateBTags = MakeTags(("Joy", 1.0f));

        // Act
        float aA = _engine.ComputeFactorA(currentTags, candidateATags);
        float aB = _engine.ComputeFactorA(currentTags, candidateBTags);

        // Assert
        Assert.That(aA, Is.GreaterThan(aB),
            $"FactorA(current, A)={aA} should be > FactorA(current, B)={aB} because A shares 'Rain' tag");
        Assert.That(aA, Is.GreaterThan(0f),
            "FactorA(current, A) should be > 0 — shares Rain tag and Rain↔Solitude via same parent");
    }

    [Test]
    public void test_factor_a_same_tag_gives_one()
    {
        // Same single tag with same weight → cosine = 1.0
        var tags1 = MakeTags(("Nostalgia", 0.8f));
        var tags2 = MakeTags(("Nostalgia", 0.8f));

        float a = _engine.ComputeFactorA(tags1, tags2);

        Assert.That(a, Is.EqualTo(1.0f).Within(0.001f),
            "Same tag with same weight should give FactorA = 1.0 (cosine = 1)");
    }

    [Test]
    public void test_factor_a_different_weights_still_one_for_single_same_tag()
    {
        // Cosine similarity of single-dimension vectors is always 1.0 (direction, not magnitude)
        var tags1 = MakeTags(("Nostalgia", 0.9f));
        var tags2 = MakeTags(("Nostalgia", 0.1f));

        float a = _engine.ComputeFactorA(tags1, tags2);

        Assert.That(a, Is.EqualTo(1.0f).Within(0.001f),
            "Cosine similarity of single same-tag vectors is always 1.0 regardless of magnitude");
    }

    [Test]
    public void test_factor_a_zero_for_completely_unrelated_tags()
    {
        // No shared tags, no parent/category overlap, no matrix entries
        var currentTags = MakeTags(("Nostalgia", 0.9f));
        var candidateTags = MakeTags(("Joy", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f),
            "Unrelated tags with no overlap should give FactorA = 0");
    }

    [Test]
    public void test_factor_a_zero_for_null_current_tags()
    {
        var candidateTags = MakeTags(("Joy", 1.0f));

        float a = _engine.ComputeFactorA(null, candidateTags);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_a_zero_for_null_candidate_tags()
    {
        var currentTags = MakeTags(("Joy", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, null);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_a_zero_for_empty_tag_lists()
    {
        var currentTags = new List<EmotionalTag>();
        var candidateTags = MakeTags(("Joy", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f));

        // Reverse
        a = _engine.ComputeFactorA(candidateTags, currentTags);
        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_a_zero_for_zero_weight_tags()
    {
        // All zero weights → norms are 0 → FactorA = 0
        var tags1 = MakeTags(("Nostalgia", 0f));
        var tags2 = MakeTags(("Nostalgia", 0f));

        float a = _engine.ComputeFactorA(tags1, tags2);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_a_ignores_null_tag_ids()
    {
        // Tags with null TagId are skipped
        var currentTags = new List<EmotionalTag>
        {
            new EmotionalTag(null, 0.9f),
            new EmotionalTag("Nostalgia", 0.7f),
        };
        var candidateTags = MakeTags(("Nostalgia", 0.8f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        // Only the Nostalgia tag contributes, null tagId skipped
        Assert.That(a, Is.GreaterThan(0f));
    }

    // =========================================================================
    // AC-2: Runtime weight changes reflected in Factor A
    // =========================================================================

    [Test]
    public void test_factor_a_increases_when_tag_weight_increases()
    {
        // Candidate B initial Nostalgia=0.5 → after change Nostalgia=0.9
        var currentTags = MakeTags(("Nostalgia", 0.9f));

        var candidateBefore = MakeTags(("Nostalgia", 0.5f));
        var candidateAfter = MakeTags(("Nostalgia", 0.9f));

        float aBefore = _engine.ComputeFactorA(currentTags, candidateBefore);
        float aAfter = _engine.ComputeFactorA(currentTags, candidateAfter);

        // Both should be 1.0 for single same-tag (cosine ignores magnitude for single dimension)
        // But with additional tags in a real scenario, weight matters
        // Let's test with current having two tags and candidate having one
        var currentMulti = MakeTags(("Nostalgia", 0.9f), ("Rain", 0.3f));
        var candLow = MakeTags(("Nostalgia", 0.1f));
        var candHigh = MakeTags(("Nostalgia", 0.9f));

        float aLow = _engine.ComputeFactorA(currentMulti, candLow);
        float aHigh = _engine.ComputeFactorA(currentMulti, candHigh);

        Assert.That(aHigh, Is.GreaterThan(aLow),
            $"Higher weight (A={aHigh}) should produce higher cosine than lower weight (A={aLow})");
    }

    [Test]
    public void test_factor_a_drops_to_zero_when_weight_set_to_zero()
    {
        // Tag with weight 0 is skipped (norm contribution = 0)
        var currentTags = MakeTags(("Nostalgia", 0.9f));

        // Candidate with Nostalgia:0 → effectively empty
        var candidateZero = MakeTags(("Nostalgia", 0f));

        float a = _engine.ComputeFactorA(currentTags, candidateZero);

        // Norm of candidate = 0 → FactorA = 0
        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void test_factor_a_reflects_multi_tag_weight_changes()
    {
        // Realistic scenario: Nostalgia weight changes in multi-tag setup
        var currentTags = MakeTags(("Nostalgia", 0.9f), ("Rain", 0.7f));

        // Candidate: Nostalgia + Joy. Nostalgia weight changes from 0.3 to 0.8
        var candOld = new List<EmotionalTag>
        {
            new EmotionalTag("Nostalgia", 0.3f),
            new EmotionalTag("Joy", 0.5f),
        };
        var candNew = new List<EmotionalTag>
        {
            new EmotionalTag("Nostalgia", 0.8f),
            new EmotionalTag("Joy", 0.5f),
        };

        float aOld = _engine.ComputeFactorA(currentTags, candOld);
        float aNew = _engine.ComputeFactorA(currentTags, candNew);

        Assert.That(aNew, Is.GreaterThan(aOld),
            $"FactorA after weight increase ({aNew}) should exceed FactorA before ({aOld})");
    }

    // =========================================================================
    // AC-3: Matrix values override default fallback rules
    // =========================================================================

    [Test]
    public void test_factor_a_uses_matrix_value_over_default()
    {
        // AC-3: Matrix says Rain↔Solitude = 0.8 (not default 0.4 for same category)
        // Default: Rain and Solitude share parent "Melancholy" → 0.6 (same parent)
        // But matrix entry of 0.8 should take precedence

        _tagSystem.ParentTags["Rain"] = "Melancholy";
        _tagSystem.ParentTags["Solitude"] = "Melancholy";
        _tagSystem.TagSimilarities[("Rain", "Solitude")] = 0.8f; // Designer override

        var currentTags = MakeTags(("Rain", 0.7f));
        var candidateTags = MakeTags(("Rain", 0.8f), ("Solitude", 0.6f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        // The 0.8 matrix value contributes to the cross-product:
        // numerator = 0.7*0.8*1.0 + 0.7*0.6*0.8 = 0.56 + 0.336 = 0.896
        // normCurrent = 0.7
        // normCandidate = sqrt(0.64 + 0.36) = sqrt(1.0) = 1.0
        // a = 0.896 / (0.7 * 1.0) = 1.28 → clamped to 1.0
        // Actually wait: normCurrent^2 = 0.7^2 = 0.49, normCurrent = 0.7
        // Cross: 0.7*0.8*1.0 + 0.7*0.6*0.8 = 0.56 + 0.336 = 0.896
        // normCandidate: 0.8^2 + 0.6^2 = 0.64 + 0.36 = 1.0, norm = 1.0
        // Cosine = 0.896 / (0.7 * 1.0) = 1.28 → Clamp01 = 1.0

        // If we use default parent rule (0.6 instead of 0.8):
        // Cross: 0.7*0.8*1.0 + 0.7*0.6*0.6 = 0.56 + 0.252 = 0.812
        // Cosine = 0.812 / 0.7 = 1.16 → Clamp01 = 1.0
        // Both clamp to 1.0 — not a good test

        // Better test: use tags where matrix value makes a visible difference below 1.0
        _tagSystem.TagSimilarities.Clear();

        // Current: {Rain:1.0}, Candidate: {Solitude:1.0}
        // Default (same parent): 0.6 → FactorA = 0.6/1.0 = 0.6
        // Matrix override 0.8: → FactorA = 0.8/1.0 = 0.8
        var simpleCurrent = MakeTags(("Rain", 1.0f));
        var simpleCandidate = MakeTags(("Solitude", 1.0f));

        // First without matrix → uses parent rule (both under Melancholy)
        float aDefault = _engine.ComputeFactorA(simpleCurrent, simpleCandidate);

        // Then with matrix override
        _tagSystem.TagSimilarities[("Rain", "Solitude")] = 0.8f;
        float aOverride = _engine.ComputeFactorA(simpleCurrent, simpleCandidate);

        Assert.That(aOverride, Is.GreaterThan(aDefault),
            $"Matrix override (0.8 → A={aOverride}) should exceed default parent rule (0.6 → A={aDefault})");
        Assert.That(aOverride, Is.EqualTo(0.8f).Within(0.01f),
            "With matrix[Rain][Solitude]=0.8, FactorA should be 0.8");
    }

    [Test]
    public void test_factor_a_falls_back_to_parent_then_category()
    {
        // Verify the fallback chain: matrix → parent → category → 0
        // No matrix entry, no parent → same category gives 0.4
        _tagSystem.TagCategories["Rain"] = "Sadness";
        _tagSystem.TagCategories["Solitude"] = "Sadness";

        var currentTags = MakeTags(("Rain", 1.0f));
        var candidateTags = MakeTags(("Solitude", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        Assert.That(a, Is.EqualTo(0.4f).Within(0.01f),
            "Same category without matrix or parent → FactorA = 0.4");
    }

    [Test]
    public void test_factor_a_parent_takes_precedence_over_category()
    {
        // Parent match (0.6) > category match (0.4) when both exist
        _tagSystem.ParentTags["Rain"] = "Melancholy";
        _tagSystem.ParentTags["Solitude"] = "Melancholy";
        _tagSystem.TagCategories["Rain"] = "Sadness";
        _tagSystem.TagCategories["Solitude"] = "Sadness";

        var currentTags = MakeTags(("Rain", 1.0f));
        var candidateTags = MakeTags(("Solitude", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        // Parent match returns 0.6 before category match is checked
        Assert.That(a, Is.EqualTo(0.6f).Within(0.01f),
            "Parent match should give 0.6, taking precedence over category 0.4");
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Test]
    public void test_factor_a_clamped_to_one()
    {
        // High weights and high similarity shouldn't exceed 1.0
        _tagSystem.TagSimilarities[("Nostalgia", "Memory")] = 1.0f;

        var currentTags = MakeTags(("Nostalgia", 1.0f));
        var candidateTags = MakeTags(("Nostalgia", 1.0f), ("Memory", 1.0f));

        float a = _engine.ComputeFactorA(currentTags, candidateTags);

        Assert.That(a, Is.LessThanOrEqualTo(1.0f));
        Assert.That(a, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void test_factor_a_nan_handling()
    {
        // Edge case that could produce NaN — should be handled gracefully
        // All zero weights
        var empty1 = MakeTags(("A", 0f), ("B", 0f));
        var empty2 = MakeTags(("A", 0f));

        float a = _engine.ComputeFactorA(empty1, empty2);

        Assert.That(a, Is.EqualTo(0f).Within(0.001f));
        Assert.That(float.IsNaN(a), Is.False);
    }

    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockDataManager : IDataManager
    {
        public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
            => new List<MemoryFragment>();
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
        public readonly Dictionary<string, string> ParentTags = new();
        public readonly Dictionary<string, string> TagCategories = new();
        public readonly Dictionary<(string, string), float> TagSimilarities = new();

        public string GetDominantCategory(List<EmotionalTag> tags) => null;
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
            return 0f;
        }
        public string GetFragmentDominantCategory(string fragmentId) => null;
        public List<EmotionalTag> GetFragmentTags(string fragmentId) => new List<EmotionalTag>();
    }

    private class MockChangeTracker : IChangeTracker
    {
        public bool GetFlag(string flagId) => false;
        public bool HasChoiceMade(string fragmentId, string choiceId) => false;
        public ObjectState GetObjectState(string fragmentId, string objectId) => ObjectState.Hidden;
        public bool HasVisited(string fragmentId) => false;
        public bool IsChapterCompleted(string chapterId) => false;
        public float GetTagWeight(string tagId) => 0f;
    }
}
