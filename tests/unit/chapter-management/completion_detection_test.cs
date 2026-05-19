using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unit tests for chapter completion detection (S002).
///
/// Covers 4 acceptance criteria:
///   AC-1: Completion via ratio + threshold — visited >= ratio AND best score < 0.30
///   AC-2: Completion via all-visited — all fragments visited, regardless of score
///   AC-3: Not complete — score above threshold keeps chapter open
///   AC-4: Empty chapter — 0 fragments triggers completion with error log
/// </summary>
public class CompletionDetectionTest
{
    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockDataManager : IDataManager
    {
        public readonly Dictionary<string, List<MemoryFragment>> FragmentsByChapter = new();
        public ChapterDefinition ChapterDef;

        public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
        {
            FragmentsByChapter.TryGetValue(chapterKey, out var list);
            return list ?? new List<MemoryFragment>();
        }

        public System.Threading.Tasks.Task<ChapterDefinition> GetChapterAsync(string chapterKey)
            => System.Threading.Tasks.Task.FromResult(ChapterDef);

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

    private class MockEndingResolver : IEndingResolver
    {
        public ResolvedEnding ResolveEnding(string chapterId)
            => new ResolvedEnding("ending_default", EndingType.ChapterEnding, 0f, true, false);
        public void OnChapterStart(string chapterId) { }
        public HashSet<string> GetUnlockedEndingIds() => new HashSet<string>();
        public MultiEndingSaveData GetSaveData()
            => new MultiEndingSaveData { UnlockedEndingIds = new string[0] };
        public void Restore(MultiEndingSaveData data) { }
    }

    private class MockAssociationProvider : IAssociationProvider
    {
        public float BestScoreToReturn = 0.15f;
        public bool ReturnEmptyList;

        public List<AssociationCandidate> ComputeAssociations(
            string currentFragmentId, string chapterKey,
            List<string> recentHistory, HashSet<string> sessionVisitedFragments)
        {
            if (ReturnEmptyList) return new List<AssociationCandidate>();

            return new List<AssociationCandidate>
            {
                new AssociationCandidate("cand_01", BestScoreToReturn, Strength.Medium,
                    DominantFactor.TagSimilarity)
            };
        }
    }

    private class MockChapterSceneProvider : IChapterSceneProvider
    {
        public Task TransitionToFragmentAsync(string chapterKey, string fragmentId)
            => Task.CompletedTask;
        public Task TransitionToChapterAsync(string chapterKey)
            => Task.CompletedTask;
        public Task PreloadChapterAsync(string chapterKey)
            => Task.CompletedTask;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MemoryFragment CreateFragment(string id, string chapterKey = "ch01")
    {
        var frag = ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = id;
        frag.ChapterKey = chapterKey;
        return frag;
    }

    private ChapterDefinition MakeChapterDef(string key = "ch01", string entryFragmentId = "frag_01",
        float completionRatio = 0.6f)
    {
        return new ChapterDefinition
        {
            ChapterKey = key,
            EntryFragmentId = entryFragmentId,
            OrderIndex = 0,
            CompletionRatio = completionRatio,
            AllowReplay = true
        };
    }

    private (ChapterManager, MockDataManager, MockAssociationProvider) CreateChapterManager(
        string chapterKey = "ch01", float completionRatio = 0.6f,
        List<MemoryFragment> fragments = null, float bestScore = 0.15f)
    {
        var dataMgr = new MockDataManager();
        var ending = new MockEndingResolver();
        var assoc = new MockAssociationProvider { BestScoreToReturn = bestScore };
        var scene = new MockChapterSceneProvider();

        dataMgr.ChapterDef = MakeChapterDef(chapterKey, completionRatio: completionRatio);

        if (fragments != null)
            dataMgr.FragmentsByChapter[chapterKey] = fragments;

        var cm = new ChapterManager(dataMgr, ending, assoc, scene, () => Task.CompletedTask);
        return (cm, dataMgr, assoc);
    }

    // =========================================================================
    // AC-1: Completion via ratio + threshold (condition B)
    // =========================================================================

    [Test]
    public async Task test_completion_via_ratio_and_threshold_returns_true()
    {
        // 10 fragments, CompletionRatio=0.6, visited=8, bestScore=0.15 → complete
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.15f);

        assoc.BestScoreToReturn = 0.15f; // < 0.30

        await cm.EnterChapter("ch01");

        // Manually add visited fragments to simulate progress
        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 8; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "8/10 visited (0.8 >= 0.6) and best score 0.15 < 0.30 → should complete.");
    }

