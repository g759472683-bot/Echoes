using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Integration tests for chapter completion transition flow (S003).
///
/// Covers 4 acceptance criteria:
///   AC-1: Normal completion flow — Ch01 complete, ending resolved, Ch02 unlocked + transition
///   AC-2: Final chapter completion — no next chapter → OnAllChaptersCompleted
///   AC-3: Auto-save failure during completion — LogError, continue transition
///   AC-4: Idempotent re-completion — HashSet adds are idempotent, events fire again
/// </summary>
public class CompletionTransitionFlowTest
{
    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockDataManager : IDataManager
    {
        public readonly Dictionary<string, List<MemoryFragment>> FragmentsByChapter = new();
        public ChapterDefinition ChapterDef;
        public ChapterDefinition Ch02Def;

        public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
        {
            FragmentsByChapter.TryGetValue(chapterKey, out var list);
            return list ?? new List<MemoryFragment>();
        }

        public System.Threading.Tasks.Task<ChapterDefinition> GetChapterAsync(string chapterKey)
        {
            if (chapterKey == "ch02" && Ch02Def != null)
                return System.Threading.Tasks.Task.FromResult(Ch02Def);
            return System.Threading.Tasks.Task.FromResult(ChapterDef);
        }

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
        public string ResolvedEndingId = "ending_A";
        public bool ThrowOnResolve;

        public ResolvedEnding ResolveEnding(string chapterId)
        {
            if (ThrowOnResolve)
                throw new System.InvalidOperationException("Test exception");
            return new ResolvedEnding(ResolvedEndingId, EndingType.ChapterEnding, 0.7f,
                false, true, new List<(string, float)> { ("ending_A", 0.7f) }, null);
        }
        public void OnChapterStart(string chapterId) { }
        public HashSet<string> GetUnlockedEndingIds() => new HashSet<string>();
        public MultiEndingSaveData GetSaveData()
            => new MultiEndingSaveData { UnlockedEndingIds = new string[0] };
        public void Restore(MultiEndingSaveData data) { }
    }

    private class MockAssociationProvider : IAssociationProvider
    {
        public List<AssociationCandidate> ComputeAssociations(
            string currentFragmentId, string chapterKey,
            List<string> recentHistory, HashSet<string> sessionVisitedFragments)
            => new List<AssociationCandidate>();
    }

    private class MockChapterSceneProvider : IChapterSceneProvider
    {
        public string LastTransitionChapter;
        public Task TransitionToFragmentAsync(string chapterKey, string fragmentId)
            => Task.CompletedTask;
        public Task TransitionToChapterAsync(string chapterKey)
        {
            LastTransitionChapter = chapterKey;
            return Task.CompletedTask;
        }
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
        int orderIndex = 0)
    {
        return new ChapterDefinition
        {
            ChapterKey = key,
            EntryFragmentId = entryFragmentId,
            OrderIndex = orderIndex,
            CompletionRatio = 0.6f,
            AllowReplay = true
        };
    }

    // =========================================================================
    // AC-1: Normal completion flow (Ch01 → Ch02)
    // =========================================================================

