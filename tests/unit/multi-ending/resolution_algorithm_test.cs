using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for the three-stage resolution algorithm (S002).
///
/// Covers 4 acceptance criteria:
///   AC-1: Basic score accumulation — triggers sum ContributionWeight, winner >= MinimumScore
///   AC-2: Essential gate failure — any IsEssential condition not met → disqualify
///   AC-3: Default fallback — no qualified endings → return IsDefault=true ending
///   AC-4: Threshold not met — score < MinimumScore → excluded, below-threshold fallback
/// </summary>
public class ResolutionAlgorithmTest
{
    // =========================================================================
    // Mocks (same pattern as data_structures_trigger_collection_test.cs)
    // =========================================================================

    private class MockEndingDefinitionProvider : IEndingDefinitionProvider
    {
        public readonly Dictionary<string, List<EndingDefinition>> Definitions = new();
        public List<EndingDefinition> GetEndingDefinitions(string chapterKey)
        {
            Definitions.TryGetValue(chapterKey, out var list);
            return list ?? new List<EndingDefinition>();
        }
    }

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
        public System.Threading.Tasks.Task<Sprite> GetIllustrationAsync(string illustrationKey)
            => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<Sprite> GetIllustrationAsync(string illustrationKey, string fragmentId)
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

    private class MockChangeTracker : IChangeTracker
    {
        private readonly Dictionary<string, bool> _flags = new();
        private readonly HashSet<string> _visited = new();
        private readonly Dictionary<string, bool> _choices = new();
        private readonly HashSet<string> _completedChapters = new();
        private readonly Dictionary<string, float> _tagWeights = new();

        public void SetFlag(string id, bool value) => _flags[id] = value;
        public bool GetFlag(string flagId) => _flags.TryGetValue(flagId, out bool v) && v;
        public bool HasChoiceMade(string fragmentId, string choiceId)
            => _choices.TryGetValue($"{fragmentId}|{choiceId}", out bool v) && v;
        public void SetChoice(string fragmentId, string choiceId)
            => _choices[$"{fragmentId}|{choiceId}"] = true;
        public ObjectState GetObjectState(string fragmentId, string objectId) => ObjectState.Hidden;
        public bool HasVisited(string fragmentId) => _visited.Contains(fragmentId);
        public void MarkVisited(string id) => _visited.Add(id);
        public bool IsChapterCompleted(string chapterId) => _completedChapters.Contains(chapterId);
        public void MarkChapterCompleted(string id) => _completedChapters.Add(id);
        public float GetTagWeight(string tagId)
            => _tagWeights.TryGetValue(tagId, out float w) ? w : 0f;
        public void SetTagWeight(string tagId, float weight) => _tagWeights[tagId] = weight;
    }

    private class MockEmotionalTagSystem : IEmotionalTagSystem
    {
        public readonly Dictionary<string, string> FragmentDominantCategories = new();
        public readonly Dictionary<string, List<EmotionalTag>> FragmentTags = new();
        public readonly Dictionary<string, string> ParentTags = new();
        public readonly Dictionary<string, string> TagCategories = new();
        public readonly Dictionary<(string, string), float> TagSimilarities = new();

