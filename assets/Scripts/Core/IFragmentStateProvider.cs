using System.Collections.Generic;

/// <summary>
/// Provides base (SO) fragment data for GetCurrentState merging (ADR-0007, DI abstraction).
///
/// Decouples ChangeTrackerCore from direct MemoryFragment/IDataManager access.
/// In production, implemented by a wrapper around IDataManager.
/// In tests, a mock returns controlled base state.
/// </summary>
public interface IFragmentStateProvider
{
    /// <summary>Returns the base (SO) visual layers for a fragment, or null if fragment not found.</summary>
    IReadOnlyList<VisualLayer> GetBaseVisualLayers(string fragmentId);

    /// <summary>Returns the base (SO) emotional tags for a fragment, or null if fragment not found.</summary>
    IReadOnlyList<EmotionalTag> GetBaseEmotionalTags(string fragmentId);

    /// <summary>Returns the base (SO) interactive objects for a fragment, or null if fragment not found.</summary>
    IReadOnlyList<InteractiveObject> GetBaseInteractiveObjects(string fragmentId);

    /// <summary>Returns true if a fragment with the given ID exists.</summary>
    bool HasFragment(string fragmentId);
}
