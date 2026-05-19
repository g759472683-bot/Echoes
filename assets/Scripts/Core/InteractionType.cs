/// <summary>
/// The interaction behaviour triggered when a player engages with an InteractiveObject.
///
///   Touch   — Instant single activation (click)
///   Drag    — Click-and-drag with threshold gating (5px trigger, 30px complete)
///   Hover   — Cursor-over detection only; fires hover events without click
///   Examine — Click-to-zoom detailed inspection mode; Cancel to exit
/// </summary>
public enum InteractionType
{
    /// <summary>Instant single-click interaction — tap/click to activate.</summary>
    Touch,

    /// <summary>Click-and-drag with distance thresholds (Story 003).</summary>
    Drag,

    /// <summary>Hover-only detection; no click behaviour.</summary>
    Hover,

    /// <summary>Click enters detailed examination mode; Cancel exits.</summary>
    Examine
}
