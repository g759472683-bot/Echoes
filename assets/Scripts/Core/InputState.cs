/// <summary>
/// Represents the current input mode of the game (ADR-0005 extension).
///
/// This replaces the simpler <see cref="ActionMap"/> enum with a richer state machine:
///   Gameplay  — Player interacts with memory fragments (mouse hover/click/scroll)
///   Menu      — UI navigation mode (keyboard: arrow keys, Enter, Escape, Tab)
///   Rebinding — Key rebinding operation in progress (all gameplay/UI input suspended)
///   Inactive  — No input accepted (used during scene transitions per ADR-0004)
///
/// The <see cref="ActionMap"/> enum is retained for backward compatibility with
/// existing systems that use <see cref="InputManager.OnSetActionMap"/>.
/// </summary>
public enum InputState
{
    /// <summary>Gameplay interaction mode — mouse-driven fragment exploration.</summary>
    Gameplay,

    /// <summary>UI navigation mode — keyboard-driven menu/screen interaction.</summary>
    Menu,

    /// <summary>Rebinding mode — interactive key remapping in progress.</summary>
    Rebinding,

    /// <summary>No input accepted — gated during scene transitions and loading.</summary>
    Inactive
}
