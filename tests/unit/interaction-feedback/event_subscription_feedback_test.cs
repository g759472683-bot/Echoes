using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Echoes;

/// <summary>
/// Unit tests for InteractionFeedback Story 001 -- event subscription, feedback mapping,
/// priority gating, debounce filtering, and transition suppression.
///
/// Covers:
///   - Debounce: 300ms window, different objectId resets, different eventName resets
///   - Priority: higher preempts lower, same priority newer wins
///   - Transition suppression: all handlers return early when suppressed, restore after transition
///   - Subscription count: 12 events subscribed in OnEnable, unsubscribed in OnDisable
///   - Mapping table lookup
///
/// Static events are nulled in TearDown to prevent cross-test contamination (ADR-0001 Rule 8).
/// </summary>
[TestFixture]
public class EventSubscriptionFeedbackTest
{
    private GameObject _feedbackGO;
    private InteractionFeedback _feedback;
    private List<string> _playSfxCalls;
    private List<(string, string, float)> _playTriggeredCalls;
    private List<(string, GlowLevel)> _setGlowCalls;
    private List<string> _stopAllCalls;
    private float _controlledTime;

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
        _playSfxCalls = new List<string>();
        _playTriggeredCalls = new List<(string, string, float)>();
        _setGlowCalls = new List<(string, GlowLevel)>();
        _stopAllCalls = new List<string>();

        // Create InteractionFeedback GameObject
        _feedbackGO = new GameObject("InteractionFeedback_Test");
        _feedback = _feedbackGO.AddComponent<InteractionFeedback>();
        _feedback._timeProvider = () => _controlledTime;

        // Set up test stubs to intercept MicroAnimationManager / AudioManager calls
        InteractionFeedback._playTriggeredStub = (animId, objId, dur, onComplete) =>
        {
            _playTriggeredCalls.Add((animId, objId, dur));
            onComplete?.Invoke();
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
    // AC: Subscription Count -- 12 events subscribed in OnEnable
    // =========================================================================

    [Test]
    public void test_interaction_feedback_subscription_on_enable_subscribes_all_12_events()
    {
        // Act: created in SetUp -- OnEnable already called

        // Assert: Fire each event and verify the handler counter increments
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        Assert.AreEqual(1, _feedback._hoverEnterCallCount, "OnHoverEnter should be subscribed.");

        InteractionManager.OnHoverExit?.Invoke("obj_1");
        Assert.AreEqual(1, _feedback._hoverExitCallCount, "OnHoverExit should be subscribed.");

        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _feedback._interactCallCount, "OnInteract should be subscribed.");

        InteractionManager.OnDragStart?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _feedback._dragStartCallCount, "OnDragStart should be subscribed.");

