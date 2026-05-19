using NUnit.Framework;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for Story 003 — Chapter Preload & Memory Management.
///
/// Covers acceptance criteria:
///   - Preload triggers when fragments at or below threshold
///   - Preload does NOT trigger above threshold
///   - Graceful handling when no next chapter exists
///   - DownloadDependenciesAsync is called with correct label
///   - Duplicate preload calls are idempotent
///   - UnloadChapter releases all chapter assets
///   - Shared assets (Shared_ prefix) are preserved during unload
///   - Failed preloads do not throw (non-blocking)
///   - Main load path works after failed preload
///   - Cache strategy enforcement (CurrentPlusNext limits to 2)
///   - In-flight preload cancellation is graceful
///   - CheckAndTriggerPreload is no-op while preload is already in-flight
/// </summary>
[TestFixture]
public class PreloadTests
{
    private GameObject _gameObject;
    private DataManager _dataManager;
    private MockAddressableLoader _mockLoader;

    // Event tracking
    private List<DataManagerState> _stateChanges;

    // =========================================================================
    // SetUp / TearDown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        DataManager.OnStateChanged = null;
        _stateChanges = new List<DataManagerState>();

        DataManager.OnStateChanged += (state) => _stateChanges.Add(state);

        _gameObject = new GameObject("DataManager_PreloadTest");
        _dataManager = _gameObject.AddComponent<DataManager>();
        _mockLoader = new MockAddressableLoader();
        _dataManager.SetLoader(_mockLoader);
    }

    [TearDown]
    public void TearDown()
    {
        DataManager.OnStateChanged = null;

        if (_gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
            _gameObject = null;
        }

        _dataManager = null;
        _mockLoader = null;
        _stateChanges = null;
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private ChapterDefinition CreateChapterDef(string chapterKey, int orderIndex,
        string entryFragmentId, params string[] fragmentAssetKeys)
    {
        var def = ScriptableObject.CreateInstance<ChapterDefinition>();
        def.ChapterKey = chapterKey;
        def.OrderIndex = orderIndex;
        def.EntryFragmentId = entryFragmentId;

        if (fragmentAssetKeys != null && fragmentAssetKeys.Length > 0)
        {
            var refs = new AssetReferenceT<MemoryFragment>[fragmentAssetKeys.Length];
            for (int i = 0; i < fragmentAssetKeys.Length; i++)
            {
                refs[i] = new AssetReferenceT<MemoryFragment>(fragmentAssetKeys[i]);
            }
            def.Fragments = refs;
        }
        else
        {
            def.Fragments = new AssetReferenceT<MemoryFragment>[0];
        }
        return def;
    }

    private MemoryFragment CreateFragment(string fragmentId, string chapterKey,
        string illustrationKey, string[] audioKeys = null)
    {
        var frag = ScriptableObject.CreateInstance<MemoryFragment>();
        frag.FragmentId = fragmentId;
        frag.ChapterKey = chapterKey;
        frag.IllustrationKey = illustrationKey;
        frag.AudioKeys = audioKeys ?? new string[0];
        frag.InteractiveObjects = new InteractiveObject[0];
        frag.ChoiceGroups = new ChoiceGroup[0];
        return frag;
    }

    private Sprite CreateTestSprite(string name)
    {
        var tex = new Texture2D(1, 1);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        sprite.name = name;
        return sprite;
    }

    /// <summary>
    /// Sets up a full metadata environment: 2 chapters with fragments each.
    /// Used by tests that need a fully initialized Ready-state DataManager.
    /// </summary>
    private async Task<DataManager> InitializeWithMetadata()
    {
        var ch01Def = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01", "frag_key_02");
        var ch02Def = CreateChapterDef("ch02", 1, "frag_03", "frag_key_03");

        var f1 = CreateFragment("frag_01", "ch01", "art_ch01_frag01",
            new[] { "audio_ambient", "audio_stinger" });
        var f2 = CreateFragment("frag_02", "ch01", "art_ch01_frag02");
        var f3 = CreateFragment("frag_03", "ch02", "art_ch02_frag03");

        _mockLoader.SetAsset("ch01", ch01Def);
        _mockLoader.SetAsset("ch02", ch02Def);
        _mockLoader.SetAsset("frag_key_01", f1);
        _mockLoader.SetAsset("frag_key_02", f2);
        _mockLoader.SetAsset("frag_key_03", f3);

        _dataManager.SetChapterKeys(new[] { "ch01", "ch02" });
        await _dataManager.InitializeAsync();

        return _dataManager;
    }

    /// <summary>
    /// Sets up metadata with 3 chapters for cache strategy tests.
    /// </summary>
    private async Task<DataManager> InitializeWithThreeChapters()
    {
        var ch01Def = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var ch02Def = CreateChapterDef("ch02", 1, "frag_02", "frag_key_02");
        var ch03Def = CreateChapterDef("ch03", 2, "frag_03", "frag_key_03");

        var f1 = CreateFragment("frag_01", "ch01", "art_ch01_frag01");
        var f2 = CreateFragment("frag_02", "ch02", "art_ch02_frag02");
        var f3 = CreateFragment("frag_03", "ch03", "art_ch03_frag03");

        _mockLoader.SetAsset("ch01", ch01Def);
        _mockLoader.SetAsset("ch02", ch02Def);
        _mockLoader.SetAsset("ch03", ch03Def);
        _mockLoader.SetAsset("frag_key_01", f1);
        _mockLoader.SetAsset("frag_key_02", f2);
        _mockLoader.SetAsset("frag_key_03", f3);

        _dataManager.SetChapterKeys(new[] { "ch01", "ch02", "ch03" });
        await _dataManager.InitializeAsync();

        return _dataManager;
    }

    // =========================================================================
    // AC: Preload triggers when fragments at or below threshold
    // =========================================================================

    /// <summary>
    /// Given: Ready state, current chapter ch01 with 3 remaining fragments
    /// (equal to default threshold of 3).
    /// When: CheckAndTriggerPreload is called.
    /// Then: Next chapter (ch02) preload is triggered, downloading Art_Ch02.
    /// </summary>
    [Test]
    public async Task test_preload_triggers_when_fragments_equal_threshold()
    {
        // Arrange
        var dm = await InitializeWithMetadata();
        dm.SetCurrentChapter("ch01");
        _mockLoader.ResetCounts();

        // Act
        dm.CheckAndTriggerPreload("ch01", remainingFragments: 3);

        // Assert: preload fires as fire-and-forget; since mock DownloadDependenciesAsync
        // returns Task.CompletedTask, the preload completes synchronously before this returns.
        Assert.IsTrue(_mockLoader.DownloadedLabels.Contains("Art_Ch02"),
            "Preload should download Art_Ch02 when fragments equal threshold (3)");
    }

    /// <summary>
    /// Given: Ready state, current chapter ch01 with 4 remaining fragments
    /// (above default threshold of 3).
    /// When: CheckAndTriggerPreload is called.
    /// Then: No preload is triggered.
    /// </summary>
    [Test]
    public async Task test_preload_not_triggered_when_above_threshold()
    {
        // Arrange
        var dm = await InitializeWithMetadata();
        dm.SetCurrentChapter("ch01");
        _mockLoader.ResetCounts();

        // Act
        dm.CheckAndTriggerPreload("ch01", remainingFragments: 4);

        // Assert
        Assert.AreEqual(0, _mockLoader.DownloadedLabels.Count,
            "Preload should NOT trigger when remaining fragments exceed threshold");
    }

    /// <summary>
    /// Given: A chapter key whose format does not yield a next chapter
    /// (no numeric suffix to increment).
    /// When: CheckAndTriggerPreload is called.
    /// Then: GetNextChapterKey returns null, the method returns gracefully,
    /// and no downloads are initiated.
    /// </summary>
    [Test]
    public async Task test_preload_not_triggered_when_no_next_chapter()
    {
        // Arrange: use a non-numeric chapter key so GetNextChapterKey returns null
        _dataManager.SetChapterKeys(new[] { "prologue" });
        _mockLoader.SetAsset("prologue", CreateChapterDef("prologue", 0, "frag_intro"));
        await _dataManager.InitializeAsync();
        _mockLoader.ResetCounts();

        // Act
        _dataManager.CheckAndTriggerPreload("prologue", remainingFragments: 1);

        // Assert: no next chapter → no download
        Assert.AreEqual(0, _mockLoader.DownloadedLabels.Count,
            "No preload should occur when GetNextChapterKey returns null");
    }

    // =========================================================================
    // AC: DownloadDependenciesAsync is called with correct label
    // =========================================================================

    /// <summary>
    /// Given: Ready state with ch02 defined.
    /// When: PreloadChapterAsync("ch02") is awaited.
    /// Then: DownloadDependenciesAsync is called with label "Art_Ch02".
    /// </summary>
    [Test]
    public async Task test_preload_chapter_downloads_dependencies()
    {
        // Arrange
        var dm = await InitializeWithMetadata();
        _mockLoader.ResetCounts();

        // Act
        await dm.PreloadChapterAsync("ch02");

        // Assert
        Assert.IsTrue(_mockLoader.DownloadedLabels.Contains("Art_Ch02"),
            "PreloadChapterAsync should call DownloadDependenciesAsync with Art_Ch02");
    }

    // =========================================================================
    // AC: Duplicate preload calls are idempotent
    // =========================================================================

    /// <summary>
    /// Given: A chapter that has already been preloaded.
    /// When: PreloadChapterAsync is called again for the same chapter.
    /// Then: The call returns immediately (no additional download).
    /// </summary>
    [Test]
    public async Task test_preload_duplicate_call_returns_immediately()
    {
        // Arrange
        var dm = await InitializeWithMetadata();

        // First preload
        await dm.PreloadChapterAsync("ch02");
        int downloadCount = _mockLoader.DownloadedLabels.Count;

        // Act: second preload for same chapter
        await dm.PreloadChapterAsync("ch02");

        // Assert: no additional download
        Assert.AreEqual(downloadCount, _mockLoader.DownloadedLabels.Count,
            "Second PreloadChapterAsync call should not trigger additional download");
    }

    // =========================================================================
    // AC: UnloadChapter releases all chapter assets
    // =========================================================================

    /// <summary>
    /// Given: Ready state with ch01 illustrations loaded into cache.
    /// When: UnloadChapter("ch01") is called.
    /// Then: Chapter assets are released via loader.Release, and
    /// GetCachedFragment returns null for ch01 fragments.
    /// </summary>
    [Test]
    public async Task test_unload_releases_all_chapter_assets()
    {
        // Arrange
        var dm = await InitializeWithMetadata();

        // Load illustration assets so they're cached (test BelongsToChapter path)
        var sprite = CreateTestSprite("art_ch01_frag01");
        _mockLoader.SetAsset("art_ch01_frag01", sprite);
        await dm.GetIllustrationAsync("art_ch01_frag01");

        int releasedBefore = _mockLoader.ReleasedKeys.Count;

        // Act
        dm.UnloadChapter("ch01");

        // Assert: chapter definition key ("ch01") released
        Assert.IsTrue(_mockLoader.ReleasedKeys.Contains("ch01"),
            "UnloadChapter must release the chapter definition key");
        Assert.IsTrue(_mockLoader.ReleasedKeys.Contains("art_ch01_frag01"),
            "UnloadChapter must release illustration keys via BelongsToChapter");

        // Assert: GetCachedFragment returns null (chapter removed from metadata)
        Assert.IsNull(dm.GetCachedFragment("ch01", "frag_01"),
            "GetCachedFragment must return null after UnloadChapter");
        Assert.IsNull(dm.GetCachedFragment("ch01", "frag_02"),
            "All fragments in unloaded chapter must return null");
    }

    /// <summary>
    /// Given: A Shared_UI asset in cache alongside chapter assets.
    /// When: UnloadChapter is called.
    /// Then: Shared_UI assets remain in cache and are not released.
    /// </summary>
    [Test]
    public async Task test_unload_does_not_touch_shared_assets()
    {
        // Arrange
        var dm = await InitializeWithMetadata();

        // Load a Shared_ asset into cache
        var sharedSprite = CreateTestSprite("Shared_UI_background");
        _mockLoader.SetAsset("Shared_UI_background", sharedSprite);
        await dm.GetIllustrationAsync("Shared_UI_background");

        int releasedBefore = _mockLoader.ReleasedKeys.Count;

        // Act
        dm.UnloadChapter("ch01");

        // Assert: Shared_UI_background still cached and NOT released
        Assert.IsTrue(dm.IsReady("Shared_UI_background"),
            "Shared_ assets must remain cached after chapter unload");
        Assert.IsFalse(_mockLoader.ReleasedKeys.Contains("Shared_UI_background"),
            "Shared_ assets must NOT be released during chapter unload");
    }

    // =========================================================================
    // AC: Failed preloads do not throw (non-blocking)
    // =========================================================================

    /// <summary>
    /// Given: A mock that throws on DownloadDependenciesAsync.
    /// When: PreloadChapterAsync is called.
    /// Then: No exception propagates to the caller, and state returns to Ready.
    /// </summary>
    [Test]
    public async Task test_preload_failure_does_not_throw()
    {
        // Arrange
        var dm = await InitializeWithMetadata();
        _mockLoader.ThrowOnDownload = true;

        // Act + Assert: no exception escapes
        await dm.PreloadChapterAsync("ch02");

        // State should be Ready (not stuck in PreloadingChapter)
        Assert.AreEqual(DataManagerState.Ready, dm.CurrentState,
            "State must be Ready after failed preload");
    }

    /// <summary>
    /// Given: A preload that failed for ch02.
    /// When: GetIllustrationAsync is called for a ch02 asset via the main load path.
    /// Then: The main load path succeeds (failed preload does not corrupt state).
    /// </summary>
    [Test]
    public async Task test_preload_failure_main_path_still_works()
    {
        // Arrange
        var dm = await InitializeWithMetadata();

        // Make preload fail
        _mockLoader.ThrowOnDownload = true;
        await dm.PreloadChapterAsync("ch02");

        // Reset and set up a regular illustration load
        _mockLoader.ThrowOnDownload = false;
        var sprite = CreateTestSprite("art_ch02_frag03");
        _mockLoader.SetAsset("art_ch02_frag03", sprite);

        // Act: main load path should still work
        var result = await dm.GetIllustrationAsync("art_ch02_frag03");

        // Assert
        Assert.IsNotNull(result, "Main load path must succeed after failed preload");
        Assert.AreEqual("art_ch02_frag03", result.name);
    }

    // =========================================================================
    // AC: Cache strategy enforcement (CurrentPlusNext limits to 2)
    // =========================================================================

    /// <summary>
    /// Given: Three chapters (ch01, ch02, ch03) with CurrentPlusNext cache strategy.
    /// When: All three are preloaded while current chapter is ch02.
    /// Then: The oldest non-current chapter (ch01) is unloaded automatically.
    /// </summary>
    [Test]
    public async Task test_cache_strategy_current_plus_next_unloads_oldest()
    {
        // Arrange: 3 chapters, current = ch02
        var dm = await InitializeWithThreeChapters();
        dm.SetCurrentChapter("ch02");

        // Preload ch01 and ch02 (within limit of 2)
        await dm.PreloadChapterAsync("ch01");
        await dm.PreloadChapterAsync("ch02");

        // Verify both still accessible
        Assert.IsNotNull(dm.GetCachedFragment("ch01", "frag_01"),
            "ch01 should be accessible before limit exceeded");
        Assert.IsNotNull(dm.GetCachedFragment("ch02", "frag_02"),
            "ch02 should be accessible before limit exceeded");

        // Act: preload ch03 — exceeds CurrentPlusNext limit of 2
        await dm.PreloadChapterAsync("ch03");

        // Assert: ch01 (oldest non-current) was evicted
        Assert.IsNull(dm.GetCachedFragment("ch01", "frag_01"),
            "Oldest non-current chapter (ch01) must be evicted when limit exceeded");

        // ch02 (current) and ch03 (newly preloaded) should still be accessible
        Assert.IsNotNull(dm.GetCachedFragment("ch02", "frag_02"),
            "Current chapter must remain cached");
        Assert.IsNotNull(dm.GetCachedFragment("ch03", "frag_03"),
            "Newly preloaded chapter must be cached");
    }

    // =========================================================================
    // AC: In-flight preload cancellation is graceful
    // =========================================================================

    /// <summary>
    /// Given: A preload in-flight (blocked on a delayed download).
    /// When: UnloadChapter cancels the preload mid-flight.
    /// Then: The cancelled preload completes gracefully without throwing.
    /// </summary>
    [Test]
    public async Task test_unload_during_preload_cancels_gracefully()
    {
        // Arrange: use delayed download to keep preload in-flight
        var delayedDownload = new TaskCompletionSource<bool>();
        var delayedLoader = new DelayedPreloadMockLoader();
        delayedLoader.SetDelayedDownload("Art_Ch02", delayedDownload);

        // Copy over assets from default mock to delayed loader
        var ch01Def = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var ch02Def = CreateChapterDef("ch02", 1, "frag_03", "frag_key_03");
        var f1 = CreateFragment("frag_01", "ch01", "art_ch01_frag01");
        var f3 = CreateFragment("frag_03", "ch02", "art_ch02_frag03");

        delayedLoader.SetAsset("ch01", ch01Def);
        delayedLoader.SetAsset("ch02", ch02Def);
        delayedLoader.SetAsset("frag_key_01", f1);
        delayedLoader.SetAsset("frag_key_03", f3);

        _dataManager.SetLoader(delayedLoader);
        _dataManager.SetChapterKeys(new[] { "ch01", "ch02" });
        await _dataManager.InitializeAsync();

        // Act: start preload (will block on delayed download)
        var preloadTask = _dataManager.PreloadChapterAsync("ch02");

        // Yield to let the async method start executing
        await Task.Yield();

        // Cancel via UnloadChapter while download is still pending
        _dataManager.UnloadChapter("ch02");

        // Complete the delayed download now (after cancellation)
        delayedDownload.SetResult(true);

        // Assert: preload completes without throwing
        await preloadTask;

        // State should be Ready
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState,
            "State must return to Ready after cancelled preload");
    }

    // =========================================================================
    // AC: CheckAndTriggerPreload is no-op while preload already in-flight
    // =========================================================================

    /// <summary>
    /// Given: A preload is already in-flight (blocked on delayed download).
    /// When: CheckAndTriggerPreload is called a second time.
    /// Then: The second call is a no-op (only one download initiated).
    /// </summary>
    [Test]
    public async Task test_check_and_trigger_during_preloading_state_is_noop()
    {
        // Arrange: set up with delayed download to keep preload in-flight
        var delayedDownload = new TaskCompletionSource<bool>();
        var delayedLoader = new DelayedPreloadMockLoader();
        delayedLoader.SetDelayedDownload("Art_Ch02", delayedDownload);

        var ch01Def = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var ch02Def = CreateChapterDef("ch02", 1, "frag_03", "frag_key_03");
        var f1 = CreateFragment("frag_01", "ch01", "art_ch01_frag01");
        var f3 = CreateFragment("frag_03", "ch02", "art_ch02_frag03");

        delayedLoader.SetAsset("ch01", ch01Def);
        delayedLoader.SetAsset("ch02", ch02Def);
        delayedLoader.SetAsset("frag_key_01", f1);
        delayedLoader.SetAsset("frag_key_03", f3);

        _dataManager.SetLoader(delayedLoader);
        _dataManager.SetChapterKeys(new[] { "ch01", "ch02" });
        await _dataManager.InitializeAsync();

        _dataManager.SetCurrentChapter("ch01");

        // Act: first trigger starts preload (blocked on delayed download)
        _dataManager.CheckAndTriggerPreload("ch01", remainingFragments: 3);
        int callsAfterFirst = delayedLoader.DownloadCallCount;

        // Second trigger should be no-op because _preloadTasks.Count > 0
        _dataManager.CheckAndTriggerPreload("ch01", remainingFragments: 3);

        // Assert: only one DownloadDependenciesAsync call
        Assert.AreEqual(1, delayedLoader.DownloadCallCount,
            "Only one download should be initiated; second trigger is no-op");

        // Cleanup: complete the download and let preload finish
        delayedDownload.SetResult(true);
        await Task.Delay(10); // brief yield to let async cleanup run

        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState,
            "State must return to Ready after preload completes");
    }
}

