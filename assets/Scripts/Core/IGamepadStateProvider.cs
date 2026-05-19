using System;

/// <summary>
/// Provides gamepad connection state for GamepadHintsManager.
///
/// In production, delegates to Unity's InputSystem.Gamepad.current or
/// InputManager.OnGamepadConnectionChanged. Extracted for DI so
/// GamepadHintsManager is pure C# testable.
/// </summary>
public interface IGamepadStateProvider
{
    /// <summary>Whether a gamepad is currently connected and active.</summary>
    bool IsGamepadConnected { get; }
}