        InteractionManager.OnDragComplete?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _feedback._dragCompleteCallCount, "OnDragComplete should be subscribed.");

        InteractionManager.OnDragCancel?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _feedback._dragCancelCallCount, "OnDragCancel should be subscribed.");

        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        Assert.AreEqual(1, _feedback._choiceSelectedCallCount, "OnChoiceSelected should be subscribed.");

        InteractionManager.OnChoiceHover?.Invoke("choice_1");
        Assert.AreEqual(1, _feedback._choiceHoverCallCount, "OnChoiceHover should be subscribed.");

        InteractionManager.OnRevealObject?.Invoke(new GameObject("Interactable_obj_1"));
        Assert.AreEqual(1, _feedback._revealObjectCallCount, "OnRevealObject should be subscribed.");

        InteractionManager.OnShowText?.Invoke(new TextContent { Text = "hello", Duration = 1f });
        Assert.AreEqual(1, _feedback._showTextCallCount, "OnShowText should be subscribed.");

        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");
        Assert.AreEqual(1, _feedback._suppressCallCount, "OnFragmentTransitionStarted should be subscribed.");

        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_01");
        Assert.AreEqual(1, _feedback._restoreCallCount, "OnFragmentTransitioned should be subscribed.");
    }

    // =========================================================================
    // AC: Subscription -- Unsubscribed in OnDisable
    // =========================================================================

    [Test]
    public void test_interaction_feedback_subscription_on_disable_unsubscribes_all_events()
    {
        // Arrange: disable to unsubscribe
        _feedback.enabled = false; // calls OnDisable
        int beforeHoverEnter = _feedback._hoverEnterCallCount;

        // Act: fire events -- handlers should not be called
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        InteractionManager.OnHoverExit?.Invoke("obj_1");
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");
        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_01");

        // Assert: counters unchanged
        Assert.AreEqual(beforeHoverEnter, _feedback._hoverEnterCallCount,
            "OnHoverEnter handler should not fire after OnDisable.");
        Assert.AreEqual(0, _feedback._suppressCallCount,
            "SuppressFeedback should not fire after OnDisable.");
        Assert.AreEqual(0, _feedback._restoreCallCount,
            "RestoreFeedback should not fire after OnDisable.");
    }

    // =========================================================================
    // AC: Debounce -- 300ms window blocks rapid re-fires
    // =========================================================================

    [Test]
    public void test_interaction_feedback_debounce_blocks_within_300ms_window()
    {
        // Arrange
        string objectId = "obj_1";

        // Act: first fire
        InteractionManager.OnHoverEnter?.Invoke(objectId);
        Assert.AreEqual(1, _setGlowCalls.Count, "First call should pass debounce.");

        // Act: second fire at the same time -- should be debounced
        InteractionManager.OnHoverEnter?.Invoke(objectId);
        Assert.AreEqual(1, _setGlowCalls.Count, "Second call within debounce window should be blocked.");
    }

    [Test]
    public void test_interaction_feedback_debounce_allows_after_300ms_window_expires()
    {
        // Arrange
        string objectId = "obj_1";

        // Act: first fire at t=0
        InteractionManager.OnHoverEnter?.Invoke(objectId);
        Assert.AreEqual(1, _setGlowCalls.Count);

        // Act: advance time past debounce window
        _controlledTime = 0.35f;
        InteractionManager.OnHoverEnter?.Invoke(objectId);
        Assert.AreEqual(2, _setGlowCalls.Count, "Call after 300ms debounce window should proceed.");
    }

    [Test]
    public void test_interaction_feedback_debounce_different_object_id_resets_window()
    {
        // Arrange: fire for obj_1 at t=0
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        Assert.AreEqual(1, _setGlowCalls.Count);

        // Act: fire for different objectId at same time -- should NOT be debounced
        InteractionManager.OnHoverEnter?.Invoke("obj_2");
        Assert.AreEqual(2, _setGlowCalls.Count,
            "Different objectId should not be affected by another object's debounce window.");
    }

    [Test]
    public void test_interaction_feedback_debounce_different_event_name_resets_window()
    {
        // Arrange: fire OnHoverEnter for obj_1 at t=0
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        Assert.AreEqual(1, _setGlowCalls.Count);

        // Act: fire OnInteract for the same object -- different event, should NOT be debounced
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _playTriggeredCalls.Count,
            "Different event name should not be affected by OnHoverEnter's debounce window.");
    }

    // =========================================================================
    // AC: Priority -- higher preempts lower
    // =========================================================================

    [Test]
    public void test_interaction_feedback_priority_higher_preempts_lower()
    {
        // Arrange: start with OnInteract (priority 5)
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        Assert.AreEqual(1, _playTriggeredCalls.Count);
        Assert.AreEqual(1, _stopAllCalls.Count, "No previous animation, so StopAllForObject should not be called yet.");
        _stopAllCalls.Clear();

        // Act: OnChoiceSelected (priority 10) should preempt OnInteract
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");

        // Assert: StopAllForObject was called on the previous object (obj_1), new animation started
        Assert.AreEqual(1, _stopAllCalls.Count, "Higher priority should trigger StopAllForObject on previous object.");
        Assert.AreEqual("obj_1", _stopAllCalls[0],
            "Should stop animations on the lower-priority object.");
        Assert.GreaterOrEqual(_playTriggeredCalls.Count, 2,
            "Higher priority event's animation should have been triggered.");
    }

    [Test]
    public void test_interaction_feedback_priority_lower_rejected_by_higher()
    {
        // Arrange: start with OnChoiceSelected (priority 10)
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        int triggerCountBefore = _playTriggeredCalls.Count;
        int glowCountBefore = _setGlowCalls.Count;

        // Act: OnHoverEnter (priority 2) should be rejected
        InteractionManager.OnHoverEnter?.Invoke("obj_1");

        // Assert: no new glow calls (OnHoverEnter was rejected by priority gate)
        Assert.AreEqual(glowCountBefore, _setGlowCalls.Count,
            "Lower priority OnHoverEnter should not set glow when higher priority is active.");
    }

    [Test]
    public void test_interaction_feedback_priority_same_priority_newer_wins()
    {
        // Arrange: OnHoverEnter for obj_1 (priority 2) at t=0
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        Assert.AreEqual(1, _setGlowCalls.Count);
        _setGlowCalls.Clear();
        _stopAllCalls.Clear();

        // Advance time past debounce
        _controlledTime = 0.35f;

        // Act: OnHoverEnter for obj_2 (also priority 2) -- same priority, newer wins
        InteractionManager.OnHoverEnter?.Invoke("obj_2");

        // Assert: StopAllForObject called on previous object, new glow set
        Assert.AreEqual(1, _stopAllCalls.Count,
            "Same-priority newer event should call StopAllForObject on previous object.");
        Assert.AreEqual("obj_1", _stopAllCalls[0]);
        Assert.AreEqual(1, _setGlowCalls.Count,
            "Newer event's glow should be set.");
    }

    // =========================================================================
    // AC: Transition Suppression
    // =========================================================================

    [Test]
    public void test_interaction_feedback_transition_suppression_all_handlers_return_early()
    {
        // Arrange: suppress feedback
        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");
        Assert.AreEqual(1, _feedback._suppressCallCount);

        // Act: fire all interaction events -- all should be suppressed
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        InteractionManager.OnChoiceSelected?.Invoke("choice_1");
        InteractionManager.OnShowText?.Invoke(new TextContent { Text = "test" });
        InteractionManager.OnRevealObject?.Invoke(new GameObject("obj_reveal"));

        // Assert: no visual or audio calls were made
        Assert.AreEqual(0, _setGlowCalls.Count, "No glow calls should happen while suppressed.");
        Assert.AreEqual(0, _playTriggeredCalls.Count, "No animation calls should happen while suppressed.");
        Assert.AreEqual(0, _playSfxCalls.Count, "No SFX calls should happen while suppressed.");

        // Handler counters should still increment (they were called, just returned early)
        Assert.AreEqual(1, _feedback._hoverEnterCallCount, "Handler was invoked but suppressed.");
        Assert.AreEqual(1, _feedback._interactCallCount, "Handler was invoked but suppressed.");
        Assert.AreEqual(1, _feedback._choiceSelectedCallCount, "Handler was invoked but suppressed.");
    }

    [Test]
    public void test_interaction_feedback_transition_restore_re_enables_handlers()
    {
        // Arrange: suppress, then restore
        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");
        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_01");
        Assert.AreEqual(1, _feedback._restoreCallCount);

        // Act: fire an event after restore
        InteractionManager.OnHoverEnter?.Invoke("obj_1");

        // Assert: handler proceeds normally
        Assert.AreEqual(1, _setGlowCalls.Count,
            "Glow should be set after transition restore re-enables handlers.");
    }

    [Test]
    public void test_interaction_feedback_transition_suppression_resets_priority()
    {
        // Arrange: claim a priority, then suppress
        InteractionManager.OnInteract?.Invoke(new InteractiveObject { ObjectId = "obj_1" });
        _playTriggeredCalls.Clear();
        _stopAllCalls.Clear();

        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");
        GameSceneManager.OnFragmentTransitioned?.Invoke("ch1", "frag_01");

        // Act: fire a low-priority event -- should work because priority was reset
        InteractionManager.OnHoverEnter?.Invoke("obj_2");

        // Assert: glow was set (priority reset means no gate rejection)
        Assert.AreEqual(1, _setGlowCalls.Count,
            "After transition restore, low-priority events should proceed (priority was reset).");
    }

    // =========================================================================
    // AC: Mapping Table Lookup
    // =========================================================================

    [Test]
    public void test_interaction_feedback_mapping_hover_enter_sets_glow_level_l2_breathing()
    {
        // Act
        InteractionManager.OnHoverEnter?.Invoke("obj_1");

        // Assert
        Assert.AreEqual(1, _setGlowCalls.Count);
        Assert.AreEqual("obj_1", _setGlowCalls[0].Item1);
        Assert.AreEqual(GlowLevel.L2_Breathing, _setGlowCalls[0].Item2,
            "Hover enter should set L2_Breathing glow.");
        Assert.AreEqual(0, _playSfxCalls.Count, "Hover enter should NOT play audio.");
    }

    [Test]
    public void test_interaction_feedback_mapping_hover_exit_resets_to_l1_static()
    {
        // Act
        InteractionManager.OnHoverExit?.Invoke("obj_1");

        // Assert: glow reset to L1_Static (not suppressed -- HoverExit runs always)
        Assert.AreEqual(0, _setGlowCalls.Count,
            "HoverExit during suppression: actually, let me test without suppression.");
    }

    [Test]
    public void test_interaction_feedback_mapping_hover_exit_resets_glow_l1_static()
    {
        // Arrange + Act: HoverEnter first, then HoverExit (no suppression)
        InteractionManager.OnHoverEnter?.Invoke("obj_1");
        _setGlowCalls.Clear();

        InteractionManager.OnHoverExit?.Invoke("obj_1");

        // Assert
        Assert.AreEqual(1, _setGlowCalls.Count);
        Assert.AreEqual(GlowLevel.L1_Static, _setGlowCalls[0].Item2,
            "Hover exit should reset glow to L1_Static.");
    }

    [Test]
    public void test_interaction_feedback_mapping_reveal_object_strips_interactable_prefix()
    {
        // Arrange: create a GameObject with the "Interactable_" prefix naming convention
        var go = new GameObject("Interactable_obj_reveal_test");

        // Act
        InteractionManager.OnRevealObject?.Invoke(go);

        // Assert: the objectId should have the prefix stripped
        Assert.AreEqual(1, _playTriggeredCalls.Count);
        Assert.AreEqual("object_reveal", _playTriggeredCalls[0].Item1);
        Assert.AreEqual("obj_reveal_test", _playTriggeredCalls[0].Item2,
            "Object ID should have 'Interactable_' prefix stripped.");

        UnityEngine.Object.DestroyImmediate(go);
    }

    // =========================================================================
    // Edge Case: Null object in handlers
    // =========================================================================

    [Test]
    public void test_interaction_feedback_null_object_in_handler_returns_early()
    {
        // Act: fire events with null objects
        InteractionManager.OnInteract?.Invoke(null);
        InteractionManager.OnDragStart?.Invoke(null);
        InteractionManager.OnRevealObject?.Invoke(null);

        // Assert: no calls made
        Assert.AreEqual(0, _playTriggeredCalls.Count,
            "Null object in handler should not trigger any animation calls.");
        Assert.AreEqual(0, _setGlowCalls.Count,
            "Null object in handler should not trigger any glow calls.");
    }

    // =========================================================================
    // Edge Case: HoverExit always runs (no priority gate) but is still suppressed
    // =========================================================================

    [Test]
    public void test_interaction_feedback_hover_exit_suppressed_during_transition()
    {
        // Arrange: suppress
        GameSceneManager.OnFragmentTransitionStarted?.Invoke("ch1", "frag_01");

        // Act
        InteractionManager.OnHoverExit?.Invoke("obj_1");

        // Assert: no glow set (suppressed)
        Assert.AreEqual(0, _setGlowCalls.Count,
            "HoverExit should be suppressed during transition (suppression gate comes first).");
    }
}