    [Test]
    public async Task test_completion_flow_advances_to_next_chapter()
    {
        var dataMgr = new MockDataManager();
        var ending = new MockEndingResolver { ResolvedEndingId = "ending_A" };
        var assoc = new MockAssociationProvider();
        var scene = new MockChapterSceneProvider();

        var ch01Def = MakeChapterDef("ch01", "frag_01", 0);
        var ch02Def = MakeChapterDef("ch02", "frag_01", 1);
        dataMgr.ChapterDef = ch01Def;
        dataMgr.Ch02Def = ch02Def;
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { CreateFragment("frag_01"), CreateFragment("frag_02") };
        dataMgr.FragmentsByChapter["ch02"] = new List<MemoryFragment>
            { CreateFragment("frag_01"), CreateFragment("frag_02") };

        int saveCount = 0;
        var cm = new ChapterManager(dataMgr, ending, assoc, scene,
            () => { saveCount++; return Task.CompletedTask; });

        string completedChapter = null;
        string startedChapter = null;
        ChapterManager.OnChapterCompleted += (ch) => completedChapter = ch;
        ChapterManager.OnChapterStarted += (ch) => startedChapter = ch;

        try
        {
            // Start in Ch01
            await cm.EnterChapter("ch01");

            // Execute completion
            await cm.ExecuteChapterCompletion("ch01");

            Assert.That(completedChapter, Is.EqualTo("ch01"),
                "OnChapterCompleted should fire for ch01.");
            Assert.That(saveCount, Is.EqualTo(1),
                "Auto-save should have been called exactly once.");
            Assert.That(scene.LastTransitionChapter, Is.EqualTo("ch02"),
                "Should transition to Ch02.");

            var completed = ((IChapterSaveRestore)cm).GetCompletedChapters();
            var unlocked = ((IChapterSaveRestore)cm).GetUnlockedChapters();
            Assert.That(completed, Does.Contain("ch01"));
            Assert.That(unlocked, Does.Contain("ch02"));
        }
        finally
        {
            ChapterManager.OnChapterCompleted -= (_) => { };
            ChapterManager.OnChapterStarted -= (_) => { };
        }
    }

