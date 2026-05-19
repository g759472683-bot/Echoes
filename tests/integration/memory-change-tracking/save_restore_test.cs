using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Integration tests for ChangeTracker save/restore (memory-change-tracking S004).
///
/// Covers 3 acceptance criteria:
///   AC-1: Basic save/restore — overlays, flags, tracking sets, OverlayVersion
///   AC-2: Orphan overlay entries — skip with warning, don't block
///   AC-3: Post-restore continuity — OverlayVersion increments, _changeLog fresh
/// </summary>
public class SaveRestoreTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private MockFragmentRegistry _registry;
    private MockFragmentStateProvider _stateProvider;
    private ChangeTrackerCore _core;
    private List<string> _warnings;

    [SetUp]
    public void SetUp()
    {
        _registry = new MockFragmentRegistry();
        _stateProvider = new MockFragmentStateProvider();

        _registry.ExistingFragments.Add("frag_A");
        _registry.ExistingFragments.Add("frag_B");
        _registry.ExistingFragments.Add("frag_C");
        _registry.ExistingFragments.Add("frag_D");
        _registry.ExistingFragments.Add("frag_E");

        _registry.LayerMutability[("frag_B", "layer_door")] = true;
        _registry.LayerMutability[("frag_A", "layer_sky")] = true;
        _registry.LayerMutability[("frag_C", "layer_fog")] = true;
        _registry.ExistingObjects.Add(("frag_B", "obj_letter"));
        _registry.ExistingObjects.Add(("frag_A", "obj_key"));
        _registry.ExistingTags.Add("hope");
        _registry.ExistingTags.Add("fear");
        _registry.ExistingTags.Add("wonder");

        _stateProvider.ExistingFragments.Add("frag_B");
        _stateProvider.VisualLayers["frag_B"] = new List<VisualLayer>
        {
            new VisualLayer("layer_door", null, true, 0, default, true),
        };
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("hope", 0.5f),
        };
        _stateProvider.InteractiveObjects["frag_B"] = new List<InteractiveObject>
        {
            new InteractiveObject { ObjectId = "obj_letter", DefaultState = ObjectState.Hidden },
        };

        _core = new ChangeTrackerCore(_registry)
        {
            StateProvider = _stateProvider
        };

        _warnings = new List<string>();
        ChangeTrackerCore.OnWarning += msg => _warnings?.Add(msg);
    }

    [TearDown]
    public void TearDown()
    {
        ChangeTrackerCore.ResetStaticEvents();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds test ChangeTrackerSaveData with known values.
    /// Uses ChangeTrackerCore.GetSaveData() after applying changes to build
    /// realistic save data, then we verify restore round-trips correctly.
    /// </summary>
    private ChangeTrackerSaveData BuildSaveData(
        int overlayCount, int flagCount, int visitedCount, int chapterCount)
    {
        // Apply changes to populate overlay
        if (overlayCount >= 1)
        {
            _core.ApplyChanges("frag_A", "choice_1", new ContentChange[]
            {
                new ToggleVisualLayer("frag_A", "layer_sky", false),
            });
        }
        if (overlayCount >= 2)
        {
            _core.ApplyChanges("frag_B", "choice_2", new ContentChange[]
            {
                new ModifyTagWeight("frag_B", "hope", WeightOperation.Set, 0.8f),
            });
        }
        if (overlayCount >= 3)
        {
            _core.ApplyChanges("frag_C", "choice_3", new ContentChange[]
            {
                new ToggleVisualLayer("frag_C", "layer_fog", true),
                new SetFlag("ch1_letter_read", true),
            });
        }

        // Set flags
        if (flagCount >= 1) _core.SetFlag("flag_alpha", true);
        if (flagCount >= 2) _core.SetFlag("flag_beta", false);

        // Record visits
        if (visitedCount >= 1) _core.RecordVisit("frag_A");
        if (visitedCount >= 2) _core.RecordVisit("frag_B");
        if (visitedCount >= 3) _core.RecordVisit("frag_C");
        if (visitedCount >= 4) _core.RecordVisit("frag_D");
        if (visitedCount >= 5) _core.RecordVisit("frag_E");

        // Record chapter completions
        if (chapterCount >= 1) _core.RecordChapterCompleted("ch_01");

        return _core.GetSaveData();
    }

    // =========================================================================
    // AC-1: Basic save/restore
    // =========================================================================

    [Test]
    public void test_save_restore_overlay_entries_preserved()
    {
        // Arrange: build save data with 3 overlays, 2 flags, 5 visits, 1 chapter
        var saveData = BuildSaveData(3, 2, 5, 1);

        // Sanity: save data has expected shape
        Assert.That(saveData.OverlayEntries.Count, Is.EqualTo(3));
        Assert.That(saveData.Flags.Count, Is.EqualTo(2));
        Assert.That(saveData.VisitedFragments.Length, Is.EqualTo(5));
        Assert.That(saveData.CompletedChapters.Length, Is.EqualTo(1));

        // Act: create a fresh core and restore
        var freshCore = new ChangeTrackerCore(_registry) { StateProvider = _stateProvider };
        freshCore.Restore(saveData);

        // Assert: restored state matches
        Assert.That(freshCore.Overlay.Count, Is.EqualTo(3),
            "_overlay should contain 3 entries after restore");
        Assert.That(freshCore.Flags.Count, Is.EqualTo(2),
            "_flags should contain 2 entries after restore");
        Assert.That(freshCore.VisitedFragments.Count, Is.EqualTo(5),
            "_visitedFragments should contain 5 entries after restore");
        Assert.That(freshCore.CompletedChapters.Count, Is.EqualTo(1),
            "_completedChapters should contain 1 entry after restore");
    }

    [Test]
    public void test_save_restore_overlay_version_preserved()
    {
        var saveData = BuildSaveData(3, 2, 5, 1);

        var freshCore = new ChangeTrackerCore(_registry) { StateProvider = _stateProvider };
        freshCore.Restore(saveData);

        // OverlayVersion should match the saved value (3 overlay + 2 flag + 5 visit + 1 chapter sets)
        Assert.That(freshCore.OverlayVersion, Is.EqualTo(saveData.OverlayVersion),
            "OverlayVersion should be restored to saved value");
        Assert.That(freshCore.OverlayVersion, Is.GreaterThan(0),
            "OverlayVersion should be non-zero after restore with content");
    }

    [Test]
    public void test_save_restore_get_current_state_after_restore()
    {
        // Arrange: apply a change on frag_B, save, restore to fresh core
        _core.ApplyChanges("frag_B", "choice_open", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_door", false),
            new ModifyTagWeight("frag_B", "hope", WeightOperation.Set, 0.8f),
        });
        var saveData = _core.GetSaveData();

        var freshCore = new ChangeTrackerCore(_registry) { StateProvider = _stateProvider };
        freshCore.Restore(saveData);

        // Act: GetCurrentState on restored core
        var state = freshCore.GetCurrentState("frag_B", _stateProvider);

        // Assert: merged state reflects overlay changes
        Assert.That(state.HasValue, Is.True);
        var resolvedState = state.Value;

        // layer_door should be hidden (overlay toggled it to false)
        bool foundDoor = false;
        foreach (var layer in resolvedState.VisualLayers)
        {
            if (layer.LayerId == "layer_door")
            {
                Assert.That(layer.Visible, Is.False,
                    "layer_door should be hidden after overlay toggle restore");
                foundDoor = true;
            }
        }
        Assert.That(foundDoor, Is.True, "Should find layer_door in resolved state");

        // hope tag should be set to 0.8
        bool foundHope = false;
        foreach (var tag in resolvedState.TagWeights)
        {
            if (tag.TagId == "hope")
            {
                Assert.That(tag.Weight, Is.EqualTo(0.8f).Within(0.001f),
                    "hope tag should be 0.8 after overlay set restore");
                foundHope = true;
            }
        }
        Assert.That(foundHope, Is.True, "Should find hope tag in resolved state");
    }

    [Test]
    public void test_save_restore_flags_individually_preserved()
    {
        _core.SetFlag("flag_alpha", true);
        _core.SetFlag("flag_beta", false);
        _core.SetFlag("flag_gamma", true);
        var saveData = _core.GetSaveData();

        Assert.That(saveData.Flags.Count, Is.EqualTo(3));

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.GetFlag("flag_alpha"), Is.True);
        Assert.That(freshCore.GetFlag("flag_beta"), Is.False);
        Assert.That(freshCore.GetFlag("flag_gamma"), Is.True);
    }

    [Test]
    public void test_save_restore_visited_fragments_preserved()
    {
        _core.RecordVisit("frag_A");
        _core.RecordVisit("frag_B");
        _core.RecordVisit("frag_C");
        var saveData = _core.GetSaveData();

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.HasVisited("frag_A"), Is.True);
        Assert.That(freshCore.HasVisited("frag_B"), Is.True);
        Assert.That(freshCore.HasVisited("frag_C"), Is.True);
        Assert.That(freshCore.HasVisited("frag_D"), Is.False,
            "Unvisited fragment should remain false");
    }

    [Test]
    public void test_save_restore_completed_chapters_preserved()
    {
        _core.RecordChapterCompleted("ch_01");
        _core.RecordChapterCompleted("ch_02");
        var saveData = _core.GetSaveData();

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.IsChapterCompleted("ch_01"), Is.True);
        Assert.That(freshCore.IsChapterCompleted("ch_02"), Is.True);
        Assert.That(freshCore.IsChapterCompleted("ch_03"), Is.False,
            "Uncompleted chapter should remain false");
    }

    [Test]
    public void test_save_restore_empty_data_new_game()
    {
        // AC-1 edge case: empty save data — should not throw
        var emptyData = new ChangeTrackerSaveData();

        var freshCore = new ChangeTrackerCore(_registry);

        Assert.DoesNotThrow(() => freshCore.Restore(emptyData),
            "Restore with empty save data should not throw");

        Assert.That(freshCore.Overlay.Count, Is.EqualTo(0));
        Assert.That(freshCore.Flags.Count, Is.EqualTo(0));
        Assert.That(freshCore.VisitedFragments.Count, Is.EqualTo(0));
        Assert.That(freshCore.CompletedChapters.Count, Is.EqualTo(0));
        Assert.That(freshCore.OverlayVersion, Is.EqualTo(0));
    }

    [Test]
    public void test_save_restore_null_collections_handled_gracefully()
    {
        // Restore with all null collections should not throw
        var data = new ChangeTrackerSaveData
        {
            OverlayEntries = null,
            Flags = null,
            VisitedFragments = null,
            CompletedChapters = null,
            OverlayVersion = 0
        };

        var freshCore = new ChangeTrackerCore(_registry);

        Assert.DoesNotThrow(() => freshCore.Restore(data));
        Assert.That(freshCore.Overlay.Count, Is.EqualTo(0));
        Assert.That(freshCore.Flags.Count, Is.EqualTo(0));
    }

    // =========================================================================
    // AC-2: Orphan overlay entries
    // =========================================================================

    [Test]
    public void test_restore_orphan_overlay_skipped_with_warning()
    {
        // Arrange: build save data, then inject an orphan entry referencing deleted fragment
        var saveData = BuildSaveData(2, 0, 0, 0);
        Assert.That(saveData.OverlayEntries.Count, Is.EqualTo(2));

        saveData.OverlayEntries.Add(new OverlayEntry
        {
            TargetFragmentId = "deleted_frag",
            ChoiceId = "old_choice",
            Overrides = new ContentOverrides
            {
                ToggledLayers = new List<ToggleLayerEntry>
                {
                    new ToggleLayerEntry("layer_old", false)
                },
                OrderIndex = 99
            }
        });
        Assert.That(saveData.OverlayEntries.Count, Is.EqualTo(3));

        // Act
        var freshCore = new ChangeTrackerCore(_registry);
        var restoreWarnings = new List<string>();
        ChangeTrackerCore.OnWarning += msg => restoreWarnings.Add(msg);

        freshCore.Restore(saveData);

        // Assert: only 2 valid entries restored (orphan skipped)
        Assert.That(freshCore.Overlay.Count, Is.EqualTo(2),
            "Only 2 overlay entries should be restored; orphan should be skipped");

        // Warning should contain orphan message
        bool foundOrphanWarning = restoreWarnings.Exists(
            w => w.Contains("orphan") && w.Contains("deleted_frag"));
        Assert.That(foundOrphanWarning, Is.True,
            "Should log warning about orphan overlay entry");

        ChangeTrackerCore.ResetStaticEvents();
    }

    [Test]
    public void test_restore_orphan_does_not_block_other_entries()
    {
        // Even with orphans, valid entries restore normally
        var saveData = BuildSaveData(2, 1, 3, 1);

        // Add orphan
        saveData.OverlayEntries.Add(new OverlayEntry
        {
            TargetFragmentId = "removed_frag",
            ChoiceId = "old_choice",
            Overrides = new ContentOverrides { OrderIndex = 99 }
        });

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        // Non-overlay data should still be restored
        Assert.That(freshCore.Flags.Count, Is.EqualTo(1),
            "Flags should restore despite orphan overlay entry");
        Assert.That(freshCore.VisitedFragments.Count, Is.EqualTo(3),
            "Visited fragments should restore despite orphan overlay entry");
        Assert.That(freshCore.CompletedChapters.Count, Is.EqualTo(1),
            "Completed chapters should restore despite orphan overlay entry");
    }

    [Test]
    public void test_restore_all_entries_orphan_clears_overlay()
    {
        // All entries are orphans — _overlay should be empty, no block
        var saveData = new ChangeTrackerSaveData
        {
            OverlayEntries = new List<OverlayEntry>
            {
                new OverlayEntry { TargetFragmentId = "gone_1", ChoiceId = "c1", Overrides = new ContentOverrides() },
                new OverlayEntry { TargetFragmentId = "gone_2", ChoiceId = "c2", Overrides = new ContentOverrides() },
            },
            Flags = new List<FlagEntry> { new FlagEntry("flag_x", true) },
            VisitedFragments = new string[] { "frag_A" },
            CompletedChapters = new string[0],
            OverlayVersion = 2
        };

        var freshCore = new ChangeTrackerCore(_registry);
        var restoreWarnings = new List<string>();
        ChangeTrackerCore.OnWarning += msg => restoreWarnings.Add(msg);

        freshCore.Restore(saveData);

        // Overlay is empty (all orphans skipped)
        Assert.That(freshCore.Overlay.Count, Is.EqualTo(0));

        // Non-overlay data restored normally
        Assert.That(freshCore.Flags.Count, Is.EqualTo(1));
        Assert.That(freshCore.GetFlag("flag_x"), Is.True);
        Assert.That(freshCore.VisitedFragments.Count, Is.EqualTo(1));

        // Two orphan warnings
        Assert.That(restoreWarnings.FindAll(w => w.Contains("orphan")).Count, Is.EqualTo(2));

        ChangeTrackerCore.ResetStaticEvents();
    }

    [Test]
    public void test_restore_empty_target_fragment_id_skipped()
    {
        var saveData = new ChangeTrackerSaveData
        {
            OverlayEntries = new List<OverlayEntry>
            {
                new OverlayEntry { TargetFragmentId = "", ChoiceId = "c1", Overrides = new ContentOverrides() },
                new OverlayEntry { TargetFragmentId = null, ChoiceId = "c2", Overrides = new ContentOverrides() },
            }
        };

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.Overlay.Count, Is.EqualTo(0),
            "Empty/null TargetFragmentId entries should be skipped");
    }

    // =========================================================================
    // AC-3: Post-restore continuity
    // =========================================================================

    [Test]
    public void test_restore_then_apply_changes_overlay_version_increments()
    {
        // AC-3: after restore, new ApplyChanges increments OverlayVersion from restored value
        var saveData = BuildSaveData(2, 1, 0, 0);
        int restoredVersion = saveData.OverlayVersion;
        Assert.That(restoredVersion, Is.GreaterThan(0));

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);
        Assert.That(freshCore.OverlayVersion, Is.EqualTo(restoredVersion));

        // Act: new choice after restore
        freshCore.ApplyChanges("frag_A", "new_choice", new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "layer_sky", true),
        });

        // Assert: version incremented from restored value
        Assert.That(freshCore.OverlayVersion, Is.EqualTo(restoredVersion + 1),
            "OverlayVersion should increment by 1 from restored value");
    }

    [Test]
    public void test_restore_clears_change_log()
    {
        // Build save data (populates _changeLog with entries)
        var saveData = BuildSaveData(3, 2, 0, 0);

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        // _changeLog should be empty after restore
        Assert.That(freshCore.ChangeLog.Count, Is.EqualTo(0),
            "_changeLog should be cleared on restore");
        Assert.That(freshCore.OverlayVersion, Is.EqualTo(saveData.OverlayVersion),
            "OverlayVersion should be restored to saved value");
    }

    [Test]
    public void test_restore_then_apply_change_log_has_one_entry()
    {
        var saveData = BuildSaveData(2, 0, 0, 0);
        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.ChangeLog.Count, Is.EqualTo(0),
            "_changeLog empty immediately after restore");

        // Apply a new change
        freshCore.ApplyChanges("frag_A", "post_restore_choice", new ContentChange[]
        {
            new SetFlag("post_restore_flag", true),
        });

        Assert.That(freshCore.ChangeLog.Count, Is.EqualTo(1),
            "_changeLog should have 1 entry for the new choice");
        Assert.That(freshCore.ChangeLog[0].ChoiceId, Is.EqualTo("post_restore_choice"));
    }

    [Test]
    public void test_restore_then_save_updates_version()
    {
        // AC-3 edge case: save immediately after restore
        var saveData = BuildSaveData(2, 1, 0, 0);
        int restoredVersion = saveData.OverlayVersion;

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        // Apply a new change
        freshCore.ApplyChanges("frag_A", "new_choice", new ContentChange[]
        {
            new SetFlag("new_flag", true),
        });

        // Save again — version should have incremented
        var newSaveData = freshCore.GetSaveData();
        Assert.That(newSaveData.OverlayVersion, Is.EqualTo(restoredVersion + 1),
            "GetSaveData after restore + new choice should have incremented OverlayVersion");
    }

    [Test]
    public void test_restore_then_save_includes_new_overlay()
    {
        var saveData = BuildSaveData(1, 0, 0, 0);
        Assert.That(saveData.OverlayEntries.Count, Is.EqualTo(1));

        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        // Apply another change
        freshCore.ApplyChanges("frag_B", "second_choice", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_door", false),
        });

        var newSaveData = freshCore.GetSaveData();
        Assert.That(newSaveData.OverlayEntries.Count, Is.EqualTo(2),
            "Save after restore should include both old and new overlay entries");
    }

    [Test]
    public void test_restore_multiple_times_replaces_state()
    {
        // Restore should fully replace, not merge
        var firstData = BuildSaveData(2, 2, 3, 1);

        var core = new ChangeTrackerCore(_registry);
        core.Restore(firstData);

        Assert.That(core.Overlay.Count, Is.EqualTo(2));
        Assert.That(core.Flags.Count, Is.EqualTo(2));

        // Restore different data
        var secondData = new ChangeTrackerSaveData
        {
            OverlayEntries = new List<OverlayEntry>(),
            Flags = new List<FlagEntry> { new FlagEntry("only_flag", true) },
            VisitedFragments = new string[] { "frag_X" },
            CompletedChapters = new string[0],
            OverlayVersion = 1
        };

        core.Restore(secondData);

        Assert.That(core.Overlay.Count, Is.EqualTo(0),
            "Overlay should be replaced, not merged");
        Assert.That(core.Flags.Count, Is.EqualTo(1),
            "Flags should be replaced, not merged");
        Assert.That(core.GetFlag("only_flag"), Is.True);
        Assert.That(core.HasVisited("frag_X"), Is.True);
        Assert.That(core.VisitedFragments.Count, Is.EqualTo(1),
            "Visited fragments should be replaced, not merged");
    }

    [Test]
    public void test_restore_preserves_overlay_key_integrity()
    {
        // Verify restored overlay entries maintain correct (fragmentId, choiceId) keys
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("flag_1", true),
        });
        _core.ApplyChanges("frag_B", "choice_Y", new ContentChange[]
        {
            new SetFlag("flag_2", false),
        });

        var saveData = _core.GetSaveData();
        var freshCore = new ChangeTrackerCore(_registry);
        freshCore.Restore(saveData);

        Assert.That(freshCore.HasOverlay("frag_A", "choice_X"), Is.True,
            "Restored overlay should be queryable by original key");
        Assert.That(freshCore.HasOverlay("frag_B", "choice_Y"), Is.True,
            "Restored overlay should be queryable by original key");
        Assert.That(freshCore.HasChoiceMade("frag_A", "choice_X"), Is.True);
        Assert.That(freshCore.HasChoiceMade("frag_B", "choice_Y"), Is.True);
    }

    [Test]
    public void test_save_restore_idempotent_round_trip()
    {
        // Full round-trip: save → restore → save → compare
        var firstSave = BuildSaveData(3, 2, 5, 1);

        var core = new ChangeTrackerCore(_registry);
        core.Restore(firstSave);

        var secondSave = core.GetSaveData();

        Assert.That(secondSave.OverlayEntries.Count, Is.EqualTo(firstSave.OverlayEntries.Count));
        Assert.That(secondSave.Flags.Count, Is.EqualTo(firstSave.Flags.Count));
        Assert.That(secondSave.VisitedFragments.Length, Is.EqualTo(firstSave.VisitedFragments.Length));
        Assert.That(secondSave.CompletedChapters.Length, Is.EqualTo(firstSave.CompletedChapters.Length));
        Assert.That(secondSave.OverlayVersion, Is.EqualTo(firstSave.OverlayVersion));
    }

    [Test]
    public void test_get_save_data_empty_core_returns_empty_data()
    {
        // Core with no state should produce valid empty save data
        var saveData = _core.GetSaveData();

        Assert.That(saveData.OverlayEntries, Is.Not.Null);
        Assert.That(saveData.OverlayEntries.Count, Is.EqualTo(0));
        Assert.That(saveData.Flags, Is.Not.Null);
        Assert.That(saveData.Flags.Count, Is.EqualTo(0));
        Assert.That(saveData.VisitedFragments, Is.Not.Null);
        Assert.That(saveData.VisitedFragments.Length, Is.EqualTo(0));
        Assert.That(saveData.CompletedChapters, Is.Not.Null);
        Assert.That(saveData.CompletedChapters.Length, Is.EqualTo(0));
        Assert.That(saveData.OverlayVersion, Is.EqualTo(0));
    }

    [Test]
    public void test_get_save_data_is_empty_flag_returns_true_for_empty()
    {
        var data = _core.GetSaveData();
        Assert.That(data.IsEmpty, Is.True);
    }

    [Test]
    public void test_get_save_data_is_empty_flag_returns_false_with_content()
    {
        var data = BuildSaveData(1, 0, 0, 0);
        Assert.That(data.IsEmpty, Is.False);
    }

    // =========================================================================
    // Mocks
    // =========================================================================

    private class MockFragmentRegistry : IFragmentRegistry
    {
        public readonly HashSet<string> ExistingFragments = new();
        public readonly Dictionary<(string, string), bool> LayerMutability = new();
        public readonly HashSet<(string, string)> ExistingObjects = new();
        public readonly HashSet<string> ExistingTags = new();

        public bool HasFragment(string fId)
            => !string.IsNullOrEmpty(fId) && ExistingFragments.Contains(fId);

        public bool IsLayerMutable(string fId, string lId)
        {
            if (string.IsNullOrEmpty(fId) || string.IsNullOrEmpty(lId)) return false;
            return LayerMutability.TryGetValue((fId, lId), out bool m) && m;
        }

        public bool HasObject(string fId, string oId)
        {
            if (string.IsNullOrEmpty(fId) || string.IsNullOrEmpty(oId)) return false;
            return ExistingObjects.Contains((fId, oId));
        }

        public bool HasTag(string tId)
            => !string.IsNullOrEmpty(tId) && ExistingTags.Contains(tId);
    }

    private class MockFragmentStateProvider : IFragmentStateProvider
    {
        public readonly HashSet<string> ExistingFragments = new();
        public readonly Dictionary<string, List<VisualLayer>> VisualLayers = new();
        public readonly Dictionary<string, List<EmotionalTag>> EmotionalTags = new();
        public readonly Dictionary<string, List<InteractiveObject>> InteractiveObjects = new();

        public bool HasFragment(string fId)
            => !string.IsNullOrEmpty(fId) && ExistingFragments.Contains(fId);

        public IReadOnlyList<VisualLayer> GetBaseVisualLayers(string fId)
        {
            VisualLayers.TryGetValue(fId, out var list);
            return list;
        }

        public IReadOnlyList<EmotionalTag> GetBaseEmotionalTags(string fId)
        {
            EmotionalTags.TryGetValue(fId, out var list);
            return list;
        }

        public IReadOnlyList<InteractiveObject> GetBaseInteractiveObjects(string fId)
        {
            InteractiveObjects.TryGetValue(fId, out var list);
            return list;
        }
    }
}
