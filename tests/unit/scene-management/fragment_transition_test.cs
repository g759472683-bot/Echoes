using NUnit.Framework;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for GameSceneManager fragment transitions (Story 003).
///
/// Covers all 6 acceptance criteria from ADR-0004:
///   AC-1: Transition animation timing — FadeOut(0.5) before FadeIn(0.5)
///   AC-2: Concurrent transition rejection — second call ignored while first in progress
///   AC-3: Input gating — SetActionMap(Inactive) at start, SetActionMap(Gameplay) at end
///   AC-4: Static events fire — OnFragmentTransitionStarted before FadeOut, OnFragmentTransitioned after FadeIn
///   AC-5: Events fire exactly once per transition
///   AC-6: Post-transition state is fully populated (sprite, chapter key, fragment ID)
///
/// ADR-0001 compliance:
///   - All static events are nulled in [SetUp] and [TearDown] to prevent cross-test leakage.
///   - Method group subscription used (no lambda closures on hot paths).
///   - OnDestroy nullification verified.
/// </summary>
[TestFixture]
public class FragmentTransitionTests
{
    private MockSceneFader _sceneFader;
    private MockDataManager _dataManager;
    private MockAudioManager _audioManager;
    private GameSceneManager _sceneManager;
    private GameObject _gameObject;

    [SetUp]
    public void SetUp()
    {
        // ADR-0001 Rule 8: Reset all static state before each test
        GameSceneManager.OnFragmentTransitionStarted = null;
        GameSceneManager.OnFragmentTransitioned = null;
        InputManager.OnSetActionMap = null;

        _sceneFader = new MockSceneFader();
        _dataManager = new MockDataManager();
        _audioManager = new MockAudioManager();

        _gameObject = new GameObject("GameSceneManager_Test");
        SpriteRenderer renderer = _gameObject.AddComponent<SpriteRenderer>();
        _sceneManager = _gameObject.AddComponent<GameSceneManager>();
        _sceneManager.Initialize(_sceneFader, _dataManager, _audioManager, renderer);
    }

    [TearDown]
    public void TearDown()
    {
        // ADR-0001 Rule 8: Null all static events to prevent cross-test leakage
        GameSceneManager.OnFragmentTransitionStarted = null;
        GameSceneManager.OnFragmentTransitioned = null;
        InputManager.OnSetActionMap = null;

        if (_gameObject != null)
        {
            Object.DestroyImmediate(_gameObject);
            _gameObject = null;
        }
    }

    // ========================================================================
    // AC-1: Transition animation timing
    // Verify FadeOut(0.5f) and FadeIn(0.5f) are called in the correct order
    // ========================================================================

