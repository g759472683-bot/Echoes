using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# change tracking core (ADR-0007 S001).
///
/// Maintains the _overlay Dictionary that records every player choice's
/// content changes. This is the "mutable layer" in the SO-immutable +
/// ChangeTracker-overlay two-layer model.
///
/// DI-ready: takes IFragmentRegistry for validation so all logic is
/// pure C# testable without Unity dependencies.
///
/// ApplyChanges algorithm (ADR-0007 §Implementation Guidelines):
///   1. Validate targetFragmentId exists → else LogWarning, skip entire call
///   2. For each ContentChange, validate and convert to ContentOverrides entry
///   3. Store in _overlay (overwrite if key exists)
///   4. Append to _changeLog
///   5. OverlayVersion++
///   6. Fire OnOverlayChanged(targetFragmentId)
/// </summary>
public class ChangeTrackerCore
{
    // =========================================================================
    // Dependencies
    // =========================================================================

    private readonly IFragmentRegistry _registry;

    /// <summary>
    /// Optional state provider for GetObjectState/GetTagWeight queries.
    /// Set after construction (before condition evaluation). Null by default.
    /// </summary>
    public IFragmentStateProvider StateProvider { get; set; }

    // =========================================================================
    // State
    // =========================================================================

    private readonly Dictionary<(string fragmentId, string choiceId), ContentOverrides> _overlay = new();
    private readonly List<ChangeLogEntry> _changeLog = new();

    /// <summary>Global narrative flags (SetFlag / GetFlag). Cross-chapter persistent.</summary>
    private readonly Dictionary<string, bool> _flags = new();

    /// <summary>Fragments the player has visited at least once.</summary>
    private readonly HashSet<string> _visitedFragments = new();

    /// <summary>Chapters the player has completed.</summary>
    private readonly HashSet<string> _completedChapters = new();

    /// <summary>Monotonically increasing version counter. Increments on every ApplyChanges call.</summary>
    public int OverlayVersion { get; private set; }

    /// <summary>Read-only view of the overlay dictionary (for test inspection).</summary>
    public IReadOnlyDictionary<(string fragmentId, string choiceId), ContentOverrides> Overlay => _overlay;

    /// <summary>Read-only view of the append-only change log (for test inspection).</summary>
    public IReadOnlyList<ChangeLogEntry> ChangeLog => _changeLog;

    /// <summary>Read-only view of global flags (for test inspection).</summary>
    public IReadOnlyDictionary<string, bool> Flags => _flags;

    /// <summary>Read-only view of visited fragment IDs (for test inspection).</summary>
    public IReadOnlyCollection<string> VisitedFragments => _visitedFragments;

    /// <summary>Read-only view of completed chapter IDs (for test inspection).</summary>
    public IReadOnlyCollection<string> CompletedChapters => _completedChapters;

    // =========================================================================
    // Events (ADR-0001 static event pattern)
    // =========================================================================

    /// <summary>Fired synchronously after ApplyChanges completes. Parameter is the target fragment ID.</summary>
    public static event Action<string> OnOverlayChanged;

    /// <summary>Fired when ApplyChanges encounters a validation issue. Parameter is the warning message.</summary>
    public static event Action<string> OnWarning;

    // =========================================================================
    // Construction
    // =========================================================================

