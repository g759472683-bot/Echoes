using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Core input management system implementing the ADR-0005 action map split
/// and a 4-state input mode machine (Gameplay, Menu, Rebinding, Inactive).
///
/// Programmatic InputActionAsset construction (ScriptableObject.CreateInstance)
/// builds two action maps at runtime:
///
///   Gameplay Map (ADR-0005):
///     Point     — Value,  Vector2,  &lt;Mouse&gt;/position
///     Click     — Button,           &lt;Mouse&gt;/leftButton
///     Scroll    — Value,  Vector2,  &lt;Mouse&gt;/scroll
///     RightClick— Button,           &lt;Mouse&gt;/rightButton
///     Pause     — Button,           &lt;Keyboard&gt;/escape
///
///   UI Map (ADR-0005):
///     Navigate    — Value,  Vector2,  WASD + ArrowKeys (2DVector composites)
///     Confirm     — Button,           &lt;Keyboard&gt;/enter
///     Cancel      — Button,           &lt;Keyboard&gt;/escape
///     TabNext     — Button,           &lt;Keyboard&gt;/tab
///     TabPrevious — Button,           Shift+Tab (OneModifier composite)
///
/// State machine:
///   Gameplay  — GameplayMap enabled, UIMap disabled
///   Menu      — UIMap enabled, GameplayMap disabled
///   Rebinding — Both maps disabled (rebinding operation in progress)
///   Inactive  — Both maps disabled (scene transitions per ADR-0004)
///
/// Auto-wired transitions:
///   - Pause action (Escape on Gameplay map) → SwitchToUIMode()
///   - Cancel action (Escape on UI map)      → SwitchToGameplayMode()
///
/// Lifecycle: This MonoBehaviour lives in the Boot scene (ADR-0004),
/// uses DontDestroyOnLoad, and persists across all scenes.
///
/// Testability:
///   - <see cref="OnInputStateChanged"/> (InputState) — new event for current consumers
///   - <see cref="OnSetActionMap"/> (ActionMap) — preserved for backward compat with
///     existing fragment_transition_test.cs and GameSceneManager
///   - Both events are nulled in OnDestroy (ADR-0001 Rule 7)
///   - Static convenience methods fire events even when no Instance exists,
///     so unit tests that mock the InputManager can still verify input gating
/// </summary>
public class InputManager : MonoBehaviour
{
    // =========================================================================
    // Singleton Access
    // =========================================================================

    /// <summary>The singleton instance, set in Awake. Null if not yet initialized.</summary>
    public static InputManager Instance { get; private set; }

    // =========================================================================
    // Events (ADR-0001 public contract)
    // =========================================================================

    /// <summary>
    /// Fires whenever the input state changes. Consumers that need to know
    /// the full InputState (including Rebinding) should subscribe here.
    /// </summary>
    public static event Action<InputState> OnInputStateChanged;

    /// <summary>
    /// Backward-compatible event mapped from <see cref="ActionMap"/>.
    /// Existing tests (fragment_transition_test.cs) and systems subscribe to this.
    /// Fires concurrently with <see cref="OnInputStateChanged"/> on every state change.
    /// </summary>
    public static event Action<ActionMap> OnSetActionMap;

    /// <summary>
    /// Fires when gameplay input becomes active (true) or inactive (false).
    /// Consumed by InGameHUD to gate HUD visibility per ADR-0006 visibility rules.
    /// Fires inside SwitchToGameplayMode (true), SwitchToUIMode (false),
    /// SwitchToInactive (false), and SwitchToRebindingMode (false).
    /// </summary>
    public static event Action<bool> OnGameplayInputActiveChanged;

    // =========================================================================
    // Internal State
    // =========================================================================

    private InputActionAsset _inputActions;
    private InputActionMap _gameplayMap;
    private InputActionMap _uiMap;
    private InputState _currentInputState;
    private bool _isInitialized;
    private bool _buildWasAttempted;

    // =========================================================================
    // Public Accessors
    // =========================================================================

    /// <summary>The current input mode (Gameplay, Menu, Rebinding, or Inactive).</summary>
    public InputState CurrentInputState => _currentInputState;

    /// <summary>
    /// The runtime InputActionAsset containing both action maps.
    /// Exposed for test verification and for advanced consumers (rebinding, etc.).
    /// </summary>
    public InputActionAsset InputActions => _inputActions;

    /// <summary>Reference to the Gameplay action map for direct action queries.</summary>
    public InputActionMap GameplayMap => _gameplayMap;

    /// <summary>Reference to the UI action map for direct action queries.</summary>
    public InputActionMap UIMap => _uiMap;

