/// <summary>
/// The type of result produced when a player interacts with an InteractiveObject.
/// Each value maps to a specific dispatch path in InteractionManager.DispatchInteractionResult.
/// </summary>
public enum ResultType
{
    /// <summary>Play a triggered micro-animation by ID.</summary>
    PlayAnimation,

    /// <summary>Show text on the HUD above the cursor.</summary>
    ShowText,

    /// <summary>Present a choice panel to the player (Story 004 full flow).</summary>
    PresentChoice,

    /// <summary>Transition to a different memory fragment.</summary>
    TransitionToFragment,

    /// <summary>Reveal a hidden InteractiveObject (enables its collider).</summary>
    RevealObject
}
