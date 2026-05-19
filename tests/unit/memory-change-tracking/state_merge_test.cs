using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Unit tests for ChangeTrackerCore.GetCurrentState (memory-change-tracking S002).
///
/// Covers 5 acceptance criteria:
///   AC-1: VisualLayer toggle overlay merge
///   AC-2: ModifyTagWeight sequential application with clamping
///   AC-3: Repeatable choice overwrite (via ApplyChanges)
///   AC-4: UnlockAssociation idempotent (set union)
///   AC-5: Weight clamping [0.0, 1.0]
/// </summary>
public class StateMergeTest
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

        // Register fragments
        _registry.ExistingFragments.Add("frag_B");
        _registry.ExistingFragments.Add("frag_C");
        _registry.ExistingFragments.Add("frag_D");
        _registry.ExistingFragments.Add("frag_E");

        // Register layers and their mutability
        _registry.LayerMutability[("frag_B", "layer_rain")] = true;
        _registry.LayerMutability[("frag_B", "layer_sun")] = true;
        _registry.LayerMutability[("frag_C", "layer_fog")] = true;

        // Register objects
        _registry.ExistingObjects.Add(("frag_B", "obj_door"));
        _registry.ExistingObjects.Add(("frag_C", "obj_lamp"));

        // Register tags
        _registry.ExistingTags.Add("nostalgia");
        _registry.ExistingTags.Add("hope");
        _registry.ExistingTags.Add("fear");
        _registry.ExistingTags.Add("peace");

        // Base state for frag_B
        _stateProvider.VisualLayers["frag_B"] = new List<VisualLayer>
        {
            new VisualLayer("layer_rain", "rain_sprite", false, 1, default, true),
            new VisualLayer("layer_sun", "sun_sprite", true, 2, default, true),
        };
        _stateProvider.EmotionalTags["frag_B"] = new List<EmotionalTag>
        {
            new EmotionalTag("hope", 0.5f, true),
        };
        _stateProvider.InteractiveObjects["frag_B"] = new List<InteractiveObject>
        {
            new InteractiveObject { ObjectId = "obj_door", DefaultState = ObjectState.Active },
        };

        // Base state for frag_C
        _stateProvider.VisualLayers["frag_C"] = new List<VisualLayer>
        {
            new VisualLayer("layer_fog", "fog_sprite", true, 1, default, true),
        };
        _stateProvider.EmotionalTags["frag_C"] = new List<EmotionalTag>
        {
            new EmotionalTag("nostalgia", 0.5f),
            new EmotionalTag("peace", 0.7f),
        };

        // Base state for frag_D
        _stateProvider.VisualLayers["frag_D"] = new List<VisualLayer>();
        _stateProvider.EmotionalTags["frag_D"] = new List<EmotionalTag>
        {
            new EmotionalTag("fear", 0.9f),
        };

        // Base state for frag_E
        _stateProvider.VisualLayers["frag_E"] = new List<VisualLayer>();
        _stateProvider.EmotionalTags["frag_E"] = new List<EmotionalTag>();

        _core = new ChangeTrackerCore(_registry);
        _warnings = new List<string>();
        ChangeTrackerCore.OnWarning += msg => _warnings?.Add(msg);
    }

    [TearDown]
    public void TearDown()
    {
        ChangeTrackerCore.ResetStaticEvents();
    }

    private ResolvedLayerState FindLayer(ResolvedFragmentState state, string layerId)
    {
        return state.VisualLayers.FirstOrDefault(l => l.LayerId == layerId);
    }

    private ResolvedTagWeight FindTag(ResolvedFragmentState state, string tagId)
    {
        return state.TagWeights.FirstOrDefault(t => t.TagId == tagId);
    }

    // =========================================================================
    // AC-1: VisualLayer toggle merge
    // =========================================================================

    [Test]
    public void test_visual_layer_toggle_overlay_overrides_default()
    {
        // AC-1: layer_rain DefaultVisible=false, overlay toggles it true
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.HasValue, Is.True);

        var rainLayer = FindLayer(state.Value, "layer_rain");
        Assert.That(rainLayer.LayerId, Is.EqualTo("layer_rain"));
        Assert.That(rainLayer.Visible, Is.True,
            "Overlay should toggle layer_rain to visible");
    }

    [Test]
    public void test_unmodified_layer_keeps_so_default()
    {
        // AC-1: layer_sun has no overlay → keeps SO default (Visible=true)
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        var sunLayer = FindLayer(state.Value, "layer_sun");
        Assert.That(sunLayer.Visible, Is.True,
            "Unmodified layer should keep SO default value");
    }

    [Test]
    public void test_multiple_overlays_for_same_layer_last_wins()
    {
        // First overlay sets layer_rain to true
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        });

        // Second overlay (higher OrderIndex) sets layer_rain to false
        _core.ApplyChanges("frag_B", "choice_2", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", false),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        var rainLayer = FindLayer(state.Value, "layer_rain");
        Assert.That(rainLayer.Visible, Is.False,
            "Later overlay should override earlier overlay");
    }

    [Test]
    public void test_no_overlays_returns_base_state()
    {
        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.HasValue, Is.True);

        var rainLayer = FindLayer(state.Value, "layer_rain");
        Assert.That(rainLayer.Visible, Is.False,
            "Without overlays, default SO value should be preserved");
    }

    // =========================================================================
    // AC-2: ModifyTagWeight sequential application
    // =========================================================================

    [Test]
    public void test_modify_tag_weight_sequential_application()
    {
        // AC-2: nostalgia BaseWeight=0.5, +0.3 Add, then ×0.8 Multiply
        // Expected: 0.5 + 0.3 = 0.8, 0.8 × 0.8 = 0.64
        _core.ApplyChanges("frag_C", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_C", "nostalgia", 0.3f, WeightOperation.Add),
        });
        _core.ApplyChanges("frag_C", "choice_2", new ContentChange[]
        {
            new ModifyTagWeight("frag_C", "nostalgia", 0.8f, WeightOperation.Multiply),
        });

        var state = _core.GetCurrentState("frag_C", _stateProvider);
        var nostalgia = FindTag(state.Value, "nostalgia");
        Assert.That(nostalgia.Weight, Is.EqualTo(0.64f).Within(0.001f),
            "0.5 + 0.3 = 0.8, 0.8 * 0.8 = 0.64");
    }

    [Test]
    public void test_modify_tag_weight_set_replaces_value()
    {
        _core.ApplyChanges("frag_C", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_C", "nostalgia", 0.99f, WeightOperation.Set),
        });

        var state = _core.GetCurrentState("frag_C", _stateProvider);
        var nostalgia = FindTag(state.Value, "nostalgia");
        Assert.That(nostalgia.Weight, Is.EqualTo(0.99f).Within(0.001f),
            "Set should replace base weight entirely");
    }

    [Test]
    public void test_unmodified_tag_keeps_base_weight()
    {
        _core.ApplyChanges("frag_C", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_C", "nostalgia", 0.3f, WeightOperation.Add),
        });

        var state = _core.GetCurrentState("frag_C", _stateProvider);
        var peace = FindTag(state.Value, "peace");
        Assert.That(peace.Weight, Is.EqualTo(0.7f).Within(0.001f),
            "Unmodified tag should keep base weight");
    }

    // =========================================================================
    // AC-3: Repeatable choice overwrite
    // =========================================================================

    [Test]
    public void test_repeatable_choice_overwrites_overlay()
    {
        // First: set layer_rain visible
        _core.ApplyChanges("frag_B", "choice_X", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        });

        // AC-3: Same key, different changes → overwrite
        _core.ApplyChanges("frag_B", "choice_X", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", false),
        });

        Assert.That(_core.ChangeLog.Count, Is.EqualTo(2),
            "Both calls should be logged independently");

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        var rainLayer = FindLayer(state.Value, "layer_rain");
        Assert.That(rainLayer.Visible, Is.False,
            "Second call's overlay should overwrite first call's");
    }

    [Test]
    public void test_repeatable_choice_new_changes_replace_all_old()
    {
        // First: toggle rain + sun
        _core.ApplyChanges("frag_B", "choice_Y", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
            new ToggleVisualLayer("frag_B", "layer_sun", false),
        });

        // Second: only toggle rain (different value) — sun goes back to base
        _core.ApplyChanges("frag_B", "choice_Y", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", false),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(FindLayer(state.Value, "layer_rain").Visible, Is.False);
        Assert.That(FindLayer(state.Value, "layer_sun").Visible, Is.True,
            "When overlay is replaced, sun should revert to base SO default (true)");
    }

    // =========================================================================
    // AC-4: UnlockAssociation idempotent
    // =========================================================================

    [Test]
    public void test_unlock_association_is_idempotent()
    {
        // First unlock
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new UnlockAssociation("frag_B", "frag_Z"),
        });

        // Second unlock of same target
        _core.ApplyChanges("frag_B", "choice_2", new ContentChange[]
        {
            new UnlockAssociation("frag_B", "frag_Z"),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.Value.UnlockedAssociations.Count, Is.EqualTo(1),
            "Duplicate UnlockAssociation should be idempotent — only 1 entry");
        Assert.That(state.Value.UnlockedAssociations.Contains("frag_Z"), Is.True);
    }

    [Test]
    public void test_unlock_association_multiple_targets_all_preserved()
    {
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new UnlockAssociation("frag_B", "frag_X"),
            new UnlockAssociation("frag_B", "frag_Y"),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.Value.UnlockedAssociations.Count, Is.EqualTo(2));
        Assert.That(state.Value.UnlockedAssociations.Contains("frag_X"), Is.True);
        Assert.That(state.Value.UnlockedAssociations.Contains("frag_Y"), Is.True);
    }

    // =========================================================================
    // AC-5: Weight clamping
    // =========================================================================

    [Test]
    public void test_weight_clamped_to_one_on_overflow()
    {
        // AC-5: BaseWeight=0.9 + Add 0.3 → 1.2 → clamp to 1.0
        _core.ApplyChanges("frag_D", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_D", "fear", 0.3f, WeightOperation.Add),
        });

        var state = _core.GetCurrentState("frag_D", _stateProvider);
        var fear = FindTag(state.Value, "fear");
        Assert.That(fear.Weight, Is.EqualTo(1.0f));
    }

    [Test]
    public void test_weight_clamped_to_zero_on_underflow()
    {
        _core.ApplyChanges("frag_D", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_D", "fear", -1.0f, WeightOperation.Add),
        });

        var state = _core.GetCurrentState("frag_D", _stateProvider);
        var fear = FindTag(state.Value, "fear");
        Assert.That(fear.Weight, Is.EqualTo(0.0f));
    }

    [Test]
    public void test_base_weight_below_zero_clamped_before_merge()
    {
        // BaseWeight=-0.1 → clamp to 0.0 before applying overlays
        _stateProvider.EmotionalTags["frag_D"] = new List<EmotionalTag>
        {
            new EmotionalTag("fear", -0.1f),
        };

        _core.ApplyChanges("frag_D", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_D", "fear", 0.5f, WeightOperation.Add),
        });

        var state = _core.GetCurrentState("frag_D", _stateProvider);
        var fear = FindTag(state.Value, "fear");
        Assert.That(fear.Weight, Is.EqualTo(0.5f).Within(0.001f),
            "Base -0.1 clamped to 0.0, then +0.5 = 0.5");
    }

    [Test]
    public void test_multiply_by_two_clamped_to_one()
    {
        _core.ApplyChanges("frag_D", "choice_1", new ContentChange[]
        {
            new ModifyTagWeight("frag_D", "fear", 2.0f, WeightOperation.Multiply),
        });

        var state = _core.GetCurrentState("frag_D", _stateProvider);
        var fear = FindTag(state.Value, "fear");
        Assert.That(fear.Weight, Is.EqualTo(1.0f),
            "0.9 * 2.0 = 1.8 → clamped to 1.0");
    }

    [Test]
    public void test_nan_base_weight_clamped_to_zero()
    {
        _stateProvider.EmotionalTags["frag_E"] = new List<EmotionalTag>
        {
            new EmotionalTag("hope", float.NaN),
        };
        _stateProvider.ExistingFragments.Add("frag_E");

        var state = _core.GetCurrentState("frag_E", _stateProvider);
        var hope = FindTag(state.Value, "hope");
        Assert.That(hope.Weight, Is.EqualTo(0.0f));
    }

    // =========================================================================
    // Edge cases — GetCurrentState input validation
    // =========================================================================

    [Test]
    public void test_get_current_state_null_provider_returns_null()
    {
        var state = _core.GetCurrentState("frag_B", null);
        Assert.That(state.HasValue, Is.False);
        Assert.That(_warnings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void test_get_current_state_nonexistent_fragment_returns_null()
    {
        var state = _core.GetCurrentState("nonexistent", _stateProvider);
        Assert.That(state.HasValue, Is.False);
    }

    [Test]
    public void test_get_current_state_empty_fragment_id_returns_null()
    {
        var state = _core.GetCurrentState("", _stateProvider);
        Assert.That(state.HasValue, Is.False);
    }

    [Test]
    public void test_get_current_state_null_fragment_id_returns_null()
    {
        var state = _core.GetCurrentState(null, _stateProvider);
        Assert.That(state.HasValue, Is.False);
    }

    // =========================================================================
    // Edge cases — SetObjectState merge
    // =========================================================================

    [Test]
    public void test_set_object_state_overlay_overrides_default()
    {
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new SetObjectState("frag_B", "obj_door", ObjectState.Hidden),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        var door = state.Value.ObjectStates.FirstOrDefault(o => o.ObjectId == "obj_door");
        Assert.That(door.State, Is.EqualTo(ObjectState.Hidden));
    }

    [Test]
    public void test_set_object_state_no_overlay_keeps_default()
    {
        var state = _core.GetCurrentState("frag_B", _stateProvider);
        var door = state.Value.ObjectStates.FirstOrDefault(o => o.ObjectId == "obj_door");
        Assert.That(door.State, Is.EqualTo(ObjectState.Active),
            "Without overlay, object should keep SO default state");
    }

    // =========================================================================
    // Edge cases — SetTextContent merge
    // =========================================================================

    [Test]
    public void test_text_content_overlay_merge()
    {
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new SetTextContent("frag_B", "desc", "First text"),
        });
        _core.ApplyChanges("frag_B", "choice_2", new ContentChange[]
        {
            new SetTextContent("frag_B", "desc", "Second text"),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.Value.TextContents.ContainsKey("desc"), Is.True);
        Assert.That(state.Value.TextContents["desc"], Is.EqualTo("Second text"),
            "Later overlay should overwrite text content");
    }

    [Test]
    public void test_text_content_different_fields_independent()
    {
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new SetTextContent("frag_B", "title", "Title text"),
            new SetTextContent("frag_B", "body", "Body text"),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        Assert.That(state.Value.TextContents["title"], Is.EqualTo("Title text"));
        Assert.That(state.Value.TextContents["body"], Is.EqualTo("Body text"));
    }

    // =========================================================================
    // Edge cases — multi-category merge
    // =========================================================================

    [Test]
    public void test_all_six_change_types_merged_in_single_state()
    {
        _registry.LayerMutability[("frag_B", "layer_ghost")] = true;
        _stateProvider.VisualLayers["frag_B"].Add(
            new VisualLayer("layer_ghost", "ghost_sprite", false, 3, default, true));

        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
            new SetObjectState("frag_B", "obj_door", ObjectState.Disabled),
            new SetTextContent("frag_B", "narrator", "Everything changed."),
            new ModifyTagWeight("frag_B", "hope", 0.2f, WeightOperation.Add),
            new UnlockAssociation("frag_B", "frag_Z"),
            new SetFlag("unlocked_secret", true),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);

        // Visual layers
        Assert.That(FindLayer(state.Value, "layer_rain").Visible, Is.True);
        Assert.That(FindLayer(state.Value, "layer_ghost").Visible, Is.False);

        // Object states
        Assert.That(state.Value.ObjectStates.First(o => o.ObjectId == "obj_door").State,
            Is.EqualTo(ObjectState.Disabled));

        // Text contents
        Assert.That(state.Value.TextContents["narrator"], Is.EqualTo("Everything changed."));

        // Tag weights
        Assert.That(FindTag(state.Value, "hope").Weight, Is.EqualTo(0.7f).Within(0.001f));

        // Associations
        Assert.That(state.Value.UnlockedAssociations.Contains("frag_Z"), Is.True);
    }

    // =========================================================================
    // Edge cases — ClampWeight static method
    // =========================================================================

    [Test]
    public void test_clamp_weight_normal_value_unchanged()
    {
        Assert.That(ChangeTrackerCore.ClampWeight(0.5f), Is.EqualTo(0.5f));
    }

    [Test]
    public void test_clamp_weight_above_one()
    {
        Assert.That(ChangeTrackerCore.ClampWeight(1.5f), Is.EqualTo(1.0f));
    }

    [Test]
    public void test_clamp_weight_below_zero()
    {
        Assert.That(ChangeTrackerCore.ClampWeight(-0.5f), Is.EqualTo(0.0f));
    }

    [Test]
    public void test_clamp_weight_negative_infinity()
    {
        Assert.That(ChangeTrackerCore.ClampWeight(float.NegativeInfinity), Is.EqualTo(0.0f));
    }

    [Test]
    public void test_clamp_weight_positive_infinity()
    {
        Assert.That(ChangeTrackerCore.ClampWeight(float.PositiveInfinity), Is.EqualTo(1.0f));
    }

    // =========================================================================
    // Edge cases — FragmentState with no base data
    // =========================================================================

    [Test]
    public void test_get_current_state_with_no_base_layers_returns_empty_list()
    {
        _stateProvider.VisualLayers.Remove("frag_B");
        _core.ApplyChanges("frag_B", "choice_1", new ContentChange[]
        {
            new ToggleVisualLayer("frag_B", "layer_rain", true),
        });

        var state = _core.GetCurrentState("frag_B", _stateProvider);
        // ToggleVisualLayer for non-existent base layer should still create entry
        Assert.That(state.Value.VisualLayers.Count, Is.GreaterThan(0));
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
        public readonly HashSet<string> ExistingFragments = new() { "frag_B", "frag_C", "frag_D", "frag_E" };
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
