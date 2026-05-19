using System;

/// <summary>
/// Pure C# weight resolution engine (ADR-0007 S003).
///
/// Merges base tag weights with ModifyTagWeight overlay entries:
///   1. Start with the tag's BaseWeight from the fragment data
///   2. Apply overlays in OrderIndex ascending order
///   3. Each overlay applies its ModOp (Add/Multiply/Set)
///   4. Clamp final result to [0.0, 1.0]
///
/// Handles NaN, infinity, and out-of-range inputs gracefully by clamping.
/// </summary>
public class TagWeightResolver
{
    // =========================================================================
    // Dependencies (DI)
    // =========================================================================

    private readonly IOverlayProvider _overlayProvider;

    // =========================================================================
    // Construction
    // =========================================================================

    public TagWeightResolver(IOverlayProvider overlayProvider)
    {
        _overlayProvider = overlayProvider ?? throw new ArgumentNullException(nameof(overlayProvider));
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Computes the effective (runtime) weight for a tag on a fragment.
    ///
    /// Formula: clamp(baseWeight → apply overlays by OrderIndex → clamp, [0.0, 1.0])
    ///
    /// Edge cases:
    ///   - baseWeight already outside [0,1] → clamped before applying overlays
    ///   - NaN/Infinity in baseWeight or overlay value → clamped to 0.0
    ///   - No overlays → baseWeight returned (clamped)
    /// </summary>
    public float GetEffectiveWeight(string fragmentId, string tagId, float baseWeight)
    {
        // Clamp initial base weight
        float weight = ClampToRange(baseWeight);

        if (string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(tagId))
        {
            return weight;
        }

        // Apply overlays in order
        var overlays = _overlayProvider.GetWeightOverlays(fragmentId);
        if (overlays == null || overlays.Count == 0)
        {
            return weight;
        }

        foreach (var overlay in overlays)
        {
            if (overlay.TagId != tagId) continue;

            float opValue = ClampToRange(overlay.Value);

            weight = overlay.Operation switch
            {
                ModOp.Add => weight + opValue,
                ModOp.Multiply => weight * opValue,
                ModOp.Set => opValue,
                _ => weight
            };

            // Re-clamp after each operation to prevent intermediate runaway
            weight = ClampToRange(weight);
        }

        return weight;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static float ClampToRange(float value)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value))
            return 0.0f;
        if (float.IsPositiveInfinity(value))
            return 1.0f;
        if (value < 0.0f) return 0.0f;
        if (value > 1.0f) return 1.0f;
        return value;
    }
}
