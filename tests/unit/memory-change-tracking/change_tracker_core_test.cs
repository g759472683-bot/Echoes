using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for ChangeTrackerCore.ApplyChanges (memory-change-tracking S001).
///
/// Covers 5 acceptance criteria:
///   AC-1: ApplyChanges basic flow (2 changes → _overlay, version++, log)
///   AC-2: Invalid TargetFragmentId → LogWarning, skip entire call
///   AC-3: IsMutable=false layer → skipped, valid changes still applied
///   AC-4: Empty/null ContentChange[] → log entry, version++, event fires
///   AC-5: Cross-fragment OnOverlayChanged fires synchronously
/// </summary>
public class ChangeTrackerCoreTest
{
    // =========================================================================
    // Test Data
    // =========================================================================

    private MockFragmentRegistry _registry;
    private ChangeTrackerCore _core;
    private List<string> _warnings;
    private List<string> _overlayEvents;

    [SetUp]
    public void SetUp()
    {
        _registry = new MockFragmentRegistry();
        _registry.ExistingFragments.Add("frag_A");
        _registry.ExistingFragments.Add("frag_B");
        _registry.LayerMutability[("frag_A", "layer_rain")] = true;  // mutable
        _registry.LayerMutability[("frag_A", "layer_static")] = false; // immutable
        _registry.ExistingObjects.Add(("frag_A", "obj_door"));
        _registry.ExistingTags.Add("nostalgia");
        _registry.ExistingTags.Add("hope");

        _core = new ChangeTrackerCore(_registry);
        _warnings = new List<string>();
        _overlayEvents = new List<string>();

        ChangeTrackerCore.OnWarning += CollectWarning;
        ChangeTrackerCore.OnOverlayChanged += CollectOverlayEvent;
    }

    [TearDown]
    public void TearDown()
    {
        ChangeTrackerCore.ResetStaticEvents();
        _warnings = null;
        _overlayEvents = null;
    }

    private void CollectWarning(string msg) => _warnings?.Add(msg);
    private void CollectOverlayEvent(string fragmentId) => _overlayEvents?.Add(fragmentId);

    // =========================================================================
    // AC-1: ApplyChanges basic flow
    // =========================================================================

