using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Narrow interface for ending resolution consumed by ChapterManager (ADR-0010).
/// Implemented by MultiEndingSystem (#14).
/// </summary>
public interface IEndingResolver
{
    ResolvedEnding ResolveEnding(string chapterId);
    void OnChapterStart(string chapterId);
    HashSet<string> GetUnlockedEndingIds();
    MultiEndingSaveData GetSaveData();
    void Restore(MultiEndingSaveData data);
}

/// <summary>
/// Narrow interface for web association queries consumed by ChapterManager.
/// Implemented by WebAssociationEngine (#13).
/// </summary>
public interface IAssociationProvider
{
    List<AssociationCandidate> ComputeAssociations(
        string currentFragmentId, string chapterKey,
        List<string> recentHistory, HashSet<string> sessionVisitedFragments);
}

/// <summary>
/// Narrow interface for scene transitions consumed by ChapterManager.
/// Implemented by GameSceneManager (#6).
/// </summary>
public interface IChapterSceneProvider
{
    Task TransitionToFragmentAsync(string chapterKey, string fragmentId);
    Task TransitionToChapterAsync(string chapterKey);
    Task PreloadChapterAsync(string chapterKey);
}
