using System;

/// <summary>
/// Abstraction over Unity InputSystem.onDeviceChange and Gamepad.current.
/// Enables pure C# testing of DeviceHotplugDetector without Unity runtime.
///
/// The production implementation wraps InputSystem.onDeviceChange and checks
/// Gamepad.current for the initial connection state.
/// </summary>
public interface IDeviceChangeProvider
{
    /// <summary>
    /// Fires when a gamepad is connected or disconnected.
    /// True = connected/reconnected. False = disconnected/removed.
    /// </summary>
    event Action<bool> OnGamepadConnectionChanged;

    /// <summary>Returns true if a gamepad is currently connected.</summary>
    bool IsGamepadConnected { get; }
}