    public ChangeTrackerCore(IFragmentRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    // =========================================================================
    // ApplyChanges
    // =========================================================================

    /// <summary>
    /// Applies a batch of ContentChanges from a player choice.
    ///
    /// Validation rules (non-blocking — invalid entries are skipped, valid ones applied):
    ///   - Null/empty changes array: no overlay change, but still logged + event fired (AC-4)
    ///   - Invalid targetFragmentId: entire call skipped (AC-2)
    ///   - ToggleVisualLayer on immutable/missing layer: that entry skipped (AC-3)
    ///   - SetObjectState with missing ObjectId: that entry skipped
    ///   - ModifyTagWeight with missing TagId: that entry skipped
    ///   - SetFlag: always applied (global, no fragment validation)
    ///   - UnlockAssociation: always stored (validation deferred to Story 002)
    ///   - SetTextContent: always stored (no structural validation)
    /// </summary>
    /// <param name="targetFragmentId">The fragment this change targets.</param>
    /// <param name="choiceId">The choice that triggered this change.</param>
    /// <param name="changes">The content changes to apply. Null is treated as empty.</param>
    public void ApplyChanges(string targetFragmentId, string choiceId, ContentChange[] changes)
    {
        // Guard: null/empty fragmentId
        if (string.IsNullOrEmpty(targetFragmentId))
        {
            FireWarning($"ApplyChanges: targetFragmentId is null or empty — skipping call.");
            return;
        }

        // AC-2: Validate targetFragmentId exists
        if (!_registry.HasFragment(targetFragmentId))
        {
            FireWarning($"ApplyChanges: fragment '{targetFragmentId}' does not exist in registry — skipping call.");
            return;
        }

        // Normalize null changes to empty array
        changes ??= Array.Empty<ContentChange>();
        int inputCount = changes.Length;

        // Build ContentOverrides from valid changes
        var overrides = new ContentOverrides();
        int appliedCount = 0;

        foreach (var change in changes)
        {
            if (change == null) continue;

            bool applied = ProcessChange(change, ref overrides);
            if (applied) appliedCount++;
        }

        // Store in overlay (overwrite if key exists). Set OrderIndex for chronological merging.
        var key = (targetFragmentId, choiceId);
        overrides.OrderIndex = OverlayVersion + 1;
        _overlay[key] = overrides;

        // Append to change log
        _changeLog.Add(new ChangeLogEntry(
            timestamp: DateTime.UtcNow,
            targetFragmentId: targetFragmentId,
            choiceId: choiceId,
            inputChangeCount: inputCount,
            appliedChangeCount: appliedCount,
            resultingOverlayVersion: OverlayVersion + 1
        ));

        // AC-4: Always increment version and fire event (even for empty input)
        OverlayVersion++;
        OnOverlayChanged?.Invoke(targetFragmentId);
    }

    // =========================================================================
    // Per-Change Processing
    // =========================================================================

    /// <summary>
    /// Processes a single ContentChange, adding its entry to the overrides struct.
    /// Returns true if the change was valid and applied; false if skipped.
    /// </summary>
    private bool ProcessChange(ContentChange change, ref ContentOverrides overrides)
    {
        switch (change)
        {
            case ToggleVisualLayer tvl:
                return ProcessToggleLayer(tvl, ref overrides);

            case SetObjectState sos:
                return ProcessSetObjectState(sos, ref overrides);

            case SetTextContent stc:
                return ProcessSetTextContent(stc, ref overrides);

            case ModifyTagWeight mtw:
                return ProcessModifyTagWeight(mtw, ref overrides);

            case UnlockAssociation ua:
                return ProcessUnlockAssociation(ua, ref overrides);

            case SetFlag sf:
                return ProcessSetFlag(sf, ref overrides);

            default:
                FireWarning($"ApplyChanges: unknown ContentChange type '{change.GetType().Name}' — skipped.");
                return false;
        }
    }

    // -------------------------------------------------------------------------
    // ToggleVisualLayer
    // -------------------------------------------------------------------------

    private bool ProcessToggleLayer(ToggleVisualLayer tvl, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(tvl.LayerId))
        {
            FireWarning($"ApplyChanges: ToggleVisualLayer has empty LayerId — skipped.");
            return false;
        }

        // AC-3: Check layer exists and is mutable
        if (!_registry.IsLayerMutable(tvl.TargetFragmentId, tvl.LayerId))
        {
            FireWarning($"ApplyChanges: layer '{tvl.LayerId}' on fragment " +
                        $"'{tvl.TargetFragmentId}' is immutable or does not exist — skipped.");
            return false;
        }

        overrides.ToggledLayers ??= new List<ToggleLayerEntry>();
        overrides.ToggledLayers.Add(new ToggleLayerEntry(tvl.LayerId, tvl.Visible));
        return true;
    }

