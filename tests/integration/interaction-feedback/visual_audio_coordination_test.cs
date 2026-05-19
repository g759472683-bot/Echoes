using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Echoes;

/// <summary>
/// Integration tests for InteractionFeedback Story 002 -- full visual + audio coordination.
///
/// Verifies that each of the 10 interaction handlers correctly coordinates both
/// MicroAnimationManager visual calls and AudioManager SFX calls in the same handler.
///
/// Covers:
///   - OnInteract calls both PlayTriggered AND PlaySFX in the same handler
///   - OnChoiceSelected preempts OnInteract visual (StopAllForObject called)
///   - Audio silent degrade: PlaySFX failure does not block visual feedback
///   - Transition suppression prevents ALL calls (visual + audio)
///   - Full mapping table coverage: all 10 events map to correct visual + audio calls
///
/// Uses InteractionFeedback's internal static stubs to intercept calls without
/// requiring real singletons (ADR-0001 testability pattern).
/// </summary>
[TestFixture]
public class VisualAudioCoordinationTest
{
    private GameObject _feedbackGO;
    private InteractionFeedback _feedback;
    private List<string> _playSfxCalls;
    private List<(string animId, string objectId, float duration)> _playTriggeredCalls;
    private List<(string animId, string objectId)> _playTriggeredNoDurationCalls;
    private List<(string objectId, GlowLevel level)> _setGlowCalls;
    private List<string> _stopAllCalls;
    private float _controlledTime;

    // SFX failure flag: when true, the PlaySFX stub throws
    private bool _sfxShouldThrow;

    // =========================================================================
    // SetUp / TearDown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        // Null all static events to prevent cross-test contamination
        InteractionManager.OnHoverEnter = null;
        InteractionManager.OnHoverExit = null;
        InteractionManager.OnInteract = null;
        InteractionManager.OnDragStart = null;
        InteractionManager.OnDragComplete = null;
        InteractionManager.OnDragCancel = null;
        InteractionManager.OnChoiceSelected = null;
        InteractionManager.OnChoiceHover = null;
        InteractionManager.OnRevealObject = null;
        InteractionManager.OnShowText = null;
        GameSceneManager.OnFragmentTransitionStarted = null;
        GameSceneManager.OnFragmentTransitioned = null;

        InteractionFeedback.ResetStubs();

        _controlledTime = 0f;
        _sfxShouldThrow = false;
        _playSfxCalls = new List<string>();
        _playTriggeredCalls = new List<(string, string, float)>();
        _playTriggeredNoDurationCalls = new List<(string, string)>();
        _setGlowCalls = new List<(string, GlowLevel)>();
        _stopAllCalls = new List<string>();

        // Create InteractionFeedback GameObject
        _feedbackGO = new GameObject("InteractionFeedback_IntegrationTest");
        _feedback = _feedbackGO.AddComponent<InteractionFeedback>();
        _feedback._timeProvider = () => _controlledTime;

