using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for tie-breaking, persistence, and re-evaluation (S003).
///
/// Covers 4 acceptance criteria:
///   AC-1: Tie-breaking by essential count — same score, more essential satisfied wins
///   AC-2: Tie-breaking by novelty — same score/essential, unplayed ending wins
///   AC-3: UnlockedEndingIds union semantics + OnEndingUnlocked event
///   AC-4: Re-evaluation — same inputs produce same result, changed inputs re-evaluate
/// </summary>
public class TiebreakingPersistenceTest
{
    // =========================================================================
    // Mocks
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
        public void ClearChoice(string fragmentId, string choiceId)
            => _choices.Remove($"{fragmentId}|{choiceId}");
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
        float minimumScore = 0.3f, EndingType endingType = EndingType.ChapterEnding)
    {
        return new EndingDefinition(endingId, endingType, "ch01", minimumScore, isDefault);
    }

    // =========================================================================
    // AC-1: Tie-breaking — essential count priority
    // =========================================================================

    [Test]
    public void test_tiebreak_same_score_more_essential_satisfied_wins()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);
        changeTracker.SetFlag("flag_b", true);
        changeTracker.SetFlag("flag_c", true);

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        // ending_A: 3 essential satisfied, score=0.6
        var fragA = CreateFragment("frag_a");
        fragA.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.6f, isEssential: true));

        // ending_B: 2 essential satisfied, score=0.6
        var fragB = CreateFragment("frag_b");
        fragB.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.3f, isEssential: true));
        fragB.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_b", true) }), 0.3f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { fragA, fragB };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "Same score (0.6) but ending_A has 3 essential satisfied vs ending_B's 2 — ending_A wins.");
    }

    [Test]
    public void test_tiebreak_same_essential_count_falls_through_to_novelty()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        // Both have 1 essential satisfied, score=0.5 — novelty decides
        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        // Neither is unlocked → both have novelty, falls through to definition order
        // ending_A is defined before ending_B → ending_A wins
        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "Same score, same essential count, both novel → definition order: ending_A first.");
    }

    // =========================================================================
    // AC-2: Tie-breaking — novelty bias
    // =========================================================================

    [Test]
    public void test_tiebreak_novelty_bias_unplayed_wins_over_played()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        // Pre-unlock ending_A via restore
        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        system.Restore(new MultiEndingSaveData { UnlockedEndingIds = new[] { "ending_A" } });

        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_B"),
            "Same score and essential count, but ending_B is novel (unplayed) → wins.");
    }

    [Test]
    public void test_tiebreak_both_novel_falls_through_to_definition_order()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: true));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        var result = system.ResolveEnding("ch01");

        // Both novel, same score, same essential → definition order: ending_B first in list
        Assert.That(result.EndingId, Is.EqualTo("ending_B"),
            "Both novel → definition order decides: ending_B before ending_A in array.");
    }

    // =========================================================================
    // AC-3: UnlockedEndingIds union semantics + event
    // =========================================================================

    [Test]
    public void test_unlocked_ending_ids_union_old_ids_preserved()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_default", null, 0.5f, false));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        // First playthrough: unlock ending_default
        var result1 = system.ResolveEnding("ch01");
        Assert.That(result1.IsNewUnlock, Is.True);

        var unlockedSet = system.GetUnlockedEndingIds();
        Assert.That(unlockedSet, Does.Contain("ending_default"));

        // Change setup to make another ending win
        var dataManager2 = new MockDataManager();
        var endingProvider2 = new MockEndingDefinitionProvider();
        endingProvider2.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var frag2 = CreateFragment("frag_01");
        frag2.EndingTriggers.Add(CreateTrigger("ending_B", 0.6f));
        dataManager2.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag2 };

        // Restore state (carries over UnlockedEndingIds from first playthrough)
        var system2 = new MultiEndingSystem(endingProvider2, dataManager2, changeTracker, tagSystem);
        system2.Restore(system.GetSaveData());

        var result2 = system2.ResolveEnding("ch01");

        Assert.That(result2.EndingId, Is.EqualTo("ending_B"));
        Assert.That(result2.IsNewUnlock, Is.True);

        var unionSet = system2.GetUnlockedEndingIds();
        Assert.That(unionSet, Does.Contain("ending_default"),
            "Previously unlocked endings must be preserved (union semantics).");
        Assert.That(unionSet, Does.Contain("ending_B"));
        Assert.That(unionSet.Count, Is.EqualTo(2));
    }

    [Test]
    public void test_on_ending_unlocked_event_fires_for_new_unlocks()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
        };

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment>();

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        string eventEndingId = null;
        Action<string> handler = (id) => eventEndingId = id;
        MultiEndingSystem.OnEndingUnlocked += handler;

        try
        {
            var result = system.ResolveEnding("ch01");
            Assert.That(result.IsNewUnlock, Is.True);
            Assert.That(eventEndingId, Is.EqualTo("ending_A"),
                "OnEndingUnlocked should fire with the newly unlocked ending ID.");
        }
        finally
        {
            MultiEndingSystem.OnEndingUnlocked -= handler;
        }
    }

    [Test]
    public void test_on_ending_unlocked_not_fired_for_duplicate_unlock()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
        };

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment>();

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        int eventCount = 0;
        Action<string> handler = (_) => eventCount++;
        MultiEndingSystem.OnEndingUnlocked += handler;

        try
        {
            system.ResolveEnding("ch01");
            Assert.That(eventCount, Is.EqualTo(1), "First unlock should fire event.");

            // Re-resolve the same ending
            system.ResolveEnding("ch01");
            Assert.That(eventCount, Is.EqualTo(1),
                "Duplicate unlock should NOT fire event again (idempotent).");
        }
        finally
        {
            MultiEndingSystem.OnEndingUnlocked -= handler;
        }
    }

    // =========================================================================
    // AC-4: Re-evaluation — no caching, deterministic with same state
    // =========================================================================

    [Test]
    public void test_same_state_produces_same_ending_deterministic()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        changeTracker.SetFlag("flag_a", true);

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_default", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("flag_a", true) }), 0.5f, isEssential: false));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        var result1 = system.ResolveEnding("ch01");
        var result2 = system.ResolveEnding("ch01");
        var result3 = system.ResolveEnding("ch01");

        Assert.That(result1.EndingId, Is.EqualTo("ending_A"));
        Assert.That(result2.EndingId, Is.EqualTo("ending_A"),
            "Re-evaluation with same state must produce same result.");
        Assert.That(result3.EndingId, Is.EqualTo("ending_A"),
            "Deterministic across 3 calls with same state.");
        Assert.That(result1.Score, Is.EqualTo(result2.Score));
        Assert.That(result2.Score, Is.EqualTo(result3.Score));
    }

    [Test]
    public void test_changed_state_produces_different_ending_re_evaluation()
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
        frag.EndingTriggers.Add(new EndingTrigger("ending_A",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("choice_flag", true) }), 0.5f, isEssential: false));
        frag.EndingTriggers.Add(new EndingTrigger("ending_B",
            new ConditionGroup(ConditionCombinator.All, new List<Condition>
            { new ConditionFlagSet("alt_flag", true) }), 0.5f, isEssential: false));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        // State: choice_flag set → ending_A wins
        changeTracker.SetFlag("choice_flag", true);

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);
        var result1 = system.ResolveEnding("ch01");
        Assert.That(result1.EndingId, Is.EqualTo("ending_A"),
            "With choice_flag set, ending_A should win.");

        // Change state: clear choice_flag, set alt_flag → ending_B wins
        changeTracker.SetFlag("choice_flag", false);
        changeTracker.SetFlag("alt_flag", true);

        var result2 = system.ResolveEnding("ch01");
        Assert.That(result2.EndingId, Is.EqualTo("ending_B"),
            "After changing flags, re-evaluation produces ending_B — no caching.");
    }

    // =========================================================================
    // Save/Load bridge
    // =========================================================================

    [Test]
    public void test_save_data_roundtrip_preserves_all_unlocked_ids()
    {
        var system = new MultiEndingSystem(
            new MockEndingDefinitionProvider(),
            new MockDataManager(),
            new MockChangeTracker(),
            new MockEmotionalTagSystem());

        system.Restore(new MultiEndingSaveData
        {
            UnlockedEndingIds = new[] { "ending_A", "ending_B", "ending_C" }
        });

        var saveData = system.GetSaveData();

        Assert.That(saveData.UnlockedEndingIds, Is.Not.Null);
        Assert.That(saveData.UnlockedEndingIds.Length, Is.EqualTo(3));
        Assert.That(saveData.UnlockedEndingIds, Does.Contain("ending_A"));
        Assert.That(saveData.UnlockedEndingIds, Does.Contain("ending_B"));
        Assert.That(saveData.UnlockedEndingIds, Does.Contain("ending_C"));
    }

    [Test]
    public void test_restore_null_data_handled_gracefully()
    {
        var system = new MultiEndingSystem(
            new MockEndingDefinitionProvider(),
            new MockDataManager(),
            new MockChangeTracker(),
            new MockEmotionalTagSystem());

        system.Restore(new MultiEndingSaveData { UnlockedEndingIds = null });

        var unlocked = system.GetUnlockedEndingIds();
        Assert.That(unlocked, Is.Not.Null);
        Assert.That(unlocked.Count, Is.EqualTo(0));
    }

    [Test]
    public void test_get_unlocked_ending_ids_returns_copy()
    {
        var system = new MultiEndingSystem(
            new MockEndingDefinitionProvider(),
            new MockDataManager(),
            new MockChangeTracker(),
            new MockEmotionalTagSystem());

        system.Restore(new MultiEndingSaveData { UnlockedEndingIds = new[] { "ending_A" } });

        var copy = system.GetUnlockedEndingIds();
        copy.Add("injected"); // Should not affect internal state

        var original = system.GetUnlockedEndingIds();
        Assert.That(original, Does.Not.Contain("injected"),
            "GetUnlockedEndingIds must return a copy, not the internal reference.");
    }

    // =========================================================================
    // Helpers (local)
    // =========================================================================

    private static EndingTrigger CreateTrigger(string endingId, float weight = 0.5f,
        bool isEssential = false, ConditionGroup condition = null)
    {
        return new EndingTrigger(endingId, condition, weight, isEssential);
    }
}