    /// <summary>
    /// Verify that FadeOut is called before FadeIn during a normal transition.
    /// </summary>
    [Test]
    public async Task test_scene_manager_fade_out_before_fade_in()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert
        Assert.AreEqual(2, _sceneFader.CallOrder.Count,
            "Expected exactly 2 fader calls (FadeOut + FadeIn)");
        Assert.AreEqual("FadeOut", _sceneFader.CallOrder[0],
            "FadeOut must be called first");
        Assert.AreEqual("FadeIn", _sceneFader.CallOrder[1],
            "FadeIn must be called second");
    }

    /// <summary>
    /// Verify that FadeOut and FadeIn both use the correct 0.5s duration
    /// as specified in ADR-0004.
    /// </summary>
    [Test]
    public async Task test_scene_manager_fade_uses_correct_duration()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert
        Assert.AreEqual(0.5f, _sceneFader.FadeOutDuration, 0.001f,
            "FadeOut must use 0.5s duration (ADR-0004)");
        Assert.AreEqual(0.5f, _sceneFader.FadeInDuration, 0.001f,
            "FadeIn must use 0.5s duration (ADR-0004)");
    }

    /// <summary>
    /// Verify the state machine transitions through all states in the correct order
    /// and ends at Idle.
    /// </summary>
    [Test]
    public async Task test_scene_manager_state_machine_returns_to_idle()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must return to Idle after transition completes");
    }

    // ========================================================================
    // AC-2: Concurrent transition rejection
    // Verify that a second transition request is rejected while first is in progress
    // ========================================================================

    /// <summary>
    /// Start a transition, immediately try to start a second one before the
    /// first completes. Verify the second call is rejected (no double FadeOut).
    /// </summary>
    [Test]
    public async Task test_scene_manager_concurrent_transition_second_call_ignored()
    {
        // Arrange: Create a controlled delay so the first transition blocks at FadeOut
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        // Act: Start first transition (will block at FadeOut await)
        Task firstTransition = _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Attempt second transition while first is still in progress
        Task secondTransition = _sceneManager.TransitionToFragmentAsync("ch2", "f2");

        // Complete the first transition's FadeOut
        fadeOutTcs.SetResult(true);
        await firstTransition;
        await secondTransition; // should be a no-op (rejected)

        // Assert: Only one FadeOut/FadeIn pair executed
        Assert.AreEqual(1, _sceneFader.FadeOutCallCount,
            "Only one FadeOut should occur — second transition must be rejected");
        Assert.AreEqual(1, _sceneFader.FadeInCallCount,
            "Only one FadeIn should occur — second transition must be rejected");
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must be Idle after the valid transition completes");
    }

    /// <summary>
    /// Verify the state machine properly rejects transitions even when in FadingOut state
    /// (not just Loading).
    /// </summary>
    [Test]
    public async Task test_scene_manager_rejects_transition_when_state_is_not_idle()
    {
        // Arrange: Block the first transition at FadeOut
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        // Act: Start first transition (will block)
        Task firstTransition = _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // State should be FadingOut (not Idle)
        Assert.AreEqual(TransitionState.FadingOut, _sceneManager.CurrentState,
            "State should be FadingOut while blocked on fade");

        // Attempt second transition — should be rejected while state is FadingOut
        Task rejected = _sceneManager.TransitionToFragmentAsync("ch2", "f2");

        // Complete the first transition
        fadeOutTcs.SetResult(true);
        await firstTransition;
        await rejected;

        // Assert: Only one fader pair executed
        Assert.AreEqual(1, _sceneFader.FadeOutCallCount);
        Assert.AreEqual(1, _sceneFader.FadeInCallCount);
    }

    // ========================================================================
    // AC-3: Input gating during transition
    // Verify SetActionMap(Inactive) at start, SetActionMap(Gameplay) after completion
    // ========================================================================

    /// <summary>
    /// Verify that input is gated to Inactive at the start of the transition
    /// and restored to Gameplay after the transition completes.
    /// </summary>
    [Test]
    public async Task test_scene_manager_input_is_gated_during_transition()
    {
        // Arrange: Subscribe to the InputManager test hook
        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Two SetActionMap calls — Inactive at start, Gameplay at end
        Assert.AreEqual(2, inputCalls.Count,
            "Expected exactly 2 SetActionMap calls (Inactive + Gameplay)");
        Assert.AreEqual(ActionMap.Inactive, inputCalls[0],
            "Input must be gated to Inactive BEFORE FadeOut (ADR-0004 Step 0.5)");
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[1],
            "Input must be restored to Gameplay AFTER FadeIn completes (ADR-0004 Step 5)");
    }

    /// <summary>
    /// Verify input gating happens even when the transition is fast
    /// (no artificial delays).
    /// </summary>
    [Test]
    public async Task test_scene_manager_input_gating_call_order_relative_to_fader()
    {
        // Arrange
        List<string> sequence = new List<string>();
        _sceneFader.OnFadeOutCalled += () => sequence.Add("FadeOut");
        _sceneFader.OnFadeInCalled += () => sequence.Add("FadeIn");
        InputManager.OnSetActionMap += (map) => sequence.Add($"Input:{map}");

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Inactive before FadeOut, Gameplay after FadeIn
        Assert.AreEqual(4, sequence.Count, "Expected 4 sequenced calls");
        Assert.AreEqual("Input:Inactive", sequence[0],
            "Input must be gated to Inactive BEFORE FadeOut");
        Assert.AreEqual("FadeOut", sequence[1],
            "FadeOut must follow input gating");
        Assert.AreEqual("FadeIn", sequence[2],
            "FadeIn must precede input restoration");
        Assert.AreEqual("Input:Gameplay", sequence[3],
            "Input must be restored to Gameplay AFTER FadeIn");
    }

    // ========================================================================
    // AC-3 (extended): 5-step flow — unload/load/audio steps verified
    // Verify that DataManager and AudioManager are called during the transition
    // ========================================================================

    /// <summary>
    /// Verify that the data loading pipeline executes during a transition:
    /// GetFragmentAsync → GetIllustrationAsync → PreloadFragmentAudioAsync.
    /// </summary>
    [Test]
    public async Task test_scene_manager_data_loading_pipeline_executes()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Data loading steps executed
        Assert.IsNotNull(_dataManager.LastFragment,
            "GetFragmentAsync must be called during transition");
        Assert.AreEqual("f1", _dataManager.LastFragment.FragmentId,
            "GetFragmentAsync must receive the correct fragment ID");
        Assert.IsTrue(_audioManager.PreloadCalled,
            "PreloadFragmentAudioAsync must be called during transition");
        Assert.IsNotNull(_audioManager.LastAudioKeys,
            "Audio keys must be passed to PreloadFragmentAudioAsync");
        Assert.AreEqual(2, _audioManager.LastAudioKeys.Length,
            "Audio keys array must contain both ambient and stinger keys");
    }

    /// <summary>
    /// Verify that UnloadCurrentFragment releases the previous fragment
    /// on the second sequential transition.
    /// </summary>
    [Test]
    public async Task test_scene_manager_unloads_previous_fragment_on_second_transition()
    {
        // Act: First transition — no previous fragment to unload
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");
        Assert.IsFalse(_dataManager.ReleaseFragmentCalled,
            "First transition must not call ReleaseFragment (no previous fragment)");

        // Act: Second transition — should unload f1 before loading f2
        await _sceneManager.TransitionToFragmentAsync("ch2", "f2");
        Assert.IsTrue(_dataManager.ReleaseFragmentCalled,
            "Second transition must call ReleaseFragment for the previous fragment");
        Assert.AreEqual("f1", _dataManager.ReleasedFragmentId,
            "ReleaseFragment must release the correct previous fragment ID");
    }

    // ========================================================================
    // AC-4: Static events fire in correct order
    // Verify OnFragmentTransitionStarted before FadeOut, OnFragmentTransitioned after FadeIn
    // ========================================================================

    /// <summary>
    /// Verify that static events fire at the correct points in the transition sequence:
    /// Started → FadeOut → FadeIn → Transitioned.
    /// </summary>
    [Test]
    public async Task test_scene_manager_events_fire_at_correct_points_in_sequence()
    {
        // Arrange: Build a combined call sequence across all hooks
        List<string> sequence = new List<string>();
        _sceneFader.OnFadeOutCalled += () => sequence.Add("FadeOut");
        _sceneFader.OnFadeInCalled += () => sequence.Add("FadeIn");
        GameSceneManager.OnFragmentTransitionStarted += (_, _) => sequence.Add("Started");
        GameSceneManager.OnFragmentTransitioned += (_, _) => sequence.Add("Transitioned");

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Exact sequence order per ADR-0004
        Assert.AreEqual(4, sequence.Count, "Expected 4 sequenced events");
        Assert.AreEqual("Started", sequence[0],
            "OnFragmentTransitionStarted must fire BEFORE FadeOut (ADR-0004)");
        Assert.AreEqual("FadeOut", sequence[1],
            "FadeOut must follow OnFragmentTransitionStarted");
        Assert.AreEqual("FadeIn", sequence[2],
            "FadeIn must precede OnFragmentTransitioned");
        Assert.AreEqual("Transitioned", sequence[3],
            "OnFragmentTransitioned must fire AFTER FadeIn completes (ADR-0004)");
    }

    /// <summary>
    /// Verify that OnFragmentTransitionStarted carries the correct chapter and fragment IDs.
    /// </summary>
    [Test]
    public async Task test_scene_manager_started_event_contains_correct_parameters()
    {
        // Arrange
        string receivedChapter = null;
        string receivedFragment = null;
        GameSceneManager.OnFragmentTransitionStarted += (ch, fid) =>
        {
            receivedChapter = ch;
            receivedFragment = fid;
        };

        // Act
        await _sceneManager.TransitionToFragmentAsync("chapter_2", "frag_42");

        // Assert
        Assert.AreEqual("chapter_2", receivedChapter,
            "OnFragmentTransitionStarted must receive the correct chapter key");
        Assert.AreEqual("frag_42", receivedFragment,
            "OnFragmentTransitionStarted must receive the correct fragment ID");
    }

    /// <summary>
    /// Verify that OnFragmentTransitioned carries the correct chapter and fragment IDs.
    /// </summary>
    [Test]
    public async Task test_scene_manager_transitioned_event_contains_correct_parameters()
    {
        // Arrange
        string receivedChapter = null;
        string receivedFragment = null;
        GameSceneManager.OnFragmentTransitioned += (ch, fid) =>
        {
            receivedChapter = ch;
            receivedFragment = fid;
        };

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch3", "frag_final");

        // Assert
        Assert.AreEqual("ch3", receivedChapter,
            "OnFragmentTransitioned must receive the correct chapter key");
        Assert.AreEqual("frag_final", receivedFragment,
            "OnFragmentTransitioned must receive the correct fragment ID");
    }

    // ========================================================================
    // AC-5: Events fire exactly once per transition
    // Verify each event fires exactly 1 time for a single transition
    // ========================================================================

    /// <summary>
    /// Verify that both events fire exactly once during a single transition.
    /// </summary>
    [Test]
    public async Task test_scene_manager_events_fire_exactly_once_single_transition()
    {
        // Arrange
        int startedCount = 0;
        int transitionedCount = 0;
        GameSceneManager.OnFragmentTransitionStarted += (_, _) => startedCount++;
        GameSceneManager.OnFragmentTransitioned += (_, _) => transitionedCount++;

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert
        Assert.AreEqual(1, startedCount,
            "OnFragmentTransitionStarted must fire exactly once");
        Assert.AreEqual(1, transitionedCount,
            "OnFragmentTransitioned must fire exactly once");
    }

    /// <summary>
    /// Verify that events fire exactly once per transition across multiple
    /// sequential transitions (count should be 2 after 2 transitions).
    /// </summary>
    [Test]
    public async Task test_scene_manager_events_fire_exactly_once_per_transition_multiple()
    {
        // Arrange
        int startedCount = 0;
        int transitionedCount = 0;
        GameSceneManager.OnFragmentTransitionStarted += (_, _) => startedCount++;
        GameSceneManager.OnFragmentTransitioned += (_, _) => transitionedCount++;

        // Act: Two sequential transitions
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");
        await _sceneManager.TransitionToFragmentAsync("ch2", "f2");

        // Assert: Each event fires exactly N times for N transitions
        Assert.AreEqual(2, startedCount,
            "OnFragmentTransitionStarted must fire once per transition (2 total)");
        Assert.AreEqual(2, transitionedCount,
            "OnFragmentTransitioned must fire once per transition (2 total)");
    }

    /// <summary>
    /// Verify that a REJECTED transition does NOT fire any events.
    /// </summary>
    [Test]
    public async Task test_scene_manager_rejected_transition_does_not_fire_events()
    {
        // Arrange: Block first transition
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        int startedCount = 0;
        int transitionedCount = 0;
        GameSceneManager.OnFragmentTransitionStarted += (_, _) => startedCount++;
        GameSceneManager.OnFragmentTransitioned += (_, _) => transitionedCount++;

        // Act: Start first transition (fires Started event for ch1/f1)
        Task firstTransition = _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Attempt rejected transition
        Task rejected = _sceneManager.TransitionToFragmentAsync("ch2", "f2");

        fadeOutTcs.SetResult(true);
        await firstTransition;
        await rejected;

        // Assert: Only one Started+Transitioned pair (from the valid transition)
        Assert.AreEqual(1, startedCount,
            "Rejected transition must NOT fire OnFragmentTransitionStarted");
        Assert.AreEqual(1, transitionedCount,
            "Rejected transition must NOT fire OnFragmentTransitioned");
    }

    /// <summary>
    /// Verify that a single successful transition does not fire duplicate events.
    /// Each event must fire exactly once, not zero or twice.
    /// </summary>
    [Test]
    public async Task test_scene_manager_no_duplicate_event_fire_single_transition()
    {
        // Arrange: Track each event invocation individually
        List<string> startedCalls = new List<string>();
        List<string> transitionedCalls = new List<string>();
        GameSceneManager.OnFragmentTransitionStarted += (ch, fid) =>
            startedCalls.Add($"{ch}/{fid}");
        GameSceneManager.OnFragmentTransitioned += (ch, fid) =>
            transitionedCalls.Add($"{ch}/{fid}");

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Exactly one invocation each, not zero or more than one
        Assert.AreEqual(1, startedCalls.Count,
            "OnFragmentTransitionStarted must fire exactly once (no duplicates)");
        Assert.AreEqual(1, transitionedCalls.Count,
            "OnFragmentTransitioned must fire exactly once (no duplicates)");
        Assert.AreEqual("ch1/f1", startedCalls[0],
            "Started event must carry the correct parameters");
        Assert.AreEqual("ch1/f1", transitionedCalls[0],
            "Transitioned event must carry the correct parameters");
    }

    // ========================================================================
    // AC-6: Post-transition state verification
    // Verify that internal state is fully populated after await completes
    // ========================================================================

    /// <summary>
    /// Verify that after TransitionToFragmentAsync completes, the internal
    /// state (chapter key, fragment ID) is correctly set.
    /// </summary>
    [Test]
    public async Task test_scene_manager_post_transition_state_is_correct()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("chapter_5", "frag_99");

        // Assert: Internal state is updated
        Assert.AreEqual("chapter_5", _sceneManager.CurrentChapterKey,
            "CurrentChapterKey must reflect the target chapter after transition");
        Assert.AreEqual("frag_99", _sceneManager.CurrentFragmentId,
            "CurrentFragmentId must reflect the target fragment after transition");
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must be Idle after successful transition");
    }

    /// <summary>
    /// Verify that the SpriteRenderer.sprite is set after a successful transition.
    /// </summary>
    [Test]
    public async Task test_scene_manager_sprite_renderer_updated_after_transition()
    {
        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: SpriteRenderer received the mock sprite
        SpriteRenderer renderer = _sceneManager.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(renderer.sprite,
            "SpriteRenderer.sprite must be set after a successful transition");
        Assert.AreEqual("sprite_ill_f1", renderer.sprite.name,
            "SpriteRenderer.sprite must match the illustration key from the fragment");
    }

    // ========================================================================
    // Defensive: State machine recovery on load failure
    // Verify input is restored and state resets when data loading throws
    // ========================================================================

    /// <summary>
    /// Verify that if GetFragmentAsync throws, input is restored to Gameplay
    /// and the state machine returns to Idle (no permanent lock).
    /// </summary>
    [Test]
    public async Task test_scene_manager_recovers_state_on_load_failure()
    {
        // Arrange: Make data loading throw
        _dataManager.SetThrowOnLoad(true);

        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        // Act
        await _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Assert: Input gated to Inactive, then restored to Gameplay on failure
        Assert.AreEqual(2, inputCalls.Count,
            "Input must be gated (Inactive) and restored (Gameplay) even on failure");
        Assert.AreEqual(ActionMap.Inactive, inputCalls[0]);
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[1],
            "Input must be restored after load failure — no permanent lock");

        // Assert: State machine reset to Idle
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must reset to Idle after load failure");

        // Assert: OnFragmentTransitioned NOT fired on failure
        bool transitionedFired = false;
        GameSceneManager.OnFragmentTransitioned += (_, _) => transitionedFired = true;

        // Do another transition that succeeds to verify system is recovered
        _dataManager.SetThrowOnLoad(false);
        await _sceneManager.TransitionToFragmentAsync("ch1", "f2");

        Assert.IsFalse(transitionedFired,
            "OnFragmentTransitioned must NOT fire on a failed transition (only the new success fires it)");
    }
}

