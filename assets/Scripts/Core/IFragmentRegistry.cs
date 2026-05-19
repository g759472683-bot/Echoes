/// <summary>
/// Fragment data provider for ChangeTracker validation (ADR-0007, DI abstraction).
///
/// Decouples ChangeTracker from direct MemoryFragment/EmotionalTagCatalog access
/// so ApplyChanges validation is pure C# testable.
///
/// In production, implemented by a wrapper that reads from IDataManager and
/// the fragment registry. In tests, a mock returns controlled answers.
/// </summary>
public interface IFragmentRegistry
{
    /// <summary>Returns true if a fragment with the given ID exists in any chapter.</summary>
    bool HasFragment(string fragmentId);

    /// <summary>
    /// Returns true if the named layer exists on the fragment and is mutable.
    /// Returns false if the layer does not exist or is immutable.
    /// </summary>
    bool IsLayerMutable(string fragmentId, string layerId);

    /// <summary>Returns true if the named object exists on the fragment.</summary>
    bool HasObject(string fragmentId, string objectId);

    /// <summary>Returns true if the given TagId exists in the EmotionalTagCatalog.</summary>
    bool HasTag(string tagId);
}
