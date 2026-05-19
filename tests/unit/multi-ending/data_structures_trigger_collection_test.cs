using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unit tests for multi-ending data structures and trigger collection (S001).
///
/// Covers 3 acceptance criteria:
///   AC-1: Default ending validation — exactly one IsDefault=true, MinimumScore=0.0 per chapter
///   AC-2: Orphan trigger handling — unknown EndingId logged as warning and ignored
///   AC-3: Trigger collection — grouped by EndingId, orphan excluded, correct fragment sources
/// </summary>
public class DataStructuresTriggerCollectionTest
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
    // AC-1: Default ending validation — ValidateDefaultEndings static method
    // =========================================================================

    /// <summary>
    /// Validates that a chapter's ending definitions have exactly one IsDefault=true.
    /// Returns a list of error strings. Empty list = valid.
    /// </summary>
    public static List<string> ValidateDefaultEndings(string chapterId, List<EndingDefinition> endings)
    {
        var errors = new List<string>();
        if (endings == null || endings.Count == 0)
        {
            errors.Add($"Chapter '{chapterId}': No ending definitions configured.");
            return errors;
        }

        int defaultCount = 0;
        foreach (var def in endings)
        {
            if (def.IsDefault)
            {
                defaultCount++;
                if (def.MinimumScore != 0f)
                    errors.Add($"Chapter '{chapterId}': Default ending '{def.EndingId}' " +
                               $"must have MinimumScore=0.0 (got {def.MinimumScore}).");
            }
        }

        if (defaultCount == 0)
            errors.Add($"Chapter '{chapterId}': No IsDefault=true ending. Exactly one required.");
        else if (defaultCount > 1)
            errors.Add($"Chapter '{chapterId}': {defaultCount} IsDefault=true endings. " +
                       "Exactly one required.");

        return errors;
    }

    [Test]
    public void test_validate_default_endings_exactly_one_default_passes()
    {
        var endings = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
            CreateEndingDef("ending_C", isDefault: false, minimumScore: 0.6f),
        };

        var errors = ValidateDefaultEndings("ch01", endings);

        Assert.That(errors.Count, Is.EqualTo(0), "Valid configuration should produce no errors.");
    }

    [Test]
    public void test_validate_default_endings_zero_default_reports_error()
    {
        var endings = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: false, minimumScore: 0.3f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
        };

        var errors = ValidateDefaultEndings("ch01", endings);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0], Does.Contain("No IsDefault=true ending"));
    }

    [Test]
    public void test_validate_default_endings_multiple_defaults_reports_error()
    {
        var endings = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: true, minimumScore: 0.0f),
        };

        var errors = ValidateDefaultEndings("ch01", endings);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0], Does.Contain("2 IsDefault=true endings"));
    }

    [Test]
    public void test_validate_default_endings_default_with_nonzero_minimum_score()
    {
        var endings = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.5f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.3f),
        };

        var errors = ValidateDefaultEndings("ch01", endings);

        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0], Does.Contain("MinimumScore=0.0"));
    }

    [Test]
    public void test_validate_default_endings_empty_list_reports_error()
    {
        var errors = ValidateDefaultEndings("ch01", new List<EndingDefinition>());
        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0], Does.Contain("No ending definitions"));
    }

    [Test]
    public void test_validate_default_endings_null_list_reports_error()
    {
        var errors = ValidateDefaultEndings("ch01", null);
        Assert.That(errors.Count, Is.EqualTo(1));
    }

    // =========================================================================
    // AC-2: Orphan trigger — unknown EndingId logged as warning
    // =========================================================================

    [Test]
    public void test_orphan_trigger_logs_warning_and_is_ignored()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("orphan_id", 0.5f)); // no matching EndingDefinition
        frag.EndingTriggers.Add(CreateTrigger("ending_A", 0.3f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        LogAssert.Expect(LogType.Warning, "MultiEndingSystem: Trigger references unknown EndingId 'orphan_id' — ignored.");

        var result = system.ResolveEnding("ch01");

        Assert.That(result.EndingId, Is.EqualTo("ending_A"),
            "Orphan trigger should be ignored; valid trigger should still resolve.");
        Assert.That(result.Score, Is.EqualTo(0.3f),
            "Only the valid trigger's weight (0.3) should be counted.");
    }

    [Test]
    public void test_multiple_orphan_triggers_each_logs_warning()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
        };

        var frag = CreateFragment("frag_01");
        frag.EndingTriggers.Add(CreateTrigger("orphan_1", 0.3f));
        frag.EndingTriggers.Add(CreateTrigger("orphan_2", 0.4f));
        frag.EndingTriggers.Add(CreateTrigger("ending_A", 0.2f));

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment> { frag };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        LogAssert.Expect(LogType.Warning, "MultiEndingSystem: Trigger references unknown EndingId 'orphan_1' — ignored.");
        LogAssert.Expect(LogType.Warning, "MultiEndingSystem: Trigger references unknown EndingId 'orphan_2' — ignored.");

        var result = system.ResolveEnding("ch01");
        Assert.That(result.EndingId, Is.EqualTo("ending_A"));
    }

    // =========================================================================
    // AC-3: Trigger collection — correct grouping by EndingId
    // =========================================================================

    [Test]
    public void test_triggers_grouped_by_ending_id_across_fragments()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
            CreateEndingDef("ending_B", isDefault: false, minimumScore: 0.5f),
            CreateEndingDef("ending_C", isDefault: false, minimumScore: 0.5f),
        };

        // 8 triggers across 5 fragments + 1 orphan
        var frag1 = CreateFragment("frag_01");
        frag1.EndingTriggers.Add(CreateTrigger("ending_A", 0.3f));
        frag1.EndingTriggers.Add(CreateTrigger("ending_B", 0.2f));

        var frag2 = CreateFragment("frag_02");
        frag2.EndingTriggers.Add(CreateTrigger("ending_A", 0.2f));
        frag2.EndingTriggers.Add(CreateTrigger("ending_C", 0.3f));

        var frag3 = CreateFragment("frag_03");
        frag3.EndingTriggers.Add(CreateTrigger("ending_B", 0.1f));
        frag3.EndingTriggers.Add(CreateTrigger("ending_C", 0.2f));

        var frag4 = CreateFragment("frag_04");
        frag4.EndingTriggers.Add(CreateTrigger("ending_A", 0.1f));
        frag4.EndingTriggers.Add(CreateTrigger("orphan_id", 0.5f));

        var frag5 = CreateFragment("frag_05");
        // No triggers — valid fragment

        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { frag1, frag2, frag3, frag4, frag5 };

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        LogAssert.Expect(LogType.Warning, "MultiEndingSystem: Trigger references unknown EndingId 'orphan_id' — ignored.");

        var result = system.ResolveEnding("ch01");

        // All 3 ending definitions qualify (all equal score 0 with no conditions)
        Assert.That(result.QualifiedEndings.Count, Is.EqualTo(3),
            "All 3 ending definitions should qualify with default minimum scores.");
    }

    [Test]
    public void test_collect_triggers_with_zero_triggers()
    {
        var dataManager = new MockDataManager();
        var endingProvider = new MockEndingDefinitionProvider();
        var changeTracker = new MockChangeTracker();
        var tagSystem = new MockEmotionalTagSystem();

        endingProvider.Definitions["ch01"] = new List<EndingDefinition>
        {
            CreateEndingDef("ending_A", isDefault: true, minimumScore: 0.0f),
        };

        // Empty chapter — no fragments
        dataManager.FragmentsByChapter["ch01"] = new List<MemoryFragment>();

        var system = new MultiEndingSystem(endingProvider, dataManager, changeTracker, tagSystem);

        var result = system.ResolveEnding("ch01");

        Assert.That(result.IsDefault, Is.True);
        Assert.That(result.Score, Is.EqualTo(0.0f));
    }
}