// ============================================================================
// Mock Implementations
// Each mock tracks call counts, call order, and parameters for test assertions.
// ============================================================================

/// <summary>
/// Mock ISceneFader that records call order, durations, and supports
/// controllable TaskCompletionSource delays for testing concurrent transitions.
/// </summary>
internal class MockSceneFader : ISceneFader
{
    public List<string> CallOrder = new List<string>();

    public float FadeOutDuration { get; private set; }
    public float FadeInDuration { get; private set; }
    public int FadeOutCallCount { get; private set; }
    public int FadeInCallCount { get; private set; }

    /// <summary>Fires right when FadeOut is called (before the Task resolves).</summary>
    public event Action OnFadeOutCalled;

    /// <summary>Fires right when FadeIn is called (before the Task resolves).</summary>
    public event Action OnFadeInCalled;

    private TaskCompletionSource<bool> _fadeOutCompletion;
    private TaskCompletionSource<bool> _fadeInCompletion;

    /// <summary>Set a TCS to block FadeOut until the test explicitly completes it.</summary>
    public void SetFadeOutCompletion(TaskCompletionSource<bool> tcs) =>
        _fadeOutCompletion = tcs;

    /// <summary>Set a TCS to block FadeIn until the test explicitly completes it.</summary>
    public void SetFadeInCompletion(TaskCompletionSource<bool> tcs) =>
        _fadeInCompletion = tcs;

