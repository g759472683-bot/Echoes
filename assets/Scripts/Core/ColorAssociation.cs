using System;
using UnityEngine;

/// <summary>
/// Color pair associated with an emotional tag (ADR-0007).
///
/// Used by downstream systems for emotion-driven visual theming:
///   - Transition system (#6) — scene tint gradients
///   - HUD (#17) — emotional indicator
///   - Micro-animation system (#9) — emotion-mapped animation parameters
///
/// Colors are hex strings (e.g., "#D4A574") for designer readability
/// and Unity Editor color picker compatibility.
/// </summary>
[Serializable]
public struct ColorAssociation
{
    /// <summary>Primary color in hex format (e.g., "#D4A574").</summary>
    public string Primary;

    /// <summary>Secondary/background color in hex format (e.g., "#8B7355").</summary>
    public string Secondary;

    public ColorAssociation(string primary, string secondary)
    {
        Primary = primary;
        Secondary = secondary;
    }
}
