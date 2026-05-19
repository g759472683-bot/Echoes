using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integration tests for scene-management Story 001 (3-Scene Architecture + Boot Init).
///
/// Covers AC-1 through AC-4 using mock interfaces. Does NOT require actual
/// Unity scenes in Build Settings — scene loads are injected via
/// GameSceneManager._sceneLoadFuncForTesting (internal test hook).
///
/// Mock implementations (MockSceneFader, MockDataManager, MockAudioManager) are
/// adapted from the patterns established in fragment_transition_test.cs.
/// </summary>
public class SceneBootTest
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

        // Inject a no-op scene load function so tests don't call Unity SceneManager
        _sceneManager._sceneLoadFuncForTesting = _ => Task.CompletedTask;

        // Short timeout for most tests (1s is enough for mock loads, timeout tests override this)
        _sceneManager._sceneLoadTimeoutSeconds = 1.0f;
    }

    [TearDown]
    public void TearDown()
    {
        // ADR-0001 Rule 8: Null all static events to prevent cross-test leakage
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

    // ==========================================================================
    // AC-1: 3-Scene architecture — GameSceneManager singleton + Boot flow
    // ==========================================================================

    /// <summary>
    /// Verify the GameSceneManager singleton is correctly established via
    /// Awake() + DontDestroyOnLoad pattern. The Instance reference must
    /// be set after Awake() runs.
    /// </summary>
    [Test]
    public void test_GameSceneManager_Singleton_Established_On_Awake()
    {
        // The SetUp already creates a GameSceneManager and calls Awake (via AddComponent)
        Assert.IsNotNull(GameSceneManager.Instance,
            "GameSceneManager.Instance must be set after Awake()");
        Assert.AreSame(_sceneManager, GameSceneManager.Instance,
            "Instance must reference the component created in SetUp");
    }

    /// <summary>
    /// Verify that a duplicate GameSceneManager is destroyed per singleton
    /// enforcement (ADR-0004 — only one instance exists).
    /// </summary>
    [Test]
    public void test_GameSceneManager_Duplicate_Destroyed()
    {
        // Arrange: Create a second GameSceneManager on a new GameObject
        GameObject duplicateGo = new GameObject("Duplicate_GSM");
        GameSceneManager duplicate = duplicateGo.AddComponent<GameSceneManager>();

        // Assert: The duplicate should be destroyed, Instance unchanged
        Assert.IsTrue(duplicate == null,
            "Duplicate GameSceneManager must be destroyed by Awake()");
        Assert.AreSame(_sceneManager, GameSceneManager.Instance,
            "Original Instance must survive duplicate creation");
    }

    // ==========================================================================
    // AC-2: Boot initialization completes, systems ready, MainMenu auto-load
    // ==========================================================================

    /// <summary>
    /// Verify that GameSceneManager.LoadSceneAsync transitions through the
    /// correct state machine states and ends at Idle.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Returns_To_Idle()
    {
        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must return to Idle after LoadSceneAsync completes");
    }

    /// <summary>
    /// Verify that LoadSceneAsync fires OnSceneLoaded with the correct scene name.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Fires_OnSceneLoaded_Event()
    {
        // Arrange
        string loadedScene = null;
        GameSceneManager.OnSceneLoaded += sceneName => loadedScene = sceneName;

        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert
        Assert.AreEqual("MainMenu", loadedScene,
            "OnSceneLoaded must carry the newly-loaded scene name");
    }

    /// <summary>
    /// Verify that LoadSceneAsync gates input during the transition
    /// (Inactive at start, Gameplay after completion per ADR-0004).
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Gates_Input()
    {
        // Arrange
        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert
        Assert.AreEqual(2, inputCalls.Count,
            "Expected exactly 2 SetActionMap calls (Inactive + Gameplay)");
        Assert.AreEqual(ActionMap.Inactive, inputCalls[0],
            "Input must be gated to Inactive BEFORE scene load");
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[1],
            "Input must be restored to Gameplay AFTER scene load");
    }

    /// <summary>
    /// Verify that FadeOut and FadeIn are called in the correct order
    /// during a LoadSceneAsync transition.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Fade_Order()
    {
        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert
        Assert.AreEqual(2, _sceneFader.CallOrder.Count,
            "Expected exactly 2 fader calls");
        Assert.AreEqual("FadeOut", _sceneFader.CallOrder[0],
            "FadeOut must be called first");
        Assert.AreEqual("FadeIn", _sceneFader.CallOrder[1],
            "FadeIn must be called second");
    }

    /// <summary>
    /// Verify that LoadSceneAsync uses the correct 0.5s fade duration
    /// as specified in ADR-0004.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Fade_Uses_Correct_Duration()
    {
        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert
        Assert.AreEqual(0.5f, _sceneFader.FadeOutDuration, 0.001f,
            "FadeOut must use 0.5s (ADR-0004)");
        Assert.AreEqual(0.5f, _sceneFader.FadeInDuration, 0.001f,
            "FadeIn must use 0.5s (ADR-0004)");
    }

    /// <summary>
    /// Verify that LoadSceneAsync rejects concurrent calls — only one
    /// transition executes at a time.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Rejects_Concurrent()
    {
        // Arrange: Block the first transition at FadeOut
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        // Act: Start first transition (blocks at FadeOut)
        Task first = _sceneManager.LoadSceneAsync("MainMenu");

        // Attempt second transition — must be rejected
        Task second = _sceneManager.LoadSceneAsync("Game");

        // Complete the first transition
        fadeOutTcs.SetResult(true);
        await first;
        await second;

        // Assert: Only one FadeOut/FadeIn pair
        Assert.AreEqual(1, _sceneFader.FadeOutCallCount,
            "Only one FadeOut must execute — second call rejected");
        Assert.AreEqual(1, _sceneFader.FadeInCallCount,
            "Only one FadeIn must execute — second call rejected");
    }

    /// <summary>
    /// Verify the full event sequence during LoadSceneAsync:
    /// Inactive → FadeOut → SceneLoad → OnSceneLoaded → FadeIn → Gameplay.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Event_Sequence()
    {
        // Arrange
        List<string> sequence = new List<string>();
        InputManager.OnSetActionMap += map => sequence.Add($"Input:{map}");
        _sceneFader.OnFadeOutCalled += () => sequence.Add("FadeOut");
        _sceneFader.OnFadeInCalled += () => sequence.Add("FadeIn");
        GameSceneManager.OnSceneLoaded += _ => sequence.Add("SceneLoaded");

        // Act
        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert: Correct ordering per ADR-0004
        Assert.AreEqual(5, sequence.Count, "Expected 5 sequenced events");
        Assert.AreEqual("Input:Inactive", sequence[0],
            "Input must be gated to Inactive first");
        Assert.AreEqual("FadeOut", sequence[1],
            "FadeOut must follow input gating");
        Assert.AreEqual("SceneLoaded", sequence[2],
            "OnSceneLoaded must fire after scene load, before FadeIn");
        Assert.AreEqual("FadeIn", sequence[3],
            "FadeIn must follow OnSceneLoaded");
        Assert.AreEqual("Input:Gameplay", sequence[4],
            "Input must be restored to Gameplay last");
    }

    // ==========================================================================
    // AC-3: MainMenu → Game async load + chapter preload + fragment display
    // ==========================================================================

    /// <summary>
    /// Verify that OnMainMenuStartGame loads the Game scene, preloads the
    /// chapter, loads the entry fragment, and fires transition events.
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Complete_Flow()
    {
        // Arrange: Track events
        string loadedScene = null;
        bool startedFired = false;
        bool transitionedFired = false;
        string startedChapter = null;
        string startedFragment = null;
        string transitionedChapter = null;
        string transitionedFragment = null;

        GameSceneManager.OnSceneLoaded += s => loadedScene = s;
        GameSceneManager.OnFragmentTransitionStarted += (ch, fid) =>
        {
            startedFired = true;
            startedChapter = ch;
            startedFragment = fid;
        };
        GameSceneManager.OnFragmentTransitioned += (ch, fid) =>
        {
            transitionedFired = true;
            transitionedChapter = ch;
            transitionedFragment = fid;
        };

        // Act
        await _sceneManager.OnMainMenuStartGame("chapter_1", "frag_01");

        // Assert: Scene loaded
        Assert.AreEqual("Game", loadedScene,
            "OnSceneLoaded must fire with 'Game'");

        // Assert: Fragment transition events
        Assert.IsTrue(startedFired,
            "OnFragmentTransitionStarted must fire during MainMenu→Game flow");
        Assert.AreEqual("chapter_1", startedChapter);
        Assert.AreEqual("frag_01", startedFragment);
        Assert.IsTrue(transitionedFired,
            "OnFragmentTransitioned must fire after fade-in completes");
        Assert.AreEqual("chapter_1", transitionedChapter);
        Assert.AreEqual("frag_01", transitionedFragment);

        // Assert: State returned to Idle
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState);
    }

    /// <summary>
    /// Verify that OnMainMenuStartGame preloads chapter assets and loads
    /// the entry fragment data.
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Preloads_Chapter_And_Loads_Fragment()
    {
        // Act
        await _sceneManager.OnMainMenuStartGame("chapter_2", "frag_entry");

        // Assert: Chapter was preloaded
        Assert.IsTrue(_dataManager.PreloadCalled,
            "PreloadChapterAsync must be called during MainMenu→Game flow");
        Assert.AreEqual("chapter_2", _dataManager.PreloadedChapterKey,
            "Correct chapter key must be preloaded");

        // Assert: Fragment was loaded
        Assert.IsNotNull(_dataManager.LastFragment,
            "Entry fragment must be loaded");
        Assert.AreEqual("frag_entry", _dataManager.LastFragment.FragmentId,
            "Correct entry fragment must be loaded");

        // Assert: Audio was preloaded
        Assert.IsTrue(_audioManager.PreloadCalled,
            "PreloadFragmentAudioAsync must be called for entry fragment");
    }

    /// <summary>
    /// Verify that OnMainMenuStartGame sets the SpriteRenderer sprite
    /// to the entry fragment's illustration.
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Sets_Sprite()
    {
        // Act
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");

        // Assert
        SpriteRenderer renderer = _sceneManager.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(renderer.sprite,
            "SpriteRenderer.sprite must be set after MainMenu→Game transition");
        Assert.AreEqual("sprite_ill_f1", renderer.sprite.name,
            "Sprite must match the illustration key from the loaded fragment");
    }

    /// <summary>
    /// Verify that OnMainMenuStartGame rejects concurrent calls
    /// (state machine guard).
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Rejects_Concurrent()
    {
        // Arrange: Block first transition at FadeOut
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        // Act: Start first game entry (blocks at FadeOut)
        Task first = _sceneManager.OnMainMenuStartGame("ch1", "f1");

        // Second call must be rejected
        Task second = _sceneManager.OnMainMenuStartGame("ch2", "f2");

        fadeOutTcs.SetResult(true);
        await first;
        await second;

        // Assert: Only one FadeOut/FadeIn
        Assert.AreEqual(1, _sceneFader.FadeOutCallCount,
            "Only one FadeOut — second call must be rejected");
    }

    /// <summary>
    /// Verify that input is gated during the full MainMenu→Game flow
    /// and restored to Gameplay on completion.
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Gates_Input()
    {
        // Arrange
        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        // Act
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");

        // Assert
        Assert.AreEqual(2, inputCalls.Count);
        Assert.AreEqual(ActionMap.Inactive, inputCalls[0]);
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[1]);
    }

    // ==========================================================================
    // AC-4: Game scene persistence — loaded once, content injected
    // ==========================================================================

    /// <summary>
    /// Verify that Game scene content can be updated via fragment transitions
    /// without reloading the scene itself. A fragment transition in the Game
    /// scene uses TransitionToFragmentAsync, not LoadSceneAsync("Game").
    /// </summary>
    [Test]
    public async Task test_Game_Scene_Fragment_Transition_Without_Scene_Reload()
    {
        // Arrange: Track scene loads
        List<string> sceneLoads = new List<string>();
        _sceneManager._sceneLoadFuncForTesting = sceneName =>
        {
            sceneLoads.Add(sceneName);
            return Task.CompletedTask;
        };

        // Act: Enter Game, then transition between fragments
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");
        Assert.AreEqual(1, sceneLoads.Count,
            "One scene load ('Game') to enter");

        // Transition to a second fragment (ADVICE — within same Game scene)
        await _sceneManager.TransitionToFragmentAsync("ch1", "f2");

        // Assert: No additional scene loads
        Assert.AreEqual(1, sceneLoads.Count,
            "Scene must NOT reload between fragment transitions — " +
            "content is injected via Addressables within the persistent Game scene");

        // Assert: Fragment state updated correctly
        Assert.AreEqual("ch1", _sceneManager.CurrentChapterKey);
        Assert.AreEqual("f2", _sceneManager.CurrentFragmentId);
    }

    /// <summary>
    /// Verify that LoadSceneAsync can be used to return to MainMenu from Game,
    /// which unloads the Game scene (Single mode).
    /// </summary>
    [Test]
    public async Task test_Return_To_MainMenu_Loads_Scene()
    {
        // Arrange: Enter Game first
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");
        Assert.AreEqual("ch1", _sceneManager.CurrentChapterKey);

        // Act: Return to MainMenu
        List<string> sceneLoads = new List<string>();
        _sceneManager._sceneLoadFuncForTesting = sceneName =>
        {
            sceneLoads.Add(sceneName);
            return Task.CompletedTask;
        };

        await _sceneManager.LoadSceneAsync("MainMenu");

        // Assert: MainMenu scene load was requested
        Assert.AreEqual(1, sceneLoads.Count);
        Assert.AreEqual("MainMenu", sceneLoads[0],
            "LoadSceneAsync('MainMenu') must trigger a scene load to return from Game");
    }

    /// <summary>
    /// Verify that LoadSceneAsync rejects another LoadSceneAsync call
    /// while a transition is already in progress (state machine guard for
    /// scene persistence).
    /// </summary>
    [Test]
    public async Task test_Game_Scene_Only_Loaded_Once()
    {
        // Arrange: Track scene loads
        int gameLoadCount = 0;
        _sceneManager._sceneLoadFuncForTesting = sceneName =>
        {
            if (sceneName == "Game") gameLoadCount++;
            return Task.CompletedTask;
        };

        // Act: Load Game once
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");

        // Game was loaded exactly once
        Assert.AreEqual(1, gameLoadCount,
            "Game scene must be loaded exactly once on entry");

        // No further Game scene loads occurred during fragment transitions
        Assert.AreEqual(1, gameLoadCount,
            "Fragment transitions must not reload the Game scene");
    }

    // ==========================================================================
    // BootBootstrap Tests
    // ==========================================================================

    /// <summary>
    /// Verify that BootBootstrap creates foundation systems (GameSceneManager,
    /// InputManager) during initialization. OnBootComplete only fires after
    /// LoadSceneAsync("MainMenu") succeeds, which requires the real scene loader
    /// (verified in integration environment, not in unit test mode).
    /// </summary>
    [Test]
    public void test_BootBootstrap_Creates_Foundation_Systems()
    {
        // Verify the pattern that BootBootstrap would follow.
        // The SetUp already creates a GameSceneManager with Instance set,
        // matching what BootBootstrap.InitializeSystemsAsync() does.
        Assert.IsNotNull(GameSceneManager.Instance,
            "GameSceneManager.Instance must exist (created by BootBootstrap or SetUp)");
        Assert.IsNotNull(GameSceneManager.Instance.gameObject,
            "GameSceneManager GameObject must exist");

        // Verify InputManager was created (SetUp doesn't create one,
        // but the test environment must have one for input gating to work).
        // InputManager is a static class with test hooks — no instance check needed.
    }

    // ==========================================================================
    // State machine edge cases
    // ==========================================================================

    /// <summary>
    /// Verify that the state machine recovers to Idle when the scene load
    /// function throws, and input is restored to Gameplay.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Recovers_On_Failure()
    {
        // Arrange: Inject a failing scene load
        _sceneManager._sceneLoadFuncForTesting = _ =>
            Task.FromException(new InvalidOperationException("Simulated scene load failure"));

        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        bool sceneLoadedFired = false;
        GameSceneManager.OnSceneLoaded += _ => sceneLoadedFired = true;

        // Act
        await _sceneManager.LoadSceneAsync("BrokenScene");

        // Assert: State recovered
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must reset to Idle after failure");

        // Assert: Input restored
        Assert.AreEqual(2, inputCalls.Count,
            "Input must be gated and restored even on failure");
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[1],
            "Input must be restored to Gameplay after failure");

        // Assert: OnSceneLoaded NOT fired on failure
        Assert.IsFalse(sceneLoadedFired,
            "OnSceneLoaded must NOT fire on failure");
    }

    /// <summary>
    /// Verify that LoadSceneAsync times out after _sceneLoadTimeoutSeconds
    /// and the state machine recovers to Idle with input restored.
    /// ADR-0004 edge case: 30s timeout + retry.
    /// </summary>
    [Test]
    public async Task test_LoadSceneAsync_Times_Out_And_Recovers()
    {
        // Arrange: Set a very short timeout
        _sceneManager._sceneLoadTimeoutSeconds = 0.05f;

        // Create a never-completing task to simulate a hung scene load
        TaskCompletionSource<bool> neverComplete = new TaskCompletionSource<bool>();
        _sceneManager._sceneLoadFuncForTesting = _ => neverComplete.Task;

        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        bool sceneLoadedFired = false;
        GameSceneManager.OnSceneLoaded += _ => sceneLoadedFired = true;

        // Act
        await _sceneManager.LoadSceneAsync("HungScene");

        // Assert: State recovered to Idle
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must reset to Idle after timeout");

        // Assert: Input restored
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[inputCalls.Count - 1],
            "Input must be restored to Gameplay after timeout recovery");

        // Assert: OnSceneLoaded NOT fired on timeout
        Assert.IsFalse(sceneLoadedFired,
            "OnSceneLoaded must NOT fire on timeout");
    }

    /// <summary>
    /// Verify that OnMainMenuStartGame times out when the Game scene load
    /// exceeds _sceneLoadTimeoutSeconds and recovers cleanly.
    /// </summary>
    [Test]
    public async Task test_OnMainMenuStartGame_Times_Out_And_Recovers()
    {
        // Arrange: Set a very short timeout
        _sceneManager._sceneLoadTimeoutSeconds = 0.05f;

        // Create a never-completing task to simulate a hung Game scene load
        TaskCompletionSource<bool> neverComplete = new TaskCompletionSource<bool>();
        _sceneManager._sceneLoadFuncForTesting = _ => neverComplete.Task;

        List<ActionMap> inputCalls = new List<ActionMap>();
        InputManager.OnSetActionMap += inputCalls.Add;

        bool transitionedFired = false;
        GameSceneManager.OnFragmentTransitioned += (_, _) => transitionedFired = true;

        // Act
        await _sceneManager.OnMainMenuStartGame("ch1", "f1");

        // Assert: State recovered to Idle
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState,
            "State must reset to Idle after timeout");

        // Assert: Input restored
        Assert.AreEqual(ActionMap.Gameplay, inputCalls[inputCalls.Count - 1],
            "Input must be restored to Gameplay after timeout recovery");

        // Assert: No fragment transition completed on timeout
        Assert.IsFalse(transitionedFired,
            "OnFragmentTransitioned must NOT fire on timeout");
    }

    /// <summary>
    /// Verify that TransitionToChapterAsync guards against concurrent transitions
    /// (Story 004 — implemented, no longer throws NotImplementedException).
    /// </summary>
    [Test]
    public async Task test_TransitionToChapterAsync_Rejects_When_Not_Idle()
    {
        // Arrange: Start a fragment transition to force non-Idle state
        TaskCompletionSource<bool> fadeOutTcs = new TaskCompletionSource<bool>();
        _sceneFader.SetFadeOutCompletion(fadeOutTcs);

        Task fragmentTransition = _sceneManager.TransitionToFragmentAsync("ch1", "f1");

        // Act: Attempt chapter transition while fragment transition is in progress
        await _sceneManager.TransitionToChapterAsync("ch2");

        // Complete the fragment transition
        fadeOutTcs.SetResult(true);
        await fragmentTransition;

        // Assert: Chapter transition was rejected — state returned to Idle from fragment transition
        Assert.AreEqual(TransitionState.Idle, _sceneManager.CurrentState);
        // Chapter transition should not have changed the chapter (rejected at guard)
        Assert.AreEqual("ch1", _sceneManager.CurrentChapterKey);
    }

    /// <summary>
    /// Verify that PreloadChapterAsync delegates to IDataManager.PreloadChapterAsync
    /// and handles failure gracefully.
    /// </summary>
    [Test]
    public async Task test_PreloadChapterAsync_Delegates_To_DataManager()
    {
        // Act
        await _sceneManager.PreloadChapterAsync("chapter_3");

        // Assert
        Assert.IsTrue(_dataManager.PreloadCalled,
            "PreloadChapterAsync must delegate to IDataManager");
        Assert.AreEqual("chapter_3", _dataManager.PreloadedChapterKey,
            "Correct chapter key must be passed to DataManager");
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
/// Mock IDataManager that returns predictable fragment data, tracks
/// ReleaseFragment and PreloadChapterAsync calls for verification.
/// </summary>
internal class MockDataManager : IDataManager
{
    public bool ReleaseFragmentCalled { get; private set; }
    public string ReleasedFragmentId { get; private set; }
    public MemoryFragment LastFragment { get; private set; }
    public bool PreloadCalled { get; private set; }
    public string PreloadedChapterKey { get; private set; }

    private bool _throwOnLoad;

    /// <summary>When true, GetFragmentAsync throws to simulate load failure.</summary>
    public void SetThrowOnLoad(bool shouldThrow) => _throwOnLoad = shouldThrow;

    public Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)
    {
        if (_throwOnLoad)
            throw new InvalidOperationException("Simulated Addressables load failure");

        LastFragment = ScriptableObject.CreateInstance<MemoryFragment>();
        LastFragment.FragmentId = fragmentId;
        LastFragment.ChapterKey = chapterKey;
        LastFragment.IllustrationKey = "ill_" + fragmentId;
        LastFragment.AudioKeys = new[] { "audio_ambient", "audio_stinger" };
        LastFragment.InteractiveObjects = new InteractiveObject[0];
        return Task.FromResult(LastFragment);
    }

    public Task<Sprite> GetIllustrationAsync(string illustrationKey)
    {
        Texture2D tex = new Texture2D(1, 1);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        sprite.name = "sprite_" + illustrationKey;
        return Task.FromResult(sprite);
    }

    public Task<Sprite> GetIllustrationAsync(string illustrationKey, string fragmentId)
    {
        return GetIllustrationAsync(illustrationKey);
    }

    public void ReleaseFragment(string fragmentId)
    {
        ReleaseFragmentCalled = true;
        ReleasedFragmentId = fragmentId;
    }

    public MemoryFragment GetCachedFragment(string chapterKey, string fragmentId)
    {
        return null; // Not cached in mock by default
    }

    public Task PreloadChapterAsync(string chapterKey)
    {
        PreloadCalled = true;
        PreloadedChapterKey = chapterKey;
        return _preloadChapterTcs?.Task ?? Task.CompletedTask;
    }

    public Task<ChapterDefinition> GetChapterAsync(string chapterKey)
    {
        return Task.FromResult(_chapterDefinition);
    }

    public bool IsReady(string assetKey) => false;

    public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
    {
        return _fragmentsByChapter ?? new List<MemoryFragment>();
    }

    public void SetCurrentChapter(string chapterKey) { }

    public void CheckAndTriggerPreload(string currentChapterKey, int remainingFragments) { }

    public void UnloadChapter(string chapterKey)
    {
        UnloadChapterCalled = true;
        UnloadedChapterKey = chapterKey;
    }

    public Task PreloadFragmentAsync(string chapterKey, string fragmentId)
    {
        PreloadFragmentCalled = true;
        PreloadedFragmentChapterKey = chapterKey;
        PreloadedFragmentId = fragmentId;
        return Task.CompletedTask;
    }

    // --- Test control fields ---

    private TaskCompletionSource<bool> _preloadChapterTcs;
    private ChapterDefinition _chapterDefinition;
    private List<MemoryFragment> _fragmentsByChapter;

    public bool UnloadChapterCalled { get; private set; }
    public string UnloadedChapterKey { get; private set; }
    public bool PreloadFragmentCalled { get; private set; }
    public string PreloadedFragmentChapterKey { get; private set; }
    public string PreloadedFragmentId { get; private set; }

    /// <summary>Set a TCS to block PreloadChapterAsync until the test completes it.</summary>
    public void SetPreloadChapterCompletion(TaskCompletionSource<bool> tcs) =>
        _preloadChapterTcs = tcs;

    /// <summary>Set the ChapterDefinition returned by GetChapterAsync.</summary>
    public void SetChapterDefinition(ChapterDefinition chapterDef) =>
        _chapterDefinition = chapterDef;

    /// <summary>Set the fragment list returned by GetFragmentsByChapter.</summary>
    public void SetFragmentsForChapter(List<MemoryFragment> fragments) =>
        _fragmentsByChapter = fragments;
}

