/// <summary>
/// Interaction state machine states for the InteractionManager (GDD scroll-interaction-system §3.5).
///
///   Idle             — No fragment active, no detection running
///   Active           — Normal hover detection loop
///   Dragging         — Drag in progress (Story 003), all other interaction blocked
///   ChoicePresenting — Choice panel visible (Story 004), all scroll interaction blocked
///   Examining        — Examine mode active (Story 002), only Cancel allowed
///   Blocked          — Transition / text display in progress, all interaction blocked
///
/// Exclusive state transitions are enforced by InteractionManager.Update() guards.
/// </summary>
public enum InteractionState
{
    /// <summary>No fragment is active; detection loop is effectively idle.</summary>
    Idle,

    /// <summary>Normal hover detection loop running. Hover → Click / Drag / HoverWait allowed.</summary>
    Active,

    /// <summary>Drag interaction in progress. Only the current drag processes; all else blocked.</summary>
    Dragging,

    /// <summary>Choice panel is presented. Only ChoiceGroup option buttons respond.</summary>
    ChoicePresenting,

    /// <summary>Examine mode active. Only Cancel exits this state.</summary>
    Examining,

    /// <summary>All interaction blocked — transition or text display in progress.</summary>
    Blocked
}