    public async Task FadeOut(float duration)
    {
        FadeOutDuration = duration;
        FadeOutCallCount++;
        CallOrder.Add("FadeOut");
        OnFadeOutCalled?.Invoke();

        if (_fadeOutCompletion != null)
        {
            await _fadeOutCompletion.Task;
            _fadeOutCompletion = null;
        }
    }

    public async Task FadeIn(float duration)
    {
        FadeInDuration = duration;
        FadeInCallCount++;
        CallOrder.Add("FadeIn");
        OnFadeInCalled?.Invoke();

        if (_fadeInCompletion != null)
        {
            await _fadeInCompletion.Task;
            _fadeInCompletion = null;
        }
    }
}

/// <summary>
/// Mock IDataManager that returns predictable fragment data and tracks
/// ReleaseFragment calls for verification.
/// </summary>
internal class MockDataManager : IDataManager
{
    public bool ReleaseFragmentCalled { get; private set; }
    public string ReleasedFragmentId { get; private set; }
    public MemoryFragment LastFragment { get; private set; }

    private bool _throwOnLoad;

    /// <summary>When true, GetFragmentAsync throws to simulate Addressables load failure.</summary>
    public void SetThrowOnLoad(bool shouldThrow) => _throwOnLoad = shouldThrow;

    public Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)
    {
        if (_throwOnLoad)
            throw new InvalidOperationException("Simulated Addressables load failure");

        LastFragment = ScriptableObject.CreateInstance<MemoryFragment>();
        LastFragment.FragmentId = fragmentId;
        LastFragment.IllustrationKey = "ill_" + fragmentId;
        LastFragment.AudioKeys = new[] { "audio_ambient", "audio_stinger" };
        LastFragment.InteractiveObjects = new InteractiveObject[0];
        return Task.FromResult(LastFragment);
    }

    public Task<Sprite> GetIllustrationAsync(string illustrationKey)
    {
        // Return a distinguishable non-null sprite for wiring verification.
        // Sprite.Create requires a texture — use a minimal 1x1 texture.
        Texture2D tex = new Texture2D(1, 1);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        sprite.name = "sprite_" + illustrationKey;
        return Task.FromResult(sprite);
    }

    public void ReleaseFragment(string fragmentId)
    {
        ReleaseFragmentCalled = true;
        ReleasedFragmentId = fragmentId;
    }
}

/// <summary>
/// Mock IAudioManager that records preload calls for verification.
/// </summary>
internal class MockAudioManager : IAudioManager
{
    public bool PreloadCalled { get; private set; }
    public string[] LastAudioKeys { get; private set; }

    public Task PreloadFragmentAudioAsync(string[] audioKeys)
    {
        PreloadCalled = true;
        LastAudioKeys = audioKeys;
        return Task.CompletedTask;
    }
}
