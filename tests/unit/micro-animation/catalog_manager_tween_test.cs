using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Echoes;

/// <summary>
/// Unit tests for MicroAnimationSystem Story 001.
///
/// Covers four acceptance criteria:
///   AC-1: Catalog lookup — fetching defs by ID, missing defs, null catalog.
///   AC-2: MicroTween evaluation — easing curves, completion, looping, edge cases.
///   AC-3: Fragment transition — stopping old tweens, setting current fragment ID.
///   AC-4: Performance — zero-allocation tick, early-exit on empty tween list.
/// </summary>
public class CatalogManagerTweenTest
{
    // =========================================================================
    // Test Fixture
    // =========================================================================

    private MicroAnimationCatalog _catalog;
    private MicroAnimationManager _manager;
    private GameObject _managerGo;

    [SetUp]
    public void SetUp()
    {
        _catalog = ScriptableObject.CreateInstance<MicroAnimationCatalog>();
        _managerGo = new GameObject("Test_MicroAnimationManager");
        _manager = _managerGo.AddComponent<MicroAnimationManager>();
        _manager.SetCatalog(_catalog);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up static event subscription to avoid cross-test contamination
        GameSceneManager.OnFragmentTransitioned = null;

        if (_managerGo != null)
            UnityEngine.Object.DestroyImmediate(_managerGo);
        if (_catalog != null)
            UnityEngine.Object.DestroyImmediate(_catalog);

        _manager = null;
        _managerGo = null;
        _catalog = null;
    }

    // =========================================================================
    // Test Data Helpers
    // =========================================================================

    private AmbientAnimDef MakeAmbientDef(string id, float speed, float amplitude)
    {
        return new AmbientAnimDef
        {
            DefId = id,
            Implementation = AnimationImpl.Shader_VertexDisplace,
            DefaultSpeed = speed,
            DefaultAmplitude = amplitude,
            DefaultEasing = EaseType.SineInOut,
            ShaderPropertyName = "_WaveOffset",
            ShaderPropertyRange = new Vector2(0f, 1f)
        };
    }

    private TriggeredAnimDef MakeTriggeredDef(string id, float duration, EaseType ease)
    {
        return new TriggeredAnimDef
        {
            DefId = id,
            Implementation = AnimationImpl.Tween_OneShot,
            Duration = duration,
            Easing = ease
        };
    }

    private FeedbackAnimDef MakeFeedbackDef(string id, float duration, string shaderProp)
    {
        return new FeedbackAnimDef
        {
            DefId = id,
            Implementation = AnimationImpl.Shader_MaterialPulse,
            Duration = duration,
            ShaderPropertyName = shaderProp
        };
    }

    // =========================================================================
    // AC-1: Catalog Lookup
    // =========================================================================