        public string GetDominantCategory(List<EmotionalTag> tags)
        {
            if (tags == null || tags.Count == 0) return null;
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
        public float GetTagSimilarity(string tagIdA, string tagIdB) => 0.5f;
        public string GetFragmentDominantCategory(string fragmentId)
        {
            FragmentDominantCategories.TryGetValue(fragmentId, out var cat);
            return cat;
        }
        public List<EmotionalTag> GetFragmentTags(string fragmentId)
        {
            FragmentTags.TryGetValue(fragmentId, out var tags);
            return tags ?? new List<EmotionalTag>();
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MemoryFragment CreateFragment(string id)
    {
        var frag = ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = id;
        frag.ChapterKey = "ch01";
        frag.EndingTriggers = new List<EndingTrigger>();
        return frag;
    }

    private static EndingDefinition CreateEndingDef(string endingId, bool isDefault = false,
        float minimumScore = 0.5f, EndingType endingType = EndingType.ChapterEnding)
    {
        return new EndingDefinition(endingId, endingType, "ch01", minimumScore, isDefault);
    }

    private static EndingTrigger CreateTrigger(string endingId, float weight = 0.5f,
        bool isEssential = false, ConditionGroup condition = null)
    {
        return new EndingTrigger(endingId, condition, weight, isEssential);
    }

    // =========================================================================
    // AC-1: Basic score accumulation
    // =========================================================================

    [Test]
    public void test_score_accumulation_contribution_weights_sum_and_threshold_pass()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
        };

        // Simulate story scenario: keep_letter(0.4) + open_window(0.3) = 0.7
        changeTracker.SetChoice("frag_03", "keep_letter");
        changeTracker.SetChoice("frag_07", "open_window");

        var frag3 = CreateFragment("frag_03");
        frag3.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionChoiceMade("frag_03", "keep_letter")
            }), 0.4f, false));

        var frag7 = CreateFragment("frag_07");
        frag7.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionChoiceMade("frag_07", "open_window")
            }), 0.3f, false));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag3, frag7 };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_B"));
        Assert.That(result.Score, Is.EqualTo(0.7f).Within(0.001f),
            "Score should be 0.4 + 0.3 = 0.7");
        Assert.That(result.IsDefault, Is.False);
    }

    [Test]
    public void test_score_clamped_to_one_even_if_ weights_exceed()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.7f));
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.6f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.Score, Is.EqualTo(1.0f).Within(0.001f),
            "Score should be clamped to 1.0 when weights sum > 1.0");
    }

    [Test]
    public void test_score_at_threshold_exactly_passes()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.5f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_B"),
            "Score exactly at threshold should pass.");
    }

    [Test]
    public void test_score_below_threshold_excluded()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.49f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "Score 0.49 < 0.5 threshold → ending_B excluded, fallback wins.");
        Assert.That(result.IsDefault, Is.True);
    }

    // =========================================================================
    // AC-2: Essential gate failure
    // =========================================================================

    [Test]
    public void test_essential_gate_unmet_flag_disqualifies_ending()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        // "found_secret" flag is false by default — essential gate should fail
        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("found_secret", true)
            }), 0.8f, isEssential: true));
        // Also add a non-essential trigger with high weight
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.9f, isEssential: false));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "Essential gate should block ending_B even with high non-essential weight.");
        Assert.That(result.IsDefault, Is.True);
    }

    [Test]
    public void test_essential_gate_satisfied_allows_ending()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("found_secret", true); // flag IS set

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("found_secret", true)
            }), 0.5f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_B"),
            "Essential gate should pass when flag is set.");
        Assert.That(result.Score, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void test_multiple_essential_triggers_any_fails_disqualifies()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);
        // flag_b remains false

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("flag_a", true)
            }), 0.3f, isEssential: true));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("flag_b", true)
            }), 0.2f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "One essential condition failing should disqualify the entire ending.");
    }

    // =========================================================================
    // AC-3: Default fallback — no qualified endings
    // =========================================================================

    [Test]
    public void test_default_fallback_when_no_endings_qualify()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_rare", isDefault: false, minimumScore: 0.5f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_rare",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("impossible_flag", true)
            }), 0.5f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_default"));
        Assert.That(result.IsDefault, Is.True);
        Assert.That(result.Score, Is.EqualTo(0.0f));
    }

    [Test]
    public void test_default_fallback_when_all_essential_gates_fail()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        // Both non-default endings have essential gates that fail
        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("secret_a", true)
            }), 0.6f, isEssential: true));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            {
                new ConditionFlagSet("secret_b", true)
            }), 0.6f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_default"));
        Assert.That(result.IsDefault, Is.True);
    }

    // =========================================================================
    // AC-4: Threshold not met — all scores below minimum
    // =========================================================================

    [Test]
    public void test_all_scores_below_minimum_returns_default()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.5f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.6f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("ending_A", 0.3f)); // 0.3 < 0.5
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.4f)); // 0.4 < 0.6

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_default"),
            "Both scores below threshold → default wins.");
        Assert.That(result.IsDefault, Is.True);
    }

    [Test]
    public void test_score_zero_threshold_zero_passes()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
        };

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment>();

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_default"));
        Assert.That(result.Score, Is.EqualTo(0.0f));
        Assert.That(result.IsDefault, Is.True);
    }

    [Test]
    public void test_winner_with_highest_score_above_threshold_wins()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("ending_A", 0.4f));
        frag.EndingTriggers.Add(CreateTrigger("ending_B", 0.7f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_B"),
            "Higher score (0.7 > 0.4) should win.");
        Assert.That(result.Score, Is.EqualTo(0.7f).Within(0.001f));
    }
}
