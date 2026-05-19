using System.Collections.Generic;

/// <summary>
/// Interface for querying emotional tag data needed by WebAssociationEngine (ADR-0009).
///
/// Provides tag vocabulary access, category classification, and tag similarity
/// matrix lookup for Factor A (cosine tag similarity) computation.
///
/// Implemented by EmotionalTagSystem (#10). This interface is consumed only by
/// WebAssociationEngine (#13) — it is intentionally narrow.
/// </summary>
public interface IEmotionalTagSystem
{
    /// <summary>
    /// Returns the dominant emotional category for a set of weighted tags.
    /// The dominant category is the one with the highest total weight.
    /// Returns null if tags list is null or empty.
    /// </summary>
    string GetDominantCategory(List<EmotionalTag> tags);

    /// <summary>
    /// Returns the parent tag ID for a given tag, or null if the tag has no parent.
    /// </summary>
    string GetParentTag(string tagId);

    /// <summary>
    /// Returns the emotional category for a given tag ID (e.g., "Sadness", "Joy").
    /// Returns null if the tag is not found.
    /// </summary>
    string GetTagCategory(string tagId);

    /// <summary>
    /// Returns the precomputed similarity between two tags [0.0, 1.0].
    /// Uses the TagSimilarityMatrix ScriptableObject for lookup.
    /// </summary>
    float GetTagSimilarity(string tagIdA, string tagIdB);

    /// <summary>
    /// Returns the dominant emotional category for a fragment by ID.
    /// Used by the rhythm penalty (Factor C) to look up categories of
    /// fragments in the recent history window.
    /// Returns null if the fragment is not found or has no tags.
    /// </summary>
    string GetFragmentDominantCategory(string fragmentId);

    /// <summary>
    /// Returns the emotional tags for a fragment by ID.
    /// Used by the engine to resolve tag data for history fragments.
    /// Returns an empty list if the fragment is not found.
    /// </summary>
    List<EmotionalTag> GetFragmentTags(string fragmentId);
}
