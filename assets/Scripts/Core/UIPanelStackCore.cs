using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# core of the UIPanelStack (ADR-0006).
///
/// Manages a LIFO panel stack with automatic input gating:
///   - Stack non-empty → UI Action Map (gameplay input suppressed)
///   - Stack empty → Gameplay Action Map (UI closed, HUD visible)
///
/// States: Empty → (PushPanel) → PanelOpen → (PushPanel) → PanelOpen
///         PanelOpen → (PopPanel, depth→0) → Empty
///         Any → (PushPanel/PopPanel) → Transitioning → target state
///
/// Max depth: 10. Operations during Transitioning are rejected.
/// Missing assets: logs error + path in dev builds; generic error in release.
///
/// Optional panel transition animations (ADR-0006 S003):
///   When IPanelAnimator is injected, PushPanel plays fade-in (0.3s)
///   and PopPanel plays fade-out (0.2s) before removing the panel.
///   ReplaceTop cross-fades: old panel fades out while new fades in.
///   When animator is null (default), transitions are skipped —
///   the core operates synchronously as before.
///
/// Dependencies are injected via constructor for pure C# testability.
/// Static events (ADR-0001) notify other systems of panel stack changes.
/// </summary>
public class UIPanelStackCore
{
    // =========================================================================
    // Constants
    // =========================================================================

    public const int MaxDepth = 10;

    // =========================================================================
    // Dependencies (DI)
    // =========================================================================

    private readonly IPanelAssetProvider _assetProvider;
    private readonly IInputModeController _inputMode;
    private readonly IPanelAnimator _animator;
    private readonly bool _isDevelopmentBuild;

    // =========================================================================
    // Internal State
    // =========================================================================

    private readonly Stack<PanelEntry> _stack = new();
    private PanelStackState _state = PanelStackState.Empty;

    // =========================================================================
    // Public Properties
    // =========================================================================

    /// <summary>Current depth of the panel stack.</summary>
    public int StackDepth => _stack.Count;

    /// <summary>Panel ID of the topmost panel, or null if stack is empty.</summary>
    public string TopPanelId => _stack.Count > 0 ? _stack.Peek().PanelId : null;

    /// <summary>Current state of the panel stack state machine.</summary>
    public PanelStackState State => _state;

    /// <summary>Whether this is a development build (controls error message detail).</summary>
    public bool IsDevelopmentBuild => _isDevelopmentBuild;

    /// <summary>Whether transition animations are enabled.</summary>
    public bool AnimationsEnabled => _animator != null;

    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>Fired after a panel is pushed onto the stack.</summary>
    public static event Action<string> OnPanelPushed;

    /// <summary>Fired after a panel is popped from the stack.</summary>
    public static event Action<string> OnPanelPopped;

    /// <summary>Fired when the input mode changes (param: "UI" or "Gameplay").</summary>
    public static event Action<string> OnInputModeChanged;

    /// <summary>Fired when an error occurs (panel not found, max depth, etc.).</summary>
    public static event Action<string> OnError;

    /// <summary>Fired when a panel transition completes (param: panelId, "fade-in" or "fade-out").</summary>
    public static event Action<string, string> OnTransitionComplete;

    /// <summary>Fired after every push/pop operation with the new stack depth.</summary>
    public static event Action<int> OnStackChanged;

    // =========================================================================
    // Construction
    // =========================================================================

    /// <summary>
    /// Creates a UIPanelStackCore without transition animations.
    /// Push/Pop operations are synchronous.
    /// </summary>
    public UIPanelStackCore(
        IPanelAssetProvider assetProvider,
        IInputModeController inputMode,
        bool isDevelopmentBuild = false)
        : this(assetProvider, inputMode, null, isDevelopmentBuild)
    {
    }