// =============================================================================
// DelayedPreloadMockLoader
// =============================================================================

/// <summary>
/// Extended mock loader that supports delaying DownloadDependenciesAsync for
/// testing cancellation and concurrent-trigger suppression.
///
/// A delayed download returns a TaskCompletionSource's Task, giving the test
/// control over when the download completes. Labels without a delayed TCS
/// delegate to the base MockAddressableLoader behavior.
/// </summary>
internal class DelayedPreloadMockLoader : MockAddressableLoader
{
    private readonly Dictionary<string, TaskCompletionSource<bool>> _delayedDownloads = new();

    /// <summary>Number of times DownloadDependenciesAsync was called.</summary>
    public int DownloadCallCount { get; private set; }

    /// <summary>
    /// Registers a delayed TaskCompletionSource for the given label.
    /// Subsequent calls to DownloadDependenciesAsync with this label will return
    /// the TCS's Task, blocking until the test completes it.
    /// </summary>
    public void SetDelayedDownload(string label, TaskCompletionSource<bool> tcs)
    {
        _delayedDownloads[label] = tcs;
    }

    /// <inheritdoc/>
    public override Task DownloadDependenciesAsync(string label)
    {
        DownloadCallCount++;

        if (_delayedDownloads.TryGetValue(label, out var tcs))
        {
            return tcs.Task;
        }

        // Fall back to base behavior (records label or throws based on ThrowOnDownload)
        return base.DownloadDependenciesAsync(label);
    }
}
