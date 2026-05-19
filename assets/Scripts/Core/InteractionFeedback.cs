using System;
using System.Collections.Generic;
using UnityEngine;
using Echoes;

/// <summary>
/// Core interaction feedback coordinator implementing ADR-0014 (Interaction Feedback System).
///
/// Pure event-driven MonoBehaviour with NO Update() method. Subscribes to 10 InteractionManager
/// static events and 2 GameSceneManager static events. Routes events through priority gating,
/// debounce filtering, and transition suppression to coordinate MicroAnimationManager visual
/// feedback and AudioManager SFX playback.
///
/// Events consumed (from InteractionManager):
///   OnHoverEnter(string), OnHoverExit(string), OnInteract(InteractiveObject),
///   OnDragStart(InteractiveObject), OnDragComplete(InteractiveObject), OnDragCancel(InteractiveObject),
///   OnChoiceSelected(string), OnChoiceHover(string), OnRevealObject(GameObject), OnShowText(TextContent)
///
/// Events consumed (from GameSceneManager):
///   OnFragmentTransitionStarted(string, string), OnFragmentTransitioned(string, string)
///
/// Priority system:
///   10: OnChoiceSelected    4: OnDragCancel
///    8: OnDragComplete      3: OnChoiceHover
///    7: OnRevealObject      2: OnHoverEnter, OnHoverExit (HoverExit always runs, no priority gate)
///    6: OnDragStart         1: OnShowText (no priority gate)
///    5: OnInteract
///
/// Implements: InteractionFeedback Epic (#18), Stories 001 and 002.
/// </summary>
public class InteractionFeedback : MonoBehaviour
{
    // =========================================================================
    // Constants
    // =========================================================================

    /// <summary>Debounce window in seconds (300ms per GDD spec).</summary>
    private const float DEBOUNCE_WINDOW = 0.3f;

    /// <summary>Priority: choice selected is the most important feedback event.</summary>
    private const int PRIORITY_CHOICE_SELECTED = 10;

    /// <summary>Priority: drag complete (player action resolved).</summary>
    private const int PRIORITY_DRAG_COMPLETE = 8;

    /// <summary>Priority: object reveal (surprise / discovery moment).</summary>
    private const int PRIORITY_REVEAL_OBJECT = 7;

    /// <summary>Priority: drag start (player begins a deliberate action).</summary>
    private const int PRIORITY_DRAG_START = 6;

    /// <summary>Priority: standard interact / click.</summary>
    private const int PRIORITY_INTERACT = 5;

    /// <summary>Priority: drag cancel (action aborted).</summary>
    private const int PRIORITY_DRAG_CANCEL = 4;

    /// <summary>Priority: hovering over a choice option.</summary>
    private const int PRIORITY_CHOICE_HOVER = 3;

    /// <summary>Priority: hovering over an interactable object.</summary>
    private const int PRIORITY_HOVER_ENTER = 2;

    /// <summary>Priority: text display (lowest, does not preempt anything).</summary>
    private const int PRIORITY_SHOW_TEXT = 1;

    // =========================================================================
    // Serialized Fields
    // =========================================================================

    [SerializeField] private FeedbackMappings _mappings;

    // =========================================================================
    // Internal State
    // =========================================================================

    private bool _feedbackSuppressed;
    private readonly Dictionary<string, float> _lastTriggerTime = new Dictionary<string, float>();
    private int _currentFeedbackPriority;
    private string _activeVisualObjectId;

    /// <summary>
    /// Test-only time provider. When null (production), uses Time.time.
    /// When set (test), returns the controlled time value. Internal for test access.
    /// </summary>
    internal Func<float> _timeProvider;

    // =========================================================================
    // Testability Wrappers (internal, set by tests to intercept calls)
    // =========================================================================

    /// <summary>Test-only: replaces MicroAnimationManager.PlayTriggered calls.</summary>
    internal static Action<string, string, float, Action> _playTriggeredStub;

    /// <summary>Test-only: replaces MicroAnimationManager.SetGlowLevel calls.</summary>
    internal static Action<string, GlowLevel> _setGlowLevelStub;

