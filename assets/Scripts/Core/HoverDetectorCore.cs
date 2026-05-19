using UnityEngine;

/// <summary>
/// Core hover detection logic — testable without Unity runtime.
///
/// Polls mouse position each frame, performs a single overlap check against
/// the Interactable physics layer, and fires OnHoverEnter/OnHoverExit events
/// when the hovered object changes. Click events are externally triggered
/// (from InputManager's Click action) and carry the hovered object ID.
///
/// Input state gating: detection and events are suppressed when the
/// InputState is not Gameplay (e.g., Menu, Rebinding, Inactive).
///
/// Events declared here (ADR-0001):
///   OnHoverEnter(string objectId, Vector2 screenPos)
///   OnHoverExit(string objectId)
///   OnClick(string objectId, Vector2 screenPos)
/// </summary>
public class HoverDetectorCore
{
    // =========================================================================
    // Events (ADR-0001 static event pattern)
    // =========================================================================

    /// <summary>Fires when the mouse enters a new interactable object.</summary>
    public static event System.Action<string, Vector2> OnHoverEnter;

    /// <summary>Fires when the mouse leaves the previously hovered object.</summary>
    public static event System.Action<string> OnHoverExit;

    /// <summary>
    /// Fires when a click occurs on a hovered object.
    /// Parameter: objectId, screen position.
    /// </summary>
    public static event System.Action<string, Vector2> OnClick;

    // =========================================================================
    // Dependencies
    // =========================================================================

    private readonly IMousePositionProvider _mouse;
    private readonly ICameraProvider _camera;
    private readonly IPhysics2DProvider _physics;

    // =========================================================================
    // Internal State
    // =========================================================================

    private string _lastHoveredId;
    private InputState _currentInputState = InputState.Gameplay;

    /// <summary>The ID of the currently hovered object, or null if none.</summary>
    public string CurrentHoveredId => _lastHoveredId;

    /// <summary>The current input state used for gating.</summary>
    public InputState CurrentInputState => _currentInputState;

    // =========================================================================
    // Construction
    // =========================================================================

    public HoverDetectorCore(
        IMousePositionProvider mouse,
        ICameraProvider camera,
        IPhysics2DProvider physics)
    {
        _mouse = mouse;
        _camera = camera;
        _physics = physics;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Updates the input state for gating. When switching away from Gameplay,
    /// any active hover is cleared and OnHoverExit is fired.
    /// </summary>
    public void SetInputState(InputState state)
    {
        if (_currentInputState == state) return;

        _currentInputState = state;

        // Clear hover when leaving Gameplay
        if (state != InputState.Gameplay && _lastHoveredId != null)
        {
            var exited = _lastHoveredId;
            _lastHoveredId = null;
            OnHoverExit?.Invoke(exited);
        }
    }

    /// <summary>
    /// Performs one frame of hover detection: reads mouse position,
    /// converts to world space, checks for overlap, and fires enter/exit
    /// events if the hovered object changed.
    ///
    /// Call once per frame from Update(). Safe to call in any input state —
    /// detection is gated to Gameplay only.
    /// </summary>
    public void UpdateHover()
    {
        if (_currentInputState != InputState.Gameplay) return;

        var mousePos = _mouse.GetMousePosition();
        var worldPos = _camera.ScreenToWorldPoint(mousePos);
        var hits = _physics.OverlapPoint(worldPos);
        var current = hits.Length > 0 ? hits[0] : null;

        if (current != _lastHoveredId)
        {
            if (_lastHoveredId != null)
                OnHoverExit?.Invoke(_lastHoveredId);

            if (current != null)
                OnHoverEnter?.Invoke(current, mousePos);

            _lastHoveredId = current;
        }
    }

    /// <summary>
    /// Processes a click action from the input system. Fires OnClick if
    /// currently hovering an object and in Gameplay state.
    /// Call from InputManager's Click action callback.
    /// </summary>
    public void ProcessClick()
    {
        if (_currentInputState != InputState.Gameplay) return;

        var mousePos = _mouse.GetMousePosition();
        if (_lastHoveredId != null)
        {
            OnClick?.Invoke(_lastHoveredId, mousePos);
        }
    }

    // =========================================================================
    // Test Support
    // =========================================================================

    /// <summary>
    /// Resets all static events to null. Must be called in [TearDown]
    /// per ADR-0001 Rule 8 to prevent cross-test leakage.
    /// </summary>
    public static void ResetStaticEvents()
    {
        OnHoverEnter = null;
        OnHoverExit = null;
        OnClick = null;
    }
}