    /// <summary>
    /// True if <see cref="Initialize"/> has been called successfully (or Awake
    /// completed without errors). Used by BootBootstrap and tests.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    /// <summary>
    /// Builds the InputActionAsset programmatically, enables it, and sets
    /// the default state to Gameplay. Uses DontDestroyOnLoad so this
    /// manager persists across all scenes (ADR-0004 Boot scene placement).
    /// </summary>
    void Awake()
    {
        // Singleton enforcement
        if (Instance != null)
        {
            Debug.LogError(
                $"[InputManager] Duplicate InputManager detected. " +
                $"Existing: {Instance.gameObject.name}, New: {gameObject.name}. Destroying new.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            _buildWasAttempted = true;
            BuildInputActions();
            if (_inputActions == null)
            {
                throw new InvalidOperationException("BuildInputActions produced null asset.");
            }

            _inputActions.Enable();

            // Wire auto-transitions
            _gameplayMap.FindAction("Pause").performed += OnPausePerformed;
            _uiMap.FindAction("Cancel").performed += OnCancelPerformed;

            // Default state: Gameplay
            SwitchToGameplayMode();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[InputManager] Initialization failed: {ex.Message}. " +
                "Input will be unavailable this session. " +
                "Ensure the Input System package is installed and functional.");
            _isInitialized = false;

            // Fire Inactive events so dependent systems know input is unavailable
            _currentInputState = InputState.Inactive;
            OnInputStateChanged?.Invoke(InputState.Inactive);
            OnSetActionMap?.Invoke(ActionMap.Inactive);
        }
    }

    /// <summary>
    /// Cleans up the InputActionAsset, nulls static events per ADR-0001 Rule 7,
    /// and clears the singleton reference.
    /// </summary>
    void OnDestroy()
    {
        if (_inputActions != null)
        {
            // Unsubscribe auto-transitions
            InputAction pauseAction = _gameplayMap?.FindAction("Pause");
            if (pauseAction != null) pauseAction.performed -= OnPausePerformed;

            InputAction cancelAction = _uiMap?.FindAction("Cancel");
            if (cancelAction != null) cancelAction.performed -= OnCancelPerformed;

            _inputActions.Disable();
            _inputActions.Dispose();
            _inputActions = null;
            _gameplayMap = null;
            _uiMap = null;
        }

        // ADR-0001 Rule 7: Null static events to prevent stale delegates
        OnInputStateChanged = null;
        OnSetActionMap = null;
        OnGameplayInputActiveChanged = null;

        if (Instance == this)
            Instance = null;
    }

    // =========================================================================
    // Public Initialization (for BootBootstrap / tests)
    // =========================================================================

    /// <summary>
    /// Verifies that Awake completed successfully. If called before Awake runs
    /// (e.g., in tests where Awake is invoked manually), triggers initialization.
    /// Throws <see cref="InvalidOperationException"/> if the InputActionAsset
    /// could not be built or action maps are missing.
    ///
    /// Idempotent — calling multiple times is safe.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // If Awake hasn't run yet (test scenario), run the build inline
        if (_inputActions == null && !_buildWasAttempted)
        {
            try
            {
                _buildWasAttempted = true;
                BuildInputActions();
                if (_inputActions != null)
                {
                    _inputActions.Enable();
                    _gameplayMap.FindAction("Pause").performed += OnPausePerformed;
                    _uiMap.FindAction("Cancel").performed += OnCancelPerformed;
                    SwitchToGameplayMode();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputManager] Initialize build failed: {ex.Message}");
                _isInitialized = false;
                _currentInputState = InputState.Inactive;
                OnInputStateChanged?.Invoke(InputState.Inactive);
                OnSetActionMap?.Invoke(ActionMap.Inactive);
                throw;
            }
        }

        // Post-condition verification
        if (_inputActions == null)
            throw new InvalidOperationException(
                "[InputManager] Initialize failed: InputActionAsset is null.");
        if (_gameplayMap == null)
            throw new InvalidOperationException(
                "[InputManager] Initialize failed: Gameplay action map is null.");
        if (_uiMap == null)
            throw new InvalidOperationException(
                "[InputManager] Initialize failed: UI action map is null.");
        if (_gameplayMap.FindAction("Pause") == null)
            throw new InvalidOperationException(
                "[InputManager] Initialize failed: Pause action not found in Gameplay map.");
        if (_uiMap.FindAction("Cancel") == null)
            throw new InvalidOperationException(
                "[InputManager] Initialize failed: Cancel action not found in UI map.");