    /// <summary>Test-only: replaces MicroAnimationManager.StopAllForObject calls.</summary>
    internal static Action<string> _stopAllForObjectStub;

    /// <summary>Test-only: replaces MicroAnimationManager.PlayTriggered(string, string) calls (no duration override).</summary>
    internal static Action<string, string> _playTriggeredNoDurationStub;

    /// <summary>Test-only: replaces AudioManager.PlaySFX calls.</summary>
    internal static Action<string> _playSfxStub;

    // =========================================================================
    // Forwarded handler counts for subscription verification (internal, test-accessible)
    // =========================================================================

    internal int _hoverEnterCallCount;
    internal int _hoverExitCallCount;
    internal int _interactCallCount;
    internal int _dragStartCallCount;
    internal int _dragCompleteCallCount;
    internal int _dragCancelCallCount;
    internal int _choiceSelectedCallCount;
    internal int _choiceHoverCallCount;
    internal int _revealObjectCallCount;
    internal int _showTextCallCount;
    internal int _suppressCallCount;
    internal int _restoreCallCount;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void OnEnable()
    {
        InteractionManager.OnHoverEnter += HandleHoverEnter;
        InteractionManager.OnHoverExit += HandleHoverExit;
        InteractionManager.OnInteract += HandleInteract;
        InteractionManager.OnDragStart += HandleDragStart;
        InteractionManager.OnDragComplete += HandleDragComplete;
        InteractionManager.OnDragCancel += HandleDragCancel;
        InteractionManager.OnChoiceSelected += HandleChoiceSelected;
        InteractionManager.OnChoiceHover += HandleChoiceHover;
        InteractionManager.OnRevealObject += HandleRevealObject;
        InteractionManager.OnShowText += HandleShowText;

        GameSceneManager.OnFragmentTransitionStarted += SuppressFeedback;
        GameSceneManager.OnFragmentTransitioned += RestoreFeedback;
    }

    private void OnDisable()
    {
        InteractionManager.OnHoverEnter -= HandleHoverEnter;
        InteractionManager.OnHoverExit -= HandleHoverExit;
        InteractionManager.OnInteract -= HandleInteract;
        InteractionManager.OnDragStart -= HandleDragStart;
        InteractionManager.OnDragComplete -= HandleDragComplete;
        InteractionManager.OnDragCancel -= HandleDragCancel;
        InteractionManager.OnChoiceSelected -= HandleChoiceSelected;
        InteractionManager.OnChoiceHover -= HandleChoiceHover;
        InteractionManager.OnRevealObject -= HandleRevealObject;
        InteractionManager.OnShowText -= HandleShowText;

        GameSceneManager.OnFragmentTransitionStarted -= SuppressFeedback;
        GameSceneManager.OnFragmentTransitioned -= RestoreFeedback;
    }

    // =========================================================================
    // Event Handlers -- InteractionManager Events (10 handlers)
    // =========================================================================

    /// <summary>
    /// Handles hover enter: sets glow to L2_Breathing for subtle breathing pulse.
    /// No audio. Priority 2, debounced at 300ms.
    /// </summary>
    /// <param name="objectId">The ObjectId of the hovered interactable.</param>
    private void HandleHoverEnter(string objectId)
    {
        _hoverEnterCallCount++;
        if (_feedbackSuppressed) return;
        if (IsDebounced(objectId, "OnHoverEnter")) return;
        if (!TryClaimFeedback(PRIORITY_HOVER_ENTER)) return;

        _activeVisualObjectId = objectId;
        CallSetGlowLevel(objectId, GlowLevel.L2_Breathing);
    }

    /// <summary>
    /// Handles hover exit: resets glow to L1_Static ink dot.
    /// No audio. No priority gate -- cleanup always runs. Releases feedback claim.
    /// </summary>
    /// <param name="objectId">The ObjectId of the previously hovered interactable.</param>
    private void HandleHoverExit(string objectId)
    {
        _hoverExitCallCount++;
        if (_feedbackSuppressed) return;

        CallSetGlowLevel(objectId, GlowLevel.L1_Static);
        ReleaseFeedback();
    }

