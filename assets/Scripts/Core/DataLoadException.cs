using System;

/// <summary>
/// Exception thrown when an asset fails to load via Addressables.
/// Carries the asset key and optional fragment ID so error-handling systems
/// can identify exactly which asset failed and why.
///
/// Two constructors:
///   DataLoadException(assetKey, innerException) — for general-purpose loads
///     where no fragment context is available (e.g., ChapterDefinition, shared assets).
///   DataLoadException(assetKey, fragmentId, innerException) — for fragment-scoped
///     loads where the owning fragment is known (e.g., illustration, audio).
///
/// Usage:
///   catch (DataLoadException ex)
///   {
///       Debug.LogError($"Failed to load '{ex.AssetKey}' for fragment '{ex.FragmentId}': {ex.Message}");
///       // Surface error UI with fragment context when FragmentId is available
///   }
/// </summary>
public class DataLoadException : Exception
{
    /// <summary>
    /// The Addressables key that failed to load.
    /// </summary>
    public string AssetKey { get; }

    /// <summary>
    /// The fragment ID that referenced the failed asset, or null if
    /// the load was not fragment-scoped (e.g., chapter metadata).
    /// </summary>
    public string FragmentId { get; }

    /// <summary>
    /// Creates a DataLoadException for a general-purpose asset load.
    /// FragmentId will be null — use this when the load has no fragment context.
    /// </summary>
    /// <param name="assetKey">The Addressables key that failed to load.</param>
    /// <param name="innerException">The original exception from Addressables, or null if none.</param>
    public DataLoadException(string assetKey, Exception innerException)
        : base($"Failed to load asset: {assetKey}", innerException)
    {
        AssetKey = assetKey;
        FragmentId = null;
    }

    /// <summary>
    /// Creates a DataLoadException for a fragment-scoped asset load.
    /// Both the asset key and the owning fragment ID are captured for diagnostics.
    /// </summary>
    /// <param name="assetKey">The Addressables key that failed to load (e.g., "art_ch01_frag01").</param>
    /// <param name="fragmentId">The fragment that referenced this asset (e.g., "frag_01").</param>
    /// <param name="innerException">The original exception from Addressables.</param>
    public DataLoadException(string assetKey, string fragmentId, Exception innerException)
        : base($"Failed to load asset '{assetKey}' for fragment '{fragmentId}': {innerException?.Message}", innerException)
    {
        AssetKey = assetKey;
        FragmentId = fragmentId;
    }
}
