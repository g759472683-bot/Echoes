using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Core interaction detection engine implementing ADR-0005 Physics2D.OverlapPoint polling.
///
/// Owns the central interaction loop for the entire game:
///   - Single Physics2D.OverlapPoint per frame (non-alloc, ContactFilter2D + fixed buffer)
///   - Action Map gating — skips detection when not in Gameplay input mode
///   - Interaction state machine — skips detection during Dragging/ChoicePresenting/Examining/Blocked
///   - Collider lifecycle — creates/destroys BoxCollider2D hitboxes on fragment transitions
///   - Hover enter/exit detection — compares current hit against _lastHovered each frame
///   - Click detection + interaction type dispatch (Story 002)
///
/// Events declared here (ADR-0001 pattern):
///   OnHoverEnter(string objectId)          — S001
///   OnHoverExit(string objectId)           — S001
///   OnInteract(InteractiveObject)          — S002 (Touch/Examine click)
///   OnDragStart(InteractiveObject)         — S002 (declared, handled S003)
///   OnDragComplete(InteractiveObject)      — S002 (declared, handled S003)
///   OnDragCancel(InteractiveObject)        — S002 (declared, handled S003)
///   OnChoiceSelected(string choiceId)      — S002 (fired, handled S004)
///   OnChoiceHover(string choiceId)         — S002 (declared, handled S004)
///   OnRevealObject(GameObject)             — S002
///   OnShowText(TextContent)                — S002
///
/// Lifecycle: Lives on a persistent GameObject in the Game scene.
/// Subscribes to GameSceneManager.OnFragmentTransitioned to rebuild colliders (ADR-0001).
/// </summary>
public class InteractionManager : MonoBehaviour
{
    // =========================================================================
    // Singleton Access
    // =========================================================================

    /// <summary>The singleton instance, set in Awake.</summary>
    public static InteractionManager Instance { get; private set; }

    // =========================================================================
    // Events (ADR-0001 static event pattern)
    // =========================================================================

    /// <summary>Fires when the cursor enters an InteractiveObject's collider.</summary>
    public static event Action<string> OnHoverEnter;

    /// <summary>Fires when the cursor leaves an InteractiveObject's collider.</summary>
    public static event Action<string> OnHoverExit;

    /// <summary>Fires when a Touch or Examine object is clicked (before result dispatch).</summary>
    public static event Action<InteractiveObject> OnInteract;

    /// <summary>Fires when a drag operation begins (Story 003).</summary>
    public static event Action<InteractiveObject> OnDragStart;

    /// <summary>Fires when a drag operation completes (Story 003).</summary>
    public static event Action<InteractiveObject> OnDragComplete;

    /// <summary>Fires when a drag operation is cancelled (Story 003).</summary>
    public static event Action<InteractiveObject> OnDragCancel;

    /// <summary>Fires when a choice is selected (Story 004).</summary>
    public static event Action<string> OnChoiceSelected;

    /// <summary>Fires when a choice option is hovered (Story 004).</summary>
    public static event Action<string> OnChoiceHover;

    /// <summary>Fires when a hidden object is revealed via RevealObject result.</summary>
    public static event Action<GameObject> OnRevealObject;

    /// <summary>Fires when text content should be displayed (from Hover or Touch→ShowText).</summary>
    public static event Action<TextContent> OnShowText;

    // =========================================================================
    // Inspector / Serialized Fields
    // =========================================================================

    [SerializeField] private LayerMask _interactableLayer;
    [SerializeField] private ContactFilter2D _filter;

    // =========================================================================
    // Internal State — Core Detection
    // =========================================================================

    private readonly Collider2D[] _results = new Collider2D[4]; // Fixed buffer, non-alloc
    private Collider2D _lastHovered;
    private readonly List<InteractiveObject> _activeObjects = new List<InteractiveObject>();
    private readonly List<GameObject> _spawnedColliderGOs = new List<GameObject>();

    private InteractionState _currentState = InteractionState.Idle;

    // =========================================================================
    // Internal State — Dependencies
    // =========================================================================

    private InputManager _inputManager;
    private IDataManager _dataManager;
    private IHUD _hud;
    private IMicroAnimationManager _microAnimation;
    private InputAction _pointAction;
    private InputAction _clickAction;
    private Camera _mainCamera;
    private int _interactableLayerIndex;

    // =========================================================================
    // Internal State — Interaction Processing
    // =========================================================================

    private float _hoverTimer;
    private InteractiveObject _hoverTarget;
    private float _lastInteractionTime;
    private string _currentChapterKey;
    private string _currentFragmentId;

    // =========================================================================
    // Internal State — Drag System (Story 003)
    // =========================================================================

    private InteractiveObject _dragTarget;
    private Vector2 _dragStartMousePos;
    private Vector2 _dragStartObjectPos;
    private float _dragTotalDistance;
    private bool _isDragging;
    private LineRenderer _trailRenderer;

    private const float DRAG_TRIGGER_THRESHOLD = 5f;
    private const float DRAG_COMPLETE_THRESHOLD = 30f;
    private const float SPRING_BACK_DURATION = 0.3f;
    private const float DRAG_TRAIL_FADE_COMPLETE = 1.0f;
    private const float DRAG_TRAIL_FADE_CANCEL = 0.3f;