    /// <summary>
    /// Handles interact (click): plays L3_flash triggered animation with touch SFX.
    /// Priority 5, debounced at 300ms. Releases feedback on animation complete.
    /// </summary>
    /// <param name="obj">The InteractiveObject that was clicked.</param>
    private void HandleInteract(InteractiveObject obj)
    {
        _interactCallCount++;
        if (_feedbackSuppressed) return;
        if (obj == null) return;
        string objectId = obj.ObjectId;
        if (IsDebounced(objectId, "OnInteract")) return;
        if (!TryClaimFeedback(PRIORITY_INTERACT)) return;

        _activeVisualObjectId = objectId;
        CallPlayTriggered("L3_flash", objectId, 0.3f, ReleaseFeedback);
        CallPlaySfx("sfx_touch_generic");
    }

    /// <summary>
    /// Handles drag start: plays drag_trail animation with drag start SFX.
    /// Priority 6. No debounce -- drag start is a deliberate action.
    /// </summary>
    /// <param name="obj">The InteractiveObject being dragged.</param>
    private void HandleDragStart(InteractiveObject obj)
    {
        _dragStartCallCount++;
        if (_feedbackSuppressed) return;
        if (obj == null) return;
        string objectId = obj.ObjectId;
        if (!TryClaimFeedback(PRIORITY_DRAG_START)) return;

        _activeVisualObjectId = objectId;
        CallPlayTriggeredNoDuration("drag_trail", objectId);
        CallPlaySfx("sfx_drag_start");
    }

    /// <summary>
    /// Handles drag complete: plays L3_flash celebration animation + drag complete SFX.
    /// Priority 8. Releases feedback on animation complete.
    /// </summary>
    /// <param name="obj">The InteractiveObject whose drag completed successfully.</param>
    private void HandleDragComplete(InteractiveObject obj)
    {
        _dragCompleteCallCount++;
        if (_feedbackSuppressed) return;
        if (obj == null) return;
        string objectId = obj.ObjectId;
        if (!TryClaimFeedback(PRIORITY_DRAG_COMPLETE)) return;

        _activeVisualObjectId = objectId;
        CallPlayTriggered("L3_flash", objectId, 0.3f, ReleaseFeedback);
        CallPlaySfx("sfx_drag_complete");
    }

    /// <summary>
    /// Handles drag cancel: plays spring_back revert animation + cancel SFX.
    /// No priority gate -- cancel cleanup always runs. Releases feedback.
    /// </summary>
    /// <param name="obj">The InteractiveObject whose drag was cancelled.</param>
    private void HandleDragCancel(InteractiveObject obj)
    {
        _dragCancelCallCount++;
        if (_feedbackSuppressed) return;
        if (obj == null) return;
        string objectId = obj.ObjectId;

        CallPlayTriggered("spring_back", objectId, 0.3f);
        CallPlaySfx("sfx_drag_cancel");
        ReleaseFeedback();
    }

    /// <summary>
    /// Handles choice selected: sets L3_InnerGlow on the choice, plays ink_to_dark
    /// animation, and plays choice confirm SFX. Priority 10 (highest).
    /// Releases feedback on animation complete.
    /// </summary>
    /// <param name="choiceId">The chosen option ID.</param>
    private void HandleChoiceSelected(string choiceId)
    {
        _choiceSelectedCallCount++;
        if (_feedbackSuppressed) return;
        if (!TryClaimFeedback(PRIORITY_CHOICE_SELECTED)) return;

        _activeVisualObjectId = choiceId;
        CallSetGlowLevel(choiceId, GlowLevel.L3_InnerGlow);
        CallPlayTriggered("ink_to_dark", choiceId, 0.4f, ReleaseFeedback);
        CallPlaySfx("sfx_choice_confirm");
    }

    /// <summary>
    /// Handles choice hover: sets L2_Breathing glow on the choice option with hover tick SFX.
    /// Priority 3, debounced at 300ms.
    /// </summary>
    /// <param name="choiceId">The hovered choice option ID.</param>
    private void HandleChoiceHover(string choiceId)
    {
        _choiceHoverCallCount++;
        if (_feedbackSuppressed) return;
        if (IsDebounced(choiceId, "OnChoiceHover")) return;
        if (!TryClaimFeedback(PRIORITY_CHOICE_HOVER)) return;

        _activeVisualObjectId = choiceId;
        CallSetGlowLevel(choiceId, GlowLevel.L2_Breathing);
        CallPlaySfx("sfx_hover_tick");
    }

