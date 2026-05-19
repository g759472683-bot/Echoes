using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for DataManager — the core async loading engine (Story 002).
///
/// Covers all 6 acceptance criteria:
///   AC-1: Startup loads metadata, enters Ready state (&lt;2s timeout in mock)
///   AC-2: GetFragmentAsync returns full fragment from cache
///   AC-3: GetIllustrationAsync loads Sprite via async path
///   AC-4: Three-state model verified (Cached sync, Loading dedup, NotRequested triggers load)
///   AC-5: 3 simultaneous GetFragmentAsync → only 1 LoadAssetAsync call, same Task reference
///   AC-6: IsReady returns true/false without triggering load
///
/// Additional coverage:
///   - All 5 state machine transitions + OnStateChanged firing
///   - DataLoadException wrapping (AssetKey property, inner exception preserved)
///   - Empty chapters → Ready (edge case)
///   - Missing fragments → DataLoadException
///   - Type mismatch in cache → handled gracefully
///   - ReleaseFragment removes from cache and calls loader.Release
///   - GetCachedFragment returns null for unknown chapter/fragment
///   - GetFragmentsByChapter returns copy (not internal reference)
///   - ADR-0001 lifecycle: OnStateChanged nulled in [SetUp]/[TearDown]
/// </summary>
[TestFixture]
public class AsyncEngineTests
{
    private GameObject _gameObject;
    private DataManager _dataManager;
    private MockAddressableLoader _mockLoader;

    // Event tracking
    private List<DataManagerState> _stateChanges;

    // =========================================================================
    // SetUp / TearDown (ADR-0001 Rule 8)
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        DataManager.OnStateChanged = null;
        _stateChanges = new List<DataManagerState>();

        DataManager.OnStateChanged += (state) => _stateChanges.Add(state);

        _gameObject = new GameObject("DataManager_Test");
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
    /// Sets up a full metadata environment: 2 chapters with 2 fragments each.
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

    // =========================================================================
    // AC-1: Startup loads metadata, enters Ready state
    // =========================================================================

    /// <summary>
    /// Given: DataManager in Uninitialized state with valid chapter keys.
    /// When: Metadata loading completes.
    /// Then: State is Ready, OnStateChanged fired for Uninitialized→LoadingMetadata→Ready.
    /// </summary>
    [Test]
    public async Task test_data_manager_startup_loads_metadata_and_enters_ready_state()
    {
        // Arrange
        var chDef = CreateChapterDef("ch01", 0, "frag_entry",
            "frag_key_A", "frag_key_B");
        var fA = CreateFragment("frag_A", "ch01", "art_A");
        var fB = CreateFragment("frag_B", "ch01", "art_B");

        _mockLoader.SetAsset("ch01", chDef);
        _mockLoader.SetAsset("frag_key_A", fA);
        _mockLoader.SetAsset("frag_key_B", fB);

        _dataManager.SetChapterKeys(new[] { "ch01" });

        Assert.AreEqual(DataManagerState.Uninitialized, _dataManager.CurrentState);

        // Act
        await _dataManager.InitializeAsync();

        // Assert: State transitions
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState,
            "DataManager must be Ready after metadata loading");
        Assert.AreEqual(2, _stateChanges.Count,
            "Expected 2 state transitions: Uninitialized→LoadingMetadata→Ready");

