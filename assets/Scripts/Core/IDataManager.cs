using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Interface for fragment data loading and lifecycle management.
/// Implemented by DataManager (ADR-0002). All public methods return Task&lt;T&gt;
/// — callers MUST await, never use .Result or .Wait() on the main thread (ADR-0007).
///
/// Full interface per ADR-0002:
///   GetChapterAsync, GetFragmentAsync, GetIllustrationAsync — asset loading
///   IsReady — synchronous cache check (does NOT trigger loads)
///   GetCachedFragment — sync lookup for prefetched fragments
///   GetFragmentsByChapter — returns all fragment IDs in a chapter
///   PreloadChapterAsync — preload heavy assets (Story 003)
///   UnloadChapter, ReleaseFragment — memory management
/// </summary>
public interface IDataManager
{
    /// <summary>
    /// Loads a ChapterDefinition ScriptableObject by chapter key.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier (e.g., "ch01").</param>
    /// <returns>Task that resolves to the ChapterDefinition.</returns>
    Task<ChapterDefinition> GetChapterAsync(string chapterKey);

    /// <summary>
    /// Loads a memory fragment's data by chapter key and fragment ID.
    /// Returns the fully populated MemoryFragment including illustration key,
    /// audio keys, and interactive object definitions.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier (e.g., "chapter_1").</param>
    /// <param name="fragmentId">The fragment identifier within the chapter (e.g., "frag_01").</param>
    /// <returns>Task that resolves to the populated MemoryFragment.</returns>
    Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId);

    /// <summary>
    /// Loads the background illustration sprite for a fragment.
    /// The sprite is applied to the scene's SpriteRenderer.
    /// </summary>
    /// <param name="illustrationKey">The illustration key from MemoryFragment.IllustrationKey.</param>
    /// <returns>Task that resolves to the loaded Sprite.</returns>
    Task<Sprite> GetIllustrationAsync(string illustrationKey);

    /// <summary>
    /// Loads the background illustration sprite for a specific fragment.
    /// On failure, the thrown <see cref="DataLoadException"/> will carry the
    /// fragment ID for diagnostics (AC-1). Prefer this overload when the
    /// calling context knows which fragment the illustration belongs to.
    /// </summary>
    /// <param name="illustrationKey">The illustration key from MemoryFragment.IllustrationKey.</param>
    /// <param name="fragmentId">The fragment that owns this illustration (for error reporting).</param>
    /// <returns>Task that resolves to the loaded Sprite.</returns>
    Task<Sprite> GetIllustrationAsync(string illustrationKey, string fragmentId);

    /// <summary>
    /// Preloads all assets for a chapter in the background (ADR-0002).
    /// Called by GameSceneManager when entering Game (initial chapter) and
    /// when 3 or fewer fragments remain in the current chapter (ADR-0004).
    /// Fire-and-forget — failures are non-blocking and logged as warnings.
    /// </summary>
    /// <param name="chapterKey">The chapter to preload.</param>
    /// <returns>Task that completes when preload finishes.</returns>
    Task PreloadChapterAsync(string chapterKey);

    /// <summary>
    /// Synchronously checks whether a given asset key is cached and ready.
    /// Returns true if the asset is in Cached state (immediately available).
    /// Returns false for Loading and NotRequested states.
    /// Does NOT trigger a load — safe to call in hot paths.
    /// </summary>
    /// <param name="assetKey">The Addressables key to check.</param>
    /// <returns>True if the asset is cached and ready for immediate access.</returns>
    bool IsReady(string assetKey);

    /// <summary>
    /// Synchronous cache lookup for an already-loaded fragment.
    /// The fragment must have been previously loaded via GetFragmentAsync.
    /// Returns null if the fragment is not in the in-memory cache.
    /// Used by InteractionManager during OnFragmentTransitioned callback.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier.</param>
    /// <param name="fragmentId">The fragment identifier.</param>
    /// <returns>The cached MemoryFragment, or null if not found.</returns>
    MemoryFragment GetCachedFragment(string chapterKey, string fragmentId);

    /// <summary>
    /// Returns all MemoryFragment instances for the given chapter.
    /// Returns an empty list (never null) for unknown chapters.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier.</param>
    /// <returns>A new List containing the chapter's fragments.</returns>
    List<MemoryFragment> GetFragmentsByChapter(string chapterKey);

    /// <summary>
    /// Sets the current chapter the player is in. Used by preload trigger
    /// to determine the next chapter and by cache strategy enforcement.
    /// </summary>
    void SetCurrentChapter(string chapterKey);

    /// <summary>
    /// Checks remaining fragments and triggers next-chapter preload if at or below
    /// the threshold. Safe to call when there is no next chapter.
    /// </summary>
    void CheckAndTriggerPreload(string currentChapterKey, int remainingFragments);

    /// <summary>
    /// Releases all Addressables handles for a given chapter.
    /// Story 003 will implement the full unload logic.
    /// </summary>
    /// <param name="chapterKey">The chapter to unload.</param>
    void UnloadChapter(string chapterKey);

    /// <summary>
    /// Releases resources held by a fragment that is no longer displayed.
    /// Called by SceneManager.UnloadCurrentFragment during transitions.
    /// </summary>
    /// <param name="fragmentId">The fragment ID to release.</param>
    void ReleaseFragment(string fragmentId);

    /// <summary>
    /// Preloads a specific fragment's data in the background (fire-and-forget).
    /// Called by GameSceneManager.PreloadNextFragmentAsync to warm the cache
    /// for the next sequential fragment. If the fragment is already cached,
    /// this is a no-op.
    /// </summary>
    /// <param name="chapterKey">The chapter identifier.</param>
    /// <param name="fragmentId">The fragment to preload.</param>
    /// <returns>Task that completes when preload finishes or fails (non-blocking).</returns>
    Task PreloadFragmentAsync(string chapterKey, string fragmentId);

    /// <summary>
    /// Serializes a state object to a JSON string using System.Text.Json.
    /// Uses camelCase naming, production-compressed output (no indentation),
    /// and includes custom converters for Unity types (Vector2, Color).
    /// Pure CPU operation — returns synchronously.
    /// </summary>
    /// <typeparam name="T">The type of state object to serialize.</typeparam>
    /// <param name="state">The state object to serialize.</param>
    /// <returns>A JSON string representation of the state object.</returns>
    string SerializeState<T>(T state) where T : class;

    /// <summary>
    /// Deserializes a JSON string back to a state object of type <typeparamref name="T"/>.
    /// Throws <see cref="DataLoadException"/> on parse failure (AssetKey="json_parse"),
    /// null result (AssetKey="deserialize"), or type mismatch.
    /// Unknown JSON fields are silently ignored. Pure CPU operation — returns synchronously.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A populated instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="DataLoadException">Thrown when JSON is invalid, null, or deserialization fails.</exception>
    T DeserializeState<T>(string json) where T : class;
}