    /// <summary>
    /// Handles reveal object: plays object_reveal animation with reveal SFX.
    /// Priority 7. Extracts object ID from the collider GameObject, stripping
    /// the "Interactable_" prefix if present.
    /// Releases feedback on animation complete.
    /// </summary>
    /// <param name="obj">The revealed collider GameObject (named "Interactable_{objectId}").</param>
    private void HandleRevealObject(GameObject obj)
    {
        _revealObjectCallCount++;
        if (_feedbackSuppressed) return;
        if (obj == null) return;
        string objectId = GetObjectId(obj);
        if (!TryClaimFeedback(PRIORITY_REVEAL_OBJECT)) return;

        _activeVisualObjectId = objectId;
        CallPlayTriggered("object_reveal", objectId, 0.5f, ReleaseFeedback);
        CallPlaySfx("sfx_reveal");
    }

    /// <summary>
    /// Handles show text: plays text appear SFX only (no visual animation).
    /// Priority 1, no priority gate -- text audio does not preempt visual feedback.
    /// </summary>
    /// <param name="textContent">The text content to display.</param>
    private void HandleShowText(TextContent textContent)
    {
        _showTextCallCount++;
        if (_feedbackSuppressed) return;
        // No priority gate for text -- it does not preempt other feedback

        CallPlaySfx("sfx_text_appear");
    }

    // =========================================================================
    // Event Handlers -- GameSceneManager Transition Events (2 handlers)
    // =========================================================================

    /// <summary>
    /// Suppresses all feedback during a fragment transition.
    /// Subscribed to GameSceneManager.OnFragmentTransitionStarted.
    /// Resets the priority state so no stale claims survive into the next fragment.
    /// </summary>
    /// <param name="chapterKey">The chapter being transitioned within.</param>
    /// <param name="fragmentId">The target fragment ID.</param>
    private void SuppressFeedback(string chapterKey, string fragmentId)
    {
        _suppressCallCount++;
        _feedbackSuppressed = true;
        _currentFeedbackPriority = 0;
    }

    /// <summary>
    /// Restores feedback after a fragment transition completes.
    /// Subscribed to GameSceneManager.OnFragmentTransitioned.
    /// </summary>
    /// <param name="chapterKey">The chapter transitioned within.</param>
    /// <param name="fragmentId">The now-active fragment ID.</param>
    private void RestoreFeedback(string chapterKey, string fragmentId)
    {
        _restoreCallCount++;
        _feedbackSuppressed = false;
    }

    // =========================================================================
    // Core Logic -- Priority Gating
    // =========================================================================

    /// <summary>
    /// Attempts to claim the feedback channel for a given priority level.
    /// Higher priority preempts lower. Equal priority allows the newer event to win.
    /// When preempting, stops all animations on the previously active object.
    /// </summary>
    /// <param name="priority">The priority level of the incoming event (0-10).</param>
    /// <returns>True if the feedback channel was claimed; false if a higher-priority
    /// feedback is active and should not be interrupted.</returns>
    private bool TryClaimFeedback(int priority)
    {
        // No current feedback -- claim it unconditionally
        if (_currentFeedbackPriority == 0)
        {
            _currentFeedbackPriority = priority;
            return true;
        }

        // Higher priority preempts lower -- stop the previous object's animations
        if (priority > _currentFeedbackPriority)
        {
            if (!string.IsNullOrEmpty(_activeVisualObjectId))
            {
                CallStopAllForObject(_activeVisualObjectId);
            }
            _currentFeedbackPriority = priority;
            return true;
        }

        // Same priority -- newer event wins, stop the previous object's animations
        if (priority == _currentFeedbackPriority)
        {
            if (!string.IsNullOrEmpty(_activeVisualObjectId))
            {
                CallStopAllForObject(_activeVisualObjectId);
            }
            return true;
        }

        // Lower priority -- rejected
        return false;
    }

