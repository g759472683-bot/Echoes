/// <summary>
/// Provides fragment emotional tag data for TagQueryEngine (DI abstraction).
///
/// In production, reads MemoryFragment.EmotionalTags via IDataManager.
/// In tests, a mock with registered fragments and tags.
/// Extracted for DI so TagQueryEngine is pure C# testable.
/// </summary>
public interface IFragmentTagProvider
{
    /// <summary>
    /// Returns all emotional tags assigned to the fragment.
    /// Returns an empty array if the fragment doesn't exist or has no tags.
    /// </summary>
    EmotionalTag[] GetFragmentTags(string fragmentId);

    /// <summary>
    /// Returns all fragment IDs known to the system.
    /// Used by QueryFragmentsByTag to scan for tag matches.
    /// </summary>
    string[] GetAllFragmentIds();
}
