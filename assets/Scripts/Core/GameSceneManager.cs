using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Threading.Tasks;

/// <summary>
/// Core scene transition manager implementing the ADR-0004 fragment transition state machine.
///
/// Events declared here (ADR-0001 pattern):
///   - OnFragmentTransitionStarted(chapterKey, fragmentId) — fires at FadingOut start
///   - OnFragmentTransitioned(chapterKey, fragmentId) — fires after FadeIn completes
///
/// State machine guards prevent concurrent transitions (rejected with Debug.LogWarning).
/// Input is gated via InputManager.SwitchToInactive() during the entire transition sequence.
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance. Set in Awake() via DontDestroyOnLoad.
    /// Null before the Boot scene creates this object.
    /// </summary>
    public static GameSceneManager Instance { get; private set; }

    // Dependencies — set via Inspector (serialized) or constructor injection (Initialize)
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private ISceneFader _sceneFader;
    private IDataManager _dataManager;
    private IAudioManager _audioManager;

    private TransitionState _currentState = TransitionState.Idle;
    private string _currentChapterKey;
    private string _currentFragmentId;

    // Chapter preload state (Story 004)
    private Task _chapterPreloadTask;
    private bool _preloadTriggeredThisChapter;
    private string _preloadedChapterKey;
    private const int PreloadThreshold = 3;

    // Error recovery state (Story 005)
    private VisualElement _errorPanel;
    private string _lastFailedChapterKey;
    private string _lastFailedFragmentChapterKey;
    private string _lastFailedFragmentId;

    /// <summary>Test-only: true while an error panel is displayed.</summary>
    internal bool _errorPanelVisible;

    /// <summary>Test-only: the last error message shown by ShowErrorPanel.</summary>
    internal string _lastErrorMessage;

    /// <summary>Test-only: the last error severity.</summary>
    internal ErrorSeverity _lastErrorSeverity;

    /// <summary>Test-only: button labels on the currently displayed error panel.</summary>
    internal string[] _lastErrorButtonLabels;

    /// <summary>Test-only: fires when the error panel is dismissed via a recovery button.</summary>
    internal event Action<string> OnErrorPanelDismissed;

    /// <summary>
    /// Test-only injection: when set, LoadSceneAsync and OnMainMenuStartGame
    /// call this function instead of the real UnityEngine.SceneManagement.SceneManager.
    /// Set to null for production (uses real scene manager).
    /// Internal visibility — used by integration tests.
    /// </summary>
    internal Func<string, System.Threading.Tasks.Task> _sceneLoadFuncForTesting;

    /// <summary>
    /// Scene load timeout in seconds (ADR-0004 edge case: 30s timeout + retry).
    /// Test-only: set to a small value (e.g. 0.1f) to verify timeout behavior quickly.
    /// </summary>
    internal float _sceneLoadTimeoutSeconds = 30f;

    /// <summary>
    /// Fires when a fragment transition begins, before the fade-out animation starts.
    /// Consumers should use this to suppress interaction detection and feedback (ADR-0014).
    /// </summary>
    public static event Action<string, string> OnFragmentTransitionStarted;

    /// <summary>
    /// Fires when a fragment transition completes, after the fade-in animation finishes.
    /// Consumers should use this to rebuild interactive colliders, refresh HUD, etc.
    /// </summary>
    public static event Action<string, string> OnFragmentTransitioned;

    /// <summary>
    /// Fires when a scene finishes loading (after LoadSceneAsync completes the
    /// SceneManager.LoadSceneAsync call and the fade-in begins).
    /// Carries the name of the newly-loaded scene (e.g. "MainMenu", "Game").
    /// </summary>
    public static event Action<string> OnSceneLoaded;

    /// <summary>
    /// Fires when a chapter transition begins (stub — Story 004).
    /// </summary>
    public static event Action<string, string> OnChapterTransitionStarted;

    /// <summary>
    /// Fires when a chapter transition completes (stub — Story 004).
    /// </summary>
    public static event Action<string, string> OnChapterTransitioned;

    /// <summary>
    /// The current state of the transition state machine (ADR-0004).
    /// Transitions are only accepted when Idle.
    /// </summary>
    public TransitionState CurrentState => _currentState;

    /// <summary>
    /// Exposed for testing — the chapter key of the currently displayed fragment.
    /// </summary>
    public string CurrentChapterKey => _currentChapterKey;

    /// <summary>
    /// Exposed for testing — the fragment ID of the currently displayed fragment.
    /// </summary>
    public string CurrentFragmentId => _currentFragmentId;

    /// <summary>
    /// Singleton enforcement and DontDestroyOnLoad setup.
    /// The GameSceneManager is created by BootBootstrap and persists across all scenes.
    /// </summary>
    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning(
                $"[GameSceneManager] Duplicate GameSceneManager detected. " +
                $"Destroying duplicate on {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Injects dependencies. Call this after MonoBehaviour instantiation (or in tests).
    /// The SpriteRenderer can also be set via the serialized field in the Unity Inspector.
    /// </summary>
    /// <param name="sceneFader">Implementation of the full-screen fade effect.</param>
    /// <param name="dataManager">Implementation of fragment data loading.</param>
    /// <param name="audioManager">Implementation of audio preloading.</param>
    /// <param name="spriteRenderer">The SpriteRenderer that displays fragment illustrations.</param>
    public void Initialize(
        ISceneFader sceneFader,
        IDataManager dataManager,
        IAudioManager audioManager,
        SpriteRenderer spriteRenderer)
    {
        _sceneFader = sceneFader;
        _dataManager = dataManager;
        _audioManager = audioManager;
        _spriteRenderer = spriteRenderer;
    }

    /// <summary>
    /// Executes a full fragment transition (ADR-0004, Section "FragmentTransition"):
    ///
    /// 1. Guards against concurrent transitions (rejects with log warning if not Idle).
    /// 2. Fires OnFragmentTransitionStarted.
    /// 3. Gates input via InputManager.SwitchToInactive().
    /// 4. Fades out (SceneFader.FadeOut, 0.5s).
    /// 5. Unloads the current fragment, loads the new fragment's assets.
    /// 6. Updates the scene (sprite, interactive objects).
    /// 7. Fades in (SceneFader.FadeIn, 0.5s).
    /// 8. Restores input to Gameplay and fires OnFragmentTransitioned.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier (e.g., "chapter_1").</param>
    /// <param name="fragmentId">The fragment identifier within the chapter (e.g., "frag_01").</param>
    /// <returns>Task that completes when the entire transition finishes, or immediately if rejected.</returns>
    public async Task TransitionToFragmentAsync(string chapterKey, string fragmentId)
    {
        // Guard: reject if already transitioning (ADR-0004 requirement)
        if (_currentState != TransitionState.Idle)
        {
            Debug.LogWarning(
                $"[GameSceneManager] TransitionToFragmentAsync rejected — " +
                $"already transitioning (state={_currentState}). " +
                $"Requested: chapter={chapterKey}, fragment={fragmentId}");
            return;
        }

        _currentState = TransitionState.FadingOut;

        // Step 0: Notify all systems transition is about to begin
        OnFragmentTransitionStarted?.Invoke(chapterKey, fragmentId);

        // Step 0.5: Gate input during transition (ADR-0004 — prevent player interaction)
        InputManager.SwitchToInactive();

        // Step 1: Fade out (ink spreading across screen)
        await _sceneFader.FadeOut(0.5f);
        if (this == null) return;

        _currentState = TransitionState.Loading;

        // Step 2: Unload current fragment resources
        UnloadCurrentFragment();

        // Step 3: Load target fragment assets (with exception recovery)
        try
        {
            MemoryFragment fragment = await _dataManager.GetFragmentAsync(chapterKey, fragmentId);
            if (this == null) return;

            Sprite sprite = await _dataManager.GetIllustrationAsync(fragment.IllustrationKey);
            if (this == null) return;

            await _audioManager.PreloadFragmentAudioAsync(fragment.AudioKeys);
            if (this == null) return;

            // Update scene
            _spriteRenderer.sprite = sprite;
            UpdateInteractiveObjects(fragment.InteractiveObjects);

            _currentChapterKey = chapterKey;
            _currentFragmentId = fragmentId;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[GameSceneManager] Fragment load failed for {chapterKey}/{fragmentId}: {ex.Message}. " +
                "Showing error panel — mask stays covered.");
            _lastFailedFragmentChapterKey = chapterKey;
            _lastFailedFragmentId = fragmentId;
            _currentState = TransitionState.Idle;
            ShowErrorPanel(
                "记忆碎片加载失败",
                ErrorSeverity.Recoverable,
                ("返回章节开头", ReturnToChapterStart));
            return;
        }

        // Step 4: Fade in (ink receding from screen)
        _currentState = TransitionState.FadingIn;
        await _sceneFader.FadeIn(0.5f);
        if (this == null) return;

        // Step 5: Restore input and notify completion
        InputManager.SwitchToGameplayMode();
        _currentState = TransitionState.Idle;
        OnFragmentTransitioned?.Invoke(chapterKey, fragmentId);

        // Story 004: Trigger chapter preload + next-fragment preload after transition
        CheckPreloadTrigger(chapterKey, fragmentId);
        PreloadNextFragmentAsync(chapterKey, fragmentId);
    }

    /// <summary>
    /// Releases the currently displayed fragment's resources via DataManager.
    /// No-op if no fragment is currently loaded (first transition).
    /// </summary>
    private void UnloadCurrentFragment()
    {
        if (_currentFragmentId != null)
        {
            _dataManager.ReleaseFragment(_currentFragmentId);
        }
    }

    /// <summary>
    /// Updates the scene with interactive objects for the new fragment.
    /// This is a placeholder stub — the actual collider creation is handled by
    /// InteractionManager when it receives OnFragmentTransitioned (Story scroll-interaction-001).
    /// </summary>
    private void UpdateInteractiveObjects(InteractiveObject[] objects)
    {
        // Handled by InteractionManager — Story scroll-interaction-001
        // This stub exists so the transition flow is complete; collider creation
        // happens when InteractionManager receives OnFragmentTransitioned.
        // InteractionManager listens to OnFragmentTransitioned and creates
        // the appropriate Collider2D components for each InteractiveObject.
    }

    /// <summary>
    /// Finds the UIDocument in the loaded Game scene, creates a SceneFader
    /// VisualElement, mounts it as a child of the root, applies the Theme.uss
    /// stylesheet, and assigns the fader to _sceneFader.
    ///
    /// Idempotent — if _sceneFader is already a SceneFader (already mounted),
    /// this is a no-op. Safe to call after every scene load that targets "Game".
    /// </summary>
    private void MountSceneFaderToGameScene()
    {
        // Idempotent: only mount once
        if (_sceneFader != null)
            return;

        UIDocument uiDocument = Object.FindObjectOfType<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogWarning(
                "[GameSceneManager] MountSceneFaderToGameScene: " +
                "no UIDocument found in Game scene — ink-fade overlay will not be available. " +
                "Add a UIDocument with the Game UI to the Game scene.");
            return;
        }

        // Theme.uss should be assigned to the Game scene's UIDocument PanelSettings
        // in the Unity Editor (PanelSettings → Style Sheets list). This avoids
        // Resources.Load (forbidden per ADR-0002). If not configured, SceneFader
        // inline styles provide fallback behavior.
        if (uiDocument.panelSettings != null &&
            uiDocument.panelSettings.themeStyleSheet == null)
        {
            Debug.LogWarning(
                "[GameSceneManager] PanelSettings.themeStyleSheet is null. " +
                "Assign Theme.uss to the Game scene's UIDocument PanelSettings " +
                "for GPU-accelerated opacity transitions.");
        }

        SceneFader sceneFader = new SceneFader();
        sceneFader.AddToDocument(uiDocument);
        _sceneFader = sceneFader;

        Debug.Log("[GameSceneManager] SceneFader mounted to Game scene UIDocument.");
    }

    /// <summary>
    /// Loads a new Unity scene asynchronously, wrapping the engine call with
    /// SceneFader transitions (ADR-0004 SceneTransition).
    ///
    /// Flow: Idle → FadingOut → Loading → FadingIn → Idle.
    /// During Loading, input is gated to Inactive per ADR-0004.
    /// Fires OnSceneLoaded after the engine load completes and before fade-in.
    ///
    /// Rejects the call with a Debug.LogWarning if the state machine is not Idle.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load (e.g. "MainMenu", "Game").</param>
    /// <returns>Task that completes when the transition finishes, or immediately if rejected.</returns>
    public async Task LoadSceneAsync(string sceneName)
    {
        // Guard: reject if already transitioning
        if (_currentState != TransitionState.Idle)
        {
            Debug.LogWarning(
                $"[GameSceneManager] LoadSceneAsync rejected — " +
                $"already transitioning (state={_currentState}). " +
                $"Requested: {sceneName}");
            return;
        }

        _currentState = TransitionState.FadingOut;

        // Gate input during transition
        InputManager.SwitchToInactive();

        // Step 1: Fade out
        if (_sceneFader != null)
        {
            await _sceneFader.FadeOut(0.5f);
            if (this == null) return;
        }

        _currentState = TransitionState.Loading;

        // Step 2: Load the scene asynchronously with timeout (ADR-0004: 30s)
        try
        {
            if (_sceneLoadFuncForTesting != null)
            {
                Task loadTask = _sceneLoadFuncForTesting(sceneName);
                Task timeoutTask = Task.Delay((int)(_sceneLoadTimeoutSeconds * 1000));
                Task completed = await Task.WhenAny(loadTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    throw new TimeoutException(
                        $"Scene load timed out after {_sceneLoadTimeoutSeconds}s: '{sceneName}'.");
                }
                await loadTask; // propagate any exception from the load task
                if (this == null) return;
            }
            else
            {
                AsyncOperation asyncOp = UnityEngine.SceneManagement.SceneManager
                    .LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

                if (asyncOp == null)
                {
                    throw new InvalidOperationException(
                        $"SceneManager.LoadSceneAsync returned null for scene '{sceneName}'. " +
                        "Verify the scene is added to Build Settings.");
                }

                float startTime = Time.unscaledTime;
                while (!asyncOp.isDone)
                {
                    if (Time.unscaledTime - startTime >= _sceneLoadTimeoutSeconds)
                    {
                        throw new TimeoutException(
                            $"Scene load timed out after {_sceneLoadTimeoutSeconds}s: '{sceneName}'.");
                    }
                    await Task.Yield();
                    if (this == null) return;
                }
            }
        }
        catch (Exception ex)
        {
            // AC-3: Log full stack trace for scene load failures
            Debug.LogError(
                $"[GameSceneManager] Scene load failed for '{sceneName}': {ex}");
            _currentState = TransitionState.Idle;

            if (sceneName == "Game")
            {
                // Fatal: Game scene is required for gameplay
                ShowErrorPanel(
                    "游戏场景加载失败，请验证游戏文件完整性",
                    ErrorSeverity.Fatal,
                    ("退出到桌面", ExitToDesktop));
            }
            else
            {
                ShowErrorPanel(
                    $"场景加载失败: {sceneName}",
                    ErrorSeverity.Recoverable,
                    ("返回主菜单", ReturnToMainMenu),
                    ("重试", () => { HideErrorPanel(); _ = LoadSceneAsync(sceneName); }));
            }
            return;
        }

        // Step 3: Notify scene loaded
        OnSceneLoaded?.Invoke(sceneName);

        // Mount SceneFader when entering the Game scene (Story 002 — ink-fade overlay)
        if (sceneName == "Game")
        {
            await Task.Yield(); // ensure UIDocument visual tree is built
            MountSceneFaderToGameScene();
        }
        else
        {
            // Unmount when leaving Game scene — VisualElement is destroyed with
            // the scene's UIDocument, so null the C# reference to prevent stale access
            _sceneFader = null;
        }

        // Step 4: Fade in
        _currentState = TransitionState.FadingIn;
        if (_sceneFader != null)
        {
            await _sceneFader.FadeIn(0.5f);
            if (this == null) return;
        }

        // Step 5: Restore input and return to Idle
        InputManager.SwitchToGameplayMode();
        _currentState = TransitionState.Idle;
    }

    /// <summary>
    /// Handles the MainMenu → Game transition (ADR-0004: player clicks "Start Game").
    ///
    /// Flow:
    ///   1. LoadSceneAsync("Game") with full ink-fade transition.
    ///   2. Preload the initial chapter assets (fire-and-forget, failure non-blocking).
    ///   3. Load the entry fragment for the initial chapter.
    ///   4. Fire OnFragmentTransitionStarted + OnFragmentTransitioned when complete.
    ///
    /// Called by MainMenuController.OnStartGameClicked().
    /// </summary>
    /// <param name="initialChapterKey">The chapter key to load (e.g. "chapter_1").</param>
    /// <param name="entryFragmentId">The entry fragment ID for the initial chapter.</param>
    /// <returns>Task that completes when the Game scene is loaded and the entry fragment
    /// is displayed.</returns>
    public async Task OnMainMenuStartGame(string initialChapterKey, string entryFragmentId)
    {
        if (_currentState != TransitionState.Idle)
        {
            Debug.LogWarning(
                $"[GameSceneManager] OnMainMenuStartGame rejected — " +
                $"already transitioning (state={_currentState}).");
            return;
        }

        _currentState = TransitionState.FadingOut;

        // Gate input
        InputManager.SwitchToInactive();

        // Step 1: Fade out (full-screen ink mask)
        if (_sceneFader != null)
        {
            await _sceneFader.FadeOut(0.5f);
            if (this == null) return;
        }

        _currentState = TransitionState.Loading;

        // Step 2: Load the Game scene with timeout (ADR-0004: 30s)
        try
        {
            if (_sceneLoadFuncForTesting != null)
            {
                Task loadTask = _sceneLoadFuncForTesting("Game");
                Task timeoutTask = Task.Delay((int)(_sceneLoadTimeoutSeconds * 1000));
                Task completed = await Task.WhenAny(loadTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    throw new TimeoutException(
                        $"Game scene load timed out after {_sceneLoadTimeoutSeconds}s.");
                }
                await loadTask; // propagate any exception
                if (this == null) return;
            }
            else
            {
                AsyncOperation asyncOp = UnityEngine.SceneManagement.SceneManager
                    .LoadSceneAsync("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);

                if (asyncOp == null)
                {
                    throw new InvalidOperationException(
                        "SceneManager.LoadSceneAsync returned null for 'Game'. " +
                        "Verify the scene is added to Build Settings.");
                }

                float startTime = Time.unscaledTime;
                while (!asyncOp.isDone)
                {
                    if (Time.unscaledTime - startTime >= _sceneLoadTimeoutSeconds)
                    {
                        throw new TimeoutException(
                            $"Game scene load timed out after {_sceneLoadTimeoutSeconds}s.");
                    }
                    await Task.Yield();
                    if (this == null) return;
                }
            }
        }
        catch (Exception ex)
        {
            // AC-3: Log full stack trace for Game scene load failures
            Debug.LogError(
                $"[GameSceneManager] Game scene load failed: {ex}");
            _currentState = TransitionState.Idle;
            ShowErrorPanel(
                "游戏场景加载失败，请验证游戏文件完整性",
                ErrorSeverity.Fatal,
                ("退出到桌面", ExitToDesktop));
            return;
        }

        OnSceneLoaded?.Invoke("Game");

        // Mount SceneFader when entering the Game scene (Story 002 — ink-fade overlay)
        await Task.Yield(); // ensure UIDocument visual tree is built
        MountSceneFaderToGameScene();

        // Step 3: Preload initial chapter assets (fire-and-forget, failure non-blocking)
        if (_dataManager != null)
        {
            try
            {
                await _dataManager.PreloadChapterAsync(initialChapterKey);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[GameSceneManager] Chapter preload failed (non-blocking): {ex.Message}. " +
                    "Main load path will retry on demand.");
            }
        }
        if (this == null) return;

        // Step 4: Load the entry fragment for the initial chapter
        try
        {
            if (_dataManager == null)
            {
                Debug.LogWarning(
                    "[GameSceneManager] OnMainMenuStartGame: IDataManager not injected — " +
                    "skipping entry fragment load. Fragment transition events will not fire.");
                InputManager.SwitchToGameplayMode();
                _currentState = TransitionState.Idle;
                return;
            }

            MemoryFragment fragment = await _dataManager.GetFragmentAsync(initialChapterKey, entryFragmentId);
            if (this == null) return;

            if (fragment != null)
            {
                Sprite sprite = await _dataManager.GetIllustrationAsync(fragment.IllustrationKey);
                if (this == null) return;

                if (_audioManager != null)
                {
                    await _audioManager.PreloadFragmentAudioAsync(fragment.AudioKeys);
                    if (this == null) return;
                }

                _spriteRenderer.sprite = sprite;
                _currentChapterKey = initialChapterKey;
                _currentFragmentId = entryFragmentId;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[GameSceneManager] Entry fragment load failed for " +
                $"{initialChapterKey}/{entryFragmentId}: {ex.Message}. " +
                "Showing error panel — mask stays covered.");
            _lastFailedFragmentChapterKey = initialChapterKey;
            _lastFailedFragmentId = entryFragmentId;
            _currentState = TransitionState.Idle;
            ShowErrorPanel(
                "记忆碎片加载失败",
                ErrorSeverity.Recoverable,
                ("返回章节开头", ReturnToChapterStart));
            return;
        }

        // Step 5: Notify — OnFragmentTransitionStarted fires before fade-in
        OnFragmentTransitionStarted?.Invoke(initialChapterKey, entryFragmentId);

        // Step 6: Fade in
        _currentState = TransitionState.FadingIn;
        if (_sceneFader != null)
        {
            await _sceneFader.FadeIn(0.5f);
            if (this == null) return;
        }

        // Step 7: Restore input and notify completion
        InputManager.SwitchToGameplayMode();
        _currentState = TransitionState.Idle;
        OnFragmentTransitioned?.Invoke(initialChapterKey, entryFragmentId);
    }

    /// <summary>
    /// Transitions to a new chapter (ADR-0004 ChapterTransition).
    ///
    /// Flow: Idle → FadingOut (1.0s) → Loading (unload old, await preload, load new) →
    /// FadingIn (1.0s) → Idle.
    ///
    /// If a background preload was triggered earlier (via CheckPreloadTrigger), this
    /// method awaits the stored Task before proceeding — making the transition
    /// near-instant if preload completed in time. If preload is missing or failed,
    /// the main load path retries.
    /// </summary>
    /// <param name="chapterKey">The target chapter key (e.g. "chapter_2").</param>
    /// <returns>Task that completes when the chapter transition finishes.</returns>
    public async Task TransitionToChapterAsync(string chapterKey)
    {
        // Guard: reject if already transitioning
        if (_currentState != TransitionState.Idle)
        {
            Debug.LogWarning(
                $"[GameSceneManager] TransitionToChapterAsync rejected — " +
                $"already transitioning (state={_currentState}). " +
                $"Requested: {chapterKey}");
            return;
        }

        _currentState = TransitionState.FadingOut;

        // Step 1: Gate input and fire start event
        InputManager.SwitchToInactive();
        OnChapterTransitionStarted?.Invoke(_currentChapterKey, chapterKey);

        // Step 2: Music crossfade out
        _audioManager?.StopMusic(1.0f);

        // Step 3: Fade out (chapter transitions use 1.0s per ADR-0004)
        if (_sceneFader != null)
        {
            await _sceneFader.FadeOut(1.0f);
            if (this == null) return;
        }

        _currentState = TransitionState.Loading;

        // Capture old chapter key before overwriting (needed for OnChapterTransitioned event)
        string oldChapterKey = _currentChapterKey;

        try
        {
            // Step 4: Unload current chapter resources
            if (oldChapterKey != null)
            {
                _dataManager?.UnloadChapter(oldChapterKey);
                _audioManager?.UnloadChapterAudio(oldChapterKey);
            }

            // Step 5: Await background preload if it was triggered and matches target
            if (_chapterPreloadTask != null && _preloadedChapterKey == chapterKey)
            {
                try
                {
                    await _chapterPreloadTask;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[GameSceneManager] Background preload for '{chapterKey}' failed: {ex.Message}. " +
                        "Falling through to main load path.");
                }
            }
            if (this == null) return;

            // Step 6: Main-path load if preload didn't happen or failed
            if (_dataManager != null && _audioManager != null)
            {
                await Task.WhenAll(
                    _dataManager.PreloadChapterAsync(chapterKey),
                    _audioManager.PreloadChapterAudioAsync(chapterKey));
                if (this == null) return;
            }
            else if (_dataManager != null)
            {
                await _dataManager.PreloadChapterAsync(chapterKey);
                if (this == null) return;
            }

            // Step 7: Load chapter definition + entry fragment
            ChapterDefinition chapterDef = await _dataManager.GetChapterAsync(chapterKey);
            if (this == null) return;

            string entryFragmentId = chapterDef?.EntryFragmentId;
            if (string.IsNullOrEmpty(entryFragmentId))
            {
                throw new InvalidOperationException(
                    $"ChapterDefinition for '{chapterKey}' has no EntryFragmentId.");
            }

            MemoryFragment fragment = await _dataManager.GetFragmentAsync(chapterKey, entryFragmentId);
            if (this == null) return;

            Sprite sprite = await _dataManager.GetIllustrationAsync(fragment.IllustrationKey);
            if (this == null) return;

            if (_audioManager != null)
            {
                await _audioManager.PreloadFragmentAudioAsync(fragment.AudioKeys);
                if (this == null) return;
            }

            _spriteRenderer.sprite = sprite;
            _currentChapterKey = chapterKey;
            _currentFragmentId = entryFragmentId;

            // Step 8: New chapter music fade in
            _audioManager?.PlayMusic(chapterKey, 1.0f);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[GameSceneManager] Chapter transition failed for '{chapterKey}': {ex.Message}. " +
                "Showing error panel — mask stays covered.");
            _lastFailedChapterKey = chapterKey;
            _currentState = TransitionState.Idle;
            ShowErrorPanel(
                "章节加载失败",
                ErrorSeverity.Recoverable,
                ("返回主菜单", ReturnToMainMenu),
                ("重试", RetryChapterTransition));
            return;
        }

        // Step 9: Fade in
        _currentState = TransitionState.FadingIn;
        if (_sceneFader != null)
        {
            await _sceneFader.FadeIn(1.0f);
            if (this == null) return;
        }

        // Step 10: Restore input, fire completion events, reset preload state
        InputManager.SwitchToGameplayMode();
        _currentState = TransitionState.Idle;
        OnChapterTransitioned?.Invoke(oldChapterKey, chapterKey);
        OnFragmentTransitioned?.Invoke(chapterKey, _currentFragmentId);

        _preloadTriggeredThisChapter = false;
        _chapterPreloadTask = null;
        _preloadedChapterKey = null;
    }

    /// <summary>
    /// Checks whether the remaining fragments in the current chapter have fallen
    /// to or below the preload threshold (3), and if so, kicks off a background
    /// preload of the next chapter (ADR-0004: ≤3 fragments remaining trigger).
    ///
    /// Idempotent within a chapter — _preloadTriggeredThisChapter prevents
    /// duplicate triggers across multiple fragment transitions.
    /// </summary>
    /// <param name="chapterKey">The current chapter key.</param>
    /// <param name="currentFragmentId">The fragment just displayed.</param>
    private void CheckPreloadTrigger(string chapterKey, string currentFragmentId)
    {
        if (_preloadTriggeredThisChapter)
            return;

        if (_dataManager == null || _audioManager == null)
            return;

        List<MemoryFragment> fragments = _dataManager.GetFragmentsByChapter(chapterKey);
        if (fragments == null || fragments.Count == 0)
            return;

        // Find index of current fragment in the ordered list
        int currentIndex = -1;
        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && fragments[i].FragmentId == currentFragmentId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
            return;

        int remaining = fragments.Count - currentIndex - 1;

        if (remaining <= PreloadThreshold)
        {
            _preloadTriggeredThisChapter = true;

            string nextChapterKey = GetNextChapterKey(chapterKey);
            if (nextChapterKey == null)
                return;

            _preloadedChapterKey = nextChapterKey;
            _chapterPreloadTask = PreloadNextChapterAsync(nextChapterKey);

            Debug.Log(
                $"[GameSceneManager] Chapter preload triggered: {remaining} fragments " +
                $"remaining in '{chapterKey}', preloading '{nextChapterKey}' in background.");
        }
    }

    /// <summary>
    /// Background preload of the next chapter's data + audio in parallel.
    /// Called via fire-and-forget from CheckPreloadTrigger. Failure logs a
    /// warning but does not throw — the main load path in TransitionToChapterAsync
    /// retries on demand.
    /// </summary>
    /// <param name="chapterKey">The chapter key to preload.</param>
    /// <returns>Task that completes when both data and audio preloads finish (or fail).</returns>
    private async Task PreloadNextChapterAsync(string chapterKey)
    {
        try
        {
            await Task.WhenAll(
                _dataManager.PreloadChapterAsync(chapterKey),
                _audioManager.PreloadChapterAudioAsync(chapterKey));
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[GameSceneManager] Background preload for '{chapterKey}' failed " +
                $"(non-blocking): {ex.Message}. Main load path will retry on demand.");
        }
    }

    /// <summary>
    /// Preloads the next sequential fragment in the current chapter (fire-and-forget).
    /// Called after each fragment transition completes. If the current fragment is
    /// the last in the chapter, this is a no-op.
    /// </summary>
    /// <param name="chapterKey">The current chapter key.</param>
    /// <param name="currentFragmentId">The fragment just displayed.</param>
    private void PreloadNextFragmentAsync(string chapterKey, string currentFragmentId)
    {
        if (_dataManager == null)
            return;

        List<MemoryFragment> fragments = _dataManager.GetFragmentsByChapter(chapterKey);
        if (fragments == null || fragments.Count == 0)
            return;

        // Find current fragment index
        int currentIndex = -1;
        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null && fragments[i].FragmentId == currentFragmentId)
            {
                currentIndex = i;
                break;
            }
        }

        // If there is a next fragment, preload it
        int nextIndex = currentIndex + 1;
        if (nextIndex < fragments.Count && fragments[nextIndex] != null)
        {
            string nextFragmentId = fragments[nextIndex].FragmentId;
            if (!string.IsNullOrEmpty(nextFragmentId))
            {
                _ = _dataManager.PreloadFragmentAsync(chapterKey, nextFragmentId);
            }
        }
    }

    /// <summary>
    /// Derives the next chapter key by incrementing the numeric suffix.
    /// Example: "chapter_1" → "chapter_2", "ch01" → "ch02".
    /// Returns null if the key format is unrecognized or parsing fails.
    /// </summary>
    /// <param name="currentChapterKey">The current chapter key.</param>
    /// <returns>The next chapter key, or null if unable to determine.</returns>
    private static string GetNextChapterKey(string currentChapterKey)
    {
        if (string.IsNullOrEmpty(currentChapterKey))
            return null;

        // Find the trailing numeric portion
        int numStart = currentChapterKey.Length - 1;
        while (numStart >= 0 && char.IsDigit(currentChapterKey[numStart]))
            numStart--;
        numStart++; // Step back to the first digit

        if (numStart >= currentChapterKey.Length)
            return null;

        string prefix = currentChapterKey.Substring(0, numStart);
        string numStr = currentChapterKey.Substring(numStart);

        if (!int.TryParse(numStr, out int num))
            return null;

        // Preserve zero-padding width
        string nextNum = (num + 1).ToString(new string('0', numStr.Length));
        return prefix + nextNum;
    }

    /// <summary>
    /// Preloads all assets for a chapter in the background.
    /// Delegates to IDataManager.PreloadChapterAsync if available.
    /// Full preload trigger logic (≤3 fragments remaining) is in CheckPreloadTrigger.
    /// </summary>
    /// <param name="chapterKey">The chapter key to preload.</param>
    /// <returns>Task that completes when preload finishes or fails (non-blocking).</returns>
    public async Task PreloadChapterAsync(string chapterKey)
    {
        if (_dataManager == null)
        {
            Debug.LogWarning(
                $"[GameSceneManager] PreloadChapterAsync('{chapterKey}') skipped — " +
                "IDataManager not injected.");
            return;
        }

        try
        {
            await _dataManager.PreloadChapterAsync(chapterKey);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[GameSceneManager] PreloadChapterAsync('{chapterKey}') failed " +
                $"(non-blocking): {ex.Message}");
        }
    }

    // =========================================================================
    // Error Recovery (Story 005 — ADR-0004 error grading)
    // =========================================================================

    /// <summary>
    /// Displays an error panel above the SceneFader overlay.
    /// Creates UI Toolkit VisualElements styled with error-panel / error-message /
    /// error-button USS classes. If no UIDocument is available (e.g., in tests),
    /// logs a warning and sets test hooks only.
    ///
    /// Called from catch blocks in transition methods. The SceneFader mask remains
    /// at opacity=1 while the error panel is shown. Recovery actions (button
    /// onClick callbacks) are responsible for calling HideErrorPanel and
    /// initiating the appropriate recovery transition.
    /// </summary>
    /// <param name="message">Error message displayed to the player.</param>
    /// <param name="severity">Error severity — determines UI tone (not currently styled differently).</param>
    /// <param name="buttons">Array of (label, onClick) tuples for recovery buttons.</param>
    private void ShowErrorPanel(string message, ErrorSeverity severity,
        params (string label, Action onClick)[] buttons)
    {
        // Update test hooks first (before UI attempt — tests may not have UIDocument)
        _errorPanelVisible = true;
        _lastErrorMessage = message;
        _lastErrorSeverity = severity;
        _lastErrorButtonLabels = new string[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
            _lastErrorButtonLabels[i] = buttons[i].label;

        // Switch to UI input map so error panel buttons are interactable
        InputManager.SwitchToUIMode();

        // Attempt UI display — find a UIDocument in the current scene
        UIDocument uiDocument = Object.FindObjectOfType<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogWarning(
                $"[GameSceneManager] Error panel (no UIDocument available): {message}");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        // Remove existing error panel if any
        HideErrorPanelVisual();

        _errorPanel = new VisualElement();
        _errorPanel.AddToClassList("error-panel");
        _errorPanel.style.position = Position.Absolute;
        _errorPanel.style.top = 0;
        _errorPanel.style.left = 0;
        _errorPanel.style.width = Length.Percent(100);
        _errorPanel.style.height = Length.Percent(100);
        _errorPanel.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        _errorPanel.pickingMode = PickingMode.Position;

        var container = new VisualElement();
        container.style.position = Position.Absolute;
        container.style.top = Length.Percent(40);
        container.style.left = Length.Percent(10);
        container.style.width = Length.Percent(80);
        container.style.alignItems = Align.Center;

        var messageLabel = new Label(message);
        messageLabel.AddToClassList("error-message");
        messageLabel.style.color = Color.white;
        messageLabel.style.fontSize = 18;
        messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        messageLabel.style.whiteSpace = WhiteSpace.Normal;
        container.Add(messageLabel);

        var buttonRow = new VisualElement();
        buttonRow.AddToClassList("error-buttons");
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.Center;
        buttonRow.style.marginTop = 20;

        foreach (var (label, onClick) in buttons)
        {
            var button = new Button(onClick) { text = label };
            button.AddToClassList("error-button");
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            buttonRow.Add(button);
        }
        container.Add(buttonRow);

        _errorPanel.Add(container);
        root.Add(_errorPanel);
        _errorPanel.BringToFront();
    }

    /// <summary>
    /// Removes the error panel VisualElement from the UIDocument root.
    /// Does NOT update test hooks — callers should set _errorPanelVisible = false.
    /// </summary>
    private void HideErrorPanelVisual()
    {
        if (_errorPanel != null)
        {
            _errorPanel.RemoveFromHierarchy();
            _errorPanel = null;
        }
    }

    /// <summary>
    /// Dismisses the error panel and restores input to Gameplay mode.
    /// Called by recovery button onClick handlers and at the start of
    /// recovery transitions.
    /// </summary>
    private void HideErrorPanel()
    {
        HideErrorPanelVisual();
        _errorPanelVisible = false;
        _lastErrorMessage = null;
        _lastErrorButtonLabels = null;
    }

    /// <summary>
    /// Wraps a task with a timeout. If the task does not complete within
    /// timeoutSeconds, a TimeoutException is thrown.
    /// Used for metadata load timeout (10s per ADR-0004 S005).
    /// Scene load timeout (30s) is handled inline in LoadSceneAsync.
    /// </summary>
    /// <param name="task">The task to wrap.</param>
    /// <param name="timeoutSeconds">Timeout in seconds.</param>
    /// <param name="errorMessage">Message for the TimeoutException.</param>
    /// <returns>The result of the task if it completes in time.</returns>
    private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutSeconds, string errorMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeoutSeconds * 1000));
        if (completed != task)
            throw new TimeoutException(errorMessage);
        return await task;
    }

    /// <summary>
    /// Recovery action: returns to the first fragment of the current chapter.
    /// Hides the error panel, then transitions to the chapter's entry fragment.
    /// Falls back to MainMenu if DataManager or chapter definition is unavailable.
    /// </summary>
    private async void ReturnToChapterStart()
    {
        string label = _lastErrorButtonLabels != null && _lastErrorButtonLabels.Length > 0
            ? _lastErrorButtonLabels[0] : "return_to_chapter_start";
        OnErrorPanelDismissed?.Invoke(label);
        HideErrorPanel();

        string chapterKey = _lastFailedFragmentChapterKey ?? _currentChapterKey;
        if (string.IsNullOrEmpty(chapterKey) || _dataManager == null)
        {
            Debug.LogWarning("[GameSceneManager] ReturnToChapterStart: no chapter key or DataManager — returning to MainMenu.");
            await LoadSceneAsync("MainMenu");
            return;
        }

        try
        {
            ChapterDefinition chapterDef = await _dataManager.GetChapterAsync(chapterKey);
            if (this == null) return;

            string entryId = chapterDef?.EntryFragmentId;
            if (string.IsNullOrEmpty(entryId))
            {
                Debug.LogWarning(
                    $"[GameSceneManager] ReturnToChapterStart: no EntryFragmentId for '{chapterKey}' — returning to MainMenu.");
                await LoadSceneAsync("MainMenu");
                return;
            }

            await TransitionToFragmentAsync(chapterKey, entryId);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[GameSceneManager] ReturnToChapterStart failed for '{chapterKey}': {ex.Message}. " +
                "Returning to MainMenu.");
            await LoadSceneAsync("MainMenu");
        }
    }

    /// <summary>
    /// Recovery action: returns to the main menu.
    /// Hides the error panel and triggers LoadSceneAsync("MainMenu").
    /// </summary>
    private async void ReturnToMainMenu()
    {
        string label = _lastErrorButtonLabels != null && _lastErrorButtonLabels.Length > 0
            ? _lastErrorButtonLabels[0] : "return_to_menu";
        OnErrorPanelDismissed?.Invoke(label);
        HideErrorPanel();
        await LoadSceneAsync("MainMenu");
    }

    /// <summary>
    /// Recovery action: retries the last failed chapter transition.
    /// Hides the error panel and re-invokes TransitionToChapterAsync.
    /// </summary>
    private async void RetryChapterTransition()
    {
        string label = _lastErrorButtonLabels != null && _lastErrorButtonLabels.Length > 1
            ? _lastErrorButtonLabels[1] : "retry";
        OnErrorPanelDismissed?.Invoke(label);
        HideErrorPanel();
        if (_lastFailedChapterKey != null)
            await TransitionToChapterAsync(_lastFailedChapterKey);
    }

    /// <summary>
    /// Recovery action: retries the last failed fragment transition.
    /// Hides the error panel and re-invokes TransitionToFragmentAsync.
    /// </summary>
    private async void RetryFragmentTransition()
    {
        string label = _lastErrorButtonLabels != null && _lastErrorButtonLabels.Length > 0
            ? _lastErrorButtonLabels[0] : "retry";
        OnErrorPanelDismissed?.Invoke(label);
        HideErrorPanel();
        if (_lastFailedFragmentChapterKey != null && _lastFailedFragmentId != null)
            await TransitionToFragmentAsync(_lastFailedFragmentChapterKey, _lastFailedFragmentId);
    }

    /// <summary>
    /// Recovery action: exits to desktop.
    /// In Editor, stops play mode. In builds, calls Application.Quit().
    /// </summary>
    private void ExitToDesktop()
    {
        string label = _lastErrorButtonLabels != null && _lastErrorButtonLabels.Length > 0
            ? _lastErrorButtonLabels[0] : "exit";
        OnErrorPanelDismissed?.Invoke(label);
        Debug.Log("[GameSceneManager] Player chose to exit — terminating application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// ADR-0001 Rule 7: Null static events to prevent stale delegate chains
    /// when the GameSceneManager GameObject is destroyed (scene unload, etc.).
    /// This prevents subscribers from receiving events from a destroyed producer.
    /// </summary>
    void OnDestroy()
    {
        // CRITICAL: Only the singleton instance cleans up static events.
        // A duplicate destroyed in Awake() must NOT null the alive singleton's events.
        if (Instance != this) return;

        OnFragmentTransitionStarted = null;
        OnFragmentTransitioned = null;
        OnSceneLoaded = null;
        OnChapterTransitionStarted = null;
        OnChapterTransitioned = null;

        Instance = null;
    }
}