    /// <summary>
    /// Creates a UIPanelStackCore with optional transition animations.
    /// When animator is non-null, PushPanel plays fade-in and PopPanel
    /// plays fade-out before the panel is removed.
    /// </summary>
    public UIPanelStackCore(
        IPanelAssetProvider assetProvider,
        IInputModeController inputMode,
        IPanelAnimator animator,
        bool isDevelopmentBuild = false)
    {
        _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        _inputMode = inputMode ?? throw new ArgumentNullException(nameof(inputMode));
        _animator = animator; // nullable — null means no animations
        _isDevelopmentBuild = isDevelopmentBuild;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Pushes a panel onto the stack. Loads the VisualTreeAsset from the
    /// registry, instantiates it, and appends to the UI root.
    ///
    /// Auto-gating: if this is the first panel (stack was empty), switches
    /// to UI input mode.
    ///
    /// When animations are enabled, plays a fade-in transition before
    /// completing. The panel is added to the stack immediately (so it
    /// appears in TopPanelId) but OnPanelPushed fires after fade-in.
    ///
    /// Rejected if:
    ///   - Stack is in Transitioning state
    ///   - Stack is at MaxDepth (10)
    ///   - Panel asset is not registered or fails to load
    /// </summary>
    public void PushPanel(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            LogError("PushPanel called with null or empty panelId");
            return;
        }

        if (_state == PanelStackState.Transitioning)
        {
            LogError($"PushPanel('{panelId}') ignored — stack is transitioning");
            return;
        }

        if (_stack.Count >= MaxDepth)
        {
            LogError($"PushPanel('{panelId}') rejected — max depth {MaxDepth} reached");
            return;
        }

        // Load panel asset
        if (!_assetProvider.HasAsset(panelId))
        {
            string msg = _isDevelopmentBuild
                ? $"UXML not found for panel '{panelId}' — path: Assets/UI/{panelId}.uxml"
                : $"Panel '{panelId}' failed to load";
            LogError(msg);
            return;
        }

        IPanelInstance panelInstance = _assetProvider.LoadPanel(panelId);
        if (panelInstance == null)
        {
            string msg = _isDevelopmentBuild
                ? $"Failed to instantiate UXML for panel '{panelId}' — path: Assets/UI/{panelId}.uxml"
                : $"Panel '{panelId}' failed to load";
            LogError(msg);
            return;
        }

        // Push to stack immediately (TopPanelId is queryable)
        _stack.Push(new PanelEntry(panelId, panelInstance));

        // Auto input gating — first panel opens
        bool isFirstPanel = _stack.Count == 1;
        if (isFirstPanel)
        {
            _inputMode.SwitchToUIMode();
        }

        // Transition or instant complete
        if (_animator != null)
        {
            _state = PanelStackState.Transitioning;
            _animator.PlayFadeIn(panelInstance, () =>
            {
                _state = PanelStackState.PanelOpen;
                if (isFirstPanel)
                    OnInputModeChanged?.Invoke("UI");
                OnPanelPushed?.Invoke(panelId);
                OnStackChanged?.Invoke(_stack.Count);
                OnTransitionComplete?.Invoke(panelId, "fade-in");
            });
        }
        else
        {
            _state = PanelStackState.PanelOpen;
            if (isFirstPanel)
                OnInputModeChanged?.Invoke("UI");
            OnPanelPushed?.Invoke(panelId);
            OnStackChanged?.Invoke(_stack.Count);
        }
    }

    /// <summary>
    /// Pops the top panel from the stack. Removes its VisualElement from the
    /// UI root. If the stack becomes empty, switches to Gameplay input mode.
    ///
    /// When animations are enabled, plays a fade-out transition on the panel
    /// before removing it from the stack. OnPanelPopped fires after the
    /// animation completes.
    ///
    /// No-op if stack is empty or in Transitioning state.
    /// </summary>
    public void PopPanel()
    {
        if (_state == PanelStackState.Transitioning)
        {
            return; // Silently ignore during transition
        }

        if (_stack.Count == 0)
        {
            return; // Nothing to pop
        }

        if (_animator != null)
        {
            _state = PanelStackState.Transitioning;
            PanelEntry entry = _stack.Peek();
            string poppedId = entry.PanelId;
            bool willBeEmpty = _stack.Count == 1;

            _animator.PlayFadeOut(entry.Instance, () =>
            {
                _stack.Pop();

                if (willBeEmpty)
                {
                    _inputMode.SwitchToGameplayMode();
                    _state = PanelStackState.Empty;
                    OnInputModeChanged?.Invoke("Gameplay");
                }
                else
                {
                    _state = PanelStackState.PanelOpen;
                }

                OnPanelPopped?.Invoke(poppedId);
                OnStackChanged?.Invoke(_stack.Count);
                OnTransitionComplete?.Invoke(poppedId, "fade-out");
            });
        }
        else
        {
            _state = PanelStackState.Transitioning;

            PanelEntry entry = _stack.Pop();
            string poppedId = entry.PanelId;

            if (_stack.Count == 0)
            {
                _inputMode.SwitchToGameplayMode();
                _state = PanelStackState.Empty;
                OnInputModeChanged?.Invoke("Gameplay");
            }
            else
            {
                _state = PanelStackState.PanelOpen;
            }

            OnPanelPopped?.Invoke(poppedId);
            OnStackChanged?.Invoke(_stack.Count);
        }
    }

