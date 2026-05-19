/// <summary>
/// Controls input mode switching between Gameplay and UI.
///
/// In production, delegates to InputManager.SwitchToUIMode() /
/// SwitchToGameplayMode(). Extracted for DI so UIPanelStackCore
/// can be tested without the real InputManager.
/// </summary>
public interface IInputModeController
{
    /// <summary>Whether UI mode is currently active.</summary>
    bool IsUIModeActive { get; }

    /// <summary>Switch to UI Action Map (gameplay input disabled).</summary>
    void SwitchToUIMode();

    /// <summary>Switch to Gameplay Action Map (UI input disabled, HUD visible).</summary>
    void SwitchToGameplayMode();
}
