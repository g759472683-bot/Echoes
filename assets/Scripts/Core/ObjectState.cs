/// <summary>
/// Default visual / interaction state for an InteractiveObject (GDD scroll-interaction-system §3.4).
///
///   Active — Full collision + interaction enabled, visible ink dot indicator
///   Hidden — No collider created, no visual indicator, revealed via story events
///   Disabled — Collider present but interactions suppressed, dimmed ink dot
/// </summary>
public enum ObjectState
{
    /// <summary>Fully interactive and visible.</summary>
    Active,

    /// <summary>Not present on scroll; no collider or visual.</summary>
    Hidden,

    /// <summary>Collider present but interaction blocked; visual is dimmed.</summary>
    Disabled
}
