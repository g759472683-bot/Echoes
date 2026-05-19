using System.Collections.Generic;

/// <summary>
/// Provides access to weight overlay data for TagWeightResolver (DI abstraction).
///
/// In production, reads from ChangeTracker._overlay dictionary.
/// In tests, a mock with registered overlays.
/// Extracted for DI so weight merge logic is pure C# testable.
/// </summary>
public interface IOverlayProvider
{
    /// <summary>
    /// Returns all weight overlays for a given fragment, sorted by OrderIndex ascending.
    /// Returns empty list if no overlays exist.
    /// </summary>
    List<TagWeightOverlay> GetWeightOverlays(string fragmentId);
}