    [Test]
    public void test_resolve_ending_throws_exception_propagates()
    {
        var dataMgr = new MockDataManager();
        dataMgr.ChapterDef = MakeChapterDef();
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { CreateFragment("frag_01") };

        var ending = new MockEndingResolver { ThrowOnResolve = true };
        var cm = new ChapterManager(dataMgr, ending,
            new MockAssociationProvider(), new MockChapterSceneProvider(),
            () => Task.CompletedTask);

        LogAssert.Expect(LogType.Error,
            "ChapterManager: ResolveEnding failed for 'ch01': Test exception");

        var ex = Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            await cm.ExecuteChapterCompletion("ch01"));
        Assert.That(ex.Message, Is.EqualTo("Test exception"));
    }

    // =========================================================================
    // AC-2: Final chapter completion
    // =========================================================================

    [Test]
    public async Task test_final_chapter_completion_fires_on_all_chapters_completed()
    {
        var dataMgr = new MockDataManager();
        var ending = new MockEndingResolver();
        var assoc = new MockAssociationProvider();
        var scene = new MockChapterSceneProvider();

        dataMgr.ChapterDef = MakeChapterDef("ch02", "frag_01", 1); // ch02 = last chapter
        dataMgr.FragmentsByChapter["ch02"] = new List<MemoryFragment>
            { CreateFragment("frag_01", "ch02"), CreateFragment("frag_02", "ch02") };

        var cm = new ChapterManager(dataMgr, ending, assoc, scene,
            () => Task.CompletedTask);

        string completedChapter = null;
        bool allCompleted = false;
        ChapterManager.OnChapterCompleted += (ch) => completedChapter = ch;
        ChapterManager.OnAllChaptersCompleted += () => allCompleted = true;

        try
        {
            await cm.EnterChapter("ch02");
            await cm.ExecuteChapterCompletion("ch02");

            Assert.That(completedChapter, Is.EqualTo("ch02"));
            Assert.That(allCompleted, Is.True,
                "OnAllChaptersCompleted should fire when final chapter completes.");
            Assert.That(scene.LastTransitionChapter, Is.Null,
                "No TransitionToChapterAsync should be called for final chapter.");
        }
        finally
        {
            ChapterManager.OnChapterCompleted -= (_) => { };
            ChapterManager.OnAllChaptersCompleted -= () => { };
        }
    }

    // =========================================================================
    // AC-3: Auto-save failure during completion
    // =========================================================================

    [Test]
    public async Task test_auto_save_failure_does_not_block_chapter_transition()
    {
        var dataMgr = new MockDataManager();
        dataMgr.ChapterDef = MakeChapterDef("ch01", "frag_01", 0);
        dataMgr.Ch02Def = MakeChapterDef("ch02", "frag_01", 1);
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { CreateFragment("frag_01"), CreateFragment("frag_02") };
        dataMgr.FragmentsByChapter["ch02"] = new List<MemoryFragment>
            { CreateFragment("frag_01") };

        var scene = new MockChapterSceneProvider();
        bool saveCalled = false;
        var cm = new ChapterManager(dataMgr,
            new MockEndingResolver(), new MockAssociationProvider(), scene,
            () => { saveCalled = true; throw new System.IO.IOException("Disk full"); });

        LogAssert.Expect(LogType.Error,
            "ChapterManager: Auto-save failed during chapter completion: Disk full");

        await cm.EnterChapter("ch01");
        await cm.ExecuteChapterCompletion("ch01");

        Assert.That(saveCalled, Is.True, "Auto-save should be attempted.");
        Assert.That(scene.LastTransitionChapter, Is.EqualTo("ch02"),
            "Transition to Ch02 should proceed despite save failure.");
    }

    // =========================================================================
    // AC-4: Idempotent re-completion
    // =========================================================================

    [Test]
    public async Task test_recompletion_is_idempotent()
    {
        var dataMgr = new MockDataManager();
        dataMgr.ChapterDef = MakeChapterDef("ch01", "frag_01", 0);
        dataMgr.Ch02Def = MakeChapterDef("ch02", "frag_01", 1);
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { CreateFragment("frag_01"), CreateFragment("frag_02") };
        dataMgr.FragmentsByChapter["ch02"] = new List<MemoryFragment>
            { CreateFragment("frag_01") };

        int saveCount = 0;
        var cm = new ChapterManager(dataMgr,
            new MockEndingResolver(), new MockAssociationProvider(),
            new MockChapterSceneProvider(),
            () => { saveCount++; return Task.CompletedTask; });

        await cm.EnterChapter("ch01");

        // First completion
        await cm.ExecuteChapterCompletion("ch01");
        Assert.That(saveCount, Is.EqualTo(1));

        // Re-complete (simulate replay + complete again)
        await cm.ExecuteChapterCompletion("ch01");
        Assert.That(saveCount, Is.EqualTo(2), "Auto-save runs again on re-completion.");

        var completed = ((IChapterSaveRestore)cm).GetCompletedChapters();
        Assert.That(completed.Length, Is.EqualTo(1),
            "CompletedChapters HashSet is idempotent — ch01 only appears once.");
    }

    [Test]
    public async Task test_recompletion_fires_events_again()
    {
        var dataMgr = new MockDataManager();
        dataMgr.ChapterDef = MakeChapterDef("ch01", "frag_01", 0);
        dataMgr.Ch02Def = MakeChapterDef("ch02", "frag_01", 1);
        dataMgr.FragmentsByChapter["ch01"] = new List<MemoryFragment>
            { CreateFragment("frag_01"), CreateFragment("frag_02") };
        dataMgr.FragmentsByChapter["ch02"] = new List<MemoryFragment>
            { CreateFragment("frag_01") };

        var cm = new ChapterManager(dataMgr,
            new MockEndingResolver(), new MockAssociationProvider(),
            new MockChapterSceneProvider(), () => Task.CompletedTask);

        int completedCount = 0;
        ChapterManager.OnChapterCompleted += (_) => completedCount++;

        try
        {
            await cm.EnterChapter("ch01");
            await cm.ExecuteChapterCompletion("ch01");
            await cm.ExecuteChapterCompletion("ch01");

            Assert.That(completedCount, Is.EqualTo(2),
                "OnChapterCompleted fires each time, even for re-completion.");
        }
        finally
        {
            ChapterManager.OnChapterCompleted -= (_) => { };
        }
    }
}
