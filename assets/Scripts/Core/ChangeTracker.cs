using UnityEngine;

/// <summary>
/// Game-scene persistent MonoBehaviour for change tracking (ADR-0007).
///
/// Wraps ChangeTrackerCore (pure C# logic) for Unity lifecycle integration.
/// Implements IChangeTracker so Condition.Evaluate() can query runtime state.
///
/// Singleton pattern — one instance per play session, created on the Game scene's
/// persistent GameObject. Query methods (GetFlag, HasVisited, etc.) are fully
/// implemented in Story 003; Story 001 provides the ApplyChanges pipeline.
///
/// <b>ADR-0001 static event:</b> OnOverlayChanged(string fragmentId)
/// </summary>
public class ChangeTracker : MonoBehaviour, IChangeTracker, IChangeTrackerInternal
{
    // =========================================================================
    // Singleton
    // =========================================================================

    public static ChangeTracker Instance { get; private set; }

    // =========================================================================
    // Core (public for test injection)
    // =========================================================================

    public ChangeTrackerCore Core { get; private set; }

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Core requires IFragmentRegistry — production implementation
        // is wired in Story 003 when Flag system is built.
        // For now, core is initialized with a no-op registry.
        // Story 003 replaces Core with fully wired instance.
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            ChangeTrackerCore.ResetStaticEvents();
        }
    }

    // =========================================================================
    // Initialization (called by BootBootstrap or test setup)
    // =========================================================================

    /// <summary>
    /// Initializes the change tracker with a fragment registry.
    /// Must be called before any ApplyChanges calls.
    /// </summary>
    public void Initialize(IFragmentRegistry registry)
    {
        Core = new ChangeTrackerCore(registry);
    }

    /// <summary>
    /// Initializes the change tracker with a fragment registry and state provider.
    /// The state provider enables GetObjectState/GetTagWeight queries via GetCurrentState.
    /// </summary>
    public void Initialize(IFragmentRegistry registry, IFragmentStateProvider stateProvider)
    {
        Core = new ChangeTrackerCore(registry) { StateProvider = stateProvider };
    }

    // =========================================================================
    // ApplyChanges (delegates to Core)
    // =========================================================================

    /// <summary>
    /// Applies a batch of ContentChanges from a player choice.
    /// Delegates to ChangeTrackerCore.ApplyChanges.
    /// </summary>
    public void ApplyChanges(string targetFragmentId, string choiceId, ContentChange[] changes)
    {
        if (Core == null)
        {
            Debug.LogWarning("ChangeTracker.ApplyChanges: Core not initialized. Call Initialize() first.");
            return;
        }
        Core.ApplyChanges(targetFragmentId, choiceId, changes);
    }

    // =========================================================================
    // Flag / Tracking Methods (delegates to Core)
    // =========================================================================

    /// <summary>Sets a global narrative flag. Delegates to Core.</summary>
    public void SetFlag(string flagId, bool value) => Core?.SetFlag(flagId, value);

    /// <summary>Records a fragment visit. Delegates to Core.</summary>
    public void RecordVisit(string fragmentId) => Core?.RecordVisit(fragmentId);

    /// <summary>Records a chapter completion. Delegates to Core.</summary>
    public void RecordChapterCompleted(string chapterId) => Core?.RecordChapterCompleted(chapterId);

    // =========================================================================
    // Save / Restore (Story 004 — delegates to Core)
    // =========================================================================

    /// <summary>
    /// Builds a serializable snapshot of the current ChangeTracker state.
    /// Delegates to ChangeTrackerCore.GetSaveData.
    /// </summary>
    public ChangeTrackerSaveData GetSaveData()
    {
        if (Core == null)
        {
            Debug.LogWarning("ChangeTracker.GetSaveData: Core not initialized. Returning empty save data.");
            return new ChangeTrackerSaveData();
        }
        return Core.GetSaveData();
    }

    /// <summary>
    /// Restores ChangeTracker state from a previously-saved snapshot.
    /// Delegates to ChangeTrackerCore.Restore.
    /// </summary>
    public void Restore(ChangeTrackerSaveData data)
    {
        if (Core == null)
        {
            Debug.LogWarning("ChangeTracker.Restore: Core not initialized. Call Initialize() first.");
            return;
        }
        Core.Restore(data);
    }

    // =========================================================================
    // IChangeTracker — Query Methods (delegate to Core)
    // =========================================================================

    /// <inheritdoc/>
    public bool GetFlag(string flagId) => Core?.GetFlag(flagId) ?? false;

    /// <inheritdoc/>
    public bool HasChoiceMade(string fragmentId, string choiceId)
    {
        if (Core == null) return false;
        return Core.HasChoiceMade(fragmentId, choiceId);
    }

    /// <inheritdoc/>
    public ObjectState GetObjectState(string fragmentId, string objectId)
    {
        if (Core == null) return ObjectState.Hidden;
        return Core.GetObjectState(fragmentId, objectId);
    }

    /// <inheritdoc/>
    public bool HasVisited(string fragmentId) => Core?.HasVisited(fragmentId) ?? false;

    /// <inheritdoc/>
    public bool IsChapterCompleted(string chapterId) => Core?.IsChapterCompleted(chapterId) ?? false;

    /// <inheritdoc/>
    public float GetTagWeight(string tagId)
    {
        // GetTagWeight needs fragment context — use empty (core handles gracefully)
        if (Core == null) return 0f;
        return Core.GetTagWeight("", tagId);
    }

    // =========================================================================
    // IChangeTrackerInternal — reserved for CrossChapterTracker (#16)
    // =========================================================================

    /// <inheritdoc/>
    void IChangeTrackerInternal.SetFlagRaw(string flagId, bool value)
    {
        Core?.SetFlagRaw(flagId, value);
    }

    /// <inheritdoc/>
    Dictionary<string, bool> IChangeTrackerInternal.GetAllFlags()
    {
        return Core?.GetAllFlags() ?? new Dictionary<string, bool>();
    }

    /// <inheritdoc/>
    void IChangeTrackerInternal.SetImmutableFlagCheck(System.Func<string, bool> isImmutableFunc)
    {
        if (Core != null)
            Core.IsFlagImmutable = isImmutableFunc;
    }
}
