using System;

/// <summary>
/// Manages gamepad hint visibility (ADR-0006).
///
/// Keyboard hints are always shown in menus. Gamepad hints (button prompts)
/// are shown only when a gamepad is connected. State is driven by
/// IGamepadStateProvider which wraps Unity's InputSystem.Gamepad.current.
///
/// Pure C# testable — IGamepadStateProvider is injected.
/// Static events (ADR-0001) notify UI elements to show/hide hints.
/// </summary>
public class GamepadHintsManager
{
    // =========================================================================
    // Dependencies (DI)
    // =========================================================================

    private readonly IGamepadStateProvider _gamepadState;

    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>Fired when gamepad connection state changes. Param: isConnected.</summary>
    public static event Action<bool> OnGamepadConnectionChanged;

    /// <summary>Fired when hint visibility should be updated. Param: "Show" or "Hide".</summary>
    public static event Action<string> OnHintsVisibilityChanged;

    // =========================================================================
    // Construction
    // =========================================================================

    public GamepadHintsManager(IGamepadStateProvider gamepadState)
    {
        _gamepadState = gamepadState ?? throw new ArgumentNullException(nameof(gamepadState));
    }

    // =========================================================================
    // Public Properties
    // =========================================================================

    /// <summary>Whether keyboard hints should be displayed. Always true in menus.</summary>
    public bool ShowKeyboardHints => true;

    /// <summary>Whether gamepad button hints should be displayed.</summary>
    public bool ShowGamepadHints => _gamepadState.IsGamepadConnected;

    /// <summary>Whether any gamepad is currently connected.</summary>
    public bool IsGamepadConnected => _gamepadState.IsGamepadConnected;

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Evaluates current hint visibility and fires events if state changed.
    /// Called when a menu opens or gamepad connection changes.
    /// </summary>
    public void RefreshHints()
    {
        bool connected = _gamepadState.IsGamepadConnected;
        OnGamepadConnectionChanged?.Invoke(connected);
        OnHintsVisibilityChanged?.Invoke(connected ? "Show" : "Hide");
    }

    /// <summary>
    /// Called by InputManager.OnGamepadConnectionChanged subscriber in production.
    /// Updates internal state and fires hint visibility events.
    /// </summary>
    public void HandleGamepadConnectionChanged()
    {
        RefreshHints();
    }

    /// <summary>Resets static events. Call in test TearDown.</summary>
    public static void ResetStaticEvents()
    {
        OnGamepadConnectionChanged = null;
        OnHintsVisibilityChanged = null;
    }
}