    /// <summary>
    /// Replaces the top panel with a different panel. Equivalent to
    /// PopPanel() + PushPanel(newPanelId) but without intermediate state exposure.
    ///
    /// When animations are enabled, the old panel fades out while the new
    /// panel fades in (cross-fade). Both transitions run simultaneously.
    ///
    /// If the stack is empty, behaves as PushPanel(newPanelId).
    /// Rejected if stack is in Transitioning state.
    /// </summary>
    public void ReplaceTop(string newPanelId)
    {
        if (string.IsNullOrEmpty(newPanelId))
        {
            LogError("ReplaceTop called with null or empty newPanelId");
            return;
        }

        if (_state == PanelStackState.Transitioning)
        {
            LogError($"ReplaceTop('{newPanelId}') ignored — stack is transitioning");
            return;
        }

        if (_stack.Count == 0)
        {
            PushPanel(newPanelId);
            return;
        }

        if (_stack.Count >= MaxDepth && !_stack.Peek().PanelId.Equals(newPanelId))
        {
            LogError($"ReplaceTop('{newPanelId}') rejected — max depth {MaxDepth} reached");
            return;
        }

        // Load the new panel asset before starting transitions
        if (!_assetProvider.HasAsset(newPanelId))
        {
            string msg = _isDevelopmentBuild
                ? $"UXML not found for panel '{newPanelId}' — path: Assets/UI/{newPanelId}.uxml"
                : $"Panel '{newPanelId}' failed to load";
            LogError(msg);
            return;
        }

        IPanelInstance newInstance = _assetProvider.LoadPanel(newPanelId);
        if (newInstance == null)
        {
            string msg = _isDevelopmentBuild
                ? $"Failed to instantiate UXML for panel '{newPanelId}' — path: Assets/UI/{newPanelId}.uxml"
                : $"Panel '{newPanelId}' failed to load";
            LogError(msg);
            return;
        }

        if (_animator != null)
        {
            // Cross-fade: old panel fades out while new panel fades in
            _state = PanelStackState.Transitioning;

            PanelEntry oldEntry = _stack.Peek();
            string oldPanelId = oldEntry.PanelId;
            bool popCompleted = false;
            bool pushCompleted = false;

            Action tryComplete = () =>
            {
                if (popCompleted && pushCompleted)
                {
                    _state = PanelStackState.PanelOpen;
                    OnStackChanged?.Invoke(_stack.Count);
                    OnTransitionComplete?.Invoke(newPanelId, "cross-fade");
                }
            };

            // Start fade-out on old panel
            _animator.PlayFadeOut(oldEntry.Instance, () =>
            {
                _stack.Pop();
                OnPanelPopped?.Invoke(oldPanelId);
                popCompleted = true;
                tryComplete();
            });

            // Push new panel and start fade-in
            _stack.Push(new PanelEntry(newPanelId, newInstance));
            _animator.PlayFadeIn(newInstance, () =>
            {
                OnPanelPushed?.Invoke(newPanelId);
                pushCompleted = true;
                tryComplete();
            });
        }
        else
        {
            // Synchronous replace
            string oldPanelId = _stack.Peek().PanelId;
            _stack.Pop();
            _stack.Push(new PanelEntry(newPanelId, newInstance));
            OnPanelPopped?.Invoke(oldPanelId);
            OnPanelPushed?.Invoke(newPanelId);
            OnStackChanged?.Invoke(_stack.Count);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void LogError(string message)
    {
        OnError?.Invoke(message);
    }

    /// <summary>Resets static events. Call in test TearDown.</summary>
    public static void ResetStaticEvents()
    {
        OnPanelPushed = null;
        OnPanelPopped = null;
        OnInputModeChanged = null;
        OnError = null;
        OnTransitionComplete = null;
    }
}

// =============================================================================
// Supporting Types
// =============================================================================

/// <summary>State machine states for UIPanelStack.</summary>
public enum PanelStackState
{
    /// <summary>No modal panels — Gameplay Action Map active, HUD visible.</summary>
    Empty,

    /// <summary>At least one panel on stack — UI Action Map active.</summary>
    PanelOpen,

    /// <summary>Panel animation/transition in progress — Push/Pop rejected.</summary>
    Transitioning
}

/// <summary>A single entry in the panel stack.</summary>
public readonly struct PanelEntry
{
    public readonly string PanelId;
    public readonly IPanelInstance Instance;

    public PanelEntry(string panelId, IPanelInstance instance)
    {
        PanelId = panelId;
        Instance = instance;
    }
}