    /// <summary>
    /// Releases the feedback claim, resetting priority to 0.
    /// Called by animation onComplete callbacks and cleanup handlers (HoverExit, DragCancel).
    /// </summary>
    private void ReleaseFeedback()
    {
        _currentFeedbackPriority = 0;
        _activeVisualObjectId = null;
    }

    // =========================================================================
    // Core Logic -- Debounce System
    // =========================================================================

    /// <summary>
    /// Checks whether the given (objectId, eventName) pair is within the 300ms debounce window.
    /// Different objectId or different eventName are tracked independently -- debounce is scoped
    /// to the specific (objectId, eventName) combination.
    /// </summary>
    /// <param name="objectId">The interactable object or choice ID.</param>
    /// <param name="eventName">The event name (e.g., "OnHoverEnter", "OnInteract").</param>
    /// <returns>True if the event should be debounced (suppressed); false if it should proceed.</returns>
    private bool IsDebounced(string objectId, string eventName)
    {
        string key = $"{objectId}|{eventName}";
        float currentTime = _timeProvider != null ? _timeProvider() : Time.time;

        if (_lastTriggerTime.TryGetValue(key, out float lastTime))
        {
            if (currentTime - lastTime < DEBOUNCE_WINDOW)
                return true;
        }

        _lastTriggerTime[key] = currentTime;
        return false;
    }

    // =========================================================================
    // Core Logic -- Object ID Resolution
    // =========================================================================

    /// <summary>
    /// Extracts a stable object ID from a GameObject reference.
    /// Strips the "Interactable_" prefix if present (matching InteractionManager's
    /// collider GameObject naming convention), otherwise returns the GameObject's name.
    /// </summary>
    /// <param name="go">The GameObject to resolve (typically a collider GO from InteractionManager).</param>
    /// <returns>The clean object ID suitable for MicroAnimationManager calls.</returns>
    private static string GetObjectId(GameObject go)
    {
        if (go == null) return string.Empty;
        string name = go.name;
        const string prefix = "Interactable_";
        if (name.StartsWith(prefix))
            return name.Substring(prefix.Length);
        return name;
    }

    // =========================================================================
    // Testability Wrappers -- Production path delegates to singletons
    // =========================================================================

    private static void CallPlayTriggered(string animId, string objectId, float overrideDuration, Action onComplete)
    {
        if (_playTriggeredStub != null)
        {
            _playTriggeredStub(animId, objectId, overrideDuration, onComplete);
            return;
        }
        MicroAnimationManager.Instance?.PlayTriggered(animId, objectId, overrideDuration, onComplete);
    }

    private static void CallPlayTriggeredNoDuration(string animId, string objectId)
    {
        if (_playTriggeredNoDurationStub != null)
        {
            _playTriggeredNoDurationStub(animId, objectId);
            return;
        }
        MicroAnimationManager.Instance?.PlayTriggered(animId, objectId);
    }

    private static void CallSetGlowLevel(string objectId, GlowLevel level)
    {
        if (_setGlowLevelStub != null)
        {
            _setGlowLevelStub(objectId, level);
            return;
        }
        MicroAnimationManager.Instance?.SetGlowLevel(objectId, level);
    }

    private static void CallStopAllForObject(string objectId)
    {
        if (_stopAllForObjectStub != null)
        {
            _stopAllForObjectStub(objectId);
            return;
        }
        MicroAnimationManager.Instance?.StopAllForObject(objectId);
    }

    private static void CallPlaySfx(string key)
    {
        if (_playSfxStub != null)
        {
            _playSfxStub(key);
            return;
        }
        AudioManager.Instance?.PlaySFX(key);
    }

    /// <summary>
    /// Resets all static stubs to null. Called in test teardown to prevent
    /// cross-test contamination.
    /// </summary>
    internal static void ResetStubs()
    {
        _playTriggeredStub = null;
        _playTriggeredNoDurationStub = null;
        _setGlowLevelStub = null;
        _stopAllForObjectStub = null;
        _playSfxStub = null;
    }

    // =========================================================================
    // ADR-0001 Rule 7: Static Event Cleanup
    // =========================================================================

    private void OnDestroy()
    {
        ResetStubs();
    }
}