        // Set up test stubs
        InteractionFeedback._playTriggeredStub = (animId, objId, dur, onComplete) =>
        {
            _playTriggeredCalls.Add((animId, objId, dur));
            onComplete?.Invoke();
        };
        InteractionFeedback._playTriggeredNoDurationStub = (animId, objId) =>
        {
            _playTriggeredNoDurationCalls.Add((animId, objId));
        };
        InteractionFeedback._setGlowLevelStub = (objId, level) =>
        {
            _setGlowCalls.Add((objId, level));
        };
        InteractionFeedback._stopAllForObjectStub = (objId) =>
        {
            _stopAllCalls.Add(objId);
        };
        InteractionFeedback._playSfxStub = (key) =>
        {
            if (_sfxShouldThrow)
                throw new InvalidOperationException("Simulated audio system failure.");
            _playSfxCalls.Add(key);
        };
    }

    [TearDown]
    public void TearDown()
    {
        InteractionManager.OnHoverEnter = null;
        InteractionManager.OnHoverExit = null;
        InteractionManager.OnInteract = null;
        InteractionManager.OnDragStart = null;
        InteractionManager.OnDragComplete = null;
        InteractionManager.OnDragCancel = null;
        InteractionManager.OnChoiceSelected = null;
        InteractionManager.OnChoiceHover = null;
        InteractionManager.OnRevealObject = null;
        InteractionManager.OnShowText = null;
        GameSceneManager.OnFragmentTransitionStarted = null;
        GameSceneManager.OnFragmentTransitioned = null;

        InteractionFeedback.ResetStubs();

        if (_feedbackGO != null)
        {
            UnityEngine.Object.DestroyImmediate(_feedbackGO);
            _feedbackGO = null;
            _feedback = null;
        }
    }

    // =========================================================================
    // AC: Full Mapping Table -- All 10 Events
    // =========================================================================

    [Test]
    public void test_visual_audio_on_interact_calls_play_triggered_and_play_sfx()
    {
        // Act
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });

        // Assert: both visual and audio
        Assert.AreEqual(1, _playTriggeredCalls.Count, "OnInteract should trigger PlayTriggered.");
        Assert.AreEqual("L3_flash", _playTriggeredCalls[0].animId);
        Assert.AreEqual("obj_1", _playTriggeredCalls[0].objectId);
        Assert.AreEqual(0.3f, _playTriggeredCalls[0].duration, 0.001f);

        Assert.AreEqual(1, _playSfxCalls.Count, "OnInteract should play SFX.");
        Assert.AreEqual("sfx_touch_generic", _playSfxCalls[0]);
    }

    [Test]
    public void test_visual_audio_on_drag_start_calls_play_triggered_and_play_sfx()
    {
        // Act
        InteractionManager.OnDragStart?.Invoke(new InteractiveObject { ObjectId = "obj_drag" });

        // Assert
        Assert.AreEqual(1, _playTriggeredNoDurationCalls.Count, "DragStart should trigger PlayTriggered (no duration override).");
        Assert.AreEqual("drag_trail", _playTriggeredNoDurationCalls[0].animId);
        Assert.AreEqual("obj_drag", _playTriggeredNoDurationCalls[0].objectId);

        Assert.AreEqual(1, _playSfxCalls.Count, "DragStart should play SFX.");
        Assert.AreEqual("sfx_drag_start", _playSfxCalls[0]);
    }

    [Test]
    public void test_visual_audio_on_drag_complete_calls_play_triggered_and_play_sfx()
    {
        // Act
        InteractionManager.OnDragComplete?.Invoke(new InteractiveObject { ObjectId = "obj_drag" });

        // Assert
        Assert.AreEqual(1, _playTriggeredCalls.Count, "DragComplete should trigger PlayTriggered.");
        Assert.AreEqual("L3_flash", _playTriggeredCalls[0].animId);
        Assert.AreEqual(0.3f, _playTriggeredCalls[0].duration, 0.001f);

        Assert.AreEqual(1, _playSfxCalls.Count, "DragComplete should play SFX.");
        Assert.AreEqual("sfx_drag_complete", _playSfxCalls[0]);
    }

    [Test]
    public void test_visual_audio_on_drag_cancel_calls_play_triggered_and_play_sfx()
    {
        // Act
        InteractionManager.OnDragCancel?.Invoke(new InteractiveObject { ObjectId = "obj_drag" });

        // Assert
        Assert.AreEqual(1, _playTriggeredCalls.Count, "DragCancel should trigger PlayTriggered.");
        Assert.AreEqual("spring_back", _playTriggeredCalls[0].animId);
        Assert.AreEqual(0.3f, _playTriggeredCalls[0].duration, 0.001f);

        Assert.AreEqual(1, _playSfxCalls.Count, "DragCancel should play SFX.");
        Assert.AreEqual("sfx_drag_cancel", _playSfxCalls[0]);

        // DragCancel does NOT have a priority gate but still calls ReleaseFeedback
        // (verified by the fact that subsequent events can claim the feedback channel)
    }

    [Test]
    public void test_visual_audio_on_choice_selected_calls_set_glow_play_triggered_and_play_sfx()
    {
        // Act
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");

        // Assert: L3_InnerGlow + ink_to_dark animation + confirm SFX
        Assert.AreEqual(1, _setGlowCalls.Count, "ChoiceSelected should set glow.");
        Assert.AreEqual("choice_1", _setGlowCalls[0].objectId);
        Assert.AreEqual(GlowLevel.L3_InnerGlow, _setGlowCalls[0].level);

        Assert.AreEqual(1, _playTriggeredCalls.Count, "ChoiceSelected should trigger animation.");
        Assert.AreEqual("ink_to_dark", _playTriggeredCalls[0].animId);
        Assert.AreEqual("choice_1", _playTriggeredCalls[0].objectId);
        Assert.AreEqual(0.4f, _playTriggeredCalls[0].duration, 0.001f);

        Assert.AreEqual(1, _playSfxCalls.Count, "ChoiceSelected should play SFX.");
        Assert.AreEqual("sfx_choice_confirm", _playSfxCalls[0]);
    }

    [Test]
    public void test_visual_audio_on_choice_hover_calls_set_glow_and_play_sfx()
    {
        // Act
        InteractionManager.OnChoiceHover?.Invoke("choice_1");

        // Assert: L2_Breathing + hover tick SFX (no triggered animation for choice hover)
        Assert.AreEqual(1, _setGlowCalls.Count, "ChoiceHover should set glow.");
        Assert.AreEqual("choice_1", _setGlowCalls[0].objectId);
        Assert.AreEqual(GlowLevel.L2_Breathing, _setGlowCalls[0].level);

        Assert.AreEqual(1, _playSfxCalls.Count, "ChoiceHover should play SFX.");
        Assert.AreEqual("sfx_hover_tick", _playSfxCalls[0]);
    }

    [Test]
    public void test_visual_audio_on_reveal_object_calls_play_triggered_and_play_sfx()
    {
        // Arrange
        var go = new GameObject("Interactable_obj_reveal");

        // Act
        InteractionManager.OnRevealObject?.Invoke(go);

        // Assert
        Assert.AreEqual(1, _playTriggeredCalls.Count, "RevealObject should trigger animation.");
        Assert.AreEqual("object_reveal", _playTriggeredCalls[0].animId);
        Assert.AreEqual("obj_reveal", _playTriggeredCalls[0].objectId,
            "Prefix 'Interactable_' should be stripped from object ID.");
        Assert.AreEqual(0.5f, _playTriggeredCalls[0].duration, 0.001f);

        Assert.AreEqual(1, _playSfxCalls.Count, "RevealObject should play SFX.");
        Assert.AreEqual("sfx_reveal", _playSfxCalls[0]);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void test_visual_audio_on_show_text_plays_sfx_only_no_visual()
    {
        // Act
        InteractionManager.OnShowText?.Invoke(new TextContent { Text = "hello", Duration = 2f });

        // Assert: audio only, no visual
        Assert.AreEqual(1, _playSfxCalls.Count, "ShowText should play SFX.");
        Assert.AreEqual("sfx_text_appear", _playSfxCalls[0]);

        Assert.AreEqual(0, _playTriggeredCalls.Count, "ShowText should NOT trigger animation.");
        Assert.AreEqual(0, _setGlowCalls.Count, "ShowText should NOT set glow.");
    }

    [Test]
    public void test_visual_audio_hover_enter_sets_glow_no_audio()
    {
        // Act
        InteractionManager.OnHoverEnter?.Invoke("obj_1");

        // Assert: visual only, no audio
        Assert.AreEqual(1, _setGlowCalls.Count, "HoverEnter should set glow.");
        Assert.AreEqual(GlowLevel.L2_Breathing, _setGlowCalls[0].level);

        Assert.AreEqual(0, _playSfxCalls.Count, "HoverEnter should NOT play audio.");
    }

    [Test]
    public void test_visual_audio_hover_exit_resets_glow_no_audio()
    {
        // Act
        InteractionManager.OnHoverExit?.Invoke("obj_1");

        // Assert
        Assert.AreEqual(1, _setGlowCalls.Count, "HoverExit should reset glow.");
        Assert.AreEqual(GlowLevel.L1_Static, _setGlowCalls[0].level);

        Assert.AreEqual(0, _playSfxCalls.Count, "HoverExit should NOT play audio.");
    }

    // =========================================================================
    // AC: OnChoiceSelected Preempts OnInteract Visual (StopAllForObject)
    // =========================================================================

    [Test]
    public void test_visual_audio_choice_selected_preempts_interact_visual_with_stop_all()
    {
        // Arrange: fire OnInteract (priority 5)
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_low" });
        Assert.AreEqual(1, _playTriggeredCalls.Count);

        // The first PlayTriggered always has StopAll called in TryClaimFeedback
        // when there was no prior feedback. Let me reset to check preemption specifically.
        _stopAllCalls.Clear();
        _playTriggeredCalls.Clear();

        // Now claim priority 5 with OnInteract on obj_low
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_low" });
        _stopAllCalls.Clear();
        _playTriggeredCalls.Clear();
        _setGlowCalls.Clear();

        // Act: OnChoiceSelected (priority 10) for a different choice -- should preempt
        InteractionManager.OnChoiceSelected?.Invoke("choice_high");

        // Assert: StopAllForObject was called on the lower-priority object
        Assert.AreEqual(1, _stopAllCalls.Count, "Higher priority should stop previous object's animations.");
        Assert.AreEqual("obj_low", _stopAllCalls[0],
            "Should stop animations on the preempted object.");

        // Assert: new visual calls made for the choice
        Assert.AreEqual(1, _setGlowCalls.Count, "Choice should set glow.");
        Assert.AreEqual("choice_high", _setGlowCalls[0].objectId);
        Assert.AreEqual(GlowLevel.L3_InnerGlow, _setGlowCalls[0].level);
    }

    // =========================================================================
    // AC: Audio Silent Degrade -- PlaySFX failure does not block visual
    // =========================================================================

    [Test]
    public void test_visual_audio_sfx_failure_does_not_block_visual_feedback()
    {
        // Arrange: make PlaySFX throw
        _sfxShouldThrow = true;

        // Act: fire OnInteract -- SFX will throw but visual should proceed
        Assert.DoesNotThrow(() =>
        {
            InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        }, "SFX failure should not propagate as an exception to the handler caller.");

        // Assert: visual still executed
        Assert.AreEqual(1, _playTriggeredCalls.Count,
            "Visual feedback should execute even when audio fails (silent degrade).");
        Assert.AreEqual("L3_flash", _playTriggeredCalls[0].animId);
    }

    // =========================================================================
    // AC: Transition Suppression Prevents ALL Calls (Visual + Audio)
    // =========================================================================

    [Test]
    public void test_visual_audio_transition_suppression_prevents_all_visual_and_audio()
    {
        // Arrange: suppress feedback
        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");

        // Act: fire multiple event types
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        InteractionManager.OnDragComplete?.Invoke(new InteractiveObject { ObjectId = "obj_drag" });
        InteractionManager.OnShowText?.Invoke(new TextContent { Text = "test" });
        InteractionManager.OnRevealObject?.Invoke(new GameObject("obj_reveal"));

        // Assert: zero visual and audio calls
        Assert.AreEqual(0, _playTriggeredCalls.Count,
            "No animations should be triggered during transition suppression.");
        Assert.AreEqual(0, _playTriggeredNoDurationCalls.Count,
            "No no-duration animations should be triggered during transition suppression.");
        Assert.AreEqual(0, _setGlowCalls.Count,
            "No glow levels should be set during transition suppression.");
        Assert.AreEqual(0, _playSfxCalls.Count,
            "No SFX should play during transition suppression.");
        Assert.AreEqual(0, _stopAllCalls.Count,
            "No stop calls should happen during transition suppression.");
    }

    // =========================================================================
    // AC: Priority System Integration -- Drag Complete vs Interact
    // =========================================================================

    [Test]
    public void test_visual_audio_drag_complete_priority_8_preempts_interact_priority_5()
    {
        // Arrange: claim with OnInteract (priority 5)
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_low" });
        _stopAllCalls.Clear();
        _playTriggeredCalls.Clear();

        // Act: OnDragComplete (priority 8) -- should preempt
        InteractionManager.OnDragComplete?.Invoke(new InteractiveObject { ObjectId = "obj_high" });

        // Assert
        Assert.AreEqual(1, _stopAllCalls.Count,
            "DragComplete (p8) should stop Interact's (p5) animations.");
        Assert.AreEqual("obj_low", _stopAllCalls[0]);
        Assert.AreEqual(1, _playTriggeredCalls.Count,
            "DragComplete's animation should fire after preempting.");
    }

    // =========================================================================
    // AC: DragCancel has no priority gate -- always runs (but is still suppressed)
    // =========================================================================

    [Test]
    public void test_visual_audio_drag_cancel_runs_even_when_higher_priority_is_active()
    {
        // Arrange: claim with high-priority OnChoiceSelected (priority 10)
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        _playTriggeredCalls.Clear();
        _setGlowCalls.Clear();

        // Act: DragCancel (priority 4, no priority gate) -- should still run
        InteractionManager.OnDragCancel?.Invoke(new InteractiveObject { ObjectId = "obj_cancel" });

        // Assert: DragCancel animation and audio fire despite lower priority
        Assert.AreEqual(1, _playTriggeredCalls.Count,
            "DragCancel should fire animation despite lower priority (no priority gate).");
        Assert.AreEqual("spring_back", _playTriggeredCalls[0].animId);
        Assert.AreEqual(1, _playSfxCalls.Count,
            "DragCancel should fire SFX despite lower priority.");
        Assert.AreEqual("sfx_drag_cancel", _playSfxCalls[0]);
    }

    // =========================================================================
    // AC: ShowText has no priority gate -- always fires audio
    // =========================================================================

    [Test]
    public void test_visual_audio_show_text_fires_even_when_higher_priority_is_active()
    {
        // Arrange: claim with high-priority OnChoiceSelected (priority 10)
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        _playSfxCalls.Clear();

        // Act: ShowText (priority 1, no priority gate)
        InteractionManager.OnShowText?.Invoke(new TextContent { Text = "test", Duration = 1f });

        // Assert: SFX still plays
        Assert.AreEqual(1, _playSfxCalls.Count,
            "ShowText SFX should play despite lower priority (no priority gate).");
        Assert.AreEqual("sfx_text_appear", _playSfxCalls[0]);
    }

    // =========================================================================
    // AC: HoverExit (cleanup) always runs -- no priority gate
    // =========================================================================

    [Test]
    public void test_visual_audio_hover_exit_cleanup_runs_even_when_higher_priority_is_active()
    {
        // Arrange: claim with high-priority OnChoiceSelected (priority 10)
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        _setGlowCalls.Clear();

        // Act: HoverExit (priority 2, no priority gate -- cleanup always runs)
        InteractionManager.OnHoverExit?.Invoke("obj_cleanup");

        // Assert: glow reset to L1_Static
        Assert.AreEqual(1, _setGlowCalls.Count,
            "HoverExit cleanup should run despite higher priority active.");
        Assert.AreEqual("obj_cleanup", _setGlowCalls[0].objectId);
        Assert.AreEqual(GlowLevel.L1_Static, _setGlowCalls[0].level);
    }

    // =========================================================================
    // AC: OnComplete callback releases feedback (priority reset)
    // =========================================================================

    [Test]
    public void test_visual_audio_on_complete_callback_releases_feedback_priority()
    {
        // Arrange: fire OnInteract (priority 5) -- the stub invokes onComplete immediately
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        _setGlowCalls.Clear();
        _stopAllCalls.Clear();

        // The onComplete callback was invoked synchronously by our stub,
        // which called ReleaseFeedback. So a new event of any priority should work.
        _controlledTime = 0.35f; // past debounce window

        // Act: fire a low-priority hover enter
        InteractionManager.OnHoverEnter?.Invoke("obj_2");

        // Assert: should work because feedback was released
        Assert.AreEqual(1, _setGlowCalls.Count,
            "After onComplete releases feedback, new events should claim the channel.");
    }

    // =========================================================================
    // AC: InteractiveObject with null ObjectId is handled gracefully
    // =========================================================================

    [Test]
    public void test_visual_audio_interact_null_object_id_handled_gracefully()
    {
        // Arrange: InteractiveObject with null ObjectId
        var obj = new InteractiveObject { ObjectId = null, Type = InteractionType.Touch };

        // Act: should not throw
        Assert.DoesNotThrow(() =>
        {
            InteractionManager.OnInteract?.Invoke(obj);
        });
    }
}