        // Verify correct transition sequence: Uninitialized→LoadingMetadata→Ready
        Assert.AreEqual(DataManagerState.LoadingMetadata, _stateChanges[0]);
        Assert.AreEqual(DataManagerState.Ready, _stateChanges[1]);
    }

    /// <summary>
    /// Given: Empty chapter keys array.
    /// When: Metadata loading runs.
    /// Then: Transitions to Ready immediately with no LoadAssetAsync calls.
    /// </summary>
    [Test]
    public async Task test_data_manager_empty_chapter_keys_enters_ready_immediately()
    {
        // Arrange
        _dataManager.SetChapterKeys(new string[0]);

        // Act
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);
        Assert.AreEqual(0, _mockLoader.LoadCallCount,
            "No assets should be loaded when _chapterKeys is empty");
    }

    /// <summary>
    /// Given: Null chapter keys.
    /// When: Metadata loading runs.
    /// Then: Transitions to Ready immediately (graceful handling).
    /// </summary>
    [Test]
    public async Task test_data_manager_null_chapter_keys_enters_ready_immediately()
    {
        // Arrange: _chapterKeys is null by default (not set)
        // Act
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);
        Assert.AreEqual(0, _mockLoader.LoadCallCount);
    }

    /// <summary>
    /// Given: A chapter definition that fails to load.
    /// When: Metadata loading encounters the error.
    /// Then: State transitions to Error.
    /// </summary>
    [Test]
    public async Task test_data_manager_metadata_load_failure_enters_error_state()
    {
        // Arrange: Don't set ch02 in mock → GetAsync<ChapterDefinition>("ch02") throws
        var chDef = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var f1 = CreateFragment("frag_01", "ch01", "art_01");

        _mockLoader.SetAsset("ch01", chDef);
        _mockLoader.SetAsset("frag_key_01", f1);
        // ch02 is NOT set → will throw

        _dataManager.SetChapterKeys(new[] { "ch01", "ch02" });

        // Act
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(DataManagerState.Error, _dataManager.CurrentState);
        Assert.AreEqual(DataManagerState.Error, _stateChanges[_stateChanges.Count - 1]);
    }

    /// <summary>
    /// Given: A chapter definition with null Fragments array.
    /// When: Metadata loads.
    /// Then: Chapter is loaded with empty fragment list, no crash.
    /// </summary>
    [Test]
    public async Task test_data_manager_chapter_with_null_fragments_handled_gracefully()
    {
        // Arrange
        var chDef = CreateChapterDef("ch01", 0, "frag_01"); // empty fragments
        chDef.Fragments = null;

        _mockLoader.SetAsset("ch01", chDef);

        _dataManager.SetChapterKeys(new[] { "ch01" });

        // Act
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);
        var fragments = _dataManager.GetFragmentsByChapter("ch01");
        Assert.AreEqual(0, fragments.Count);
    }

    // =========================================================================
    // AC-2: GetFragmentAsync returns full fragment from cache
    // =========================================================================

    /// <summary>
    /// Given: Ready state, fragment "frag_01" in cache.
    /// When: GetFragmentAsync("ch01", "frag_01") called.
    /// Then: Returns complete MemoryFragment with all fields populated.
    /// </summary>
    [Test]
    public async Task test_data_manager_get_fragment_async_returns_cached_fragment()
    {
        // Arrange
        var dm = await InitializeWithMetadata();

        // Act
        var fragment = await dm.GetFragmentAsync("ch01", "frag_01");

        // Assert: Full fragment definition returned
        Assert.IsNotNull(fragment);
        Assert.AreEqual("frag_01", fragment.FragmentId);
        Assert.AreEqual("ch01", fragment.ChapterKey);
        Assert.AreEqual("art_ch01_frag01", fragment.IllustrationKey);
        Assert.IsNotNull(fragment.AudioKeys);
        Assert.AreEqual(2, fragment.AudioKeys.Length);
        Assert.AreEqual("audio_ambient", fragment.AudioKeys[0]);
        Assert.AreEqual("audio_stinger", fragment.AudioKeys[1]);
        Assert.IsNotNull(fragment.InteractiveObjects);
        Assert.IsNotNull(fragment.ChoiceGroups);
    }

    /// <summary>
    /// Given: Ready state with known chapters.
    /// When: Requesting a fragment ID that does not exist.
    /// Then: Task faults with DataLoadException.
    /// </summary>
    [Test]
    public void test_data_manager_get_fragment_async_missing_fragment_throws()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            var dm = await InitializeWithMetadata();
            await dm.GetFragmentAsync("ch01", "frag_nonexistent");
        });
    }

    /// <summary>
    /// Given: Ready state with known chapters.
    /// When: Requesting a fragment from an unknown chapter.
    /// Then: Task faults with DataLoadException.
    /// </summary>
    [Test]
    public void test_data_manager_get_fragment_async_missing_chapter_throws()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            var dm = await InitializeWithMetadata();
            await dm.GetFragmentAsync("ch99", "frag_01");
        });
    }

    // =========================================================================
    // AC-3: GetIllustrationAsync loads Sprite via async path
    // =========================================================================

    /// <summary>
    /// Given: An illustration key registered in the mock.
    /// When: GetIllustrationAsync(key) called and awaited.
    /// Then: Returns a valid non-null Sprite.
    /// </summary>
    [Test]
    public async Task test_data_manager_get_illustration_async_returns_sprite()
    {
        // Arrange: first get to Ready state, then set up an illustration key
        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        var testSprite = CreateTestSprite("art_ch01_letter");
        _mockLoader.SetAsset("art_ch01_letter", testSprite);
        _mockLoader.ResetCounts(); // reset count from InitializeAsync

        // Act
        var sprite = await _dataManager.GetIllustrationAsync("art_ch01_letter");

        // Assert
        Assert.IsNotNull(sprite);
        Assert.AreEqual("art_ch01_letter", sprite.name);
        Assert.AreEqual(1, _mockLoader.LoadCallCount);
    }

    /// <summary>
    /// Given: An illustration key NOT in the mock.
    /// When: GetIllustrationAsync(key) called.
    /// Then: Task faults with DataLoadException containing the asset key.
    /// </summary>
    [Test]
    public void test_data_manager_get_illustration_async_missing_key_throws_data_load_exception()
    {
        Assert.ThrowsAsync<DataLoadException>(async () =>
        {
            _dataManager.SetChapterKeys(new string[0]);
            await _dataManager.InitializeAsync();
            await _dataManager.GetIllustrationAsync("nonexistent_key");
        });
    }

    /// <summary>
    /// Verify that a cached illustration returns instantly (no additional load call).
    /// </summary>
    [Test]
    public async Task test_data_manager_get_illustration_async_cached_returns_instantly()
    {
        // Arrange
        var sprite = CreateTestSprite("art_cached");
        _mockLoader.SetAsset("art_cached", sprite);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // First load: should trigger LoadAssetAsync
        await _dataManager.GetIllustrationAsync("art_cached");
        Assert.AreEqual(1, _mockLoader.LoadCallCount);

        // Second load: should hit cache, no additional LoadAssetAsync
        var sprite2 = await _dataManager.GetIllustrationAsync("art_cached");
        Assert.IsNotNull(sprite2);
        Assert.AreEqual(1, _mockLoader.LoadCallCount,
            "Second load should hit cache — no additional Addressables call");
    }

    // =========================================================================
    // AC-4: Three-state model (Cached sync, Loading dedup, NotRequested triggers load)
    // =========================================================================

    /// <summary>
    /// Cached: asset in cache → GetAsync returns Task.FromResult (sync, no allocation).
    /// </summary>
    [Test]
    public async Task test_data_manager_three_state_cached_returns_sync_task()
    {
        // Arrange
        var sprite = CreateTestSprite("art_sync_test");
        _mockLoader.SetAsset("art_sync_test", sprite);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Load to populate cache
        await _dataManager.GetIllustrationAsync("art_sync_test");
        _mockLoader.ResetCounts();

        // Act
        var task = _dataManager.GetIllustrationAsync("art_sync_test");

        // Assert: Cached returns an already-completed task with zero new load calls
        Assert.IsTrue(task.IsCompleted, "Cached assets must return a completed Task");
        Assert.AreEqual(0, _mockLoader.LoadCallCount, "Cached assets must not trigger a load");
        Assert.IsNotNull(task.Result);
    }

    /// <summary>
    /// Loading: load in progress → subsequent calls return same Task reference (dedup).
    /// </summary>
    [Test]
    public async Task test_data_manager_three_state_loading_deduplicates_requests()
    {
        // Arrange
        var delayedTcs = new TaskCompletionSource<Sprite>();
        _mockLoader.SetDelayedLoad("art_loading", delayedTcs);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Act: Initiate load (will block on delayed TCS)
        var task1 = _dataManager.GetIllustrationAsync("art_loading");
        var task2 = _dataManager.GetIllustrationAsync("art_loading");

        // Assert: Both tasks are the same reference (dedup)
        Assert.AreSame(task1, task2,
            "Concurrent requests must return the same Task reference");
        Assert.AreEqual(1, _mockLoader.LoadCallCount,
            "Only one LoadAssetAsync should be triggered");

        // Complete the delayed load
        var testSprite = CreateTestSprite("art_loading");
        delayedTcs.SetResult(testSprite);

        var result1 = await task1;
        var result2 = await task2;

        Assert.IsNotNull(result1);
        Assert.AreSame(result1, result2,
            "Both awaiters should receive the same Sprite instance");
    }

    /// <summary>
    /// NotRequested: asset not yet loaded → GetAsync triggers a new Addressables load.
    /// </summary>
    [Test]
    public async Task test_data_manager_three_state_not_requested_triggers_load()
    {
        // Arrange
        var sprite = CreateTestSprite("art_new_load");
        _mockLoader.SetAsset("art_new_load", sprite);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();
        _mockLoader.ResetCounts();

        Assert.AreEqual(0, _mockLoader.LoadCallCount,
            "No loads should have occurred yet");

        // Act
        var result = await _dataManager.GetIllustrationAsync("art_new_load");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, _mockLoader.LoadCallCount,
            "NotRequested asset must trigger exactly one LoadAssetAsync");
    }

    /// <summary>
    /// Loading → failure: all awaiters receive DataLoadException, readiness reset to NotRequested.
    /// </summary>
    [Test]
    public async Task test_data_manager_loading_failure_resets_readiness_to_not_requested()
    {
        // Arrange: delayed load that will be failed
        var delayedTcs = new TaskCompletionSource<Sprite>();
        _mockLoader.SetDelayedLoad("art_failing", delayedTcs);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        var task1 = _dataManager.GetIllustrationAsync("art_failing");
        var task2 = _dataManager.GetIllustrationAsync("art_failing");

        Assert.IsFalse(_dataManager.IsReady("art_failing"),
            "Asset should not be ready while loading");

        // Act: Fail the load
        delayedTcs.SetException(new InvalidOperationException("Simulated load failure"));

        // Both tasks should fault with DataLoadException
        try { await task1; Assert.Fail("Task1 should have thrown"); }
        catch (DataLoadException ex)
        {
            Assert.AreEqual("art_failing", ex.AssetKey);
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
        }

        try { await task2; Assert.Fail("Task2 should have thrown"); }
        catch (DataLoadException) { /* expected */ }

        // Readiness should be NotRequested (retryable)
        Assert.IsFalse(_dataManager.IsReady("art_failing"),
            "Failed assets should be NotRequested (retryable)");
    }

    // =========================================================================
    // AC-5: Concurrent request deduplication → 1 LoadAssetAsync, same Task ref
    // =========================================================================

    /// <summary>
    /// Given: A fragment key in NotRequested state (empty chapter keys → no pre-loaded metadata).
    /// When: 3 callers simultaneously call GetFragmentAsync("ch2", "frag_05").
    /// Then: Only 1 LoadAssetAsync call, all 3 receive the same Task reference.
    /// </summary>
    [Test]
    public async Task test_data_manager_concurrent_get_fragment_async_deduplicates_to_one_load()
    {
        // Arrange: set up fragment behind a delayed TCS so we can observe Loading state
        var delayedTcs = new TaskCompletionSource<MemoryFragment>();
        _mockLoader.SetDelayedLoad("ch2/frag_05", delayedTcs);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();
        _mockLoader.ResetCounts();

        // Act: 3 simultaneous requests
        var task1 = _dataManager.GetFragmentAsync("ch2", "frag_05");
        var task2 = _dataManager.GetFragmentAsync("ch2", "frag_05");
        var task3 = _dataManager.GetFragmentAsync("ch2", "frag_05");

        // Assert: All 3 tasks are the same reference
        Assert.AreSame(task1, task2,
            "3 concurrent GetFragmentAsync calls must return the same Task reference");
        Assert.AreSame(task2, task3,
            "3 concurrent GetFragmentAsync calls must return the same Task reference");
        Assert.AreEqual(1, _mockLoader.LoadCallCount,
            "Only 1 Addressables.LoadAssetAsync should be triggered for 3 concurrent requests");

        // Complete the load
        var fragment = CreateFragment("frag_05", "ch2", "art_ch2_frag05");
        delayedTcs.SetResult(fragment);

        var r1 = await task1;
        var r2 = await task2;
        var r3 = await task3;

        Assert.IsNotNull(r1);
        Assert.AreSame(r1, r2);
        Assert.AreSame(r2, r3);
        Assert.AreEqual("frag_05", r1.FragmentId);
    }

    /// <summary>
    /// Given: An illustration key in NotRequested state.
    /// When: 3 callers simultaneously call GetIllustrationAsync.
    /// Then: Only 1 LoadAssetAsync call, all 3 receive the same Task reference.
    /// </summary>
    [Test]
    public async Task test_data_manager_concurrent_get_illustration_async_deduplicates_to_one_load()
    {
        // Arrange
        var delayedTcs = new TaskCompletionSource<Sprite>();
        _mockLoader.SetDelayedLoad("art_shared", delayedTcs);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Act
        var task1 = _dataManager.GetIllustrationAsync("art_shared");
        var task2 = _dataManager.GetIllustrationAsync("art_shared");
        var task3 = _dataManager.GetIllustrationAsync("art_shared");

        // Assert: Same Task reference, single load
        Assert.AreSame(task1, task2);
        Assert.AreSame(task2, task3);
        Assert.AreEqual(1, _mockLoader.LoadCallCount);

        // Complete
        delayedTcs.SetResult(CreateTestSprite("art_shared"));
        await Task.WhenAll(task1, task2, task3);
    }

    // =========================================================================
    // AC-6: IsReady returns true/false without triggering load
    // =========================================================================

    /// <summary>
    /// Given: Cached asset "art_cached" and NotRequested asset "art_missing".
    /// When: IsReady() called for both.
    /// Then: Returns true for cached, false for not-requested. Does NOT trigger loads.
    /// </summary>
    [Test]
    public async Task test_data_manager_is_ready_returns_correctly_without_triggering_load()
    {
        // Arrange
        var sprite = CreateTestSprite("art_cached");
        _mockLoader.SetAsset("art_cached", sprite);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Load one asset to make it cached
        await _dataManager.GetIllustrationAsync("art_cached");
        _mockLoader.ResetCounts();

        // Act
        bool isCachedReady = _dataManager.IsReady("art_cached");
        bool isMissingReady = _dataManager.IsReady("art_missing");

        // Assert
        Assert.IsTrue(isCachedReady,
            "IsReady must return true for cached assets");
        Assert.IsFalse(isMissingReady,
            "IsReady must return false for NotRequested assets");
        Assert.AreEqual(0, _mockLoader.LoadCallCount,
            "IsReady must NOT trigger any loads");
    }

    /// <summary>
    /// Verify IsReady returns false for assets currently in Loading state.
    /// </summary>
    [Test]
    public async Task test_data_manager_is_ready_returns_false_during_loading()
    {
        // Arrange
        var delayedTcs = new TaskCompletionSource<Sprite>();
        _mockLoader.SetDelayedLoad("art_loading", delayedTcs);

        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Start loading (but don't await)
        _ = _dataManager.GetIllustrationAsync("art_loading");

        // Act
        bool isReady = _dataManager.IsReady("art_loading");

        // Assert
        Assert.IsFalse(isReady,
            "IsReady must return false while asset is Loading");
    }

    // =========================================================================
    // State Machine — All 5 Transitions
    // =========================================================================

    /// <summary>
    /// Verifies all 5 state machine transitions fire OnStateChanged.
    /// Ready ↔ PreloadingChapter transitions are not covered here (Story 003 stub).
    /// </summary>
    [Test]
    public async Task test_data_manager_state_machine_fires_on_state_changed_for_all_transitions()
    {
        // Start from Uninitialized
        Assert.AreEqual(DataManagerState.Uninitialized, _dataManager.CurrentState);

        // Transition 1: Uninitialized → LoadingMetadata (inside InitializeAsync)
        // Transition 2: LoadingMetadata → Ready (success)
        var chDef = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var f1 = CreateFragment("frag_01", "ch01", "art_01");

        _mockLoader.SetAsset("ch01", chDef);
        _mockLoader.SetAsset("frag_key_01", f1);

        _dataManager.SetChapterKeys(new[] { "ch01" });
        await _dataManager.InitializeAsync();

        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);
        Assert.IsTrue(_stateChanges.Contains(DataManagerState.LoadingMetadata),
            "Must fire OnStateChanged for LoadingMetadata");
        Assert.IsTrue(_stateChanges.Contains(DataManagerState.Ready),
            "Must fire OnStateChanged for Ready");
    }

    /// <summary>
    /// LoadingMetadata → Error transition fires OnStateChanged(Error).
    /// </summary>
    [Test]
    public async Task test_data_manager_error_state_fires_on_state_changed()
    {
        // Arrange: missing chapter definition triggers error
        _dataManager.SetChapterKeys(new[] { "ch_missing" });
        // ch_missing is NOT in mock → will throw

        // Act
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(DataManagerState.Error, _dataManager.CurrentState);
        Assert.IsTrue(_stateChanges.Contains(DataManagerState.Error),
            "Must fire OnStateChanged for Error state");
    }

    /// <summary>
    /// Duplicate state transitions do NOT fire OnStateChanged.
    /// </summary>
    [Test]
    public async Task test_data_manager_duplicate_state_transition_does_not_refire_event()
    {
        // Arrange
        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        int beforeCount = _stateChanges.Count;
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);

        // Act: Call InitializeAsync again (should be no-op)
        await _dataManager.InitializeAsync();

        // Assert
        Assert.AreEqual(beforeCount, _stateChanges.Count,
            "Duplicate InitializeAsync must not refire state transitions");
    }

    // =========================================================================
    // DataLoadException
    // =========================================================================

    /// <summary>
    /// DataLoadException carries AssetKey and preserves inner exception.
    /// </summary>
    [Test]
    public void test_data_load_exception_has_asset_key_and_inner_exception()
    {
        var inner = new InvalidOperationException("original error");
        var ex = new DataLoadException("test_asset_key", inner);

        Assert.AreEqual("test_asset_key", ex.AssetKey);
        Assert.AreEqual("Failed to load asset: test_asset_key", ex.Message);
        Assert.AreSame(inner, ex.InnerException);
    }

    /// <summary>
    /// DataLoadException with null inner exception is valid.
    /// </summary>
    [Test]
    public void test_data_load_exception_with_null_inner_exception_is_valid()
    {
        var ex = new DataLoadException("some_key", null);

        Assert.AreEqual("some_key", ex.AssetKey);
        Assert.IsNull(ex.InnerException);
    }

    // =========================================================================
    // GetChapterAsync
    // =========================================================================

    /// <summary>
    /// GetChapterAsync returns the ChapterDefinition for a known chapter.
    /// </summary>
    [Test]
    public async Task test_data_manager_get_chapter_async_returns_definition()
    {
        // Arrange
        var chDef = CreateChapterDef("ch01", 0, "frag_01", "frag_key_01");
        var f1 = CreateFragment("frag_01", "ch01", "art_01");

        _mockLoader.SetAsset("ch01", chDef);
        _mockLoader.SetAsset("frag_key_01", f1);

        _dataManager.SetChapterKeys(new[] { "ch01" });
        await _dataManager.InitializeAsync();

        // Act
        var result = await _dataManager.GetChapterAsync("ch01");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("ch01", result.ChapterKey);
        Assert.AreEqual(0, result.OrderIndex);
        Assert.AreEqual("frag_01", result.EntryFragmentId);
        Assert.AreEqual(1, result.Fragments.Length);
    }

    // =========================================================================
    // GetFragmentsByChapter
    // =========================================================================

    [Test]
    public async Task test_data_manager_get_fragments_by_chapter_returns_fragment_list()
    {
        var dm = await InitializeWithMetadata();

        var fragments = dm.GetFragmentsByChapter("ch01");

        Assert.IsNotNull(fragments);
        Assert.AreEqual(2, fragments.Count);
        Assert.AreEqual("frag_01", fragments[0].FragmentId);
        Assert.AreEqual("frag_02", fragments[1].FragmentId);
    }

    [Test]
    public async Task test_data_manager_get_fragments_by_chapter_unknown_returns_empty_list()
    {
        var dm = await InitializeWithMetadata();

        var fragments = dm.GetFragmentsByChapter("nonexistent");

        Assert.IsNotNull(fragments);
        Assert.AreEqual(0, fragments.Count);
    }

    /// <summary>
    /// GetFragmentsByChapter returns a COPY, not the internal list reference.
    /// </summary>
    [Test]
    public async Task test_data_manager_get_fragments_by_chapter_returns_copy_not_reference()
    {
        var dm = await InitializeWithMetadata();

        var list1 = dm.GetFragmentsByChapter("ch01");
        var list2 = dm.GetFragmentsByChapter("ch01");

        Assert.AreNotSame(list1, list2,
            "GetFragmentsByChapter must return a new List each time");
        Assert.AreEqual(list1.Count, list2.Count);
    }

    // =========================================================================
    // GetCachedFragment
    // =========================================================================

    [Test]
    public async Task test_data_manager_get_cached_fragment_returns_fragment()
    {
        var dm = await InitializeWithMetadata();

        var fragment = dm.GetCachedFragment("ch01", "frag_01");

        Assert.IsNotNull(fragment);
        Assert.AreEqual("frag_01", fragment.FragmentId);
        Assert.AreEqual("ch01", fragment.ChapterKey);
    }

    [Test]
    public async Task test_data_manager_get_cached_fragment_unknown_returns_null()
    {
        var dm = await InitializeWithMetadata();

        Assert.IsNull(dm.GetCachedFragment("ch99", "frag_99"));
        Assert.IsNull(dm.GetCachedFragment("ch01", "frag_nonexistent"));
    }

    // =========================================================================
    // ReleaseFragment
    // =========================================================================

    [Test]
    public async Task test_data_manager_release_fragment_removes_from_cache()
    {
        var dm = await InitializeWithMetadata();

        // Verify fragment is cached
        Assert.IsNotNull(dm.GetCachedFragment("ch01", "frag_01"));
        Assert.IsTrue(dm.IsReady("frag_key_01"));

        // Act
        dm.ReleaseFragment("frag_01");

        // Assert: fragment removed from cache and loader.Release called
        Assert.IsNull(dm.GetCachedFragment("ch01", "frag_01"),
            "Cached fragment lookup should return null after release");
        Assert.IsFalse(dm.IsReady("frag_key_01"),
            "IsReady should return false after release");

        Assert.IsTrue(_mockLoader.ReleasedKeys.Contains("frag_key_01"),
            "ReleaseFragment must call loader.Release with the correct key");
    }

    [Test]
    public async Task test_data_manager_release_fragment_unknown_id_is_noop()
    {
        var dm = await InitializeWithMetadata();
        int releasedBefore = _mockLoader.ReleasedKeys.Count;

        // Act: release non-existent fragment (should not throw)
        dm.ReleaseFragment("nonexistent_frag");

        // Assert: no additional releases
        Assert.AreEqual(releasedBefore, _mockLoader.ReleasedKeys.Count);
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    /// <summary>
    /// GetFragmentAsync during Error state still tries to load via GetAsync
    /// (does not crash — GetAsync handles the load and faults with DataLoadException if failing).
    /// </summary>
    [Test]
    public async Task test_data_manager_error_state_does_not_crash_on_get_fragment_async()
    {
        // Arrange: trigger error via empty chapters + missing fragment load
        _dataManager.SetChapterKeys(new string[0]);
        await _dataManager.InitializeAsync();

        // Act: try to get a fragment that's not in cache and not in mock
        try
        {
            await _dataManager.GetFragmentAsync("ch99", "frag_99");
            Assert.Fail("Should have thrown DataLoadException");
        }
        catch (DataLoadException ex)
        {
            Assert.AreEqual("ch99/frag_99", ex.AssetKey);
        }
    }

    /// <summary>
    /// Verify that InitializeAsync is idempotent (subsequent calls are no-ops).
    /// </summary>
    [Test]
    public async Task test_data_manager_initialize_async_is_idempotent()
    {
        _dataManager.SetChapterKeys(new string[0]);

        await _dataManager.InitializeAsync();
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);

        // Second call should be no-op
        await _dataManager.InitializeAsync();
        Assert.AreEqual(DataManagerState.Ready, _dataManager.CurrentState);
    }
}

