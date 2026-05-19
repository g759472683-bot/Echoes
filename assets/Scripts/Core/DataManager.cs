using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Controls how many chapters' illustration assets are kept in memory simultaneously.
/// </summary>
public enum ChapterCacheStrategy
{
    /// <summary>Only the current chapter's illustrations are cached.</summary>
    CurrentOnly,
    /// <summary>Current chapter + next chapter (preloaded). Max 2 chapters.</summary>
    CurrentPlusNext,
    /// <summary>All preloaded chapters remain cached (use with care on memory-constrained platforms).</summary>
    All
}

/// <summary>
/// Core async loading engine that wraps Unity Addressables with a three-state readiness model
/// (Cached / Loading / NotRequested) and concurrent request deduplication.
///
/// Implements <see cref="IDataManager"/> as defined by ADR-0002. All public methods return
/// <see cref="Task{T}"/> — callers MUST await, never use .Result or .Wait() on the main thread.
///
/// State machine (ADR-0002):
///   Uninitialized → LoadingMetadata → Ready (success path)
///   LoadingMetadata → Error (failure)
///   Error → Uninitialized (return-to-menu retry)
///   Ready ↔ PreloadingChapter (Story 003)
///
/// Events declared here (ADR-0001):
///   OnStateChanged(DataManagerState) — fires on every state transition
///
/// Usage:
///   // Production: DataManager MonoBehaviour in scene, configured via Inspector _chapterKeys.
///   // Test: Create GameObject, AddComponent, inject mock loader + chapter keys, call InitializeAsync().
///   var dm = gameObject.AddComponent<DataManager>();
///   dm.SetLoader(new MockAddressableLoader());
///   dm.SetChapterKeys(new[] { "ch01", "ch02" });
///   await dm.InitializeAsync();
///   var fragment = await dm.GetFragmentAsync("ch01", "frag_03");
/// </summary>
public class DataManager : MonoBehaviour, IDataManager
{
    // =========================================================================
    // Static Events (ADR-0001)
    // =========================================================================

    /// <summary>
    /// Fires on every DataManagerState transition. External systems (HUD, SceneManager)
    /// subscribe via OnEnable / OnDisable. Tests must null this in [TearDown].
    /// </summary>
    public static event Action<DataManagerState> OnStateChanged;

    // =========================================================================
    // Configuration (injectable for tests)
    // =========================================================================

    /// <summary>
    /// Addressables keys for all chapter definitions. Set via Inspector in production,
    /// or via SetChapterKeys() in tests before InitializeAsync().
    /// </summary>
    [SerializeField]
    private string[] _chapterKeys;

    private IAddressableLoader _loader;