    [Test]
    public void test_apply_changes_stores_overlay_with_two_changes()
    {
        // AC-1: 2 ContentChanges → _overlay populated
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "layer_rain", true),
            new SetObjectState("frag_A", "obj_door", ObjectState.Hidden),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_core.HasOverlay("frag_A", "choice_X"), Is.True);
        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.HasValue, Is.True);
        Assert.That(overlay.Value.ToggledLayers, Is.Not.Null);
        Assert.That(overlay.Value.ToggledLayers.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.ToggledLayers[0].LayerId, Is.EqualTo("layer_rain"));
        Assert.That(overlay.Value.ToggledLayers[0].Visible, Is.True);
        Assert.That(overlay.Value.ObjectStates, Is.Not.Null);
        Assert.That(overlay.Value.ObjectStates.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.ObjectStates[0].ObjectId, Is.EqualTo("obj_door"));
        Assert.That(overlay.Value.ObjectStates[0].NewState, Is.EqualTo(ObjectState.Hidden));
    }

    [Test]
    public void test_apply_changes_increments_overlay_version()
    {
        var changes = new ContentChange[]
        {
            new SetFlag("flag_test", true),
        };

        Assert.That(_core.OverlayVersion, Is.EqualTo(0));
        _core.ApplyChanges("frag_A", "choice_X", changes);
        Assert.That(_core.OverlayVersion, Is.EqualTo(1));
    }

    [Test]
    public void test_apply_changes_adds_log_entry()
    {
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "layer_rain", false),
        };

        _core.ApplyChanges("frag_A", "choice_1", changes);

        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));
        var entry = _core.ChangeLog[0];
        Assert.That(entry.TargetFragmentId, Is.EqualTo("frag_A"));
        Assert.That(entry.ChoiceId, Is.EqualTo("choice_1"));
        Assert.That(entry.InputChangeCount, Is.EqualTo(1));
        Assert.That(entry.AppliedChangeCount, Is.EqualTo(1));
        Assert.That(entry.ResultingOverlayVersion, Is.EqualTo(1));
    }

    [Test]
    public void test_duplicate_choice_overwrites_overlay_and_increments_version()
    {
        // First call
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("flag_a", true),
        });
        Assert.That(_core.OverlayVersion, Is.EqualTo(1));
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));

        // Second call with same key → overwrite
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("flag_b", true),
        });

        Assert.That(_core.OverlayVersion, Is.EqualTo(2));
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(2));
        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.HasValue, Is.True);
        Assert.That(overlay.Value.SetFlags.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.SetFlags[0].FlagId, Is.EqualTo("flag_b"),
            "Second call should overwrite first call's overlay");
    }

    [Test]
    public void test_multiple_fragments_have_independent_overlays()
    {
        _core.ApplyChanges("frag_A", "choice_1", new ContentChange[]
        {
            new SetFlag("flag_a", true),
        });
        _core.ApplyChanges("frag_B", "choice_2", new ContentChange[]
        {
            new SetFlag("flag_b", false),
        });

        Assert.That(_core.HasOverlay("frag_A", "choice_1"), Is.True);
        Assert.That(_core.HasOverlay("frag_B", "choice_2"), Is.True);
        Assert.That(_core.OverlayVersion, Is.EqualTo(2));
    }

    // =========================================================================
    // AC-2: Invalid TargetFragmentId
    // =========================================================================

    [Test]
    public void test_invalid_fragment_id_skips_entire_call()
    {
        var changes = new ContentChange[]
        {
            new SetFlag("flag_test", true),
        };

        _core.ApplyChanges("nonexistent_frag", "choice_1", changes);

        Assert.That(_core.OverlayVersion, Is.EqualTo(0),
            "Version should not increment for invalid fragment");
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(0),
            "No log entry for invalid fragment");
        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_warnings[0], Does.Contain("nonexistent_frag"));
    }

    [Test]
    public void test_null_fragment_id_skips_call()
    {
        _core.ApplyChanges(null, "choice_1", new ContentChange[]
        {
            new SetFlag("flag_test", true),
        });

        Assert.That(_core.OverlayVersion, Is.EqualTo(0));
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(0));
        Assert.That(_warnings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void test_empty_fragment_id_skips_call()
    {
        _core.ApplyChanges("", "choice_1", new ContentChange[]
        {
            new SetFlag("flag_test", true),
        });

        Assert.That(_core.OverlayVersion, Is.EqualTo(0));
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(0));
        Assert.That(_warnings.Count, Is.GreaterThan(0));
    }

    // =========================================================================
    // AC-3: IsMutable=false layer rejection
    // =========================================================================

    [Test]
    public void test_immutable_layer_is_skipped_with_warning()
    {
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "layer_static", true), // IsMutable=false
            new SetObjectState("frag_A", "obj_door", ObjectState.Active), // valid
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        // Warning for immutable layer
        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_warnings[0], Does.Contain("layer_static"));

        // Valid change should still be applied
        Assert.That(_core.HasOverlay("frag_A", "choice_X"), Is.True);
        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.HasValue, Is.True);
        Assert.That(overlay.Value.ToggledLayers, Is.Null,
            "Immutable layer toggle should not be stored");
        Assert.That(overlay.Value.ObjectStates, Is.Not.Null);
        Assert.That(overlay.Value.ObjectStates.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.ObjectStates[0].ObjectId, Is.EqualTo("obj_door"));

        // Version should increment (valid changes applied)
        Assert.That(_core.OverlayVersion, Is.EqualTo(1));
    }

    [Test]
    public void test_nonexistent_layer_is_skipped()
    {
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "nonexistent_layer", true),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_warnings[0], Does.Contain("nonexistent_layer"));

        // Log entry recorded with 0 applied changes
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(0));
    }

    [Test]
    public void test_set_object_state_with_invalid_object_id_is_skipped()
    {
        var changes = new ContentChange[]
        {
            new SetObjectState("frag_A", "nonexistent_obj", ObjectState.Hidden),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_warnings[0], Does.Contain("nonexistent_obj"));

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.HasValue, Is.True);
        Assert.That(overlay.Value.ObjectStates, Is.Null,
            "Invalid object state should not be stored");
    }

    [Test]
    public void test_modify_tag_weight_with_invalid_tag_id_is_skipped()
    {
        var changes = new ContentChange[]
        {
            new ModifyTagWeight("frag_A", "phantom_tag", 0.5f, WeightOperation.Add),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_warnings[0], Does.Contain("phantom_tag"));

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.HasValue, Is.True);
        Assert.That(overlay.Value.TagWeightMods, Is.Null);
    }

    [Test]
    public void test_valid_modify_tag_weight_is_stored_with_correct_mod_op()
    {
        _registry.ExistingTags.Add("fear");

        var changes = new ContentChange[]
        {
            new ModifyTagWeight("frag_A", "fear", 0.3f, WeightOperation.Add),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.TagWeightMods, Is.Not.Null);
        Assert.That(overlay.Value.TagWeightMods.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.TagWeightMods[0].TagId, Is.EqualTo("fear"));
        Assert.That(overlay.Value.TagWeightMods[0].Operation, Is.EqualTo(ModOp.Add));
        Assert.That(overlay.Value.TagWeightMods[0].Value, Is.EqualTo(0.3f));
    }

    // =========================================================================
    // AC-4: Empty ContentChange array
    // =========================================================================

    [Test]
    public void test_empty_changes_array_logs_and_increments_version()
    {
        _core.ApplyChanges("frag_A", "choice_empty", new ContentChange[0]);

        // No overlay change
        Assert.That(_core.HasOverlay("frag_A", "choice_empty"), Is.True,
            "Key should exist in overlay even with empty changes");
        var overlay = _core.GetOverlay("frag_A", "choice_empty");
        Assert.That(overlay.Value.TotalChanges, Is.EqualTo(0));

        // Log entry recorded
        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));
        Assert.That(_core.ChangeLog[0].InputChangeCount, Is.EqualTo(0));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(0));

        // Version incremented
        Assert.That(_core.OverlayVersion, Is.EqualTo(1));

        // Event fired
        Assert.That(_overlayEvents.Count, Is.EqualTo(1));
        Assert.That(_overlayEvents[0], Is.EqualTo("frag_A"));
    }

    [Test]
    public void test_null_changes_array_treated_as_empty()
    {
        _core.ApplyChanges("frag_A", "choice_null", null);

        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));
        Assert.That(_core.ChangeLog[0].InputChangeCount, Is.EqualTo(0));
        Assert.That(_core.OverlayVersion, Is.EqualTo(1));
        Assert.That(_overlayEvents.Count, Is.EqualTo(1));
    }

    // =========================================================================
    // AC-5: Cross-fragment OnOverlayChanged fires synchronously
    // =========================================================================

    [Test]
    public void test_on_overlay_changed_fires_with_target_fragment_id()
    {
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        };

        // frag_B also needs to be valid + have the layer
        _registry.ExistingFragments.Add("frag_B");
        _registry.LayerMutability[("frag_B", "layer_rain")] = true;

        _core.ApplyChanges("frag_B", "choice_A1", changes);

        Assert.That(_overlayEvents.Count, Is.EqualTo(1));
        Assert.That(_overlayEvents[0], Is.EqualTo("frag_B"));
    }

    [Test]
    public void test_on_overlay_changed_fires_synchronously_within_apply_changes()
    {
        bool fired = false;
        ChangeTrackerCore.OnOverlayChanged += _ => { fired = true; };

        _core.ApplyChanges("frag_A", "choice_sync", new ContentChange[]
        {
            new SetFlag("flag_sync", true),
        });

        Assert.That(fired, Is.True,
            "OnOverlayChanged must fire synchronously within ApplyChanges (same frame)");
    }

    [Test]
    public void test_on_overlay_changed_not_fired_for_invalid_fragment()
    {
        _core.ApplyChanges("nonexistent", "choice_1", new ContentChange[]
        {
            new SetFlag("flag_test", true),
        });

        Assert.That(_overlayEvents.Count, Is.EqualTo(0),
            "OnOverlayChanged should not fire when fragment is invalid");
    }

    // =========================================================================
    // Edge cases — constructor
    // =========================================================================

    [Test]
    public void test_constructor_rejects_null_registry()
    {
        Assert.Throws<ArgumentNullException>(() => new ChangeTrackerCore(null));
    }

    // =========================================================================
    // Edge cases — ResetStaticEvents
    // =========================================================================

    [Test]
    public void test_reset_static_events_clears_all_subscribers()
    {
        bool wasCalled = false;
        ChangeTrackerCore.OnWarning += _ => { wasCalled = true; };
        ChangeTrackerCore.ResetStaticEvents();

        // Trigger warning via invalid fragment
        _core.ApplyChanges("bad_frag", "c", new ContentChange[] { new SetFlag("f", true) });

        Assert.That(wasCalled, Is.False,
            "ResetStaticEvents should remove all subscribers");
    }

    // =========================================================================
    // Edge cases — HasOverlay / GetOverlay
    // =========================================================================

    [Test]
    public void test_has_overlay_returns_false_for_unregistered_key()
    {
        Assert.That(_core.HasOverlay("frag_A", "never_called"), Is.False);
    }

    [Test]
    public void test_get_overlay_returns_null_for_unregistered_key()
    {
        var result = _core.GetOverlay("frag_A", "never_called");
        Assert.That(result.HasValue, Is.False);
    }

    // =========================================================================
    // Edge cases — null ContentChange in array
    // =========================================================================

    [Test]
    public void test_null_entry_in_changes_array_is_skipped()
    {
        var changes = new ContentChange[]
        {
            new SetFlag("flag_a", true),
            null, // should be skipped silently
            new SetFlag("flag_b", false),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_core.ChangeLog.Count, Is.EqualTo(1));
        Assert.That(_core.ChangeLog[0].InputChangeCount, Is.EqualTo(3));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(2));

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.SetFlags.Count, Is.EqualTo(2));
    }

    // =========================================================================
    // Edge cases — empty string IDs within valid changes
    // =========================================================================

    [Test]
    public void test_empty_layer_id_skipped_with_warning()
    {
        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "", true),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(0));
    }

    [Test]
    public void test_empty_object_id_skipped_with_warning()
    {
        var changes = new ContentChange[]
        {
            new SetObjectState("frag_A", "", ObjectState.Hidden),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void test_empty_text_field_id_skipped_with_warning()
    {
        var changes = new ContentChange[]
        {
            new SetTextContent("frag_A", "", "new text"),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void test_empty_flag_id_skipped_with_warning()
    {
        var changes = new ContentChange[]
        {
            new SetFlag("", true),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(0));
    }

    // =========================================================================
    // Edge cases — SetTextContent (no structural validation, always stored)
    // =========================================================================

    [Test]
    public void test_set_text_content_is_always_stored()
    {
        var changes = new ContentChange[]
        {
            new SetTextContent("frag_A", "title_text", "The world has changed."),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.TextOverrides, Is.Not.Null);
        Assert.That(overlay.Value.TextOverrides.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.TextOverrides[0].TextFieldId, Is.EqualTo("title_text"));
        Assert.That(overlay.Value.TextOverrides[0].NewText, Is.EqualTo("The world has changed."));
    }

    [Test]
    public void test_set_text_content_stores_empty_new_text()
    {
        var changes = new ContentChange[]
        {
            new SetTextContent("frag_A", "title_text", null),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.TextOverrides[0].NewText, Is.EqualTo(""),
            "Null NewText should be stored as empty string");
    }

    // =========================================================================
    // Edge cases — UnlockAssociation
    // =========================================================================

    [Test]
    public void test_unlock_association_is_stored()
    {
        var changes = new ContentChange[]
        {
            new UnlockAssociation("frag_A", "frag_B"),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.UnlockedAssociations, Is.Not.Null);
        Assert.That(overlay.Value.UnlockedAssociations.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.UnlockedAssociations[0], Is.EqualTo("frag_B"));
    }

    [Test]
    public void test_unlock_association_empty_target_skipped()
    {
        var changes = new ContentChange[]
        {
            new UnlockAssociation("frag_A", ""),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        Assert.That(_warnings.Count, Is.GreaterThan(0));
        Assert.That(_core.ChangeLog[0].AppliedChangeCount, Is.EqualTo(0));
    }

    // =========================================================================
    // Edge cases — SetFlag (always applied, no fragment validation)
    // =========================================================================

    [Test]
    public void test_set_flag_stored_without_fragment_validation()
    {
        // SetFlag doesn't use TargetFragmentId for validation — operates globally
        var changes = new ContentChange[]
        {
            new SetFlag("global_flag", true),
        };

        // Even on a non-existent fragment, SetFlag should work
        // But the fragment validation happens first (AC-2), so this would fail.
        // Testing on a valid fragment:
        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.SetFlags, Is.Not.Null);
        Assert.That(overlay.Value.SetFlags.Count, Is.EqualTo(1));
        Assert.That(overlay.Value.SetFlags[0].FlagId, Is.EqualTo("global_flag"));
        Assert.That(overlay.Value.SetFlags[0].Value, Is.True);
    }

    // =========================================================================
    // Edge cases — TotalChanges counter
    // =========================================================================

    [Test]
    public void test_total_changes_counts_all_categories()
    {
        _registry.ExistingObjects.Add(("frag_A", "obj_box"));
        _registry.LayerMutability[("frag_A", "layer_fog")] = true;
        _registry.ExistingTags.Add("sorrow");

        var changes = new ContentChange[]
        {
            new ToggleVisualLayer("frag_A", "layer_fog", true),
            new SetObjectState("frag_A", "obj_box", ObjectState.Active),
            new SetTextContent("frag_A", "desc", "Changed."),
            new ModifyTagWeight("frag_A", "sorrow", 0.2f, WeightOperation.Add),
            new UnlockAssociation("frag_A", "frag_B"),
            new SetFlag("flag_all", true),
        };

        _core.ApplyChanges("frag_A", "choice_X", changes);

        var overlay = _core.GetOverlay("frag_A", "choice_X");
        Assert.That(overlay.Value.TotalChanges, Is.EqualTo(6));
    }

    // =========================================================================
    // Edge cases — OverlayVersion starts at 0 and increments monotonically
    // =========================================================================

    [Test]
    public void test_overlay_version_starts_at_zero()
    {
        Assert.That(_core.OverlayVersion, Is.EqualTo(0));
    }

    [Test]
    public void test_overlay_version_increments_across_multiple_calls()
    {
        _core.ApplyChanges("frag_A", "c1", new ContentChange[] { new SetFlag("a", true) });
        _core.ApplyChanges("frag_A", "c2", new ContentChange[] { new SetFlag("b", true) });
        _core.ApplyChanges("frag_B", "c3", new ContentChange[] { new SetFlag("c", true) });

        Assert.That(_core.OverlayVersion, Is.EqualTo(3));
    }

    // =========================================================================
    // Edge cases — ChangeLog entry fields
    // =========================================================================

    [Test]
    public void test_log_entry_timestamp_is_recent()
    {
        var before = DateTime.UtcNow;
        _core.ApplyChanges("frag_A", "choice_X", new ContentChange[]
        {
            new SetFlag("flag_t", true),
        });
        var after = DateTime.UtcNow;

        var entry = _core.ChangeLog[0];
        Assert.That(entry.Timestamp, Is.GreaterThanOrEqualTo(before.AddSeconds(-1)));
        Assert.That(entry.Timestamp, Is.LessThanOrEqualTo(after.AddSeconds(1)));
    }

    [Test]
    public void test_log_entry_records_resulting_version()
    {
        _core.ApplyChanges("frag_A", "c1", new ContentChange[] { new SetFlag("a", true) });
        Assert.That(_core.ChangeLog[0].ResultingOverlayVersion, Is.EqualTo(1));

        _core.ApplyChanges("frag_A", "c2", new ContentChange[] { new SetFlag("b", true) });
        Assert.That(_core.ChangeLog[1].ResultingOverlayVersion, Is.EqualTo(2));
    }

    // =========================================================================
    // Mock Fragment Registry
    // =========================================================================

    private class MockFragmentRegistry : IFragmentRegistry
    {
        public readonly HashSet<string> ExistingFragments = new();
        public readonly Dictionary<(string, string), bool> LayerMutability = new();
        public readonly HashSet<(string, string)> ExistingObjects = new();
        public readonly HashSet<string> ExistingTags = new();

        public bool HasFragment(string fragmentId)
        {
            return !string.IsNullOrEmpty(fragmentId) && ExistingFragments.Contains(fragmentId);
        }

        public bool IsLayerMutable(string fragmentId, string layerId)
        {
            if (string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(layerId))
                return false;
            return LayerMutability.TryGetValue((fragmentId, layerId), out bool mutable) && mutable;
        }

        public bool HasObject(string fragmentId, string objectId)
        {
            if (string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(objectId))
                return false;
            return ExistingObjects.Contains((fragmentId, objectId));
        }

        public bool HasTag(string tagId)
        {
            return !string.IsNullOrEmpty(tagId) && ExistingTags.Contains(tagId);
        }
    }
}