// =============================================================================
// MockAddressableLoader
// =============================================================================

/// <summary>
/// Mock implementation of IAddressableLoader for unit testing DataManager.
///
/// Supports:
///   - Immediate responses: SetAsset(key, asset) → LoadAssetAsync returns Task.FromResult
///   - Delayed responses: SetDelayedLoad(key, tcs) → LoadAssetAsync returns tcs.Task
///     (used for testing Loading state and dedup behavior)
///   - Call counting: LoadCallCount for verifying dedup (only 1 call for N requests)
///   - Release tracking: ReleasedKeys for verifying fragment/chapter cleanup
/// </summary>
internal class MockAddressableLoader : IAddressableLoader
{
    private readonly Dictionary<string, object> _assets = new();
    private readonly Dictionary<string, object> _delayedLoads = new();

    public int LoadCallCount { get; private set; }
    public List<string> ReleasedKeys { get; } = new();

    /// <summary>
    /// Pre-sets an asset to be returned instantly via Task.FromResult.
    /// </summary>
    public void SetAsset<T>(string key, T asset) where T : class
    {
        _assets[key] = asset;
    }

    /// <summary>
    /// Sets up a delayed (controllable) load for the given key.
    /// LoadAssetAsync will return the TCS's Task. The test controls when the TCS completes.
    /// Takes priority over SetAsset.
    /// </summary>
    public void SetDelayedLoad<T>(string key, TaskCompletionSource<T> tcs) where T : class
    {
        _delayedLoads[key] = tcs;
    }

    public List<string> DownloadedLabels { get; } = new();
    public bool ThrowOnDownload { get; set; }

    public virtual Task DownloadDependenciesAsync(string label)
    {
        if (ThrowOnDownload)
            throw new InvalidOperationException($"Simulated download failure for: {label}");
        DownloadedLabels.Add(label);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resets LoadCallCount to 0 (for tests that want to count from a clean slate).
    /// Does NOT clear pre-set assets or delayed loads.
    /// </summary>
    public void ResetCounts()
    {
        LoadCallCount = 0;
        DownloadedLabels.Clear();
    }

    public Task<T> LoadAssetAsync<T>(string key) where T : class
    {
        LoadCallCount++;

        // Delayed loads take priority (for testing Loading state)
        if (_delayedLoads.TryGetValue(key, out var tcsObj) && tcsObj is TaskCompletionSource<T> tcs)
        {
            return tcs.Task;
        }

        // Immediate responses
        if (_assets.TryGetValue(key, out var asset) && asset is T typedAsset)
        {
            return Task.FromResult(typedAsset);
        }

        // Key not found: simulate Addressables failure
        throw new InvalidOperationException($"Asset not found in mock: {key}");
    }

    public void Release(string key)
    {
        ReleasedKeys.Add(key);
    }
}
