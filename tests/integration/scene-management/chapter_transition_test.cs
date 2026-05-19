using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integration tests for scene-management Story 004 (Chapter Transition + Preload Coordination).
///
/// Covers AC-1 through AC-5 using mock ISceneFader, IDataManager, IAudioManager.
/// No real Unity scenes or Addressables required — all loading is injected via mocks.
///
/// Mock implementations are shared with scene_boot_test.cs (MockSceneFader, MockDataManager,
/// MockAudioManager). Additional test-control fields were added for Story 004.
/// </summary>
public class ChapterTransitionTest
{
    private GameObject _gameObject;
    private GameSceneManager _sceneManager;
    private MockSceneFader _sceneFader;
    private MockDataManager _dataManager;
    private MockAudioManager _audioManager;

    [SetUp]
    public void SetUp()
    {
        _sceneFader = new MockSceneFader();
        _dataManager = new MockDataManager();
        _audioManager = new MockAudioManager();

        _gameObject = new GameObject("GameSceneManager_Test");
        SpriteRenderer renderer = _gameObject.AddComponent<SpriteRenderer>();
        _sceneManager = _gameObject.AddComponent<GameSceneManager>();
        _sceneManager.Initialize(_sceneFader, _dataManager, _audioManager, renderer);

        _sceneManager._sceneLoadFuncForTesting = _ => Task.CompletedTask;
        _sceneManager._sceneLoadTimeoutSeconds = 1.0f;
    }

    [TearDown]
    public void TearDown()
    {
        GameSceneManager.OnFragmentTransitionStarted = null;
        GameSceneManager.OnFragmentTransitioned = null;
        GameSceneManager.OnSceneLoaded = null;
        GameSceneManager.OnChapterTransitionStarted = null;
        GameSceneManager.OnChapterTransitioned = null;
        BootBootstrap.OnBootComplete = null;
        InputManager.OnSetActionMap = null;
        InputManager.OnInputStateChanged = null;

        _sceneManager._sceneLoadFuncForTesting = null;

        if (_gameObject != null)
        {
            Object.DestroyImmediate(_gameObject);
            _gameObject = null;
        }
    }

    // Helper: Create a list of MemoryFragments with sequential IDs
    private static List<MemoryFragment> MakeFragments(string[] fragmentIds)
    {
        List<MemoryFragment> fragments = new List<MemoryFragment>();
        foreach (string id in fragmentIds)
        {
            MemoryFragment frag = ScriptableObject.CreateInstance<MemoryFragment>();
            frag.FragmentId = id;
            frag.ChapterKey = "chapter_1";
            frag.IllustrationKey = "ill_" + id;
            frag.AudioKeys = new[] { "audio_" + id };
            fragments.Add(frag);
        }
        return fragments;
    }

    // Helper: Create a ChapterDefinition with the given entry fragment
    private static ChapterDefinition MakeChapterDef(string chapterKey, string entryFragmentId)
    {
        ChapterDefinition def = ScriptableObject.CreateInstance<ChapterDefinition>();
        def.ChapterKey = chapterKey;
        def.EntryFragmentId = entryFragmentId;
        def.OrderIndex = 0;
        return def;
    }

    // ==========================================================================
    // AC-1: Preload trigger — ≤3 fragments remaining, background preload kicks off
    // ==========================================================================

