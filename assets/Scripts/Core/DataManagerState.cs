/// <summary>
/// Five-state lifecycle for the DataManager core engine.
/// Drives cross-system communication via <see cref="DataManager.OnStateChanged"/>.
///
/// State diagram:
///   Uninitialized → LoadingMetadata → Ready (success path)
///   LoadingMetadata → Error (failure path)
///   Error → Uninitialized (return-to-menu retry)
///   Ready ↔ PreloadingChapter (chapter preload begin/complete)
/// </summary>
public enum DataManagerState
{
    /// <summary>Initial state before any metadata loading has begun. No assets are available.</summary>
    Uninitialized,

    /// <summary>Chapter definitions and fragment metadata are being loaded from Addressables.</summary>
    LoadingMetadata,

    /// <summary>All metadata is loaded and cached. Assets can be requested via GetFragmentAsync, etc.</summary>
    Ready,

    /// <summary>A chapter preload is in progress (Story 003). Existing cached assets remain available.</summary>
    PreloadingChapter,

    /// <summary>Metadata loading failed. No assets are available. Callers should return to main menu.</summary>
    Error
}