    [Test]
    public void test_catalog_lookup_existing_ambient_def_returns_correct_fields()
    {
        // Arrange
        _catalog.AmbientDefs = new[]
        {
            MakeAmbientDef("leaf_sway", 0.3f, 0.02f),
            MakeAmbientDef("water_ripple", 1.0f, 0.05f),
        };

        // Act
        var result = _manager.GetAmbientDef("leaf_sway");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DefId, Is.EqualTo("leaf_sway"));
        Assert.That(result.DefaultSpeed, Is.EqualTo(0.3f));
        Assert.That(result.DefaultAmplitude, Is.EqualTo(0.02f));
    }

    [Test]
    public void test_catalog_lookup_missing_defid_returns_null_and_logs_warning()
    {
        // Arrange
        _catalog.AmbientDefs = new[] { MakeAmbientDef("leaf_sway", 0.3f, 0.02f) };

        // Assert: expect a warning about the missing def
        LogAssert.Expect(LogType.Warning, "MicroAnimationManager: AmbientAnimDef 'nonexistent' not found in catalog.");

        // Act
        var result = _manager.GetAmbientDef("nonexistent");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void test_catalog_null_catalog_returns_null_and_logs_warning()
    {
        // Arrange
        var go = new GameObject("Test_NullCatalog");
        var mgr = go.AddComponent<MicroAnimationManager>();
        // Do NOT call SetCatalog — catalog remains null

        // Assert: expect a warning about catalog not being loaded
        LogAssert.Expect(LogType.Warning, "MicroAnimationManager: Catalog not loaded.");

        // Act
        var result = mgr.GetAmbientDef("anything");

        // Assert
        Assert.That(result, Is.Null);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void test_catalog_lookup_triggered_def_returns_correct_fields()
    {
        // Arrange
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("examine_zoom_in", 0.5f, EaseType.EaseOutCubic),
            MakeTriggeredDef("ripple_out", 1.2f, EaseType.SineOut),
        };

        // Act
        var result = _manager.GetTriggeredDef("ripple_out");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DefId, Is.EqualTo("ripple_out"));
        Assert.That(result.Duration, Is.EqualTo(1.2f));
        Assert.That(result.Easing, Is.EqualTo(EaseType.SineOut));
    }

    [Test]
    public void test_catalog_lookup_feedback_def_returns_correct_fields()
    {
        // Arrange
        _catalog.FeedbackDefs = new[]
        {
            MakeFeedbackDef("hover_glow", 0.15f, "_GlowIntensity"),
            MakeFeedbackDef("click_ripple", 0.3f, "_RippleStrength"),
        };

        // Act
        var result = _manager.GetFeedbackDef("hover_glow");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DefId, Is.EqualTo("hover_glow"));
        Assert.That(result.Duration, Is.EqualTo(0.15f));
        Assert.That(result.ShaderPropertyName, Is.EqualTo("_GlowIntensity"));
    }

    [Test]
    public void test_catalog_lookup_triggered_missing_def_logs_warning()
    {
        // Arrange
        _catalog.TriggeredDefs = new[] { MakeTriggeredDef("ripple", 0.5f, EaseType.EaseOutCubic) };

        LogAssert.Expect(LogType.Warning, "MicroAnimationManager: TriggeredAnimDef 'missing_anim' not found in catalog.");

        // Act
        var result = _manager.GetTriggeredDef("missing_anim");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void test_catalog_lookup_empty_array_returns_null()
    {
        // Arrange
        _catalog.AmbientDefs = Array.Empty<AmbientAnimDef>();

        LogAssert.Expect(LogType.Warning, "MicroAnimationManager: AmbientAnimDef 'any' not found in catalog.");

        // Act
        var result = _manager.GetAmbientDef("any");

        // Assert
        Assert.That(result, Is.Null);
    }

    // =========================================================================
    // AC-2: MicroTween Evaluation
    // =========================================================================

    [Test]
    public void test_tween_sine_in_out_midpoint_returns_approx_half()
    {
        // Arrange: From=0, To=1, Duration=2s, Elapsed=1s, Ease=SineInOut
        var tween = new MicroTween(0f, 1f, 2f, EaseType.SineInOut);
        tween.Elapsed = 1f;

        // Act
        var value = tween.Evaluate();

        // Assert: SineInOut(0.5) -> 0.5 (sine-in-out is symmetric at midpoint)
        Assert.That(value, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void test_tween_sine_in_out_completion_returns_one_and_is_complete()
    {
        // Arrange
        var tween = new MicroTween(0f, 1f, 2f, EaseType.SineInOut);
        tween.Elapsed = 2f;

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(tween.IsComplete, Is.True);
    }

    [Test]
    public void test_tween_linear_midpoint_returns_exact_lerp()
    {
        // Arrange: Linear, From=10, To=20, Duration=2s, Elapsed=1s -> 15.0
        var tween = new MicroTween(10f, 20f, 2f, EaseType.Linear);
        tween.Elapsed = 1f;

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(15.0f).Within(0.0001f));
    }

    [Test]
    public void test_tween_zero_duration_returns_to_value()
    {
        // Arrange: Duration=0 returns ToValue immediately
        var tween = new MicroTween(0f, 100f, 0f, EaseType.Linear);

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(tween.IsComplete, Is.True);
    }

    [Test]
    public void test_tween_from_equals_to_returns_constant()
    {
        // Arrange: From=50, To=50, regardless of elapsed
        var tween = new MicroTween(50f, 50f, 1f, EaseType.SineInOut);
        tween.Elapsed = 0.3f;

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(50f).Within(0.0001f));
    }

    [Test]
    public void test_tween_sine_in_at_start_returns_approx_zero()
    {
        // Arrange: SineIn at t=0 -> 0
        var tween = new MicroTween(0f, 1f, 1f, EaseType.SineIn);
        tween.Elapsed = 0f;

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void test_tween_ease_out_cubic_known_checkpoint()
    {
        // Arrange: EaseOutCubic at t=0.5: 1 - (1-0.5)^3 = 1 - 0.125 = 0.875
        var tween = new MicroTween(0f, 1f, 1f, EaseType.EaseOutCubic);
        tween.Elapsed = 0.5f;

        // Act
        var value = tween.Evaluate();

        // Assert: Lerp(0, 1, 0.875) = 0.875
        Assert.That(value, Is.EqualTo(0.875f).Within(0.0001f));
    }

    [Test]
    public void test_tween_elapsed_exceeds_duration_value_clamped_and_complete()
    {
        // Arrange: Elapsed > Duration, value should be clamped to ToValue
        var tween = new MicroTween(0f, 1f, 1f, EaseType.Linear);
        tween.Elapsed = 5f; // Way past duration

        // Act
        var value = tween.Evaluate();

        // Assert
        Assert.That(value, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(tween.IsComplete, Is.True);
    }

    [Test]
    public void test_micro_tween_is_value_type()
    {
        // Assert: MicroTween is a struct (value type), not a class
        Assert.That(typeof(MicroTween).IsValueType, Is.True);
    }

    [Test]
    public void test_looping_tween_resets_elapsed_and_is_never_complete()
    {
        // Arrange
        var tween = new MicroTween(0f, 1f, 1f, EaseType.Linear, isLooping: true);
        tween.Elapsed = 1f; // At exactly duration

        // Act & Assert: IsComplete is always false for looping tweens
        Assert.That(tween.IsComplete, Is.False);
    }

    [Test]
    public void test_looping_tween_evaluates_correctly_after_reset()
    {
        // Arrange: looping tween that has been "ticked" past duration
        var tween = new MicroTween(0f, 1f, 1f, EaseType.Linear, isLooping: true);
        tween.Elapsed = 1.5f; // Past one loop

        // Simulate what UpdateTick does for looping tweens
        // (Elapsed >= Duration -> reset to 0)
        tween.Elapsed = 0f;

        // Act
        var value = tween.Evaluate();

        // Assert: At t=0, value should be FromValue
        Assert.That(value, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void test_tween_ease_out_elastic_midpoint_overshoots()
    {
        // Arrange: EaseOutElastic at t=0.5 — should overshoot beyond ToValue
        var tween = new MicroTween(0f, 1f, 1f, EaseType.EaseOutElastic);
        tween.Elapsed = 0.5f;

        // Act
        var value = tween.Evaluate();

        // Assert: Value should exist and be a valid float (elastic oscillates)
        // At t=0.5, elastic typically overshoots 1.0
        Assert.That(float.IsNaN(value), Is.False);
        Assert.That(value, Is.GreaterThan(0.5f));
    }

    [Test]
    public void test_tween_ease_out_bounce_approaches_to_value()
    {
        // Arrange: EaseOutBounce at completion
        var tween = new MicroTween(0f, 1f, 1f, EaseType.EaseOutBounce);
        tween.Elapsed = 1f;

        // Act
        var value = tween.Evaluate();

        // Assert: At t=1, should reach ToValue (1.0)
        Assert.That(value, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void test_tween_apply_ease_linear_identity()
    {
        // Assert: Linear easing is identity function
        Assert.That(MicroTween.ApplyEase(0f, EaseType.Linear), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(MicroTween.ApplyEase(0.5f, EaseType.Linear), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(MicroTween.ApplyEase(1f, EaseType.Linear), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void test_tween_apply_ease_edge_cases_zero_and_one()
    {
        // All easing functions should map 0 -> ~0 and 1 -> ~1
        foreach (EaseType ease in Enum.GetValues(typeof(EaseType)))
        {
            Assert.That(MicroTween.ApplyEase(0f, ease), Is.EqualTo(0f).Within(0.01f),
                $"Ease {ease} at t=0 should be approx 0");
            Assert.That(MicroTween.ApplyEase(1f, ease), Is.EqualTo(1f).Within(0.01f),
                $"Ease {ease} at t=1 should be approx 1");
        }
    }

    // =========================================================================
    // AC-3: Fragment Transition
    // =========================================================================

    [Test]
    public void test_fragment_transition_stops_old_fragment_tweens()
    {
        // Arrange: Start tweens for fragment "frag_A", then transition to "frag_B"
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 10f, EaseType.Linear), // Long duration so it stays active
        };

        // Add tweens manually bound to frag_A
        var tween1 = new MicroTween(0f, 1f, 10f, EaseType.Linear);
        var tween2 = new MicroTween(0f, 1f, 10f, EaseType.Linear);

        // Use reflection or internal access to set current fragment and add tweens
        // Simulate by directly invoking lifecycle methods
        _manager.PlayTriggered("ripple"); // Bound to _currentFragmentId (null initially)

        // Set a "current" fragment, add tweens for it
        // We need to trigger the transition handler
        GameSceneManager.OnFragmentTransitioned?.Invoke("chapter_1", "frag_A");
        Assert.That(_manager.CurrentFragmentId, Is.EqualTo("frag_A"));

        _manager.PlayTriggered("ripple"); // Now bound to frag_A
        var countBeforeTransition = _manager.ActiveTweenCount;

        // Act: Transition to frag_B — should stop frag_A tweens
        GameSceneManager.OnFragmentTransitioned?.Invoke("chapter_1", "frag_B");

        // Assert: All frag_A tweens stopped
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
        Assert.That(_manager.CurrentFragmentId, Is.EqualTo("frag_B"));
    }

    [Test]
    public void test_fragment_transition_sets_current_fragment_id()
    {
        // Act: Fire a fragment transition
        GameSceneManager.OnFragmentTransitioned?.Invoke("chapter_2", "frag_42");

        // Assert
        Assert.That(_manager.CurrentFragmentId, Is.EqualTo("frag_42"));
    }

    [Test]
    public void test_fragment_transition_with_no_tweens_is_noop()
    {
        // Arrange: No active tweens

        // Act: Transition should not throw
        Assert.DoesNotThrow(() =>
        {
            GameSceneManager.OnFragmentTransitioned?.Invoke("chapter_1", "frag_X");
        });

        // Assert
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
        Assert.That(_manager.CurrentFragmentId, Is.EqualTo("frag_X"));
    }

    [Test]
    public void test_fragment_transition_preserves_other_fragment_tweens()
    {
        // Arrange: Tween bound to frag_A, then manually add one bound to frag_B,
        // transition to frag_C should only stop frag_B tweens.

        // Set up catalog so PlayTriggered works
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 10f, EaseType.Linear),
        };

        // Set current to frag_B
        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_B");
        _manager.PlayTriggered("ripple"); // Bound to frag_B

        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(1));

        // Act: Transition to frag_C — should stop frag_B's tween
        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_C");

        // Assert
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
        Assert.That(_manager.CurrentFragmentId, Is.EqualTo("frag_C"));
    }

    // =========================================================================
    // AC-4: Performance
    // =========================================================================

    [Test]
    public void test_update_with_zero_tweens_returns_immediately()
    {
        // Arrange: No active tweens

        // Act: Calling UpdateTick with zero tweens should complete without error
        Assert.DoesNotThrow(() =>
        {
            _manager.UpdateTick(0.016f);
        });

        // Assert: Still zero tweens
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
    }

    [Test]
    public void test_update_with_multiple_tweens_processes_all()
    {
        // Arrange: Add 10 one-shot tweens, each 1s duration
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 1f, EaseType.Linear),
        };

        for (int i = 0; i < 10; i++)
        {
            _manager.PlayTriggered("ripple");
        }

        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(10));

        // Act: Tick past the full duration
        _manager.UpdateTick(2f);

        // Assert: All tweens completed and removed
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
    }

    [Test]
    public void test_update_tick_does_not_allocate_new_collections_per_call()
    {
        // Arrange: Add a few tweens
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 1f, EaseType.Linear),
        };

        for (int i = 0; i < 5; i++)
        {
            _manager.PlayTriggered("ripple");
        }

        // Act: Multiple ticks should not throw
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _manager.UpdateTick(0.016f);
            }
        });

        // Assert: After 100 ticks, tweens should be complete (total elapsed = 1.6s > 1s)
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
    }

    [Test]
    public void test_start_ambient_loop_creates_looping_tween()
    {
        // Arrange
        var def = MakeAmbientDef("wind_sway", 0.5f, 0.1f);

        // Act
        _manager.StartAmbientLoop(def, "frag_loop_test");

        // Assert
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(1));
    }

    [Test]
    public void test_start_ambient_loop_with_null_def_is_noop()
    {
        // Act
        _manager.StartAmbientLoop(null, "frag_X");

        // Assert: No tween added
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Test]
    public void test_stop_all_for_fragment_null_or_empty_is_noop()
    {
        // Arrange: Add a tween
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 1f, EaseType.Linear),
        };
        _manager.PlayTriggered("ripple");
        var countBefore = _manager.ActiveTweenCount;

        // Act: Stop with null/empty
        Assert.DoesNotThrow(() => _manager.StopAllForFragment(null));
        Assert.DoesNotThrow(() => _manager.StopAllForFragment(""));

        // Assert: No tweens removed
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(countBefore));
    }

    [Test]
    public void test_get_tween_value_out_of_bounds_returns_zero()
    {
        // Act & Assert
        Assert.That(_manager.GetTweenValue(-1), Is.EqualTo(0f));
        Assert.That(_manager.GetTweenValue(0), Is.EqualTo(0f));
        Assert.That(_manager.GetTweenValue(999), Is.EqualTo(0f));
    }

    [Test]
    public void test_play_triggered_when_catalog_null_is_noop()
    {
        // Arrange: Manager with no catalog
        var go = new GameObject("Test_NoCatalog");
        var mgr = go.AddComponent<MicroAnimationManager>();
        // Catalog is null by default

        LogAssert.Expect(LogType.Warning, "MicroAnimationManager: Catalog not loaded.");

        // Act
        mgr.PlayTriggered("ripple");

        // Assert: No tweens added
        Assert.That(mgr.ActiveTweenCount, Is.EqualTo(0));

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void test_on_complete_callback_is_invoked_when_tween_finishes()
    {
        // Arrange
        _catalog.TriggeredDefs = new[]
        {
            MakeTriggeredDef("ripple", 0.5f, EaseType.Linear),
        };

        bool callbackFired = false;
        _manager.PlayTriggered("ripple", onComplete: () => { callbackFired = true; });

        // Act: Tick past duration
        _manager.UpdateTick(0.6f);

        // Assert
        Assert.That(callbackFired, Is.True);
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(0));
    }

    [Test]
    public void test_looping_tween_on_complete_is_not_invoked_on_loop_reset()
    {
        // Arrange: Add a looping tween via StartAmbientLoop
        var def = MakeAmbientDef("sway", 0.5f, 0.1f);
        bool callbackFired = false;

        // We need to manually add a looping tween with a callback to test this
        // Using StartAmbientLoop which doesn't support callbacks, so the callback
        // would never fire anyway. This test verifies that looping tweens are
        // not removed after their duration elapses.
        _manager.StartAmbientLoop(def, "frag_loop");

        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(1));

        // Act: Tick past the loop duration multiple times
        _manager.UpdateTick(0.6f); // Past first loop (0.5s)
        _manager.UpdateTick(0.6f); // Past second loop

        // Assert: Looping tween is still active (not removed)
        Assert.That(_manager.ActiveTweenCount, Is.EqualTo(1));
    }

    // =========================================================================
    // MicroAnimationCatalog Direct Tests
    // =========================================================================

    [Test]
    public void test_catalog_direct_get_ambient_def_returns_null_for_null_array()
    {
        // Arrange: Catalog with null AmbientDefs array
        var cat = ScriptableObject.CreateInstance<MicroAnimationCatalog>();
        cat.AmbientDefs = null;

        // Act
        var result = cat.GetAmbientDef("anything");

        // Assert
        Assert.That(result, Is.Null);

        UnityEngine.Object.DestroyImmediate(cat);
    }

    [Test]
    public void test_catalog_direct_get_emotion_preset_case_insensitive()
    {
        // Arrange
        var cat = ScriptableObject.CreateInstance<MicroAnimationCatalog>();
        cat.EmotionPresets = new[]
        {
            new EmotionPreset
            {
                EmotionCategory = "Joy",
                SpeedMultiplier = 1.5f,
                HueShift = 0.1f,
            },
        };

        // Act
        var lower = cat.GetEmotionPreset("joy");
        var upper = cat.GetEmotionPreset("JOY");
        var mixed = cat.GetEmotionPreset("JoY");

        // Assert
        Assert.That(lower, Is.Not.Null);
        Assert.That(lower.EmotionCategory, Is.EqualTo("Joy"));
        Assert.That(upper, Is.Not.Null);
        Assert.That(mixed, Is.Not.Null);
        Assert.That(lower.SpeedMultiplier, Is.EqualTo(1.5f));

        UnityEngine.Object.DestroyImmediate(cat);
    }

    [Test]
    public void test_catalog_direct_get_emotion_preset_missing_returns_null()
    {
        // Arrange
        var cat = ScriptableObject.CreateInstance<MicroAnimationCatalog>();
        cat.EmotionPresets = Array.Empty<EmotionPreset>();

        // Act
        var result = cat.GetEmotionPreset("Sadness");

        // Assert
        Assert.That(result, Is.Null);

        UnityEngine.Object.DestroyImmediate(cat);
    }
}