    /// <summary>
    /// GIVEN chapter 1 has 10 fragments, WHEN player enters the 8th fragment
    /// (index 7, 3 remaining), THEN background preload triggers for chapter 2.
    /// </summary>
    [Test]
    public async Task test_preload_trigger_when_3_fragments_remain()
    {
        // Arrange: Chapter 1 has 10 fragments
        string[] ids = { "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        // Track preload calls
        bool dataPreloadStarted = false;
        TaskCompletionSource<bool> dataPreloadTcs = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(dataPreloadTcs);

        bool audioPreloadCalled = false;
        _audioManager.OnPreloadChapterAudioCalled += _ => audioPreloadCalled = true;

        // Act: Enter the 8th fragment (index 7, remaining = 10 - 8 = 2 ≤ 3)
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f8");

        // Complete the background preload
        dataPreloadTcs.SetResult(true);

        // Wait one frame for fire-and-forget to propagate
        await Task.Yield();

        // Assert: Preload was triggered for chapter_2
        Assert.AreEqual("chapter_2", _dataManager.PreloadedChapterKey,
            "Preload must target the next chapter (chapter_2)");
        Assert.IsTrue(audioPreloadCalled,
            "Audio preload must be called in parallel with data preload");
    }

    /// <summary>
    /// GIVEN chapter 1 has 10 fragments, WHEN player is on fragment 5
    /// (index 4, 6 remaining), THEN preload does NOT trigger.
    /// </summary>
    [Test]
    public async Task test_preload_not_triggered_when_many_fragments_remain()
    {
        // Arrange: Chapter 1 has 10 fragments
        string[] ids = { "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        TaskCompletionSource<bool> preloadTcs = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(preloadTcs);
        bool audioPreloadCalled = false;
        _audioManager.OnPreloadChapterAudioCalled += _ => audioPreloadCalled = true;

        // Act: Enter fragment 5 (index 4, remaining = 10 - 5 = 5 > 3)
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f5");

        // Complete any possible preload
        preloadTcs.SetResult(true);
        await Task.Yield();

        // Assert: Preload was NOT triggered (remaining > 3)
        Assert.IsFalse(_dataManager.PreloadCalled || audioPreloadCalled,
            "Preload must NOT trigger when more than 3 fragments remain");
    }

    /// <summary>
    /// GIVEN preload was already triggered, WHEN player enters another fragment
    /// in the same chapter, THEN preload does NOT trigger again (no duplicate).
    /// </summary>
    [Test]
    public async Task test_preload_trigger_no_duplicate_within_same_chapter()
    {
        // Arrange: Chapter 1 has 6 fragments
        string[] ids = { "f1", "f2", "f3", "f4", "f5", "f6" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        TaskCompletionSource<bool> preloadTcs = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(preloadTcs);

        int preloadChapterAudioCalls = 0;
        _audioManager.OnPreloadChapterAudioCalled += _ => preloadChapterAudioCalls++;

        // Act: Enter f4 (index 3, remaining = 6 - 4 = 2 ≤ 3 → triggers preload)
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f4");
        preloadTcs.SetResult(true);
        await Task.Yield();

        int firstCallCount = preloadChapterAudioCalls;

        // Reset preload tracking
        TaskCompletionSource<bool> preloadTcs2 = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(preloadTcs2);

        // Enter f5 (index 4, remaining = 1 — still ≤ 3, but already triggered)
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f5");
        preloadTcs2.SetResult(true);
        await Task.Yield();

        // Assert: PreloadChapterAudioAsync was NOT called again (no duplicate trigger)
        Assert.AreEqual(firstCallCount, preloadChapterAudioCalls,
            "Preload must not re-trigger within the same chapter");
    }

    // ==========================================================================
    // AC-2: Full chapter transition flow — music crossfade, load, unload, events
    // ==========================================================================

    /// <summary>
    /// GIVEN chapter 1 is active, WHEN TransitionToChapterAsync("chapter_2") is called,
    /// THEN the full sequence executes: StopMusic → FadeOut(1.0s) → Unload Ch01 →
    /// Load Ch02 → PlayMusic → FadeIn(1.0s) → events fire.
    /// </summary>
    [Test]
    public async Task test_chapter_transition_full_flow()
    {
        // Arrange: Set up chapter 1 as current, chapter 2 as target
        _dataManager.SetFragmentsForChapter(MakeFragments(new[] { "f_entry" }));

        ChapterDefinition ch2Def = MakeChapterDef("chapter_2", "frag_ch2_entry");
        _dataManager.SetChapterDefinition(ch2Def);

        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f_entry");
        Assert.AreEqual("chapter_1", _sceneManager.CurrentChapterKey);

        // Track events
        string transitionStartedFrom = null;
        string transitionStartedTo = null;
        string transitionedFrom = null;
        string transitionedTo = null;
        bool fragmentTransitionedFired = false;

        GameSceneManager.OnChapterTransitionStarted += (from, to) =>
        {
            transitionStartedFrom = from;
            transitionStartedTo = to;
        };
        GameSceneManager.OnChapterTransitioned += (from, to) =>
        {
            transitionedFrom = from;
            transitionedTo = to;
        };
        GameSceneManager.OnFragmentTransitioned += (ch, fid) =>
        {
            fragmentTransitionedFired = true;
        };

        // Act
        await _sceneManager.TransitionToChapterAsync("chapter_2");

        // Assert: State returned to Idle
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState);

        // Assert: Music crossfade
        Assert.AreEqual(1, _audioManager.StopMusicCallCount,
            "StopMusic must be called once for crossfade out");
        Assert.AreEqual(1.0f, _audioManager.LastStopMusicFadeTime, 0.001f,
            "StopMusic must use 1.0s fade time");
        Assert.AreEqual(1, _audioManager.PlayMusicCallCount,
            "PlayMusic must be called for new chapter music");
        Assert.AreEqual("chapter_2", _audioManager.LastPlayMusicChapterKey);
        Assert.AreEqual(1.0f, _audioManager.LastPlayMusicFadeTime, 0.001f);

        // Assert: Unload old chapter
        Assert.IsTrue(_dataManager.UnloadChapterCalled,
            "UnloadChapter must be called for old chapter");
        Assert.AreEqual("chapter_1", _dataManager.UnloadedChapterKey);
        Assert.AreEqual(1, _audioManager.UnloadChapterAudioCallCount,
            "UnloadChapterAudio must be called for old chapter");

        // Assert: Fade durations (chapter transitions = 1.0s)
        Assert.AreEqual(1.0f, _sceneFader.FadeOutDuration, 0.001f);
        Assert.AreEqual(1.0f, _sceneFader.FadeInDuration, 0.001f);

        // Assert: Current state updated
        Assert.AreEqual("chapter_2", _sceneManager.CurrentChapterKey);
        Assert.AreEqual("frag_ch2_entry", _sceneManager.CurrentFragmentId);

        // Assert: Events fired in correct order
        Assert.AreEqual("chapter_1", transitionStartedFrom);
        Assert.AreEqual("chapter_2", transitionStartedTo);
        Assert.AreEqual("chapter_1", transitionedFrom);
        Assert.AreEqual("chapter_2", transitionedTo);
        Assert.IsTrue(fragmentTransitionedFired,
            "OnFragmentTransitioned must fire for the entry fragment of the new chapter");
    }

    /// <summary>
    /// Verify that TransitionToChapterAsync fires OnFragmentTransitioned with
    /// the correct chapter key and entry fragment ID.
    /// </summary>
    [Test]
    public async Task test_chapter_transition_fires_fragment_transitioned()
    {
        // Arrange
        ChapterDefinition ch2Def = MakeChapterDef("chapter_2", "frag_first");
        _dataManager.SetChapterDefinition(ch2Def);

        string fragmentChapter = null;
        string fragmentId = null;
        GameSceneManager.OnFragmentTransitioned += (ch, fid) =>
        {
            fragmentChapter = ch;
            fragmentId = fid;
        };

        // Act
        await _sceneManager.TransitionToChapterAsync("chapter_2");

        // Assert
        Assert.AreEqual("chapter_2", fragmentChapter);
        Assert.AreEqual("frag_first", fragmentId);
    }

    // ==========================================================================
    // AC-3: Parallel preload — Task.WhenAll for DataManager + AudioManager
    // ==========================================================================

    /// <summary>
    /// GIVEN preload is triggered, WHEN PreloadNextChapterAsync executes,
    /// THEN DataManager.PreloadChapterAsync and AudioManager.PreloadChapterAudioAsync
    /// are both called (parallel execution verified by both being started before
    /// either completes).
    /// </summary>
    [Test]
    public async Task test_preload_is_parallel_both_started_before_completion()
    {
        // Arrange: Block both preloads so we can verify parallel start
        TaskCompletionSource<bool> dataTcs = new TaskCompletionSource<bool>();
        TaskCompletionSource<bool> audioTcs = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(dataTcs);

        bool audioStarted = false;
        _audioManager.OnPreloadChapterAudioCalled += _ => audioStarted = true;
        _audioManager.SetPreloadChapterAudioCompletion(audioTcs);

        // Arrange fragments so preload triggers
        string[] ids = { "f1", "f2", "f3", "f4" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        // Act: Enter f4 (last, remaining = 0 — triggers preload)
        Task transition = _sceneManager.TransitionToFragmentAsync("chapter_1", "f4");

        // Wait for preload to trigger (data blocks on TCS, audio should be called)
        await Task.Yield();
        await Task.Yield();
        await Task.Yield();

        // Assert: Both preload calls were made (parallel start)
        Assert.IsTrue(_dataManager.PreloadCalled,
            "DataManager.PreloadChapterAsync must be called");
        Assert.IsTrue(audioStarted,
            "AudioManager.PreloadChapterAudioAsync must be called in parallel");

        // Clean up: complete both TCSs
        dataTcs.SetResult(true);
        audioTcs.SetResult(true);
        await transition;
    }

    // ==========================================================================
    // AC-4: Fast skip — preload incomplete, TransitionToChapterAsync awaits it
    // ==========================================================================

    /// <summary>
    /// GIVEN background preload is still in progress (slow), WHEN player reaches
    /// chapter boundary and TransitionToChapterAsync is called, THEN the method
    /// awaits the incomplete preload Task before proceeding. The mask stays
    /// covering the screen during the wait.
    /// </summary>
    [Test]
    public async Task test_fast_skip_awaits_incomplete_preload()
    {
        // Arrange: Set up fragments so preload triggers
        string[] ids = { "f1", "f2", "f3", "f4" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        // Block the preload — simulate slow background load
        TaskCompletionSource<bool> slowPreloadTcs = new TaskCompletionSource<bool>();
        _dataManager.SetPreloadChapterCompletion(slowPreloadTcs);

        TaskCompletionSource<bool> slowAudioTcs = new TaskCompletionSource<bool>();
        _audioManager.SetPreloadChapterAudioCompletion(slowAudioTcs);

        ChapterDefinition ch2Def = MakeChapterDef("chapter_2", "frag_entry");
        _dataManager.SetChapterDefinition(ch2Def);

        // Enter f4 — triggers background preload (blocks on TCS)
        Task transition = _sceneManager.TransitionToFragmentAsync("chapter_1", "f4");
        await Task.Yield();
        await Task.Yield();
        await Task.Yield();

        Assert.IsTrue(_dataManager.PreloadCalled,
            "Background preload should have been triggered");

        // Wait for fragment transition to complete (preload is fire-and-forget, doesn't block it)
        await transition;

        // Now: Attempt chapter transition while preload is still incomplete
        Task chapterTransition = _sceneManager.TransitionToChapterAsync("chapter_2");

        // Let it reach the await point
        await Task.Yield();
        await Task.Yield();

        // Assert: Chapter transition has NOT completed yet (stuck waiting for preload)
        Assert.AreNotEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "Chapter transition must NOT complete while preload is still in progress");

        // Complete the slow preload
        slowPreloadTcs.SetResult(true);
        slowAudioTcs.SetResult(true);

        await chapterTransition;

        // Assert: Now it completed
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState);
        Assert.AreEqual("chapter_2", _sceneManager.CurrentChapterKey);
    }

    // ==========================================================================
    // AC-5: PreloadNextFragmentAsync — fire-and-forget, enables zero-latency
    // ==========================================================================

    /// <summary>
    /// GIVEN a fragment transition completes, WHEN there is a next fragment in
    /// the chapter, THEN PreloadNextFragmentAsync is called (fire-and-forget)
    /// for the next sequential fragment.
    /// </summary>
    [Test]
    public async Task test_preload_next_fragment_after_transition()
    {
        // Arrange: Chapter has 3 fragments
        string[] ids = { "f_a", "f_b", "f_c" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        // Act: Transition to f_a
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f_a");

        // Assert: Next fragment (f_b) was preloaded
        Assert.IsTrue(_dataManager.PreloadFragmentCalled,
            "PreloadFragmentAsync must be called for the next fragment after a transition");
        Assert.AreEqual("chapter_1", _dataManager.PreloadedFragmentChapterKey);
        Assert.AreEqual("f_b", _dataManager.PreloadedFragmentId,
            "Must preload the NEXT sequential fragment (f_b), not the current one");
    }

    /// <summary>
    /// GIVEN the current fragment is the last in the chapter, WHEN the transition
    /// completes, THEN PreloadFragmentAsync is NOT called (no next fragment).
    /// </summary>
    [Test]
    public async Task test_preload_next_fragment_not_called_when_last_fragment()
    {
        // Arrange: Chapter has only 1 fragment
        string[] ids = { "f_only" };
        _dataManager.SetFragmentsForChapter(MakeFragments(ids));

        // Act: Transition to the only fragment
        await _sceneManager.TransitionToFragmentAsync("chapter_1", "f_only");

        // Assert: No next fragment to preload
        Assert.IsFalse(_dataManager.PreloadFragmentCalled,
            "PreloadFragmentAsync must NOT be called when current fragment is the last one");
    }

    // ==========================================================================
    // Edge cases
    // ==========================================================================

    /// <summary>
    /// Verify that TransitionToChapterAsync recovers gracefully when the chapter
    /// data load fails. State must reset to Idle and input must be restored.
    /// </summary>
    [Test]
    public async Task test_chapter_transition_recovers_on_load_failure()
    {
        // Arrange: Make GetFragmentAsync throw
        _dataManager.SetThrowOnLoad(true);
        _dataManager.SetChapterDefinition(MakeChapterDef("chapter_2", "frag_x"));

        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        // Act
        await _sceneManager.TransitionToChapterAsync("chapter_2");

        // Assert: State recovered
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must reset to Idle after load failure");

        // Assert: Input restored
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[inputCalls.Count - 1],
            "Input must be restored to Gameplay after failure");
    }

    /// <summary>
    /// Verify that TransitionToChapterAsync rejects concurrent calls
    /// (state machine guard — only one transition at a time).
    /// </summary>
    [Test]
    public async Task test_chapter_transition_rejects_concurrent()
    {
        // Arrange: Block at FadeOut
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        _dataManager.SetChapterDefinition(MakeChapterDef("chapter_2", "frag_e"));
        _dataManager.SetChapterDefinition(MakeChapterDef("chapter_3", "frag_e3"));

        // Start first chapter transition (blocks at FadeOut)
        Task first = _sceneManager.TransitionToChapterAsync("chapter_2");

        // Second call must be rejected
        await _sceneManager.TransitionToChapterAsync("chapter_3");

        // Complete first
        fadeOutTcs.SetResult(true);
        await first;

        // Assert: Only one FadeOut/FadeIn pair
        Assert.AreEqual(1, _sceneFader.FadeOutCallCount,
            "Only one FadeOut — second call must be rejected");
    }
}
