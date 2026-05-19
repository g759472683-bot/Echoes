/// <summary>
/// Minimal read-only view of a MemoryFragment for the validation engine.
///
/// Extracted from MemoryFragment to decouple FragmentValidator from Unity's
/// ScriptableObject — enables pure C# unit testing with mock objects.
/// MemoryFragment implicitly implements this interface.
/// </summary>
public interface IFragmentValidationTarget
{
    string FragmentId { get; }
    string ChapterKey { get; }
    InteractiveObject[] InteractiveObjects { get; }
    ChoiceGroup[] ChoiceGroups { get; }
}