    [Test]
    public async Task test_completion_at_exact_ratio_boundary_passes()
    {
        // visited=6, ratio=0.6, CompletionRatio=0.6 → meets threshold
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.10f);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 6; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "6/10 = 0.6 exactly meets ratio, score 0.10 < 0.30 → should complete.");
    }

    [Test]
    public async Task test_no_candidates_completes_chapter()
    {
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.15f);
        assoc.ReturnEmptyList = true; // No candidates → auto-complete

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 7; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "No candidates → chapter naturally exhausted → complete.");
    }

    // =========================================================================
    // AC-2: Completion via all-visited (condition A)
    // =========================================================================

    [Test]
    public async Task test_completion_via_all_visited_always_true_regardless_of_score()
    {
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.99f);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 10; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "All 10/10 visited → condition A takes priority, regardless of score.");
    }

    [Test]
    public async Task test_completion_all_visited_with_completion_ratio_1_0()
    {
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, _) = CreateChapterManager(
            completionRatio: 1.0f, fragments: fragments);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 10; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "All visited → condition A completes even when CompletionRatio=1.0.");
    }

    // =========================================================================
    // AC-3: Not complete — score above threshold
    // =========================================================================

    [Test]
    public async Task test_not_complete_when_score_above_threshold()
    {
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.45f);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 8; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.False,
            "8/10 visited (ratio met) but best score 0.45 >= 0.30 → NOT complete.");
    }

    [Test]
    public async Task test_not_complete_when_ratio_not_met()
    {
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, _) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 5; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.False,
            "5/10 = 0.5 < 0.6 (ratio not met) → NOT complete regardless of score.");
    }

    [Test]
    public async Task test_score_exactly_at_threshold_not_complete()
    {
        // Best score = 0.30 exactly → NOT complete (threshold is strict less-than)
        var fragments = new List<MemoryFragment>();
        for (int i = 1; i <= 10; i++)
            fragments.Add(CreateFragment($"frag_{i:D2}"));

        var (cm, dataMgr, assoc) = CreateChapterManager(
            completionRatio: 0.6f, fragments: fragments, bestScore: 0.30f);

        await cm.EnterChapter("ch01");

        var visitedField = typeof(ChapterManager).GetField("_chapterVisitedFragments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var visited = (HashSet<string>)visitedField.GetValue(cm);
        for (int i = 1; i <= 7; i++)
            visited.Add($"frag_{i:D2}");

        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.False,
            "Score = 0.30 exactly → NOT passing (strict < 0.30 required).");
    }

    // =========================================================================
    // AC-4: Empty chapter
    // =========================================================================

    [Test]
    public async Task test_empty_chapter_triggers_completion_with_error_log()
    {
        var (cm, dataMgr, _) = CreateChapterManager(fragments: new List<MemoryFragment>());
        // Override with empty fragment list
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>();

        LogAssert.Expect(LogType.Error,
            "ChapterManager: Chapter 'ch01' has 0 fragments — auto-completing.");

        // EnterChapter should detect 0 fragments and trigger completion
        // But since ExecuteChapterCompletion needs scene transitions, let's test
        // the completion check directly
        bool result = cm.CheckChapterCompletion("ch01");
        Assert.That(result, Is.True,
            "0 fragments → always complete.");
    }
}