    // -------------------------------------------------------------------------
    // SetObjectState
    // -------------------------------------------------------------------------

    private bool ProcessSetObjectState(SetObjectState sos, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(sos.ObjectId))
        {
            FireWarning($"ApplyChanges: SetObjectState has empty ObjectId — skipped.");
            return false;
        }

        if (!_registry.HasObject(sos.TargetFragmentId, sos.ObjectId))
        {
            FireWarning($"ApplyChanges: object '{sos.ObjectId}' does not exist on fragment " +
                        $"'{sos.TargetFragmentId}' — skipped.");
            return false;
        }

        overrides.ObjectStates ??= new List<ObjectStateEntry>();
        overrides.ObjectStates.Add(new ObjectStateEntry(sos.ObjectId, sos.NewState));
        return true;
    }

    // -------------------------------------------------------------------------
    // SetTextContent
    // -------------------------------------------------------------------------

    private bool ProcessSetTextContent(SetTextContent stc, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(stc.TextFieldId))
        {
            FireWarning($"ApplyChanges: SetTextContent has empty TextFieldId — skipped.");
            return false;
        }

        overrides.TextOverrides ??= new List<TextOverrideEntry>();
        overrides.TextOverrides.Add(new TextOverrideEntry(stc.TextFieldId, stc.NewText ?? ""));
        return true;
    }

    // -------------------------------------------------------------------------
    // ModifyTagWeight
    // -------------------------------------------------------------------------

    private bool ProcessModifyTagWeight(ModifyTagWeight mtw, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(mtw.TagId))
        {
            FireWarning($"ApplyChanges: ModifyTagWeight has empty TagId — skipped.");
            return false;
        }

        if (!_registry.HasTag(mtw.TagId))
        {
            FireWarning($"ApplyChanges: tag '{mtw.TagId}' does not exist in catalog — skipped.");
            return false;
        }

        // Convert WeightOperation → ModOp (same ordinal values: Add=0, Multiply=1, Set=2)
        var modOp = mtw.Operation switch
        {
            WeightOperation.Add => ModOp.Add,
            WeightOperation.Multiply => ModOp.Multiply,
            WeightOperation.Set => ModOp.Set,
            _ => ModOp.Add
        };

        overrides.TagWeightMods ??= new List<TagWeightModEntry>();
        overrides.TagWeightMods.Add(new TagWeightModEntry(mtw.TagId, modOp, mtw.Delta, OverlayVersion + 1));
        return true;
    }

    // -------------------------------------------------------------------------
    // UnlockAssociation
    // -------------------------------------------------------------------------

    private bool ProcessUnlockAssociation(UnlockAssociation ua, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(ua.AssociationTargetId))
        {
            FireWarning($"ApplyChanges: UnlockAssociation has empty AssociationTargetId — skipped.");
            return false;
        }

        overrides.UnlockedAssociations ??= new List<string>();
        overrides.UnlockedAssociations.Add(ua.AssociationTargetId);
        return true;
    }

    // -------------------------------------------------------------------------
    // SetFlag
    // -------------------------------------------------------------------------

    private bool ProcessSetFlag(SetFlag sf, ref ContentOverrides overrides)
    {
        if (string.IsNullOrEmpty(sf.FlagId))
        {
            FireWarning($"ApplyChanges: SetFlag has empty FlagId — skipped.");
            return false;
        }

        overrides.SetFlags ??= new List<FlagSetEntry>();
        overrides.SetFlags.Add(new FlagSetEntry(sf.FlagId, sf.Value));

        // Also write directly to _flags for immediate query (Story 003)
        SetFlag(sf.FlagId, sf.Value);
        return true;
    }

    // =========================================================================
    // GetCurrentState (Story 002 — State Merge Algorithm)
    // =========================================================================

    /// <summary>
    /// Computes the resolved fragment state by merging base SO data with all overlays
    /// targeting this fragment, applied in chronological order (OrderIndex ascending).
    ///
    /// Merge strategies per change type:
    ///   - ToggleVisualLayer: directly overwrites Visible value (later wins)
    ///   - SetObjectState: directly overwrites State value (later wins)
    ///   - SetTextContent: directly overwrites text field (later wins)
    ///   - ModifyTagWeight: sequential application (Add/Multiply/Set) with clamping
    ///   - UnlockAssociation: set union (idempotent — duplicates skipped)
    ///
    /// Returns null if the fragment does not exist in the state provider.
    /// </summary>
    public ResolvedFragmentState? GetCurrentState(
        string fragmentId, IFragmentStateProvider stateProvider)
    {
        if (stateProvider == null)
        {
            FireWarning("GetCurrentState: stateProvider is null.");
            return null;
        }

        if (string.IsNullOrEmpty(fragmentId) || !stateProvider.HasFragment(fragmentId))
        {
            return null;
        }

        // 1. Load base SO data
        var baseLayers = stateProvider.GetBaseVisualLayers(fragmentId);
        var baseTags = stateProvider.GetBaseEmotionalTags(fragmentId);
        var baseObjects = stateProvider.GetBaseInteractiveObjects(fragmentId);

        // 2. Collect all overlay entries targeting this fragment, sorted by OrderIndex
        var relevantOverlays = new List<ContentOverrides>();
        foreach (var kv in _overlay)
        {
            if (kv.Key.fragmentId == fragmentId)
            {
                relevantOverlays.Add(kv.Value);
            }
        }
        relevantOverlays.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));

        // 3. Build resolved state starting from base SO values
        var resolvedLayers = new Dictionary<string, bool>();
        if (baseLayers != null)
        {
            foreach (var layer in baseLayers)
            {
                if (string.IsNullOrEmpty(layer.LayerId)) continue;
                resolvedLayers[layer.LayerId] = layer.DefaultVisible;
            }
        }

        var resolvedObjects = new Dictionary<string, ObjectState>();
        if (baseObjects != null)
        {
            foreach (var obj in baseObjects)
            {
                if (string.IsNullOrEmpty(obj.ObjectId)) continue;
                resolvedObjects[obj.ObjectId] = obj.DefaultState;
            }
        }

        // Tag weights: start from base, apply ModifyTagWeight overlays sequentially
        var tagWeights = new Dictionary<string, float>();
        if (baseTags != null)
        {
            foreach (var tag in baseTags)
            {
                if (string.IsNullOrEmpty(tag.TagId)) continue;
                tagWeights[tag.TagId] = ClampWeight(tag.BaseWeight);
            }
        }

        var textContents = new Dictionary<string, string>();
        var unlockedAssociations = new HashSet<string>();

        // 4. Apply each overlay in OrderIndex ascending order
        foreach (var overlay in relevantOverlays)
        {
            ApplyToggleLayers(overlay, resolvedLayers);
            ApplyObjectStates(overlay, resolvedObjects);
            ApplyTextOverrides(overlay, textContents);
            ApplyTagWeightMods(overlay, tagWeights);
            ApplyUnlockedAssociations(overlay, unlockedAssociations);
        }

        // 5. Build immutable snapshot
        var layerList = new List<ResolvedLayerState>();
        foreach (var kv in resolvedLayers)
            layerList.Add(new ResolvedLayerState(kv.Key, kv.Value));

        var tagList = new List<ResolvedTagWeight>();
        foreach (var kv in tagWeights)
            tagList.Add(new ResolvedTagWeight(kv.Key, kv.Value));

        var objectList = new List<ResolvedObjectStateEntry>();
        foreach (var kv in resolvedObjects)
            objectList.Add(new ResolvedObjectStateEntry(kv.Key, kv.Value));

        return new ResolvedFragmentState(
            fragmentId: fragmentId,
            visualLayers: layerList,
            tagWeights: tagList,
            objectStates: objectList,
            textContents: textContents,
            unlockedAssociations: unlockedAssociations
        );
    }

    // -------------------------------------------------------------------------
    // Merge helpers
    // -------------------------------------------------------------------------

    private void ApplyToggleLayers(ContentOverrides overlay, Dictionary<string, bool> resolvedLayers)
    {
        if (overlay.ToggledLayers == null) return;
        foreach (var entry in overlay.ToggledLayers)
        {
            if (string.IsNullOrEmpty(entry.LayerId)) continue;
            resolvedLayers[entry.LayerId] = entry.Visible;
        }
    }

    private void ApplyObjectStates(ContentOverrides overlay, Dictionary<string, ObjectState> resolvedObjects)
    {
        if (overlay.ObjectStates == null) return;
        foreach (var entry in overlay.ObjectStates)
        {
            if (string.IsNullOrEmpty(entry.ObjectId)) continue;
            resolvedObjects[entry.ObjectId] = entry.NewState;
        }
    }

    private static void ApplyTextOverrides(ContentOverrides overlay, Dictionary<string, string> textContents)
    {
        if (overlay.TextOverrides == null) return;
        foreach (var entry in overlay.TextOverrides)
        {
            if (string.IsNullOrEmpty(entry.TextFieldId)) continue;
            textContents[entry.TextFieldId] = entry.NewText;
        }
    }

    private void ApplyTagWeightMods(ContentOverrides overlay, Dictionary<string, float> tagWeights)
    {
        if (overlay.TagWeightMods == null) return;

        // Sort by per-entry OrderIndex for deterministic sequential application
        var sorted = new List<TagWeightModEntry>(overlay.TagWeightMods);
        sorted.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));

        foreach (var entry in sorted)
        {
            if (string.IsNullOrEmpty(entry.TagId)) continue;

            float current = tagWeights.TryGetValue(entry.TagId, out float w) ? w : 0f;

            float result = entry.Operation switch
            {
                ModOp.Add => current + entry.Value,
                ModOp.Multiply => current * entry.Value,
                ModOp.Set => entry.Value,
                _ => current
            };

            tagWeights[entry.TagId] = ClampWeight(result);
        }
    }

    private static void ApplyUnlockedAssociations(ContentOverrides overlay, HashSet<string> unlocked)
    {
        if (overlay.UnlockedAssociations == null) return;
        foreach (var targetId in overlay.UnlockedAssociations)
        {
            if (!string.IsNullOrEmpty(targetId))
                unlocked.Add(targetId); // HashSet — idempotent
        }
    }

    /// <summary>Clamps a weight value to [0.0, 1.0], handling NaN and Infinity.</summary>
    public static float ClampWeight(float value)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value))
            return 0.0f;
        if (float.IsPositiveInfinity(value))
            return 1.0f;
        if (value < 0.0f) return 0.0f;
        if (value > 1.0f) return 1.0f;
        return value;
    }

    // =========================================================================
    // Flag System (Story 003)
    // =========================================================================

    /// <summary>
    /// Optional callback wired by CrossChapterTracker. When set, SetFlag calls
    /// this before allowing a true→false transition. If it returns true, the
    /// SetFlag(false) is rejected with LogWarning (IsImmutable protection).
    /// </summary>
    public Func<string, bool> IsFlagImmutable { get; set; }

    /// <summary>
    /// Sets a global narrative flag to the given boolean value.
    /// Idempotent — if the flag already has the same value, this is a no-op
    /// and OverlayVersion does not increment.
    ///
    /// If IsFlagImmutable is set and returns true for this flag, and the current
    /// value is true while the new value is false → the call is rejected with
    /// a warning (ADR-0011 IsImmutable protection).
    /// </summary>
    public void SetFlag(string flagId, bool value)
    {
        if (string.IsNullOrEmpty(flagId))
        {
            FireWarning("SetFlag: flagId is null or empty — skipped.");
            return;
        }

        if (_flags.TryGetValue(flagId, out bool existing) && existing == value)
            return; // Idempotent — same value

        // IsImmutable guard (ADR-0011): reject true→false for immutable flags
        if (existing && !value && IsFlagImmutable != null && IsFlagImmutable(flagId))
        {
            Debug.LogWarning(
                $"ChangeTracker: Immutable flag '{flagId}' is already true — " +
                $"SetFlag(false) rejected.");
            return;
        }

        _flags[flagId] = value;
        OverlayVersion++;
    }

    /// <summary>
    /// Directly sets a flag value without validation, events, or OverlayVersion
    /// increment. For CrossChapterTracker use only — initialization, replay reset,
    /// and save restoration. Never use for player-driven flag changes.
    /// </summary>
    public void SetFlagRaw(string flagId, bool value)
    {
        if (string.IsNullOrEmpty(flagId)) return;
        _flags[flagId] = value;
    }

    /// <summary>
    /// Returns a shallow copy of the entire _flags dictionary.
    /// For CrossChapterTracker persistence bridge.
    /// </summary>
    public Dictionary<string, bool> GetAllFlags()
    {
        return new Dictionary<string, bool>(_flags);
    }

    /// <summary>
    /// Returns the current boolean value of a global narrative flag.
    /// Unset flags return false.
    /// </summary>
    public bool GetFlag(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return false;
        return _flags.TryGetValue(flagId, out bool value) && value;
    }

    // =========================================================================
    // Tracking Sets (Story 003)
    // =========================================================================

    /// <summary>Records that the player has visited the given fragment.</summary>
    public void RecordVisit(string fragmentId)
    {
        if (string.IsNullOrEmpty(fragmentId)) return;
        if (_visitedFragments.Add(fragmentId))
            OverlayVersion++;
    }

    /// <summary>Returns true if the player has visited the given fragment at least once.</summary>
    public bool HasVisited(string fragmentId)
    {
        if (string.IsNullOrEmpty(fragmentId)) return false;
        return _visitedFragments.Contains(fragmentId);
    }

    /// <summary>Records that the player has completed the given chapter.</summary>
    public void RecordChapterCompleted(string chapterId)
    {
        if (string.IsNullOrEmpty(chapterId)) return;
        if (_completedChapters.Add(chapterId))
            OverlayVersion++;
    }

    /// <summary>Returns true if the specified chapter has been completed.</summary>
    public bool IsChapterCompleted(string chapterId)
    {
        if (string.IsNullOrEmpty(chapterId)) return false;
        return _completedChapters.Contains(chapterId);
    }

    /// <summary>Returns true if the player has made the specified choice on the given fragment.</summary>
    public bool HasChoiceMade(string fragmentId, string choiceId)
    {
        return HasOverlay(fragmentId, choiceId);
    }

    /// <summary>
    /// Returns the current resolved state of an interactive object.
    /// Delegates to GetCurrentState and looks up the object.
    /// Returns ObjectState.Hidden if the state cannot be resolved.
    /// </summary>
    public ObjectState GetObjectState(string fragmentId, string objectId)
    {
        if (StateProvider == null || string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(objectId))
            return ObjectState.Hidden;

        var state = GetCurrentState(fragmentId, StateProvider);
        if (!state.HasValue) return ObjectState.Hidden;

        foreach (var obj in state.Value.ObjectStates)
        {
            if (obj.ObjectId == objectId)
                return obj.State;
        }
        return ObjectState.Hidden;
    }

    /// <summary>
    /// Returns the resolved emotional tag weight on the given fragment.
    /// Returns 0f if the state cannot be resolved or the tag is not found.
    /// </summary>
    public float GetTagWeight(string fragmentId, string tagId)
    {
        if (StateProvider == null || string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(tagId))
            return 0f;

        var state = GetCurrentState(fragmentId, StateProvider);
        if (!state.HasValue) return 0f;

        foreach (var tag in state.Value.TagWeights)
        {
            if (tag.TagId == tagId)
                return tag.Weight;
        }
        return 0f;
    }

    // =========================================================================
    // Save / Restore (Story 004)
    // =========================================================================

    /// <summary>
    /// Builds a serializable snapshot of the current ChangeTracker state.
    /// Overlay entries, flags, visited fragments, completed chapters, and
    /// OverlayVersion are captured. _changeLog is NOT persisted — it starts
    /// fresh on restore.
    /// </summary>
    public ChangeTrackerSaveData GetSaveData()
    {
        var overlayEntries = new List<OverlayEntry>(_overlay.Count);
        foreach (var kv in _overlay)
        {
            overlayEntries.Add(new OverlayEntry
            {
                TargetFragmentId = kv.Key.fragmentId,
                ChoiceId = kv.Key.choiceId,
                Overrides = kv.Value
            });
        }

        var flagEntries = new List<FlagEntry>(_flags.Count);
        foreach (var kv in _flags)
        {
            flagEntries.Add(new FlagEntry(kv.Key, kv.Value));
        }

        var visitedArray = new string[_visitedFragments.Count];
        _visitedFragments.CopyTo(visitedArray);

        var chaptersArray = new string[_completedChapters.Count];
        _completedChapters.CopyTo(chaptersArray);

        return new ChangeTrackerSaveData
        {
            OverlayEntries = overlayEntries,
            Flags = flagEntries,
            VisitedFragments = visitedArray,
            CompletedChapters = chaptersArray,
            OverlayVersion = OverlayVersion
        };
    }

    /// <summary>
    /// Restores ChangeTracker state from a previously-saved snapshot.
    ///
    /// All current state is cleared first. Overlay entries referencing fragments
    /// not in the current registry are skipped with a warning (orphan handling).
    /// _changeLog is cleared — only new-session choices are recorded after restore.
    /// OverlayVersion is set to the restored value; subsequent ApplyChanges calls
    /// continue incrementing from there.
    /// </summary>
    public void Restore(ChangeTrackerSaveData data)
    {
        // Clear all state
        _overlay.Clear();
        _flags.Clear();
        _visitedFragments.Clear();
        _completedChapters.Clear();
        _changeLog.Clear();

        // Restore overlay entries with orphan validation (AC-2)
        if (data.OverlayEntries != null)
        {
            foreach (var entry in data.OverlayEntries)
            {
                if (string.IsNullOrEmpty(entry.TargetFragmentId))
                {
                    FireWarning("Restore: overlay entry has null or empty TargetFragmentId — skipping.");
                    continue;
                }

                if (!_registry.HasFragment(entry.TargetFragmentId))
                {
                    FireWarning($"Restore: orphan overlay entry — fragment " +
                                $"'{entry.TargetFragmentId}' not found in registry — skipping.");
                    continue;
                }

                _overlay[(entry.TargetFragmentId, entry.ChoiceId)] = entry.Overrides;
            }
        }

        // Restore flags
        if (data.Flags != null)
        {
            foreach (var flag in data.Flags)
            {
                if (!string.IsNullOrEmpty(flag.FlagId))
                    _flags[flag.FlagId] = flag.Value;
            }
        }

        // Restore tracking sets
        if (data.VisitedFragments != null)
        {
            foreach (var fragId in data.VisitedFragments)
            {
                if (!string.IsNullOrEmpty(fragId))
                    _visitedFragments.Add(fragId);
            }
        }

        if (data.CompletedChapters != null)
        {
            foreach (var chId in data.CompletedChapters)
            {
                if (!string.IsNullOrEmpty(chId))
                    _completedChapters.Add(chId);
            }
        }

        // Restore version counter — subsequent ApplyChanges continue from here
        OverlayVersion = data.OverlayVersion;
    }

    // =========================================================================
    // Query Helpers (for tests and Story 002/003)
    // =========================================================================

    /// <summary>Returns true if an overlay exists for the given fragment+choice key.</summary>
    public bool HasOverlay(string fragmentId, string choiceId)
    {
        return _overlay.ContainsKey((fragmentId, choiceId));
    }

    /// <summary>Returns the ContentOverrides for a given key, or null if not found.</summary>
    public ContentOverrides? GetOverlay(string fragmentId, string choiceId)
    {
        if (_overlay.TryGetValue((fragmentId, choiceId), out var overrides))
            return overrides;
        return null;
    }

    // =========================================================================
    // Test Support
    // =========================================================================

    /// <summary>Resets all static events. For test teardown only.</summary>
    public static void ResetStaticEvents()
    {
        OnOverlayChanged = null;
        OnWarning = null;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void FireWarning(string message)
    {
        OnWarning?.Invoke(message);
    }
}
