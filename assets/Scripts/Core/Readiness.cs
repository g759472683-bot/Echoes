/// <summary>
/// Three-state readiness model for assets managed by DataManager.
/// Every asset tracked in the _readiness dictionary is in exactly one of these states.
///
/// State transitions:
///   NotRequested → Loading (when GetAsync initiates a load)
///   Loading → Cached (on successful load)
///   Loading → NotRequested (on load failure, retryable)
///   Cached → NotRequested (when released via ReleaseFragment/UnloadChapter)
/// </summary>
internal enum Readiness
{
    /// <summary>Asset has not been requested or has been released. Next access triggers a load.</summary>
    NotRequested,

    /// <summary>Asset is actively being loaded. Concurrent requests return the same Task reference.</summary>
    Loading,

    /// <summary>Asset is in memory. GetAsync returns immediately with zero allocations.</summary>
    Cached
}
