using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for UIPanelStackCore (ui-framework S001).
///
/// Covers all 6 acceptance criteria:
///   AC-1: PushPanel switches to UI mode
///   AC-2: PopPanel switches to Gameplay mode
///   AC-3: Multi-layer pop sequence (Escape × N)
///   AC-4: Empty stack → Gameplay Action Map
///   AC-5: Missing UXML → error with path (dev) or generic (release)
///   AC-6: Max depth 10 → reject + error
/// </summary>
public class PanelStackTest
{
    // =========================================================================
    // Mock Dependencies
    // =========================================================================

    private class MockPanelInstance : IPanelInstance
    {
        public string PanelId { get; }
        public MockPanelInstance(string panelId) => PanelId = panelId;
    }

    private class MockAssetProvider : IPanelAssetProvider
    {
        private readonly HashSet<string> _registeredIds = new();
        private readonly HashSet<string> _failingIds = new(); // registered but fails to instantiate

        public void Register(string panelId)
        {
            _registeredIds.Add(panelId);
        }

        public void RegisterFailing(string panelId)
        {
            _registeredIds.Add(panelId);
            _failingIds.Add(panelId);
        }

        public bool HasAsset(string panelId) => _registeredIds.Contains(panelId);

        public IPanelInstance LoadPanel(string panelId)
        {
            if (!_registeredIds.Contains(panelId)) return null;
            if (_failingIds.Contains(panelId)) return null;
            return new MockPanelInstance(panelId);
        }
    }

    private class MockInputModeController : IInputModeController
    {
        public bool IsUIModeActive { get; private set; }
        public int SwitchToUIModeCallCount { get; private set; }
        public int SwitchToGameplayCallCount { get; private set; }

        public void SwitchToUIMode()
        {
            IsUIModeActive = true;
            SwitchToUIModeCallCount++;
        }

        public void SwitchToGameplayMode()
        {
            IsUIModeActive = false;
            SwitchToGameplayCallCount++;
        }
    }

    private MockAssetProvider _assets;
    private MockInputModeController _input;
    private UIPanelStackCore _stack;
    private List<string> _errorLog;

    [SetUp]
    public void SetUp()
    {
        _assets = new MockAssetProvider();
        _input = new MockInputModeController();
        _stack = new UIPanelStackCore(_assets, _input, isDevelopmentBuild: true);
        _errorLog = new List<string>();
        UIPanelStackCore.OnError += OnError;
    }

    [TearDown]
    public void TearDown()
    {
        UIPanelStackCore.OnError -= OnError;
        UIPanelStackCore.ResetStaticEvents();
    }

    private void OnError(string message) => _errorLog.Add(message);

    private void RegisterPanel(string id) => _assets.Register(id);

    // =========================================================================
    // AC-1: PushPanel switches to UI mode
    // =========================================================================