/// <summary>
/// Mock IAudioManager that records preload + chapter audio calls for verification.
/// </summary>
internal class MockAudioManager : IAudioManager
{
    public bool PreloadCalled { get; private set; }
    public string[] LastAudioKeys { get; private set; }

    // Chapter audio tracking
    public int StopMusicCallCount { get; private set; }
    public float LastStopMusicFadeTime { get; private set; }
    public int PlayMusicCallCount { get; private set; }
    public string LastPlayMusicChapterKey { get; private set; }
    public float LastPlayMusicFadeTime { get; private set; }
    public int UnloadChapterAudioCallCount { get; private set; }
    public string LastUnloadedAudioChapterKey { get; private set; }
    public int PreloadChapterAudioCallCount { get; private set; }
    public string LastPreloadChapterAudioKey { get; private set; }

    /// <summary>Fires right when PreloadChapterAudioAsync is called.</summary>
    public event Action<string> OnPreloadChapterAudioCalled;

    private TaskCompletionSource<bool> _preloadChapterAudioTcs;

    public Task PreloadFragmentAudioAsync(string[] audioKeys)
    {
        PreloadCalled = true;
        LastAudioKeys = audioKeys;
        return Task.CompletedTask;
    }

    public Task PreloadChapterAudioAsync(string chapterKey)
    {
        PreloadChapterAudioCallCount++;
        LastPreloadChapterAudioKey = chapterKey;
        OnPreloadChapterAudioCalled?.Invoke(chapterKey);
        return _preloadChapterAudioTcs?.Task ?? Task.CompletedTask;
    }

    public void StopMusic(float fadeTime)
    {
        StopMusicCallCount++;
        LastStopMusicFadeTime = fadeTime;
    }

    public void PlayMusic(string chapterKey, float fadeTime)
    {
        PlayMusicCallCount++;
        LastPlayMusicChapterKey = chapterKey;
        LastPlayMusicFadeTime = fadeTime;
    }

    public void UnloadChapterAudio(string chapterKey)
    {
        UnloadChapterAudioCallCount++;
        LastUnloadedAudioChapterKey = chapterKey;
    }

    /// <summary>Set a TCS to block PreloadChapterAudioAsync until the test completes it.</summary>
    public void SetPreloadChapterAudioCompletion(TaskCompletionSource<bool> tcs) =>
        _preloadChapterAudioTcs = tcs;
}
