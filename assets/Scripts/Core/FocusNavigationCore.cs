using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# core of keyboard navigation and focus management (ADR-0006).
///
/// Tracks the last focused element per panel. On panel open, restores the
/// saved focus position or auto-focuses the first focusable element. On
/// panel close, captures the current focus for later restoration.
///
/// Navigation delegates to IFocusProvider which wraps Unity's FocusController
/// (FocusNextInDirection, FocusNext, FocusPrevious, Focus).
///
/// State machine per panel:
///   Unvisited → (HandlePanelOpened) → Focused (auto-focus first)
///   Focused → (HandlePanelClosing) → LastFocusStored
///   LastFocusStored → (HandlePanelOpened) → Focused (restore)
///   LastFocusStored → (HandlePanelClosing again, overwrites)
/// </summary>
public class FocusNavigationCore
{
    // =========================================================================
    // Dependencies (DI)
    // =========================================================================

    private readonly IFocusProvider _focusProvider;

    // =========================================================================
    // Internal State
    // =========================================================================

    /// <summary>Maps panelId → last focused element ID before that panel was covered.</summary>
    private readonly Dictionary<string, string> _lastFocused = new();

    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>Fired when an element receives focus. Params: (panelId, elementId).</summary>
    public static event Action<string, string> OnElementFocused;

    /// <summary>Fired when a panel's last focus position was restored.</summary>
    public static event Action<string> OnFocusRestored;

    /// <summary>Fired when a panel auto-focused its first element (no saved focus).</summary>
    public static event Action<string> OnAutoFocused;

    /// <summary>Fired when Confirm (Enter/Gamepad A) triggers a button click.</summary>
    public static event Action<string> OnConfirmed;

    /// <summary>Fired when Cancel (Escape/Gamepad B) is pressed.</summary>
    public static event Action OnCancelled;

    /// <summary>Fired on errors (null panelId, etc.).</summary>
    public static event Action<string> OnError;

    // =========================================================================
    // Construction
    // =========================================================================

    public FocusNavigationCore(IFocusProvider focusProvider)
    {
        _focusProvider = focusProvider ?? throw new ArgumentNullException(nameof(focusProvider));
    }

    // =========================================================================
    // Public API — Panel Lifecycle
    // =========================================================================

    /// <summary>
    /// Called when a panel is opened (pushed onto the stack).
    /// Restores the last focused element for this panel, or auto-focuses
    /// the first focusable element if no saved focus exists.
    /// </summary>
    public void HandlePanelOpened(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("HandlePanelOpened called with null or empty panelId");
            return;
        }

        if (_lastFocused.TryGetValue(panelId, out string lastId) && lastId != null)
        {
            _focusProvider.FocusElement(panelId, lastId);
            OnElementFocused?.Invoke(panelId, lastId);
            OnFocusRestored?.Invoke(panelId);
        }
        else
        {
            string firstId = _focusProvider.FocusFirst(panelId);
            if (firstId != null)
            {
                OnElementFocused?.Invoke(panelId, firstId);
                OnAutoFocused?.Invoke(panelId);
            }
            // If no focusable elements exist, nothing is focused — that's valid
        }
    }

    /// <summary>
    /// Called before a panel is closed (popped from the stack).
    /// Captures the current focus position so it can be restored when
    /// the panel is re-opened.
    /// </summary>
    public void HandlePanelClosing(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("HandlePanelClosing called with null or empty panelId");
            return;
        }

        string currentFocus = _focusProvider.CaptureCurrentFocus(panelId);
        if (currentFocus != null)
        {
            _lastFocused[panelId] = currentFocus;
        }
        else
        {
            // No element focused — remove any stale saved focus for this panel.
            // This handles the edge case where all focusable elements were removed
            // or the panel was closed without any element ever receiving focus.
            _lastFocused.Remove(panelId);
        }
    }

    // =========================================================================
    // Public API — Navigation
    // =========================================================================

    /// <summary>
    /// Navigate focus in a cardinal direction (Arrow keys).
    /// Delegates to Unity's FocusNextInDirection via IFocusProvider.
    /// </summary>
    public void NavigateDirection(string panelId, NavigationDirection direction)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("NavigateDirection called with null or empty panelId");
            return;
        }

        string newFocusId = _focusProvider.NavigateDirection(panelId, direction);
        if (newFocusId != null)
        {
            OnElementFocused?.Invoke(panelId, newFocusId);
        }
    }

    /// <summary>
    /// Move focus to the next focusable group (Tab key).
    /// Delegates to Unity's FocusNext via IFocusProvider.
    /// </summary>
    public void FocusNextGroup(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("FocusNextGroup called with null or empty panelId");
            return;
        }

        string newFocusId = _focusProvider.FocusNext(panelId);
        if (newFocusId != null)
        {
            OnElementFocused?.Invoke(panelId, newFocusId);
        }
    }

    /// <summary>
    /// Move focus to the previous focusable group (Shift+Tab).
    /// Delegates to Unity's FocusPrevious via IFocusProvider.
    /// </summary>
    public void FocusPreviousGroup(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("FocusPreviousGroup called with null or empty panelId");
            return;
        }

        string newFocusId = _focusProvider.FocusPrevious(panelId);
        if (newFocusId != null)
        {
            OnElementFocused?.Invoke(panelId, newFocusId);
        }
    }

    // =========================================================================
    // Public API — Actions
    // =========================================================================

    /// <summary>
    /// Confirm (Enter key / Gamepad A). Activates the currently focused element.
    /// For buttons, fires clicked. For toggles/sliders, fires submit.
    /// </summary>
    public void Confirm(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            OnError?.Invoke("Confirm called with null or empty panelId");
            return;
        }

        bool activated = _focusProvider.ActivateFocused(panelId);
        if (activated)
        {
            OnConfirmed?.Invoke(panelId);
        }
    }

    /// <summary>
    /// Cancel (Escape key / Gamepad B). Signals that the top panel
    /// should be popped. The actual pop is handled by UIPanelStackCore
    /// listening to this event or the Confirm handler calling PopPanel().
    /// </summary>
    public void Cancel()
    {
        OnCancelled?.Invoke();
    }

    // =========================================================================
    // Public API — Query (for testing)
    // =========================================================================

    /// <summary>Gets the last focused element ID for a panel, or null if none saved.</summary>
    public string GetLastFocused(string panelId)
    {
        _lastFocused.TryGetValue(panelId, out string id);
        return id;
    }

    /// <summary>Clears all saved focus state. Useful for full stack reset.</summary>
    public void ClearAllFocusState()
    {
        _lastFocused.Clear();
    }

    /// <summary>Clears saved focus for a specific panel.</summary>
    public void ClearFocusState(string panelId)
    {
        _lastFocused.Remove(panelId);
    }

    /// <summary>Number of panels with saved focus state.</summary>
    public int SavedFocusCount => _lastFocused.Count;

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Resets static events. Call in test TearDown.</summary>
    public static void ResetStaticEvents()
    {
        OnElementFocused = null;
        OnFocusRestored = null;
        OnAutoFocused = null;
        OnConfirmed = null;
        OnCancelled = null;
        OnError = null;
    }
}