    // Story 004 — Choice panel positioning constants
    private const float CHOICE_PANEL_WIDTH = 300f;
    private const float CHOICE_PANEL_HEIGHT = 200f;
    private const float CHOICE_PANEL_OFFSET = 50f;

    // Test input injection (zero-allocation when disabled)
    internal Vector2 _injectedWorldMousePos = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
    internal bool _injectedClickReleasedThisFrame;

    // Tracks the InteractiveObject that triggered the current interaction (for panel positioning in S004)
    private InteractiveObject _currentInteractiveObject;

    // Exposed for tests
    internal float HoverTimer => _hoverTimer;
    internal InteractiveObject HoverTarget => _hoverTarget;

    // =========================================================================
    // Public Accessors
    // =========================================================================

    /// <summary>The current interaction state (Idle / Active / Dragging / etc.).</summary>
    public InteractionState CurrentState => _currentState;

    /// <summary>The number of non-Hidden InteractiveObjects on the current fragment.</summary>
    public int ActiveObjectCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _activeObjects.Count; i++)
            {
                if (_activeObjects[i].DefaultState != ObjectState.Hidden)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// The Collider2D currently being hovered (or null if cursor is over empty space).
    /// Exposed for test assertions.
    /// </summary>
    public Collider2D LastHovered => _lastHovered;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError(
                $"[InteractionManager] Duplicate instance detected. " +
                $"Existing: {Instance.gameObject.name}, New: {gameObject.name}. Destroying new.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        GameSceneManager.OnFragmentTransitioned += OnFragmentTransitioned;
    }

    void OnDisable()
    {
        GameSceneManager.OnFragmentTransitioned -= OnFragmentTransitioned;
    }

    void Start()
    {
        _inputManager = InputManager.Instance;
        if (_inputManager == null)
            Debug.LogWarning("[InteractionManager] InputManager.Instance is null — input gating will be skipped.");

        // Cache Point and Click actions to avoid per-frame FindAction allocation
        if (_inputManager != null && _inputManager.GameplayMap != null)
        {
            _pointAction = _inputManager.GameplayMap.FindAction("Point");
            _clickAction = _inputManager.GameplayMap.FindAction("Click");
        }

        // Cache Camera.main (avoid per-frame FindGameObjectWithTag in Update)
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            Debug.LogWarning("[InteractionManager] Camera.main is null — screen-to-world conversion will be skipped.");

        // Cache the layer index from the LayerMask (avoid Mathf.Log per collider creation)
        if (_interactableLayer.value != 0)
            _interactableLayerIndex = (int)Mathf.Log(_interactableLayer.value, 2);
        else
            _interactableLayerIndex = LayerMask.NameToLayer("Interactable");
    }

    /// <summary>
    /// Injects dependencies. Called by BootBootstrap in production
    /// or by test fixtures in unit/integration tests.
    /// </summary>
    public void Initialize(
        IDataManager dataManager,
        IHUD hud = null,
        IMicroAnimationManager microAnimation = null)
    {
        _dataManager = dataManager;
        _hud = hud;
        _microAnimation = microAnimation;
    }

    /// <summary>
    /// Per-frame detection loop (ADR-0005).
    ///
    /// Guard order:
    ///   1. Action Map gate — return if not Gameplay (input system level)
    ///   2. Drag active update — runs every frame during drag (BEFORE state gate, Story 003)
    ///   3. State machine gate — return if ChoicePresenting/Examining/Blocked/Idle
    ///   4. Point action + camera null guard
    ///   5. Physics2D.OverlapPoint — single non-alloc query
    ///   6. Hover change detection — compare hit vs _lastHovered
    ///   7. Hover stay timer — for Hover-type objects (0.5s delay)
    ///   8. Click detection — for Touch/Examine/Drag objects
    /// </summary>
    void Update()
    {
        // Guard 1: Action Map gating (ADR-0005 — only process in Gameplay mode)
        if (_inputManager != null && _inputManager.CurrentInputState != InputState.Gameplay)
            return;

        // Drag active update — runs every frame during drag (Story 003, BEFORE state gate)
        if (_currentState == InteractionState.Dragging)
        {
            HandleDragUpdate();
            return;
        }

        // Guard 2: State machine — skip detection in non-Active states
        if (_currentState is InteractionState.ChoicePresenting or
                                 InteractionState.Examining or
                                 InteractionState.Blocked or
                                 InteractionState.Idle)
            return;

        // Guard 3: Point action must be cached and camera must be available
        if (_pointAction == null || _mainCamera == null)
            return;

        // Read mouse position via Input System (not Legacy Input — ADR-0005 forbidden)
        Vector2 mousePos = _pointAction.ReadValue<Vector2>();
        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(mousePos);

        // Single non-alloc OverlapPoint (ADR-0005: no OverlapPointAll, no Raycaster)
        int hitCount = Physics2D.OverlapPoint(worldPos, _filter, _results);

        // Pick highest SortOrder when multiple colliders overlap (QA AC-1 edge case)
        Collider2D hit = null;
        if (hitCount > 0)
        {
            hit = _results[0];
            if (hitCount > 1)
            {
                int bestOrder = int.MinValue;
                for (int i = 0; i < hitCount; i++)
                {
                    var ir = _results[i].GetComponent<InteractableRef>();
                    int order = ir != null ? ir.SortOrder : 0;
                    if (order > bestOrder)
                    {
                        bestOrder = order;
                        hit = _results[i];
                    }
                }
            }
        }

        // Hover change detection
        if (hit != _lastHovered)
        {
            if (_lastHovered != null)
                OnHoverExitHandler(_lastHovered);

            // Reset hover timer on target change
            _hoverTarget = null;
            _hoverTimer = 0f;

            if (hit != null)
                OnHoverEnterHandler(hit);

            _lastHovered = hit;
        }
        else if (hit != null)
        {
            // Hover stay — for Hover-type objects with 0.5s delay
            OnHoverStayHandler(hit);
        }

        // Click detection — process interaction on click
        if (_clickAction != null && _clickAction.WasPressedThisFrame())
        {
            // Check for Drag-type objects first (Story 003)
            if (hit != null)
            {
                var refHolder = hit.GetComponent<InteractableRef>();
                if (refHolder != null && refHolder.InteractionType == InteractionType.Drag)
                {
                    InteractiveObject obj = FindActiveObject(refHolder.ObjectId);
                    if (obj != null && CanInteract(obj))
                        StartDrag(obj);
                    return; // Don't fall through to ProcessClick for drag objects
                }
            }
            ProcessClick(hit);
        }
    }

    // =========================================================================
    // Click Processing
    // =========================================================================

    /// <summary>
    /// Processes a click at the currently hovered collider.
    /// Routes to the appropriate handler based on InteractionType.
    /// </summary>
    private void ProcessClick(Collider2D hit)
    {
        if (hit == null)
            return;

        var refHolder = hit.GetComponent<InteractableRef>();
        if (refHolder == null)
            return;

        InteractiveObject obj = FindActiveObject(refHolder.ObjectId);
        if (obj == null)
            return;

        if (!CanInteract(obj))
            return;

        ProcessInteraction(obj);
    }

    /// <summary>
    /// Routes interaction processing by InteractionType.
    /// Drag is handled by Story 003 — this method skips it.
    /// </summary>
    private void ProcessInteraction(InteractiveObject obj)
    {
        switch (obj.Type)
        {
            case InteractionType.Touch:
                HandleTouch(obj);
                break;
            case InteractionType.Hover:
                HandleHover(obj);
                break;
            case InteractionType.Examine:
                HandleExamine(obj);
                break;
            case InteractionType.Drag:
                // Drag handled by Story 003 — do not fire OnInteract
                break;
        }
    }

    // =========================================================================
    // Touch Handler
    // =========================================================================

    /// <summary>
    /// Handles a Touch-type interaction: fires OnInteract, then dispatches the result.
    /// Enforces a 0.3s debounce to prevent rapid repeated clicks on the same object.
    /// </summary>
    private async void HandleTouch(InteractiveObject obj)
    {
        try
        {
            // Debounce — GDD rule: prevent rapid repeated clicks
            if (Time.time - _lastInteractionTime < 0.3f)
                return;
            _lastInteractionTime = Time.time;

            // Fire event BEFORE dispatch so subscribers can react
            OnInteract?.Invoke(obj);

            // Dispatch the interaction result
            if (obj.OnInteract != null)
            {
                _currentInteractiveObject = obj;
                await DispatchInteractionResult(obj.OnInteract);
                if (this == null) return;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // =========================================================================
    // Hover Handler
    // =========================================================================

    /// <summary>
    /// Handles a Hover-type interaction: fires OnInteract and OnShowText.
    /// Hover-type objects trigger on click (not on hover duration).
    /// The 0.5s hover delay for showing text is handled in OnHoverStayHandler.
    /// </summary>
    private async void HandleHover(InteractiveObject obj)
    {
        try
        {
            OnInteract?.Invoke(obj);
            if (obj.OnInteract != null)
            {
                _currentInteractiveObject = obj;
                await DispatchInteractionResult(obj.OnInteract);
                if (this == null) return;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // =========================================================================
    // Examine Handler
    // =========================================================================

    /// <summary>
    /// Handles an Examine-type interaction: enters Examining state,
    /// plays zoom-in animation, waits for Cancel, then plays zoom-out.
    /// All other interaction detection is suppressed during examination.
    /// </summary>
    private async void HandleExamine(InteractiveObject obj)
    {
        try
        {
            _currentState = InteractionState.Examining;
            OnInteract?.Invoke(obj);

            // Play zoom-in animation
            if (_microAnimation != null)
            {
                await _microAnimation.PlayTriggered("examine_zoom_in");
                if (this == null) return;
            }

            // Wait for Cancel input to exit
            await WaitForCancelInput();
            if (this == null) return;

            // Play zoom-out and restore state
            if (_microAnimation != null)
            {
                await _microAnimation.PlayTriggered("examine_zoom_out");
                if (this == null) return;
            }

            _currentState = InteractionState.Active;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Waits until the Cancel action (Escape) is pressed.
    /// Uses Keyboard.current polling — the UIMap Cancel action is disabled during
    /// Gameplay mode, so subscribing to InputAction callbacks would hang forever.
    /// This is a pragmatic short-duration poll (Examining state only).
    /// </summary>
    private async Task WaitForCancelInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            // No keyboard connected — auto-exit after 1s safety fallback
            await Task.Delay(1000);
            return;
        }

        // Poll escapeKey each frame with a timeout guard
        float elapsed = 0f;
        const float maxWait = 30f; // 30s hard timeout

        while (elapsed < maxWait)
        {
            await Task.Yield();
            if (this == null) return;

            elapsed += Time.deltaTime;

            if (keyboard.escapeKey.wasPressedThisFrame)
                return;
        }

        Debug.LogWarning("[InteractionManager] WaitForCancelInput timed out after 30s.");
    }

    // =========================================================================
    // Drag Handler (Story 003)
    // =========================================================================

    /// <summary>
    /// Initiates a drag operation. Sets state to Dragging.
    /// OnDragStart fires only after the 5px threshold is crossed in HandleDragUpdate.
    /// </summary>
    private void StartDrag(InteractiveObject obj)
    {
        _dragTarget = obj;
        _dragStartMousePos = _pointAction != null ? _pointAction.ReadValue<Vector2>() : Vector2.zero;
        // Find the collider GameObject to get the world position of the object
        GameObject colGO = FindColliderGO(obj.ObjectId);
        _dragStartObjectPos = colGO != null ? (Vector2)colGO.transform.position : obj.HitboxCenter;
        _dragTotalDistance = 0f;
        _isDragging = false;

        _currentState = InteractionState.Dragging;
    }

    /// <summary>
    /// Called every frame during drag (from Update when _currentState == Dragging).
    /// Before 5px threshold: checks if drag should activate.
    /// After 5px threshold: moves object with mouse delta and renders drag trail.
    /// On mouse release: calls CompleteDrag.
    /// </summary>
    private void HandleDragUpdate()
    {
        if (_dragTarget == null)
        {
            // If CompleteDrag was already called and is running async (SpringBack, dispatch),
            // _isDragging will be false — let CompleteDrag finish and set the state.
            if (!_isDragging)
                return;

            // Safety net: drag target disappeared during active drag
            _currentState = InteractionState.Active;
            return;
        }

        // Use injected test input if available, otherwise real input
        Vector2 currentMousePos;
        if (_injectedWorldMousePos.x != float.NegativeInfinity)
        {
            currentMousePos = _injectedWorldMousePos;
        }
        else
        {
            currentMousePos = _pointAction != null ? _pointAction.ReadValue<Vector2>() : Vector2.zero;
        }
        Vector2 worldMousePos = _mainCamera != null ? _mainCamera.ScreenToWorldPoint(currentMousePos) : currentMousePos;
        Vector2 dragStartWorldPos = _mainCamera != null ? _mainCamera.ScreenToWorldPoint(_dragStartMousePos) : _dragStartMousePos;
        Vector2 delta = worldMousePos - dragStartWorldPos;

        if (!_isDragging)
        {
            // Check 5px trigger threshold
            if (delta.magnitude >= DRAG_TRIGGER_THRESHOLD)
            {
                _isDragging = true;
                OnDragStart?.Invoke(_dragTarget);
            }
            else
            {
                // Under threshold — check for mouse release (cancel drag if under 5px)
                bool clickReleased;
                if (_injectedWorldMousePos.x != float.NegativeInfinity)
                {
                    clickReleased = _injectedClickReleasedThisFrame;
                    _injectedClickReleasedThisFrame = false;
                }
                else
                {
                    clickReleased = _clickAction != null && _clickAction.WasReleasedThisFrame();
                }
                if (clickReleased)
                {
                    OnDragCancel?.Invoke(_dragTarget);
                    _dragTarget = null;
                    _currentState = InteractionState.Active;
                }
                return;
            }
        }

        // Move object to follow mouse delta
        Vector2 newPos = _dragStartObjectPos + delta;
        GameObject colGO = FindColliderGO(_dragTarget.ObjectId);
        if (colGO != null)
            colGO.transform.position = newPos;

        _dragTotalDistance = delta.magnitude;

        // Render drag trail
        RenderDragTrail(_dragStartObjectPos, newPos);

        // Check mouse release
        bool released;
        if (_injectedWorldMousePos.x != float.NegativeInfinity)
        {
            released = _injectedClickReleasedThisFrame;
            _injectedClickReleasedThisFrame = false;
        }
        else
        {
            released = _clickAction != null && _clickAction.WasReleasedThisFrame();
        }
        if (released)
        {
            CompleteDrag();
        }
    }

    /// <summary>
    /// Completes the drag operation. If distance >= 30px, fires OnDragComplete
    /// and dispatches OnInteract. Otherwise, springs back to original position.
    /// </summary>
    private async void CompleteDrag()
    {
        try
        {
            _isDragging = false;
            InteractiveObject target = _dragTarget;
            _dragTarget = null;
            // _currentState is still Dragging — prevents re-entry into detection loop

            if (_dragTotalDistance >= DRAG_COMPLETE_THRESHOLD)
            {
                // Drag complete — fire events and dispatch interaction result
                OnDragComplete?.Invoke(target);
                FadeDragTrail(DRAG_TRAIL_FADE_COMPLETE);

                if (target.OnInteract != null)
                {
                    _currentInteractiveObject = target;
                    await DispatchInteractionResult(target.OnInteract);
                    if (this == null) return;
                }
            }
            else
            {
                // Spring back — incomplete drag
                OnDragCancel?.Invoke(target);
                FadeDragTrail(DRAG_TRAIL_FADE_CANCEL);

                GameObject colGO = FindColliderGO(target.ObjectId);
                if (colGO != null)
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    StartCoroutine(SpringBackCo(colGO.transform, colGO.transform.position, _dragStartObjectPos, SPRING_BACK_DURATION, () => tcs.TrySetResult(true)));
                    await tcs.Task;
                    if (this == null) return;
                }
            }

            // Only restore Active if no external system changed state during async gap
            if (_currentState == InteractionState.Dragging)
                _currentState = InteractionState.Active;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Springs a transform back to a target position using EaseOutCubic easing.
    /// f(t) = 1 - (1 - t)^3. Coroutine — zero per-frame GC.
    /// </summary>
    private System.Collections.IEnumerator SpringBackCo(Transform target, Vector2 from, Vector2 to, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            if (target != null)
                target.position = Vector2.Lerp(from, to, ease);
            else
                break;
            yield return null;
        }
        if (target != null)
            target.position = to;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Renders a drag trail from the object's original position to its current position.
    /// Creates a LineRenderer if one doesn't exist yet. Reuses it across frames.
    /// </summary>
    private void RenderDragTrail(Vector2 from, Vector2 to)
    {
        if (_trailRenderer == null)
        {
            var go = new GameObject("DragTrail");
            go.transform.SetParent(transform);
            _trailRenderer = go.AddComponent<LineRenderer>();
            _trailRenderer.material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
            _trailRenderer.startColor = new Color(0.3f, 0.2f, 0.2f, 0.5f); // Ink wash color
            _trailRenderer.endColor = new Color(0.3f, 0.2f, 0.2f, 0.1f);
            _trailRenderer.startWidth = 0.03f;
            _trailRenderer.endWidth = 0.01f;
            _trailRenderer.positionCount = 2;
        }
        _trailRenderer.SetPosition(0, from);
        _trailRenderer.SetPosition(1, to);
    }

    /// <summary>
    /// Fades the drag trail to transparent over the specified duration, then destroys it.
    /// If a new drag starts during fade, the old trail is destroyed immediately.
    /// Coroutine — zero per-frame GC.
    /// </summary>
    private System.Collections.IEnumerator FadeDragTrailCo(float duration)
    {
        if (_trailRenderer == null)
            yield break;

        LineRenderer trail = _trailRenderer;
        _trailRenderer = null; // Detach so new drags create a new trail

        float elapsed = 0f;
        Color startColor = trail.startColor;
        Color endColor = trail.endColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (trail != null)
            {
                trail.startColor = Color.Lerp(startColor, Color.clear, t);
                trail.endColor = Color.Lerp(endColor, Color.clear, t);
            }
            else
            {
                break;
            }
            yield return null;
        }

        if (trail != null)
            Destroy(trail.gameObject);
    }

    private void FadeDragTrail(float duration)
    {
        StartCoroutine(FadeDragTrailCo(duration));
    }

    // =========================================================================
    // Interaction Result Dispatch
    // =========================================================================

    /// <summary>
    /// Dispatches an InteractionResult after OnInteract has fired.
    /// Each ResultType maps to a specific system call (HUD, MicroAnimation, SceneManager).
    /// </summary>
    private async Task DispatchInteractionResult(InteractionResult result)
    {
        switch (result.ResultType)
        {
            case ResultType.PlayAnimation:
                if (_microAnimation != null)
                {
                    await _microAnimation.PlayTriggered(result.AnimationId);
                    if (this == null) return;
                }
                else
                    Debug.LogWarning(
                        $"[InteractionManager] PlayAnimation '{result.AnimationId}' skipped — " +
                        "MicroAnimationManager not injected.");
                break;

            case ResultType.ShowText:
                OnShowText?.Invoke(result.TextContent);
                if (_hud != null)
                    _hud.ShowFragmentText(result.TextContent, Vector2.zero); // Position set by HUD (cursor + 20px)
                else
                    Debug.LogWarning(
                        "[InteractionManager] ShowText skipped — HUD not injected.");
                break;

            case ResultType.PresentChoice:
                await HandlePresentChoice(result.ChoiceGroupId);
                if (this == null) return;
                break;

            case ResultType.TransitionToFragment:
                _currentState = InteractionState.Blocked;
                if (GameSceneManager.Instance != null)
                {
                    await GameSceneManager.Instance
                        .TransitionToFragmentAsync(
                            GameSceneManager.Instance.CurrentChapterKey,
                            result.TargetFragmentId);
                    if (this == null) return;
                }
                // State restored by OnFragmentTransitioned callback
                break;

            case ResultType.RevealObject:
            {
                InteractiveObject targetObj = null;
                for (int i = 0; i < _activeObjects.Count; i++)
                {
                    if (_activeObjects[i].ObjectId == result.TargetObjectId)
                    {
                        targetObj = _activeObjects[i];
                        break;
                    }
                }
                if (targetObj != null)
                {
                    EnableObjectCollider(targetObj);
                    OnRevealObject?.Invoke(FindColliderGO(targetObj.ObjectId));
                    if (_microAnimation != null)
                    {
                        await _microAnimation.PlayTriggered("object_appear");
                        if (this == null) return;
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[InteractionManager] RevealObject: target '{result.TargetObjectId}' " +
                        "not found in _activeObjects.");
                }
                break;
            }
        }
    }

    // =========================================================================
    // Choice Flow (Story 004)
    // =========================================================================

    /// <summary>
    /// Handles the full PresentChoice flow: evaluates available choices, auto-applies
    /// single-option groups, shows the choice panel, and routes selection/cancel.
    /// </summary>
    private async Task HandlePresentChoice(string choiceGroupId)
    {
        try
        {
            var choiceGroup = FindChoiceGroup(choiceGroupId);
            if (choiceGroup == null)
            {
                Debug.LogWarning(
                    $"[InteractionManager] PresentChoice: ChoiceGroup '{choiceGroupId}' " +
                    "not found on current fragment.");
                return;
            }

            if (_hud == null)
            {
                Debug.LogWarning(
                    "[InteractionManager] PresentChoice skipped — HUD not injected.");
                return;
            }

            // All choices are currently available — ChoiceCondition evaluation is future work
            Choice[] available = choiceGroup.Choices ?? System.Array.Empty<Choice>();

            // Auto-apply: single-choice with MaxSelections=1 — skip panel (AC-4)
            // No panel shown → no HideChoicePanel needed, no mode switch needed.
            // State stays Active throughout (no ChoicePresenting gap).
            if (choiceGroup.MaxSelections == 1 && available.Length == 1)
            {
                OnChoiceSelected?.Invoke(available[0].ChoiceId);
                ChangeTracker.Instance?.ApplyChanges(
                    _currentFragmentId,
                    available[0].ChoiceId,
                    available[0].ContentChanges?.ToArray());
                return;
            }

            // No available choices — no panel, no changes (AC-4 edge case)
            if (available.Length == 0)
            {
                Debug.LogWarning(
                    $"[InteractionManager] ChoiceGroup '{choiceGroupId}' has 0 available options.");
                return;
            }

            // Show choice panel
            _currentState = InteractionState.ChoicePresenting;
            InputManager.SwitchToUIMode();

            Vector2 panelPos = CalculateChoicePanelPosition(_currentInteractiveObject);

            string selectedId = await _hud.ShowChoicePanel(choiceGroup, panelPos);
            if (this == null) return;

            if (selectedId == null)
            {
                // Escape cancel — "do nothing" is valid (AC-3)
                HandleChoiceCancelled();
                return;
            }

            // Find the chosen option
            Choice chosen = null;
            for (int i = 0; i < available.Length; i++)
            {
                if (available[i].ChoiceId == selectedId)
                {
                    chosen = available[i];
                    break;
                }
            }

            if (chosen == null)
            {
                Debug.LogWarning(
                    $"[InteractionManager] ChoiceId '{selectedId}' not found in group '{choiceGroupId}'.");
                HandleChoiceCancelled();
                return;
            }

            await ApplyChoice(chosen, _currentFragmentId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Applies the selected choice: fires OnChoiceSelected, records changes,
    /// hides the panel, and restores the interaction state (AC-2).
    /// </summary>
    private async Task ApplyChoice(Choice choice, string fragmentId)
    {
        OnChoiceSelected?.Invoke(choice.ChoiceId);

        ChangeTracker.Instance?.ApplyChanges(
            fragmentId,
            choice.ChoiceId,
            choice.ContentChanges?.ToArray());

        if (_hud != null)
            await _hud.HideChoicePanel(0.3f);
        if (this == null) return;

        InputManager.SwitchToGameplayMode();
        _currentState = InteractionState.Active;
    }

    /// <summary>
    /// Cancels the current choice: closes the panel, restores state.
    /// No ContentChanges applied — "no choice" is a valid outcome (AC-3).
    /// </summary>
    private void HandleChoiceCancelled()
    {
        // Fire-and-forget hide — no await needed for cancel
        if (_hud != null)
            _ = _hud.HideChoicePanel(0.3f);

        InputManager.SwitchToGameplayMode();
        _currentState = InteractionState.Active;
    }

    /// <summary>
    /// Calculates the screen-space position for the choice panel relative to the
    /// anchor object. Prefers right side, falls back to below, then screen center.
    /// </summary>
    private Vector2 CalculateChoicePanelPosition(InteractiveObject anchor)
    {
        if (_mainCamera == null || anchor == null)
            return new Vector2(Screen.width / 2f, Screen.height / 2f);

        GameObject colGO = FindColliderGO(anchor.ObjectId);
        Vector3 worldPos = colGO != null ? colGO.transform.position : (Vector3)anchor.HitboxCenter;
        Vector2 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        // Prefer right side
        Vector2 rightPos = screenPos + new Vector2(CHOICE_PANEL_OFFSET, 0f);
        if (rightPos.x + CHOICE_PANEL_WIDTH <= Screen.width)
            return rightPos;

        // Fallback below
        Vector2 belowPos = screenPos + new Vector2(0f, -CHOICE_PANEL_OFFSET);
        if (belowPos.y - CHOICE_PANEL_HEIGHT >= 0)
            return belowPos;

        // Fallback center
        return new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    // =========================================================================
    // Fragment Transition Handler
    // =========================================================================

    /// <summary>
    /// Subscribed to GameSceneManager.OnFragmentTransitioned.
    /// Clears all previous colliders and rebuilds BoxCollider2D hitboxes for the
    /// new fragment's InteractiveObjects (ADR-0005, GDD scroll-interaction-system §3.2).
    ///
    /// Hidden objects (DefaultState == Hidden) get no collider — they are revealed
    /// later via story events (ChangeTracker.ApplyChanges → SetObjectState or RevealObject).
    /// </summary>
    public void OnFragmentTransitioned(string chapterKey, string fragmentId)
    {
        ClearAllColliders();
        _activeObjects.Clear();
        _lastHovered = null;
        _hoverTarget = null;
        _hoverTimer = 0f;
        _lastInteractionTime = 0f;
        _currentChapterKey = chapterKey;
        _currentFragmentId = fragmentId;

        if (_dataManager == null)
        {
            Debug.LogError("[InteractionManager] DataManager is null — cannot rebuild colliders.");
            _currentState = InteractionState.Idle;
            return;
        }

        // Synchronous cache lookup: fragment data was already loaded by GameSceneManager
        // during the transition. This reads the in-memory cache — no async needed.
        MemoryFragment fragment = _dataManager.GetCachedFragment(chapterKey, fragmentId);
        if (fragment == null || fragment.InteractiveObjects == null || fragment.InteractiveObjects.Length == 0)
        {
            _currentState = InteractionState.Idle;
            return;
        }

        foreach (var obj in fragment.InteractiveObjects)
        {
            // Always register in _activeObjects — Hidden objects need to be findable
            // for RevealObject lookups even though they have no collider.
            _activeObjects.Add(obj);

            if (obj.DefaultState == ObjectState.Hidden)
                continue;

            var go = new GameObject($"Interactable_{obj.ObjectId}");
            go.transform.position = obj.HitboxCenter;
            go.layer = _interactableLayerIndex;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = obj.HitboxSize;
            col.isTrigger = true;

            // Attach a reference component so tests can map Collider2D → ObjectId
            var refHolder = go.AddComponent<InteractableRef>();
            refHolder.ObjectId = obj.ObjectId;
            refHolder.InteractionType = obj.Type;
            refHolder.SortOrder = obj.SortOrder;

            _spawnedColliderGOs.Add(go);
        }

        _currentState = InteractionState.Active;
    }

    // =========================================================================
    // Hover Handlers
    // =========================================================================

    /// <summary>
    /// Called when the cursor enters a new collider. Fires <see cref="OnHoverEnter"/>.
    /// </summary>
    private void OnHoverEnterHandler(Collider2D hit)
    {
        var refHolder = hit.GetComponent<InteractableRef>();
        if (refHolder != null)
            OnHoverEnter?.Invoke(refHolder.ObjectId);
    }

    /// <summary>
    /// Called each frame the cursor stays over the same collider.
    /// For Hover-type objects, accumulates time and fires OnShowText after 0.5s.
    /// </summary>
    private void OnHoverStayHandler(Collider2D hit)
    {
        var refHolder = hit.GetComponent<InteractableRef>();
        if (refHolder == null)
            return;

        // Only Hover-type objects get the 0.5s delayed text reveal
        if (refHolder.InteractionType != InteractionType.Hover)
            return;

        InteractiveObject obj = FindActiveObject(refHolder.ObjectId);
        if (obj == null)
            return;

        if (_hoverTarget != obj)
        {
            _hoverTarget = obj;
            _hoverTimer = 0f;
        }

        _hoverTimer += Time.deltaTime;
        if (_hoverTimer >= 0.5f && obj.OnInteract != null)
        {
            OnShowText?.Invoke(obj.OnInteract.TextContent);
            if (_hud != null)
                _hud.ShowFragmentText(obj.OnInteract.TextContent, Vector2.zero);
            _hoverTimer = 0f; // Fire once per hover session
        }
    }

    /// <summary>
    /// Called when the cursor leaves a previously hovered collider. Fires <see cref="OnHoverExit"/>.
    /// </summary>
    private void OnHoverExitHandler(Collider2D previous)
    {
        var refHolder = previous?.GetComponent<InteractableRef>();
        if (refHolder != null)
            OnHoverExit?.Invoke(refHolder.ObjectId);
    }

    // =========================================================================
    // Object State Helpers
    // =========================================================================

    /// <summary>
    /// Returns true if the object can be interacted with.
    /// Disabled objects have colliders but do not respond to interaction.
    /// Hidden objects have no colliders and cannot be reached here.
    /// </summary>
    private bool CanInteract(InteractiveObject obj)
    {
        if (obj.DefaultState == ObjectState.Disabled)
            return false;
        return true;
    }

    /// <summary>
    /// Creates a collider for a previously Hidden object (RevealObject).
    /// The object's DefaultState is changed to Active.
    /// </summary>
    private void EnableObjectCollider(InteractiveObject obj)
    {
        var go = new GameObject($"Interactable_{obj.ObjectId}");
        go.transform.position = obj.HitboxCenter;
        go.layer = _interactableLayerIndex;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = obj.HitboxSize;
        col.isTrigger = true;

        var refHolder = go.AddComponent<InteractableRef>();
        refHolder.ObjectId = obj.ObjectId;
        refHolder.InteractionType = obj.Type;
        refHolder.SortOrder = obj.SortOrder;

        obj.DefaultState = ObjectState.Active;
        _spawnedColliderGOs.Add(go);
    }

    /// <summary>
    /// Finds an active InteractiveObject by ObjectId in the current fragment's object list.
    /// Uses a for loop to avoid lambda GC allocation in the hot path.
    /// </summary>
    private InteractiveObject FindActiveObject(string objectId)
    {
        for (int i = 0; i < _activeObjects.Count; i++)
        {
            if (_activeObjects[i].ObjectId == objectId)
                return _activeObjects[i];
        }
        return null;
    }

    /// <summary>
    /// Finds the collider GameObject for a given ObjectId.
    /// Uses a for loop to avoid lambda GC allocation in the hot path (Story 003 drag).
    /// </summary>
    private GameObject FindColliderGO(string objectId)
    {
        for (int i = 0; i < _spawnedColliderGOs.Count; i++)
        {
            var go = _spawnedColliderGOs[i];
            if (go != null && go.name == $"Interactable_{objectId}")
                return go;
        }
        return null;
    }

    /// <summary>
    /// Looks up a ChoiceGroup from the current fragment's data.
    /// </summary>
    private ChoiceGroup FindChoiceGroup(string groupId)
    {
        if (_dataManager == null || _currentChapterKey == null || _currentFragmentId == null)
            return null;
        MemoryFragment fragment = _dataManager.GetCachedFragment(_currentChapterKey, _currentFragmentId);
        return fragment?.GetChoiceGroup(groupId);
    }

    // =========================================================================
    // Collider Lifecycle
    // =========================================================================

    /// <summary>
    /// Destroys all GameObjects created for the current fragment's colliders.
    /// Called before rebuilding for a new fragment.
    /// </summary>
    private void ClearAllColliders()
    {
        foreach (var go in _spawnedColliderGOs)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedColliderGOs.Clear();

        // Clean up drag state (Story 003)
        if (_trailRenderer != null)
        {
            Destroy(_trailRenderer.gameObject);
            _trailRenderer = null;
        }
        _dragTarget = null;
        _isDragging = false;
    }

    // =========================================================================
    // State Machine — Public API (for external systems)
    // =========================================================================

    /// <summary>
    /// Sets the interaction state to Blocked (e.g., during text display or transitions).
    /// All interaction detection is suppressed until the state is changed back.
    /// </summary>
    public void SetBlocked()
    {
        _currentState = InteractionState.Blocked;
    }

    /// <summary>
    /// Restores the interaction state to Active after a blocking operation completes.
    /// Only transitions from Blocked (or Idle) to Active — never overrides Dragging/Examining.
    /// </summary>
    public void SetActive()
    {
        if (_currentState is InteractionState.Blocked or InteractionState.Idle)
            _currentState = InteractionState.Active;
    }

    /// <summary>
    /// Enters Dragging state (Story 003).
    /// </summary>
    public void SetDragging()
    {
        _currentState = InteractionState.Dragging;
    }

    /// <summary>
    /// Enters ChoicePresenting state (Story 004).
    /// </summary>
    public void SetChoicePresenting()
    {
        _currentState = InteractionState.ChoicePresenting;
    }

    // =========================================================================
    // ADR-0001 Rule 7: Static Event Cleanup
    // =========================================================================

    void OnDestroy()
    {
        ClearAllColliders();

        OnHoverEnter = null;
        OnHoverExit = null;
        OnInteract = null;
        OnDragStart = null;
        OnDragComplete = null;
        OnDragCancel = null;
        OnChoiceSelected = null;
        OnChoiceHover = null;
        OnRevealObject = null;
        OnShowText = null;

        if (Instance == this)
            Instance = null;
    }
}

/// <summary>
/// Lightweight component attached to each interactable collider GameObject.
/// Stores the ObjectId and metadata so the InteractionManager can resolve
/// Collider2D hits back to InteractiveObject data without a Dictionary lookup.
/// </summary>
public class InteractableRef : MonoBehaviour
{
    /// <summary>Matches InteractiveObject.ObjectId.</summary>
    public string ObjectId;

    /// <summary>Interaction type from the source InteractiveObject.</summary>
    public InteractionType InteractionType;

    /// <summary>Sort order for disambiguating overlapping colliders.</summary>
    public int SortOrder;
}