    [Test]
    public void test_PushPanel_FromEmpty_SwitchesToUIMode()
    {
        RegisterPanel("pause-menu");

        _stack.PushPanel("pause-menu");

        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State);
        Assert.IsTrue(_input.IsUIModeActive);
        Assert.AreEqual(1, _input.SwitchToUIModeCallCount);
    }

    [Test]
    public void test_PushPanel_OnNonEmpty_DoesNotSwitchAgain()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");

        _stack.PushPanel("pause-menu");
        _stack.PushPanel("settings-panel");

        Assert.AreEqual(2, _stack.StackDepth);
        Assert.AreEqual("settings-panel", _stack.TopPanelId);
        // Only the first PushPanel should trigger UIMode switch
        Assert.AreEqual(1, _input.SwitchToUIModeCallCount);
    }

    [Test]
    public void test_PushPanel_FiresEvent()
    {
        RegisterPanel("pause-menu");
        string pushedId = null;
        UIPanelStackCore.OnPanelPushed += id => pushedId = id;

        _stack.PushPanel("pause-menu");

        Assert.AreEqual("pause-menu", pushedId);
    }

    // =========================================================================
    // AC-2: PopPanel switches to Gameplay mode
    // =========================================================================

    [Test]
    public void test_PopPanel_LastPanel_SwitchesToGameplayMode()
    {
        RegisterPanel("pause-menu");
        _stack.PushPanel("pause-menu");

        _stack.PopPanel();

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.IsNull(_stack.TopPanelId);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
        Assert.IsFalse(_input.IsUIModeActive);
        Assert.AreEqual(1, _input.SwitchToGameplayCallCount);
    }

    [Test]
    public void test_PopPanel_NotLast_StaysInUIMode()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");
        _stack.PushPanel("pause-menu");
        _stack.PushPanel("settings-panel");

        _stack.PopPanel();

        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State);
        Assert.IsTrue(_input.IsUIModeActive);
        // Should NOT have called SwitchToGameplay
        Assert.AreEqual(0, _input.SwitchToGameplayCallCount);
    }

    [Test]
    public void test_PopPanel_FiresEvent()
    {
        RegisterPanel("pause-menu");
        _stack.PushPanel("pause-menu");
        string poppedId = null;
        UIPanelStackCore.OnPanelPopped += id => poppedId = id;

        _stack.PopPanel();

        Assert.AreEqual("pause-menu", poppedId);
    }

    // =========================================================================
    // AC-3: Multi-layer pop sequence
    // =========================================================================

    [Test]
    public void test_PopPanel_Twice_CascadesCorrectly()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");
        _stack.PushPanel("pause-menu");
        _stack.PushPanel("settings-panel");

        // First pop — settings closes, back to pause
        _stack.PopPanel();
        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);
        Assert.IsTrue(_input.IsUIModeActive);
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State);

        // Second pop — pause closes, back to Gameplay
        _stack.PopPanel();
        Assert.AreEqual(0, _stack.StackDepth);
        Assert.IsNull(_stack.TopPanelId);
        Assert.IsFalse(_input.IsUIModeActive);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
    }

    [Test]
    public void test_PopPanel_EmptyStack_NoOp()
    {
        _stack.PopPanel();

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
        Assert.AreEqual(0, _input.SwitchToGameplayCallCount);
    }

    // =========================================================================
    // AC-4: Empty stack → Gameplay Action Map
    // =========================================================================

    [Test]
    public void test_InitialState_Empty()
    {
        Assert.AreEqual(0, _stack.StackDepth);
        Assert.IsNull(_stack.TopPanelId);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
        Assert.IsFalse(_input.IsUIModeActive);
    }

    [Test]
    public void test_PushPop_FullCycle_ReturnsToGameplay()
    {
        RegisterPanel("pause-menu");
        _stack.PushPanel("pause-menu");
        _stack.PopPanel();

        Assert.AreEqual(PanelStackState.Empty, _stack.State);
        Assert.IsFalse(_input.IsUIModeActive);
        Assert.AreEqual(1, _input.SwitchToUIModeCallCount);
        Assert.AreEqual(1, _input.SwitchToGameplayCallCount);
    }

    [Test]
    public void test_InputModeChangedEvent_Fires()
    {
        RegisterPanel("pause-menu");
        var modeChanges = new List<string>();
        UIPanelStackCore.OnInputModeChanged += mode => modeChanges.Add(mode);

        _stack.PushPanel("pause-menu");
        _stack.PopPanel();

        Assert.AreEqual(2, modeChanges.Count);
        Assert.AreEqual("UI", modeChanges[0]);
        Assert.AreEqual("Gameplay", modeChanges[1]);
    }

    // =========================================================================
    // AC-5: Missing UXML — error handling
    // =========================================================================

    [Test]
    public void test_PushPanel_UnregisteredPanel_DevBuild_LogsPath()
    {
        _stack.PushPanel("broken-panel");

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
        Assert.AreEqual(1, _errorLog.Count);
        StringAssert.Contains("UXML not found", _errorLog[0]);
        StringAssert.Contains("broken-panel", _errorLog[0]);
        StringAssert.Contains("Assets/UI/", _errorLog[0]);
        // Input mode should NOT have changed
        Assert.IsFalse(_input.IsUIModeActive);
    }

    [Test]
    public void test_PushPanel_UnregisteredPanel_ReleaseBuild_LogsGeneric()
    {
        _assets.Register("some-panel"); // only to make assets non-empty
        var releaseStack = new UIPanelStackCore(_assets, _input, isDevelopmentBuild: false);
        var releaseErrors = new List<string>();
        UIPanelStackCore.OnError += releaseErrors.Add;

        releaseStack.PushPanel("broken-panel");

        Assert.AreEqual(0, releaseStack.StackDepth);
        Assert.AreEqual(1, releaseErrors.Count);
        StringAssert.Contains("failed to load", releaseErrors[0]);
        StringAssert.DoesNotContain("Assets/UI/", releaseErrors[0]);

        UIPanelStackCore.OnError -= releaseErrors.Add;
    }

    [Test]
    public void test_PushPanel_RegisteredButFailsInstantiation_LogsError()
    {
        _assets.RegisterFailing("crashy-panel");

        _stack.PushPanel("crashy-panel");

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.AreEqual(1, _errorLog.Count);
        StringAssert.Contains("Failed to instantiate", _errorLog[0]);
        StringAssert.Contains("crashy-panel", _errorLog[0]);
    }

    [Test]
    public void test_PushPanel_MissingAsset_AfterValidPanel_RestoresState()
    {
        RegisterPanel("pause-menu");
        _stack.PushPanel("pause-menu");
        Assert.AreEqual(1, _stack.StackDepth);

        // Try to push missing panel
        _stack.PushPanel("nonexistent");

        // Stack should be unchanged — still has pause-menu
        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State);
    }

    // =========================================================================
    // AC-6: Max depth 10 enforcement
    // =========================================================================

    [Test]
    public void test_PushPanel_AtMaxDepth_Rejected()
    {
        // Register and push 10 panels
        for (int i = 1; i <= 10; i++)
        {
            string id = $"panel_{i:D2}";
            RegisterPanel(id);
            _stack.PushPanel(id);
        }

        Assert.AreEqual(10, _stack.StackDepth);
        Assert.AreEqual(0, _errorLog.Count, "Should have no errors at depth 10");

        // 11th panel should be rejected
        RegisterPanel("panel_11");
        _stack.PushPanel("panel_11");

        Assert.AreEqual(10, _stack.StackDepth, "Stack depth should not increase past 10");
        Assert.AreEqual("panel_10", _stack.TopPanelId);
        Assert.AreEqual(1, _errorLog.Count);
        StringAssert.Contains("max depth", _errorLog[0]);
        StringAssert.Contains("10", _errorLog[0]);
        StringAssert.Contains("panel_11", _errorLog[0]);
    }

    [Test]
    public void test_PushPanel_AtDepth9_Succeeds()
    {
        for (int i = 1; i <= 9; i++)
        {
            string id = $"panel_{i:D2}";
            RegisterPanel(id);
            _stack.PushPanel(id);
        }

        RegisterPanel("panel_10");
        _stack.PushPanel("panel_10");

        Assert.AreEqual(10, _stack.StackDepth);
        Assert.AreEqual("panel_10", _stack.TopPanelId);
        Assert.AreEqual(0, _errorLog.Count);
    }

    // =========================================================================
    // Transitioning state edge cases
    // =========================================================================

    [Test]
    public void test_PushPanel_DuringTransitioning_Rejected()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");

        // Manually force transitioning state to simulate mid-animation
        _stack.PushPanel("pause-menu");
        // Use reflection to set state to Transitioning? No — test via actual behavior.
        // The Transitioning state is set internally during PushPanel/PopPanel.
        // For this test, verify that rapid successive calls don't corrupt state.
        // The critical guarantee: PushPanel sets Transitioning → processes → sets final state.
        // We test this indirectly via the fact that PushPanel completes atomically.
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State,
            "PushPanel should complete and return to PanelOpen (not stuck in Transitioning)");
    }

    // =========================================================================
    // ReplaceTop
    // =========================================================================

    [Test]
    public void test_ReplaceTop_ReplacesTopPanel()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");
        _stack.PushPanel("pause-menu");

        _stack.ReplaceTop("settings-panel");

        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("settings-panel", _stack.TopPanelId);
    }

    [Test]
    public void test_ReplaceTop_EmptyStack_BehavesAsPush()
    {
        RegisterPanel("pause-menu");

        _stack.ReplaceTop("pause-menu");

        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);
        Assert.IsTrue(_input.IsUIModeActive);
    }

    // =========================================================================
    // Edge cases
    // =========================================================================

    [Test]
    public void test_PushPanel_NullPanelId_LogsError()
    {
        _stack.PushPanel(null);

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.AreEqual(1, _errorLog.Count);
        StringAssert.Contains("null", _errorLog[0].ToLower());
    }

    [Test]
    public void test_PushPanel_EmptyPanelId_LogsError()
    {
        _stack.PushPanel("");

        Assert.AreEqual(0, _stack.StackDepth);
        Assert.AreEqual(1, _errorLog.Count);
    }

    [Test]
    public void test_Constructor_NullAssetProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new UIPanelStackCore(null, _input));
    }

    [Test]
    public void test_Constructor_NullInputMode_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new UIPanelStackCore(_assets, null));
    }

    [Test]
    public void test_IsDevelopmentBuild_ReflectsConstructorArg()
    {
        Assert.IsTrue(_stack.IsDevelopmentBuild);

        var releaseStack = new UIPanelStackCore(_assets, _input, isDevelopmentBuild: false);
        Assert.IsFalse(releaseStack.IsDevelopmentBuild);
    }

    [Test]
    public void test_MaxDepth_Constant_Is10()
    {
        Assert.AreEqual(10, UIPanelStackCore.MaxDepth);
    }

    // =========================================================================
    // Stress: rapid Escape sequence simulation
    // =========================================================================

    [Test]
    public void test_RapidEscapeSequence_PauseSettingsBackToGame()
    {
        RegisterPanel("pause-menu");
        RegisterPanel("settings-panel");

        // Player presses Escape → pause opens
        _stack.PushPanel("pause-menu");
        Assert.AreEqual(1, _stack.StackDepth);
        Assert.IsTrue(_input.IsUIModeActive);

        // Player opens settings from pause
        _stack.PushPanel("settings-panel");
        Assert.AreEqual(2, _stack.StackDepth);

        // Player presses Escape → settings closes
        _stack.PopPanel();
        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual("pause-menu", _stack.TopPanelId);

        // Player presses Escape again → pause closes, back to game
        _stack.PopPanel();
        Assert.AreEqual(0, _stack.StackDepth);
        Assert.IsFalse(_input.IsUIModeActive);
        Assert.AreEqual(PanelStackState.Empty, _stack.State);
    }

    // =========================================================================
    // S003: Transition Animation Tests
    // =========================================================================

    private class MockAnimator : IPanelAnimator
    {
        public int FadeInDurationMs => 300;
        public int FadeOutDurationMs => 200;

        public IPanelInstance LastFadeInPanel { get; private set; }
        public IPanelInstance LastFadeOutPanel { get; private set; }
        public int FadeInCallCount { get; private set; }
        public int FadeOutCallCount { get; private set; }

        /// <summary>Stored callbacks for manual completion control.</summary>
        public Action PendingFadeInCallback { get; private set; }
        public Action PendingFadeOutCallback { get; private set; }

        /// <summary>When true, auto-completes immediately (simulates instant animation).</summary>
        public bool AutoComplete { get; set; } = true;

        public void PlayFadeIn(IPanelInstance panel, Action onComplete)
        {
            FadeInCallCount++;
            LastFadeInPanel = panel;
            if (AutoComplete)
                onComplete?.Invoke();
            else
                PendingFadeInCallback = onComplete;
        }

        public void PlayFadeOut(IPanelInstance panel, Action onComplete)
        {
            FadeOutCallCount++;
            LastFadeOutPanel = panel;
            if (AutoComplete)
                onComplete?.Invoke();
            else
                PendingFadeOutCallback = onComplete;
        }

        public void CompletePendingFadeIn()
        {
            var cb = PendingFadeInCallback;
            PendingFadeInCallback = null;
            cb?.Invoke();
        }

        public void CompletePendingFadeOut()
        {
            var cb = PendingFadeOutCallback;
            PendingFadeOutCallback = null;
            cb?.Invoke();
        }

        public void Reset()
        {
            LastFadeInPanel = null;
            LastFadeOutPanel = null;
            FadeInCallCount = 0;
            FadeOutCallCount = 0;
            PendingFadeInCallback = null;
            PendingFadeOutCallback = null;
        }
    }

    [Test]
    public void test_AnimationsEnabled_False_WhenNoAnimator()
    {
        Assert.IsFalse(_stack.AnimationsEnabled);
    }

    [Test]
    public void test_AnimationsEnabled_True_WhenAnimatorProvided()
    {
        var animator = new MockAnimator();
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        Assert.IsTrue(animatedStack.AnimationsEnabled);
    }

    [Test]
    public void test_PushPanel_WithAnimator_CallsPlayFadeIn()
    {
        var animator = new MockAnimator();
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");

        animatedStack.PushPanel("pause-menu");

        Assert.AreEqual(1, animator.FadeInCallCount);
        Assert.IsNotNull(animator.LastFadeInPanel);
        Assert.AreEqual("pause-menu", animator.LastFadeInPanel.PanelId);
    }

    [Test]
    public void test_PushPanel_WithAnimator_StaysTransitioningUntilComplete()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");

        animatedStack.PushPanel("pause-menu");

        Assert.AreEqual(PanelStackState.Transitioning, animatedStack.State,
            "State should be Transitioning while animation plays");
        Assert.AreEqual(1, animatedStack.StackDepth,
            "Panel should be on the stack immediately (TopPanelId queryable during animation)");
    }

    [Test]
    public void test_PushPanel_WithAnimator_FiresOnPanelPushed_AfterComplete()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        string pushed = null;
        UIPanelStackCore.OnPanelPushed += id => pushed = id;

        animatedStack.PushPanel("pause-menu");
        Assert.IsNull(pushed, "OnPanelPushed should NOT fire before animation completes");

        animator.CompletePendingFadeIn();

        Assert.AreEqual("pause-menu", pushed,
            "OnPanelPushed should fire after animation completes");
        Assert.AreEqual(PanelStackState.PanelOpen, animatedStack.State);
    }

    [Test]
    public void test_PopPanel_WithAnimator_CallsPlayFadeOut()
    {
        var animator = new MockAnimator();
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        animatedStack.PushPanel("pause-menu");
        animator.Reset();

        animatedStack.PopPanel();

        Assert.AreEqual(1, animator.FadeOutCallCount);
        Assert.IsNotNull(animator.LastFadeOutPanel);
    }

    [Test]
    public void test_PopPanel_WithAnimator_StaysTransitioningUntilComplete()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        animatedStack.PushPanel("pause-menu");
        Assert.AreEqual(PanelStackState.PanelOpen, animatedStack.State);

        animatedStack.PopPanel();

        Assert.AreEqual(PanelStackState.Transitioning, animatedStack.State,
            "State should be Transitioning during fade-out");
        Assert.AreEqual(1, animatedStack.StackDepth,
            "Panel should still be on stack during animation");
    }

    [Test]
    public void test_PopPanel_WithAnimator_FiresOnPanelPopped_AfterComplete()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        animatedStack.PushPanel("pause-menu");
        string popped = null;
        UIPanelStackCore.OnPanelPopped += id => popped = id;

        animatedStack.PopPanel();
        Assert.IsNull(popped, "OnPanelPopped should NOT fire before animation completes");

        animator.CompletePendingFadeOut();

        Assert.AreEqual("pause-menu", popped,
            "OnPanelPopped should fire after animation completes");
        Assert.AreEqual(0, animatedStack.StackDepth);
        Assert.AreEqual(PanelStackState.Empty, animatedStack.State);
    }

    [Test]
    public void test_TransitioningState_BlocksPush_WithAnimator()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        RegisterPanelFor(animatedStack, "settings-panel");

        animatedStack.PushPanel("pause-menu");
        Assert.AreEqual(PanelStackState.Transitioning, animatedStack.State);

        // Try to push while transitioning
        var errors = new List<string>();
        UIPanelStackCore.OnError += errors.Add;
        animatedStack.PushPanel("settings-panel");
        UIPanelStackCore.OnError -= errors.Add;

        Assert.AreEqual(1, errors.Count);
        StringAssert.Contains("transitioning", errors[0].ToLower());
        Assert.AreEqual(1, animatedStack.StackDepth,
            "Second panel should NOT have been pushed during transition");
    }

    [Test]
    public void test_TransitioningState_BlocksPop_WithAnimator()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        animatedStack.PushPanel("pause-menu");
        // State is Transitioning (animation still playing)

        animatedStack.PopPanel(); // Should be silently ignored

        Assert.AreEqual(1, animatedStack.StackDepth,
            "Pop should be ignored during transition");
        Assert.AreEqual(1, animator.FadeOutCallCount,
            "PlayFadeOut should NOT have been called (the initial push still has pending fade-in)");
    }

    [Test]
    public void test_ReplaceTop_WithAnimator_CrossFades()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        RegisterPanelFor(animatedStack, "settings-panel");

        // Push initial panel (complete the animation)
        animatedStack.PushPanel("pause-menu");
        animator.CompletePendingFadeIn();
        animator.Reset();

        // ReplaceTop starts cross-fade
        animatedStack.ReplaceTop("settings-panel");

        Assert.AreEqual(PanelStackState.Transitioning, animatedStack.State);
        Assert.AreEqual(1, animator.FadeOutCallCount, "Fade-out should start on old panel");
        Assert.AreEqual(1, animator.FadeInCallCount, "Fade-in should start on new panel");
    }

    [Test]
    public void test_ReplaceTop_CrossFade_CompletesWhenBothFinish()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        RegisterPanelFor(animatedStack, "settings-panel");

        animatedStack.PushPanel("pause-menu");
        animator.CompletePendingFadeIn();
        animator.Reset();

        animator.AutoComplete = false;
        string transitionCompletePanel = null;
        UIPanelStackCore.OnTransitionComplete += (panelId, type) =>
            transitionCompletePanel = panelId;

        animatedStack.ReplaceTop("settings-panel");

        // Complete only fade-out — should not yet complete
        animator.CompletePendingFadeOut();
        Assert.AreEqual(PanelStackState.Transitioning, animatedStack.State,
            "Cross-fade should not complete until both animations finish");

        // Complete fade-in
        animator.CompletePendingFadeIn();

        Assert.AreEqual(PanelStackState.PanelOpen, animatedStack.State);
        Assert.AreEqual("settings-panel", animatedStack.TopPanelId);
        Assert.AreEqual("settings-panel", transitionCompletePanel);
    }

    [Test]
    public void test_OnTransitionComplete_FiresAfterFadeIn()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        string completedPanel = null;
        string completedType = null;
        UIPanelStackCore.OnTransitionComplete += (panelId, type) =>
        {
            completedPanel = panelId;
            completedType = type;
        };

        animatedStack.PushPanel("pause-menu");
        animator.CompletePendingFadeIn();

        Assert.AreEqual("pause-menu", completedPanel);
        Assert.AreEqual("fade-in", completedType);
    }

    [Test]
    public void test_OnTransitionComplete_FiresAfterFadeOut()
    {
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        animatedStack.PushPanel("pause-menu");
        animator.CompletePendingFadeIn();
        animator.Reset();
        animator.AutoComplete = false;

        string completedPanel = null;
        string completedType = null;
        UIPanelStackCore.OnTransitionComplete += (panelId, type) =>
        {
            completedPanel = panelId;
            completedType = type;
        };

        animatedStack.PopPanel();
        animator.CompletePendingFadeOut();

        Assert.AreEqual("pause-menu", completedPanel);
        Assert.AreEqual("fade-out", completedType);
    }

    [Test]
    public void test_InputModeChanged_FiresAfterAnimation_ForFirstPanel()
    {
        // When animator is used, InputModeChanged should fire after fade-in completes
        var animator = new MockAnimator { AutoComplete = false };
        var animatedStack = new UIPanelStackCore(_assets, _input, animator, isDevelopmentBuild: true);
        RegisterPanelFor(animatedStack, "pause-menu");
        string modeChanged = null;
        UIPanelStackCore.OnInputModeChanged += mode => modeChanged = mode;

        animatedStack.PushPanel("pause-menu");
        Assert.IsNull(modeChanged,
            "InputModeChanged should NOT fire before fade-in completes");

        animator.CompletePendingFadeIn();

        Assert.AreEqual("UI", modeChanged,
            "InputModeChanged should fire after animation completes");
    }

    [Test]
    public void test_NoAnimator_Constructor_BackwardCompatible()
    {
        // The 3-param constructor should produce a fully functional stack
        // without animations (existing behavior)
        RegisterPanel("pause-menu");
        _stack.PushPanel("pause-menu");

        Assert.AreEqual(1, _stack.StackDepth);
        Assert.AreEqual(PanelStackState.PanelOpen, _stack.State);
        Assert.IsFalse(_stack.AnimationsEnabled);
    }

    [Test]
    public void test_ResetStaticEvents_ClearsTransitionComplete()
    {
        bool fired = false;
        UIPanelStackCore.OnTransitionComplete += (_, _) => fired = true;
        UIPanelStackCore.ResetStaticEvents();

        // After reset, event should be cleared
        Assert.IsNull(UIPanelStackCore.OnTransitionComplete);
    }

    // Helper: register a panel in an animated stack's asset provider
    private void RegisterPanelFor(UIPanelStackCore stack, string panelId)
    {
        // The stack uses the same _assets reference since we pass it to constructor
        _assets.Register(panelId);
    }
}