        _isInitialized = true;
    }

    /// <summary>
    /// Test-only: forces the InputManager into an uninitialized state to simulate
    /// a failed Awake/Initialize build. After calling this, <see cref="Initialize"/>
    /// will detect null internal state and throw <see cref="InvalidOperationException"/>.
    /// Used to verify AC-5 error handling (missing/corrupt InputActionAsset).
    ///
    /// Internal visibility — called by integration tests only.
    /// </summary>
    internal void ForceUninitializeForTesting()
    {
        _buildWasAttempted = true;
        _isInitialized = false;

        if (_inputActions != null)
        {
            // Unsubscribe auto-transitions before disposing
            InputAction pauseAction = _gameplayMap?.FindAction("Pause");
            if (pauseAction != null) pauseAction.performed -= OnPausePerformed;

            InputAction cancelAction = _uiMap?.FindAction("Cancel");
            if (cancelAction != null) cancelAction.performed -= OnCancelPerformed;

            _inputActions.Disable();
            _inputActions.Dispose();
        }

        _inputActions = null;
        _gameplayMap = null;
        _uiMap = null;
        _currentInputState = InputState.Inactive;
    }

    // =========================================================================
    // State Machine — Public Static API
    //
    // All state transitions are exposed as static methods so consumers
    // (GameSceneManager, etc.) can call them without an Instance reference.
    // When the InputManager singleton exists, these methods manipulate the
    // real InputActionMaps. When Instance is null (e.g., unit test scenarios),
    // events still fire so input-gating assertions continue to work.
    // =========================================================================

    /// <summary>
    /// Switches to Gameplay input mode. Enables the Gameplay action map,
    /// disables the UI map. Fires both <see cref="OnInputStateChanged"/>
    /// (with <see cref="InputState.Gameplay"/>) and
    /// <see cref="OnSetActionMap"/> (with <see cref="ActionMap.Gameplay"/>).
    /// </summary>
    public static void SwitchToGameplayMode()
    {
        if (Instance != null)
        {
            Instance.ApplyGameplayMode();
        }

        OnInputStateChanged?.Invoke(InputState.Gameplay);
        OnSetActionMap?.Invoke(ActionMap.Gameplay);
        OnGameplayInputActiveChanged?.Invoke(true);
    }

    /// <summary>
    /// Switches to UI (Menu) input mode. Enables the UI action map,
    /// disables the Gameplay map. Fires both events.
    /// </summary>
    public static void SwitchToUIMode()
    {
        if (Instance != null)
        {
            Instance.ApplyUIMode();
        }

        OnInputStateChanged?.Invoke(InputState.Menu);
        OnSetActionMap?.Invoke(ActionMap.UI);
        OnGameplayInputActiveChanged?.Invoke(false);
    }

    /// <summary>
    /// Switches to Rebinding input mode. Disables BOTH maps so no
    /// gameplay or UI input is processed during interactive rebinding.
    /// Fires <see cref="OnInputStateChanged"/> with <see cref="InputState.Rebinding"/>
    /// and <see cref="OnSetActionMap"/> with <see cref="ActionMap.Inactive"/>.
    /// </summary>
    public static void SwitchToRebindingMode()
    {
        if (Instance != null)
        {
            Instance.ApplyRebindingMode();
        }

        OnInputStateChanged?.Invoke(InputState.Rebinding);
        OnSetActionMap?.Invoke(ActionMap.Inactive); // closest legacy match
        OnGameplayInputActiveChanged?.Invoke(false);
    }

    /// <summary>
    /// Switches to Inactive input mode. Disables ALL action maps.
    /// Used by <see cref="GameSceneManager"/> during fragment transitions
    /// per ADR-0004 to prevent player interaction while loading.
    /// Fires <see cref="OnInputStateChanged"/> with <see cref="InputState.Inactive"/>
    /// and <see cref="OnSetActionMap"/> with <see cref="ActionMap.Inactive"/>.
    /// </summary>
    public static void SwitchToInactive()
    {
        if (Instance != null)
        {
            Instance.ApplyInactiveMode();
        }

        OnInputStateChanged?.Invoke(InputState.Inactive);
        OnSetActionMap?.Invoke(ActionMap.Inactive);
        OnGameplayInputActiveChanged?.Invoke(false);
    }

    // =========================================================================
    // State Machine — Private Instance Implementation
    // (called by static methods via Instance, and by internal callbacks)
    // =========================================================================

    private void ApplyGameplayMode()
    {
        _uiMap?.Disable();
        _gameplayMap?.Enable();
        _currentInputState = InputState.Gameplay;
    }

    private void ApplyUIMode()
    {
        _gameplayMap?.Disable();
        _uiMap?.Enable();
        _currentInputState = InputState.Menu;
    }

    private void ApplyRebindingMode()
    {
        _gameplayMap?.Disable();
        _uiMap?.Disable();
        _currentInputState = InputState.Rebinding;
    }

    private void ApplyInactiveMode()
    {
        _gameplayMap?.Disable();
        _uiMap?.Disable();
        _currentInputState = InputState.Inactive;
    }

    // =========================================================================
    // Action Callbacks (Auto-Wired Transitions)
    // =========================================================================

    /// <summary>
    /// Handler for the Pause action on the Gameplay map.
    /// Escape pressed during Gameplay → switch to UI (Menu) mode.
    /// Satisfies AC-2: "Player presses Escape" triggers Pause.
    /// Calls the static API so events fire for all subscribers.
    ///
    /// Internal visibility for integration test access (action wiring verification).
    /// </summary>
    internal void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        // Guard: only process if currently in Gameplay (AC-2 re-entrancy edge case)
        if (_currentInputState != InputState.Gameplay) return;
        SwitchToUIMode();
    }

    /// <summary>
    /// Handler for the Cancel action on the UI map.
    /// Escape pressed during UI → switch to Gameplay mode.
    /// Satisfies AC-3: Escape in UI triggers Cancel → Gameplay mode.
    /// Calls the static API so events fire for all subscribers.
    ///
    /// Internal visibility for integration test access (action wiring verification).
    /// </summary>
    internal void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        // Guard: only process if currently in Menu (AC-3 re-entrancy edge case)
        if (_currentInputState != InputState.Menu) return;
        SwitchToGameplayMode();
    }

    // =========================================================================
    // Programmatic InputActionAsset Construction (ADR-0005)
    // =========================================================================

    /// <summary>
    /// Builds the InputActionAsset with two action maps (Gameplay and UI)
    /// using programmatic construction (ScriptableObject.CreateInstance).
    /// All bindings follow the ADR-0005 action map specification.
    ///
    /// This is called exactly once during Awake() or Initialize().
    /// Exceptions propagate to the caller for error handling (AC-5).
    /// </summary>
    private void BuildInputActions()
    {
        _inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
        _inputActions.name = "GameInputActions";

        BuildGameplayMap();
        BuildUIMap();

        _inputActions.AddActionMap(_gameplayMap);
        _inputActions.AddActionMap(_uiMap);
    }

    /// <summary>
    /// Builds the Gameplay action map with Point, Click, Scroll, RightClick,
    /// and Pause actions per ADR-0005.
    /// </summary>
    private void BuildGameplayMap()
    {
        _gameplayMap = new InputActionMap("Gameplay");

        // Point: Mouse position, continuous Value (Vector2)
        _gameplayMap.AddAction(
            name: "Point",
            type: InputActionType.Value,
            binding: "<Mouse>/position");

        // Click: Left mouse button, discrete Button
        _gameplayMap.AddAction(
            name: "Click",
            type: InputActionType.Button,
            binding: "<Mouse>/leftButton");

        // Scroll: Mouse scroll wheel, continuous Value (Vector2)
        _gameplayMap.AddAction(
            name: "Scroll",
            type: InputActionType.Value,
            binding: "<Mouse>/scroll");

        // RightClick: Right mouse button, discrete Button
        _gameplayMap.AddAction(
            name: "RightClick",
            type: InputActionType.Button,
            binding: "<Mouse>/rightButton");

        // Pause: Escape key, discrete Button
        // Wired to SwitchToUIMode() via OnPausePerformed callback
        _gameplayMap.AddAction(
            name: "Pause",
            type: InputActionType.Button,
            binding: "<Keyboard>/escape");
    }

    /// <summary>
    /// Builds the UI action map with Navigate (WASD+ArrowKeys composite),
    /// Confirm, Cancel, TabNext, and TabPrevious actions per ADR-0005.
    /// </summary>
    private void BuildUIMap()
    {
        _uiMap = new InputActionMap("UI");

        // Navigate: 2D directional input via 2DVector composites
        // Composite 1: WASD
        // Composite 2: Arrow Keys
        InputAction navigateAction = _uiMap.AddAction(
            name: "Navigate",
            type: InputActionType.Value);

        navigateAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        navigateAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        // Confirm: Enter key, discrete Button
        _uiMap.AddAction(
            name: "Confirm",
            type: InputActionType.Button,
            binding: "<Keyboard>/enter");

        // Cancel: Escape key, discrete Button
        // Wired to SwitchToGameplayMode() via OnCancelPerformed callback
        _uiMap.AddAction(
            name: "Cancel",
            type: InputActionType.Button,
            binding: "<Keyboard>/escape");

        // TabNext: Tab key, discrete Button (advance to next UI element)
        _uiMap.AddAction(
            name: "TabNext",
            type: InputActionType.Button,
            binding: "<Keyboard>/tab");

        // TabPrevious: Shift+Tab chord, discrete Button
        // Uses OneModifier composite: Shift as modifier, Tab as binding
        InputAction tabPreviousAction = _uiMap.AddAction(
            name: "TabPrevious",
            type: InputActionType.Button);

        tabPreviousAction.AddCompositeBinding("OneModifier")
            .With("Modifier", "<Keyboard>/shift")
            .With("Binding", "<Keyboard>/tab");
    }
}
