using System;
using System.Linq;

/// <summary>
/// Manages runtime key rebinding: start, complete, cancel, timeout, duplicate
/// resolution, and PlayerPrefs persistence. Testable without Unity runtime
/// via IBindingStore + IInputActionLookup + ITimeProvider abstractions.
///
/// Events declared here (ADR-0001):
///   OnRebindingStarted(string actionName)
///   OnRebindingCompleted(string actionName, string newBindingPath)
///   OnRebindingCancelled(string actionName)
///   OnDuplicateCleared(string actionName)
/// </summary>
public class InputRebindingManager
{
    // =========================================================================
    // Events (ADR-0001 static event pattern)
    // =========================================================================

    /// <summary>Fires when interactive rebinding begins for an action.</summary>
    public static event Action<string> OnRebindingStarted;

    /// <summary>Fires when rebinding completes successfully.</summary>
    public static event Action<string, string> OnRebindingCompleted;

    /// <summary>Fires when rebinding is cancelled (timeout, Escape, device disconnect).</summary>
    public static event Action<string> OnRebindingCancelled;

    /// <summary>
    /// Fires when another action's binding was cleared due to a duplicate.
    /// Parameter is the action name that lost its binding.
    /// </summary>
    public static event Action<string> OnDuplicateCleared;

    // =========================================================================
    // Dependencies
    // =========================================================================

    private readonly IBindingStore _store;
    private readonly IInputActionLookup _actions;
    private readonly ITimeProvider _time;

    // =========================================================================
    // Internal State
    // =========================================================================

    private string _currentRebindingAction;
    private string _originalBindingPath;
    private float _rebindingStartTime;
    private bool _isRebinding;

    /// <summary>Timeout duration in seconds for interactive rebinding.</summary>
    public const float TimeoutSeconds = 30f;

    /// <summary>True if a rebinding operation is currently in progress.</summary>
    public bool IsRebinding => _isRebinding;

    /// <summary>The name of the action currently being rebound, or null.</summary>
    public string CurrentRebindingAction => _currentRebindingAction;

    /// <summary>Prefs key used for binding override persistence.</summary>
    public const string RebindingPrefsKey = "InputRebindingOverrides";

    // =========================================================================
    // Construction
    // =========================================================================

    public InputRebindingManager(
        IBindingStore store,
        IInputActionLookup actions,
        ITimeProvider time)
    {
        _store = store;
        _actions = actions;
        _time = time;
    }

    // =========================================================================
    // Public API — Rebinding Lifecycle
    // =========================================================================

    /// <summary>
    /// Begins interactive rebinding for the named action.
    /// Switches InputManager to Rebinding state. The caller is responsible
    /// for the actual interactive input capture (PerformInteractiveRebinding).
    /// Call CompleteRebinding or CancelRebinding when the interaction finishes.
    /// </summary>
    public void StartRebinding(string actionName)
    {
        if (_isRebinding) return;
        if (!_actions.ActionExists(actionName)) return;

        _currentRebindingAction = actionName;
        _originalBindingPath = _actions.GetBindingPath(actionName);
        _rebindingStartTime = _time.Time;
        _isRebinding = true;

        OnRebindingStarted?.Invoke(actionName);
        InputManager.SwitchToRebindingMode();
    }

    /// <summary>
    /// Completes the current rebinding operation. Applies the new binding path,
    /// resolves duplicates (clearing any other action using the same path),
    /// persists to store, and returns to Menu state.
    /// </summary>
    public void CompleteRebinding(string newBindingPath)
    {
        if (!_isRebinding) return;
        if (string.IsNullOrEmpty(newBindingPath)) return;

        ResolveDuplicateBindings(_currentRebindingAction, newBindingPath);
        _actions.SetBindingPath(_currentRebindingAction, newBindingPath);
        SaveBindingsToStore();

        var completedAction = _currentRebindingAction;
        _isRebinding = false;
        _currentRebindingAction = null;

        OnRebindingCompleted?.Invoke(completedAction, newBindingPath);
        InputManager.SwitchToUIMode();
    }

    /// <summary>
    /// Cancels the current rebinding operation. Restores the original binding
    /// path and returns to Menu state. No changes are persisted.
    /// </summary>
    public void CancelRebinding()
    {
        if (!_isRebinding) return;

        _actions.SetBindingPath(_currentRebindingAction, _originalBindingPath);

        var cancelledAction = _currentRebindingAction;
        _isRebinding = false;
        _currentRebindingAction = null;

        OnRebindingCancelled?.Invoke(cancelledAction);
        InputManager.SwitchToUIMode();
    }

    /// <summary>
    /// Checks whether the rebinding timeout has elapsed.
    /// Call from Update() during Rebinding state. If 30s have passed
    /// without completion, cancels the rebinding automatically.
    /// </summary>
    public void CheckTimeout()
    {
        if (!_isRebinding) return;

        if (_time.Time - _rebindingStartTime >= TimeoutSeconds)
        {
            CancelRebinding();
        }
    }

    // =========================================================================
    // Public API — Persistence
    // =========================================================================

    /// <summary>
    /// Loads binding overrides from the persistent store and applies them
    /// to the action lookup. Call once during initialization, after
    /// InputActionAsset is built.
    /// </summary>
    public void LoadBindings()
    {
        var json = _store.LoadOverrides();
        if (string.IsNullOrEmpty(json)) return;

        var pairs = json.Split(';');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2 && _actions.ActionExists(parts[0]))
            {
                _actions.SetBindingPath(parts[0], parts[1]);
            }
        }
    }

    /// <summary>
    /// Saves all current binding paths to the persistent store.
    /// Called automatically by CompleteRebinding.
    /// </summary>
    public void SaveBindingsToStore()
    {
        var pairs = _actions.GetAllActionNames()
            .Select(name => $"{name}={_actions.GetBindingPath(name)}");
        _store.SaveOverrides(string.Join(";", pairs));
    }

    // =========================================================================
    // Private Helpers
    // =========================================================================

    /// <summary>
    /// Scans all actions for a binding path matching newBindingPath.
    /// If another action uses the same path, its binding is cleared.
    /// The action that lost its binding may become unbound (empty path).
    /// </summary>
    private void ResolveDuplicateBindings(string exceptAction, string newBindingPath)
    {
        foreach (var actionName in _actions.GetAllActionNames())
        {
            if (actionName == exceptAction) continue;
            if (_actions.GetBindingPath(actionName) == newBindingPath)
            {
                _actions.SetBindingPath(actionName, "");
                OnDuplicateCleared?.Invoke(actionName);
            }
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
        OnRebindingStarted = null;
        OnRebindingCompleted = null;
        OnRebindingCancelled = null;
        OnDuplicateCleared = null;
    }
}