    /// <summary>
    /// Shared JsonSerializerOptions for SerializeState/DeserializeState (ADR-0003).
    /// Stateless and thread-safe once configured. Uses camelCase naming, compressed
    /// output, and custom converters for Unity types (Vector2, Color).
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters =
        {
            new Vector2Converter(),
            new ColorConverter()
        }
    };

    // =========================================================================
    // Caches & State (ADR-0002 core data structures)
    // =========================================================================

    /// <summary>Asset cache: Addressables key → loaded asset (any type).</summary>
    private readonly Dictionary<string, object> _cache = new();

    /// <summary>Pending async loads: key → TaskCompletionSource.Task (stored as Task for dedup).</summary>
    private readonly Dictionary<string, Task> _pendingLoads = new();

    /// <summary>Readiness tracking: key → current state in three-state model.</summary>
    private readonly Dictionary<string, Readiness> _readiness = new();

    /// <summary>Chapter → list of loaded MemoryFragment SOs (populated during metadata loading).</summary>
    private readonly Dictionary<string, List<MemoryFragment>> _chapterFragments = new();

    /// <summary>FragmentId → Addressables cache key (for ReleaseFragment lookup).</summary>
    private readonly Dictionary<string, string> _fragmentKeyMap = new();

    /// <summary>FragmentId → ChapterKey (for ReleaseFragment _chapterFragments cleanup).</summary>
    private readonly Dictionary<string, string> _fragmentChapterMap = new();

    /// <summary>ChapterKey → in-flight preload Task (for chapter transition await).</summary>
    private readonly Dictionary<string, Task> _preloadTasks = new();

    /// <summary>Chapters whose illustrations have been preloaded via DownloadDependenciesAsync.</summary>
    private readonly HashSet<string> _preloadedChapters = new();

    /// <summary>Cancellation tokens for in-flight preloads (clean cancel on UnloadChapter).</summary>
    private readonly Dictionary<string, CancellationTokenSource> _preloadCancellations = new();

    /// <summary>Remaining fragment count that triggers next-chapter preload.</summary>
    [SerializeField] [Range(1, 5)] private int _preloadThreshold = 3;

    /// <summary>How many chapters' illustrations to keep in memory.</summary>
    [SerializeField] private ChapterCacheStrategy _chapterIllustrationCache = ChapterCacheStrategy.CurrentPlusNext;

    /// <summary>The chapter the player is currently in (set by SceneManager via SetCurrentChapter).</summary>
    private string _currentChapterKey;

    // =========================================================================
    // State Machine
    // =========================================================================

    private DataManagerState _state = DataManagerState.Uninitialized;
    private bool _initialized;

    /// <summary>Exposes current state for tests. Immutable from outside.</summary>
    internal DataManagerState CurrentState => _state;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        if (_loader == null)
            _loader = new UnityAddressableLoader();
    }

    private async void Start()
    {
        if (!_initialized)
        {
            _initialized = true;
            await LoadMetadata();
        }
    }

    private void OnDestroy()
    {
        OnStateChanged = null;
    }

    // =========================================================================
    // Test Injection Points
    // =========================================================================

    /// <summary>
    /// Injects a mock IAddressableLoader for unit testing.
    /// Must be called BEFORE InitializeAsync() or Start().
    /// </summary>
    internal void SetLoader(IAddressableLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// Injects chapter keys for unit testing. Mirrors the Inspector [SerializeField].
    /// Must be called BEFORE InitializeAsync() or Start().
    /// </summary>
    internal void SetChapterKeys(string[] keys)
    {
        _chapterKeys = keys;
    }

    /// <summary>
    /// Explicitly triggers metadata loading. Used in tests where Unity Start() is not
    /// called automatically. Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    internal async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await LoadMetadata();
    }

    // =========================================================================
    // Metadata Loading
    // =========================================================================

    /// <summary>
    /// Loads all ChapterDefinition SOs and their fragment metadata.
    /// On success, transitions to Ready. On failure, transitions to Error.
    /// Empty _chapterKeys is valid — transitions to Ready immediately.
    /// </summary>
    private async Task LoadMetadata()
    {
        if (_state != DataManagerState.Uninitialized) return;

        SetState(DataManagerState.LoadingMetadata);

        try
        {
            if (_chapterKeys == null || _chapterKeys.Length == 0)
            {
                SetState(DataManagerState.Ready);
                return;
            }

            foreach (var chapterKey in _chapterKeys)
            {
                var chapterDef = await GetAsync<ChapterDefinition>(chapterKey);

                var fragments = new List<MemoryFragment>();
                if (chapterDef.Fragments != null)
                {
                    foreach (var fragRef in chapterDef.Fragments)
                    {
                        string fragKey = fragRef.RuntimeKey.ToString();
                        var frag = await GetAsync<MemoryFragment>(fragKey);
                        fragments.Add(frag);
                        _fragmentKeyMap[frag.FragmentId] = fragKey;
                        _fragmentChapterMap[frag.FragmentId] = chapterKey;
                    }
                }
                _chapterFragments[chapterKey] = fragments;
            }

            SetState(DataManagerState.Ready);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] Metadata load failed: {ex.Message}");
            SetState(DataManagerState.Error);
        }
    }

    private void SetState(DataManagerState newState)
    {
        if (_state == newState) return;
        _state = newState;
        OnStateChanged?.Invoke(newState);
    }

    // =========================================================================
    // Core Loading Engine — GetAsync<T>
    // =========================================================================

    /// <summary>
    /// Core async load method with three-state readiness and dedup.
    ///
    /// - Cached: returns completed Task.FromResult (zero allocation).
    /// - Loading: returns the EXISTING Task from _pendingLoads (dedup — same reference).
    /// - NotRequested: creates a TaskCompletionSource, kicks off Addressables load,
    ///   caches on success, throws DataLoadException on failure.
    ///
    /// NEVER use async/await on this method — it returns the raw TCS.Task for dedup
    /// so that concurrent callers receive the identical Task reference.
    ///
    /// <paramref name="fragmentId"/> is optional. When provided and the load fails,
    /// the resulting <see cref="DataLoadException"/> will carry the fragment ID
    /// for diagnostics (Story 004 AC-1).
    /// </summary>
    private Task<T> GetAsync<T>(string key, string fragmentId = null) where T : class
    {
        // --- Cached path: immediate return, zero allocation ---
        if (_cache.TryGetValue(key, out var cached) && cached is T typed)
        {
            _readiness[key] = Readiness.Cached;
            return Task.FromResult(typed);
        }

        // --- Loading path: dedup — return existing Task reference ---
        if (_pendingLoads.TryGetValue(key, out var existingTask))
        {
            if (existingTask is Task<T> typedTask)
                return typedTask;

            // Same key requested with incompatible type parameter — fault with
            // DataLoadException rather than throwing InvalidCastException.
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetException(new DataLoadException(key,
                new InvalidOperationException(
                    $"Asset '{key}' is loading as {existingTask.GetType().GetGenericArguments()[0].Name}, " +
                    $"cannot cast to {typeof(T).Name}")));
            return tcs.Task;
        }

        // --- NotRequested path: initiate load ---
        _readiness[key] = Readiness.Loading;
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingLoads[key] = tcs.Task;
        _ = LoadAndCacheAsync(key, tcs, fragmentId);
        return tcs.Task;
    }

    /// <summary>
    /// Executes the actual Addressables load for a given key and completes the TCS.
    /// Runs as a fire-and-forget task; the caller receives the TCS.Task which is
    /// completed (SetResult or SetException) inside this method.
    ///
    /// <paramref name="fragmentId"/> is optional. When provided and the load fails,
    /// the <see cref="DataLoadException"/> is constructed with the three-parameter
    /// overload so error handlers can identify the owning fragment.
    /// </summary>
    private async Task LoadAndCacheAsync<T>(string key, TaskCompletionSource<T> tcs, string fragmentId = null) where T : class
    {
        try
        {
            T asset = await _loader.LoadAssetAsync<T>(key);
            _cache[key] = asset;
            _readiness[key] = Readiness.Cached;
            tcs.SetResult(asset);
        }
        catch (Exception ex)
        {
            _readiness[key] = Readiness.NotRequested;
            var dle = fragmentId != null
                ? new DataLoadException(key, fragmentId, ex)
                : new DataLoadException(key, ex);
            tcs.SetException(dle);
        }
        finally
        {
            _pendingLoads.Remove(key);
        }
    }

    // =========================================================================
    // IDataManager — Public API
    // =========================================================================

    /// <inheritdoc/>
    public Task<ChapterDefinition> GetChapterAsync(string chapterKey)
    {
        return GetAsync<ChapterDefinition>(chapterKey);
    }

    /// <summary>
    /// Returns a MemoryFragment by chapter key and fragment ID.
    /// Uses the fast path via _chapterFragments lookup for pre-loaded fragments,
    /// falling back to GetAsync for fragments loaded outside metadata.
    ///
    /// Cached fragments return in under 50ms (AC-2). Missing fragments throw
    /// DataLoadException or result in a Task faulted with DataLoadException.
    /// </summary>
    public Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)
    {
        // Fast path: check chapter fragments dictionary (populated during metadata)
        if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
        {
            var match = fragments.FirstOrDefault(f => f.FragmentId == fragmentId);
            if (match != null)
                return Task.FromResult(match);
        }

        // Slow path: load via Addressables (three-state dedup)
        string key = $"{chapterKey}/{fragmentId}";
        return GetAsync<MemoryFragment>(key);
    }

    /// <inheritdoc/>
    public Task<Sprite> GetIllustrationAsync(string assetKey)
    {
        return GetAsync<Sprite>(assetKey);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On load failure, the resulting <see cref="DataLoadException"/> includes
    /// both <c>AssetKey</c> and <c>FragmentId</c> for precise error diagnosis.
    /// </remarks>
    public Task<Sprite> GetIllustrationAsync(string assetKey, string fragmentId)
    {
        return GetAsync<Sprite>(assetKey, fragmentId);
    }

    /// <inheritdoc/>
    public MemoryFragment GetCachedFragment(string chapterKey, string fragmentId)
    {
        if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
        {
            return fragments.FirstOrDefault(f => f.FragmentId == fragmentId);
        }
        return null;
    }

    /// <summary>
    /// Returns the cached readiness state for the given asset key.
    /// Returns true only if the asset is Cached (immediately available).
    /// Does NOT trigger a load for NotRequested assets (AC-6).
    /// </summary>
    public bool IsReady(string assetKey)
    {
        return _readiness.TryGetValue(assetKey, out var readiness)
            && readiness == Readiness.Cached;
    }

    /// <summary>
    /// Returns a copy of the fragment list for the given chapter.
    /// Returns an empty list (not null) for unknown chapters.
    /// </summary>
    public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
    {
        if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
            return new List<MemoryFragment>(fragments);
        return new List<MemoryFragment>();
    }

    /// <summary>
    /// Releases the Addressables handle for a fragment, removing it from the cache.
    /// The fragment can be re-loaded on next request.
    /// </summary>
    public void ReleaseFragment(string fragmentId)
    {
        if (_fragmentKeyMap.TryGetValue(fragmentId, out var cacheKey))
        {
            _loader.Release(cacheKey);
            _cache.Remove(cacheKey);
            _readiness.Remove(cacheKey);
            _fragmentKeyMap.Remove(fragmentId);

            // Also remove from _chapterFragments so GetCachedFragment / GetFragmentAsync
            // fast path don't return a fragment whose Addressables handle was released.
            if (_fragmentChapterMap.TryGetValue(fragmentId, out var chapterKey))
            {
                _fragmentChapterMap.Remove(fragmentId);
                if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
                {
                    fragments.RemoveAll(f => f.FragmentId == fragmentId);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task PreloadFragmentAsync(string chapterKey, string fragmentId)
    {
        MemoryFragment fragment = GetCachedFragment(chapterKey, fragmentId);
        if (fragment == null || string.IsNullOrEmpty(fragment.IllustrationKey))
            return;

        try
        {
            await GetAsync<Sprite>(fragment.IllustrationKey, fragmentId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[DataManager] Fragment preload failed for {chapterKey}/{fragmentId}: {ex.Message}");
        }
    }

    // =========================================================================
    // Serialization Bridge (ADR-0003)
    // =========================================================================

    /// <inheritdoc/>
    public string SerializeState<T>(T state) where T : class
    {
        return JsonSerializer.Serialize(state, _jsonOptions);
    }

    /// <inheritdoc/>
    public T DeserializeState<T>(string json) where T : class
    {
        if (json == null)
            throw new DataLoadException("json_parse", "",
                new ArgumentNullException(nameof(json)));

        try
        {
            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            if (result == null)
                throw new DataLoadException("deserialize", "",
                    new InvalidOperationException($"Deserialization returned null for {typeof(T).Name}"));
            return result;
        }
        catch (DataLoadException)
        {
            // Re-throw DataLoadException as-is (null result case above).
            throw;
        }
        catch (JsonException ex)
        {
            throw new DataLoadException("json_parse", "", ex);
        }
        catch (Exception ex)
        {
            // NotSupportedException (unsupported type), ArgumentException (bad options),
            // or any unexpected failure — wrap so callers see uniform DLE contract.
            throw new DataLoadException("deserialize", "", ex);
        }
    }

    // =========================================================================
    // Story 003 — Chapter Preload & Memory Management
    // =========================================================================

    /// <summary>
    /// Sets the current chapter the player is in. Used by cache strategy enforcement
    /// and preload trigger to determine the "next" chapter.
    /// </summary>
    public void SetCurrentChapter(string chapterKey)
    {
        _currentChapterKey = chapterKey;
        EnforceCacheStrategy();
    }

    /// <summary>
    /// Checks whether preload should trigger based on remaining fragments.
    /// Called by SceneManager when fragment progress updates.
    /// If remainingFragments ≤ _preloadThreshold, kicks off PreloadChapterAsync for the next chapter.
    /// Safe to call when no next chapter exists — gracefully no-ops.
    /// </summary>
    public void CheckAndTriggerPreload(string currentChapterKey, int remainingFragments)
    {
        if (remainingFragments > _preloadThreshold) return;
        if (_preloadTasks.Count > 0) return;

        string nextChapter = GetNextChapterKey(currentChapterKey);
        if (nextChapter == null) return;

        _ = PreloadChapterAsync(nextChapter);
    }

    /// <inheritdoc/>
    public async Task PreloadChapterAsync(string chapterKey)
    {
        // Already preloaded or preloading
        if (_preloadedChapters.Contains(chapterKey) || _preloadTasks.ContainsKey(chapterKey))
            return;

        var cts = new CancellationTokenSource();
        _preloadCancellations[chapterKey] = cts;

        var preloadTask = PreloadChapterInternalAsync(chapterKey, cts.Token);
        _preloadTasks[chapterKey] = preloadTask;

        if (_preloadTasks.Count == 1)
            SetState(DataManagerState.PreloadingChapter);

        try
        {
            await preloadTask;
        }
        finally
        {
            _preloadTasks.Remove(chapterKey);
            _preloadCancellations.Remove(chapterKey);
            cts.Dispose();

            if (_preloadTasks.Count == 0)
            {
                if (_state == DataManagerState.PreloadingChapter)
                    SetState(DataManagerState.Ready);
            }
        }
    }

    private async Task PreloadChapterInternalAsync(string chapterKey, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            string artGroupLabel = GetArtGroupLabel(chapterKey);
            await _loader.DownloadDependenciesAsync(artGroupLabel);

            // Re-check cancellation before marking as preloaded — UnloadChapter may
            // have cancelled during the download, and we must not re-add a chapter
            // that was just removed.
            ct.ThrowIfCancellationRequested();

            _preloadedChapters.Add(chapterKey);
            EnforceCacheStrategy();
        }
        catch (OperationCanceledException)
        {
            // Preload was cancelled by UnloadChapter — silent, expected.
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DataManager] Preload failed for chapter {chapterKey}: {ex.Message}");
            // Never throw — main load path retries on chapter transition.
        }
    }

    /// <inheritdoc/>
    public void UnloadChapter(string chapterKey)
    {
        // Cancel in-flight preload before releasing handles
        if (_preloadCancellations.TryGetValue(chapterKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _preloadCancellations.Remove(chapterKey);
        }
        _preloadTasks.Remove(chapterKey);
        _preloadedChapters.Remove(chapterKey);

        // Gather cache keys belonging to this chapter
        var keysToRelease = new List<string>();
        foreach (var kvp in _cache)
        {
            if (kvp.Key == chapterKey ||
                kvp.Key.StartsWith(chapterKey + "/") ||
                BelongsToChapter(chapterKey, kvp.Key))
            {
                keysToRelease.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRelease)
        {
            if (key.StartsWith("Shared_")) continue;
            _loader.Release(key);
            _cache.Remove(key);
            _readiness.Remove(key);
            _pendingLoads.Remove(key);
        }

        // Release the download handle for this chapter's art group (if preloaded)
        string artGroupLabel = GetArtGroupLabel(chapterKey);
        _loader.Release(artGroupLabel);

        // Remove chapter fragments from metadata dictionaries
        if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
        {
            foreach (var frag in fragments)
            {
                _fragmentKeyMap.Remove(frag.FragmentId);
                _fragmentChapterMap.Remove(frag.FragmentId);
            }
            _chapterFragments.Remove(chapterKey);
        }
    }

    private void EnforceCacheStrategy()
    {
        if (_chapterIllustrationCache == ChapterCacheStrategy.All) return;

        if (_chapterIllustrationCache == ChapterCacheStrategy.CurrentPlusNext
            && _preloadedChapters.Count > 2)
        {
            var nextChapter = GetNextChapterKey(_currentChapterKey);
            var toRemove = _preloadedChapters
                .Where(c => c != _currentChapterKey && c != nextChapter)
                .OrderBy(c => c)
                .FirstOrDefault();
            if (toRemove != null)
            {
                UnloadChapter(toRemove);
            }
        }
        else if (_chapterIllustrationCache == ChapterCacheStrategy.CurrentOnly
                 && _preloadedChapters.Count > 1)
        {
            var toRemove = _preloadedChapters
                .Where(c => c != _currentChapterKey)
                .OrderBy(c => c)
                .FirstOrDefault();
            if (toRemove != null)
            {
                UnloadChapter(toRemove);
            }
        }
    }

    private bool BelongsToChapter(string chapterKey, string assetKey)
    {
        if (_chapterFragments.TryGetValue(chapterKey, out var fragments))
        {
            return fragments.Any(f =>
                f.IllustrationKey == assetKey ||
                (f.AudioKeys != null && f.AudioKeys.Contains(assetKey)));
        }
        return false;
    }

    private static string GetArtGroupLabel(string chapterKey)
    {
        // "ch01" → "Art_Ch01", "ch02" → "Art_Ch02"
        return $"Art_{char.ToUpper(chapterKey[0])}{chapterKey.Substring(1)}";
    }

    private static string GetNextChapterKey(string currentChapterKey)
    {
        // Simple numeric increment: "ch01" → "ch02", "ch02" → "ch03"
        if (currentChapterKey.Length >= 4
            && int.TryParse(currentChapterKey.Substring(2), out int num))
        {
            return $"ch{num + 1:D2}";
        }
        return null;
    }
}

// =============================================================================
// Internal Addressable Loader Abstraction (testability seam)
// =============================================================================

/// <summary>
/// Internal interface that wraps Unity Addressables for testability.
/// The production implementation (<see cref="UnityAddressableLoader"/>) delegates
/// to the real Addressables API. Unit tests use a mock that returns pre-set assets.
/// </summary>
internal interface IAddressableLoader
{
    /// <summary>
    /// Loads an asset of type T by key, wrapping Addressables.LoadAssetAsync.
    /// </summary>
    Task<T> LoadAssetAsync<T>(string key) where T : class;

    /// <summary>
    /// Releases the Addressables handle for the given key.
    /// </summary>
    void Release(string key);

    /// <summary>
    /// Downloads AssetBundle dependencies for the given Addressables label.
    /// Maps to Addressables.DownloadDependenciesAsync(label).
    /// </summary>
    Task DownloadDependenciesAsync(string label);
}

/// <summary>
/// Production IAddressableLoader that delegates to the real Unity Addressables API.
/// Each load creates an AsyncOperationHandle which is tracked for release.
/// All load calls are wrapped in try/catch per ADR-0002 (Addressables 6.2+ throws on failure).
/// </summary>
internal class UnityAddressableLoader : IAddressableLoader
{
    private readonly Dictionary<string, AsyncOperationHandle> _handles = new();
    private readonly Dictionary<string, AsyncOperationHandle> _downloadHandles = new();

    public async Task<T> LoadAssetAsync<T>(string key) where T : class
    {
        var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(key);
        _handles[key] = handle;
        return await handle.Task;
    }

    public void Release(string key)
    {
        if (_handles.TryGetValue(key, out var handle))
        {
            if (handle.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(handle);
            _handles.Remove(key);
        }
        if (_downloadHandles.TryGetValue(key, out var downloadHandle))
        {
            if (downloadHandle.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(downloadHandle);
            _downloadHandles.Remove(key);
        }
    }

    public async Task DownloadDependenciesAsync(string label)
    {
        var handle = UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync(label);
        _downloadHandles[label] = handle;
        await handle.Task;
    }
}
