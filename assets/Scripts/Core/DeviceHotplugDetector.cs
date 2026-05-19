using System;

/// <summary>
/// Detects gamepad hot-plug events and exposes connection state to UI systems.
/// Testable without Unity runtime via IDeviceChangeProvider abstraction.
///
/// Events declared here (ADR-0001):
///   OnGamepadConnectionChanged(bool isConnected)
/// </summary>
public class DeviceHotplugDetector
{
    // =========================================================================
    // Events (ADR-0001 static event pattern)
    // =========================================================================

    /// <summary>
    /// Fires when a gamepad is connected or disconnected.
    /// Subscribed by UI Framework (#5) and Main Menu (#19) to show/hide
    /// gamepad button prompts.
    /// </summary>
    public static event Action<bool> OnGamepadConnectionChanged;

    // =========================================================================
    // Dependencies
    // =========================================================================

    private readonly IDeviceChangeProvider _deviceProvider;

    // =========================================================================
    // Internal State
    // =========================================================================

    private bool _isConnected;
    private InputState _currentInputState = InputState.Gameplay;
    private bool _isInitialized;

    /// <summary>True if a gamepad is currently connected.</summary>
    public bool IsGamepadConnected => _isConnected;

    /// <summary>
    /// True if gamepad button prompts should be shown.
    /// Gamepad must be connected AND the input state must be Menu
    /// (gamepad is only used for menu navigation per ADR-0005).
    /// </summary>
    public bool ShouldShowGamepadHints => _isConnected && _currentInputState == InputState.Menu;

    /// <summary>
    /// True if gamepad input should be processed.
    /// Same condition as ShouldShowGamepadHints — gamepad only works in menus.
    /// </summary>
    public bool IsGamepadInputEnabled => _isConnected && _currentInputState == InputState.Menu;

    /// <summary>The current input state used for gamepad input gating.</summary>
    public InputState CurrentInputState => _currentInputState;

    /// <summary>True after Initialize() has been called successfully.</summary>
    public bool IsInitialized => _isInitialized;

    // =========================================================================
    // Construction
    // =========================================================================

    public DeviceHotplugDetector(IDeviceChangeProvider deviceProvider)
    {
        _deviceProvider = deviceProvider;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Subscribes to device change events and reads the initial connection state.
    /// Call once during game start-up (after InputSystem is available).
    /// Idempotent — calling multiple times is safe.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        _deviceProvider.OnGamepadConnectionChanged += HandleConnectionChanged;
        _isConnected = _deviceProvider.IsGamepadConnected;
        _isInitialized = true;
    }

    /// <summary>
    /// Updates the input state for gamepad input gating.
    /// When switching to Menu: gamepad input becomes active if connected.
    /// When switching to Gameplay/Inactive/Rebinding: gamepad input is suppressed.
    /// </summary>
    public void SetInputState(InputState state)
    {
        _currentInputState = state;
    }

    /// <summary>
    /// Unsubscribes from device change events. Call during shutdown
    /// to prevent stale delegates.
    /// </summary>
    public void Shutdown()
    {
        _deviceProvider.OnGamepadConnectionChanged -= HandleConnectionChanged;
        _isInitialized = false;
    }

    // =========================================================================
    // Private Handlers
    // =========================================================================

    /// <summary>
    /// Handles device change notifications from the provider.
    /// Fires OnGamepadConnectionChanged for UI system subscribers.
    /// </summary>
    private void HandleConnectionChanged(bool connected)
    {
        _isConnected = connected;
        OnGamepadConnectionChanged?.Invoke(connected);
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
        OnGamepadConnectionChanged = null;
    }
}
