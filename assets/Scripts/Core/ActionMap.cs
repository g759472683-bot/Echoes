/// <summary>
/// Identifies which Input System Action Map is currently active.
/// Only one map is active at a time (mutually exclusive, ADR-0005).
/// This is a minimal stub — the full enum is defined in InputManager.
/// </summary>
public enum ActionMap
{
    /// <summary>Gameplay interaction mode (mouse hover/click/drag on scroll).</summary>
    Gameplay,
    /// <summary>UI navigation mode (keyboard: arrow keys, Enter, Escape, Tab).</summary>
    UI,
    /// <summary>No input accepted (used during scene transitions, ADR-0004).</summary>
    Inactive
}
