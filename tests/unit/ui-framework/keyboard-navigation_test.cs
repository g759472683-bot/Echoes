using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for FocusNavigationCore and GamepadHintsManager (ui-framework S004).
///
/// Covers all 6 acceptance criteria:
///   AC-1: Panel open auto-focuses first focusable element
///   AC-2: Arrow key navigation moves focus
///   AC-3: PopPanel restores last focus position
///   AC-4: Enter/Confirm triggers button action
///   AC-5: Gamepad hints visibility — keyboard always, gamepad conditional
///   AC-6: Tab/Shift+Tab focus group switching
/// </summary>
public class KeyboardNavigationTest
{
    // =========================================================================
    // Mock Dependencies
    // =========================================================================

    /// <summary>
    /// Mock IFocusProvider that simulates a panel with linearly ordered
    /// focusable elements. Navigation moves through the list index.
    /// </summary>
    private class MockFocusProvider : IFocusProvider
    {
        private readonly Dictionary<string, List<string>> _elements = new();
        private readonly Dictionary<string, string> _currentFocus = new();
        private readonly HashSet<string> _unfocusablePanels = new();

        // Tracking for assertions
        public string LastNavigatedElement { get; private set; }
        public NavigationDirection LastDirection { get; private set; }
        public string LastActivatedElement { get; private set; }
        public bool WasActivateCalled { get; private set; }
        public int FocusNextCallCount { get; private set; }
        public int FocusPreviousCallCount { get; private set; }
        public int NavigateDirectionCallCount { get; private set; }
        public int FocusFirstCallCount { get; private set; }
        public int CaptureCurrentFocusCallCount { get; private set; }
        public int FocusElementCallCount { get; private set; }

        /// <summary>Register a panel with ordered focusable element IDs.</summary>
        public void RegisterPanel(string panelId, string[] elementIds)
        {
            _elements[panelId] = new List<string>(elementIds);
            _currentFocus[panelId] = elementIds.Length > 0 ? elementIds[0] : null;
        }

        /// <summary>Mark a panel as having no focusable elements (even if registered).</summary>
        public void SetUnfocusable(string panelId)
        {
            _unfocusablePanels.Add(panelId);
        }

        /// <summary>Manually set the current focus for a panel (for setup).</summary>
        public void SetCurrentFocus(string panelId, string elementId)
        {
            _currentFocus[panelId] = elementId;
        }

        /// <summary>Get all registered element IDs for a panel.</summary>
        public List<string> GetElementIds(string panelId)
        {
            _elements.TryGetValue(panelId, out var ids);
            return ids;
        }

        public void ResetTracking()
        {
            LastNavigatedElement = null;
            LastActivatedElement = null;
            WasActivateCalled = false;
            FocusNextCallCount = 0;
            FocusPreviousCallCount = 0;
            NavigateDirectionCallCount = 0;
            FocusFirstCallCount = 0;
            CaptureCurrentFocusCallCount = 0;
            FocusElementCallCount = 0;
        }

        // =====================================================================
        // IFocusProvider Implementation
        // =====================================================================

        public string FocusFirst(string panelId)
        {
            FocusFirstCallCount++;

            if (_unfocusablePanels.Contains(panelId))
                return null;

            if (_elements.TryGetValue(panelId, out var ids) && ids.Count > 0)
            {
                _currentFocus[panelId] = ids[0];
                return ids[0];
            }
            return null;
        }

        public string CaptureCurrentFocus(string panelId)
        {
            CaptureCurrentFocusCallCount++;
            _currentFocus.TryGetValue(panelId, out var id);
            return id;
        }

        public void FocusElement(string panelId, string elementId)
        {
            FocusElementCallCount++;

            if (_elements.TryGetValue(panelId, out var ids) && ids.Contains(elementId))
            {
                _currentFocus[panelId] = elementId;
            }
            // If element not found, silently no-op (element may have been removed)
        }

        public string NavigateDirection(string panelId, NavigationDirection direction)
        {
            NavigateDirectionCallCount++;
            LastDirection = direction;

            if (!_elements.TryGetValue(panelId, out var ids))
                return null;
            if (!_currentFocus.TryGetValue(panelId, out var current) || current == null)
                return null;

            int index = ids.IndexOf(current);
            if (index < 0) return null;

            int newIndex;
            if (direction == NavigationDirection.Up || direction == NavigationDirection.Left)
                newIndex = index - 1;
            else
                newIndex = index + 1;

            // Clamp to bounds — no wrapping for directional navigation
            if (newIndex < 0 || newIndex >= ids.Count)
                return null;

            _currentFocus[panelId] = ids[newIndex];
            LastNavigatedElement = ids[newIndex];
            return ids[newIndex];
        }

        public string FocusNext(string panelId)
        {
            FocusNextCallCount++;

            if (!_elements.TryGetValue(panelId, out var ids))
                return null;
            if (!_currentFocus.TryGetValue(panelId, out var current) || current == null)
                return null;

            int index = ids.IndexOf(current);
            if (index < 0) return null;

            // Tab wraps around to first element
            int newIndex = (index + 1) % ids.Count;
            _currentFocus[panelId] = ids[newIndex];
            LastNavigatedElement = ids[newIndex];
            return ids[newIndex];
        }

        public string FocusPrevious(string panelId)
        {
            FocusPreviousCallCount++;

            if (!_elements.TryGetValue(panelId, out var ids))
                return null;
            if (!_currentFocus.TryGetValue(panelId, out var current) || current == null)
                return null;

            int index = ids.IndexOf(current);
            if (index < 0) return null;

            // Shift+Tab wraps around to last element
            int newIndex = index == 0 ? ids.Count - 1 : index - 1;
            _currentFocus[panelId] = ids[newIndex];
            LastNavigatedElement = ids[newIndex];
            return ids[newIndex];
        }

        public bool ActivateFocused(string panelId)
        {
            WasActivateCalled = true;

            if (_currentFocus.TryGetValue(panelId, out var id) && id != null)
            {
                LastActivatedElement = id;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Mock IGamepadStateProvider with controllable connection state.
    /// </summary>
    private class MockGamepadStateProvider : IGamepadStateProvider
    {
        public bool IsGamepadConnected { get; set; }
    }

    // =========================================================================
    // Setup / Teardown
    // =========================================================================

    private MockFocusProvider _focusProvider;
    private FocusNavigationCore _navigation;
    private MockGamepadStateProvider _gamepadState;
    private GamepadHintsManager _hintsManager;

    [SetUp]
    public void SetUp()
    {
        _focusProvider = new MockFocusProvider();
        _navigation = new FocusNavigationCore(_focusProvider);
        _gamepadState = new MockGamepadStateProvider();
        _hintsManager = new GamepadHintsManager(_gamepadState);
    }

    [TearDown]
    public void TearDown()
    {
        FocusNavigationCore.ResetStaticEvents();
        GamepadHintsManager.ResetStaticEvents();
    }

    // =========================================================================
    // AC-1: Panel open auto-focuses first focusable element
    // =========================================================================

    [Test]
    public void test_auto_focus_first_element_on_panel_open()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "slider-volume", "btn-quit" });
        string focusedElement = null;
        FocusNavigationCore.OnElementFocused += (panelId, elementId) =>
            focusedElement = elementId;

        // Act
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(focusedElement, Is.EqualTo("btn-continue"),
            "First focusable element should auto-receive focus");
        Assert.That(_focusProvider.FocusFirstCallCount, Is.EqualTo(1));
    }

    [Test]
    public void test_auto_focus_fires_event_with_correct_panel_id()
    {
        // Arrange
        _focusProvider.RegisterPanel("settings", new[] { "slider-master" });
        string eventPanelId = null;
        FocusNavigationCore.OnElementFocused += (panelId, _) => eventPanelId = panelId;

        // Act
        _navigation.HandlePanelOpened("settings");

        // Assert
        Assert.That(eventPanelId, Is.EqualTo("settings"));
    }

    [Test]
    public void test_auto_focus_fires_auto_focused_event()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu", new[] { "btn-continue" });
        string autoFocusedPanel = null;
        FocusNavigationCore.OnAutoFocused += (panelId) => autoFocusedPanel = panelId;

        // Act
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(autoFocusedPanel, Is.EqualTo("pause-menu"),
            "OnAutoFocused should fire on first open (no saved focus)");
    }

    [Test]
    public void test_panel_with_no_focusable_elements_does_not_focus()
    {
        // Arrange
        _focusProvider.RegisterPanel("empty-panel", new string[0]);
        string focusedElement = null;
        FocusNavigationCore.OnElementFocused += (_, elementId) => focusedElement = elementId;

        // Act
        _navigation.HandlePanelOpened("empty-panel");

        // Assert
        Assert.That(focusedElement, Is.Null,
            "No element should be focused when panel has no focusable elements");
    }

    [Test]
    public void test_panel_with_no_focusable_elements_still_allows_cancel()
    {
        // Edge case from QA AC-1: Cancel still works even with no focusable elements
        // Arrange
        bool cancelFired = false;
        FocusNavigationCore.OnCancelled += () => cancelFired = true;

        // Act
        _navigation.Cancel();

        // Assert
        Assert.That(cancelFired, Is.True,
            "Cancel (Escape) should still work even with no focusable elements");
    }

    [Test]
    public void test_null_panel_id_on_open_logs_error()
    {
        // Arrange
        string errorMsg = null;
        FocusNavigationCore.OnError += (msg) => errorMsg = msg;

        // Act
        _navigation.HandlePanelOpened(null);

        // Assert
        Assert.That(errorMsg, Is.Not.Null.And.Contains("null"),
            "Null panelId should log an error");
    }

    [Test]
    public void test_empty_panel_id_on_open_logs_error()
    {
        // Arrange
        string errorMsg = null;
        FocusNavigationCore.OnError += (msg) => errorMsg = msg;

        // Act
        _navigation.HandlePanelOpened("");

        // Assert
        Assert.That(errorMsg, Is.Not.Null.And.Contains("empty"),
            "Empty panelId should log an error");
    }

    // =========================================================================
    // AC-2: Arrow key navigation moves focus
    // =========================================================================

    [Test]
    public void test_arrow_down_moves_focus_to_next_element()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "slider-volume", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu"); // focuses btn-continue
        _focusProvider.ResetTracking();
        string focusedElement = null;
        FocusNavigationCore.OnElementFocused += (_, elementId) => focusedElement = elementId;

        // Act
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);

        // Assert
        Assert.That(focusedElement, Is.EqualTo("btn-settings"),
            "Arrow Down should move focus to the next element");
        Assert.That(_focusProvider.LastDirection, Is.EqualTo(NavigationDirection.Down));
    }

    [Test]
    public void test_arrow_up_moves_focus_to_previous_element()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "slider-volume" });
        _navigation.HandlePanelOpened("pause-menu"); // focuses btn-continue
        // Navigate down first to reach second element
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);
        _focusProvider.ResetTracking();
        string focusedElement = null;
        FocusNavigationCore.OnElementFocused += (_, elementId) => focusedElement = elementId;

        // Act
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Up);

        // Assert
        Assert.That(focusedElement, Is.EqualTo("btn-continue"),
            "Arrow Up should move focus back to the previous element");
    }

    [Test]
    public void test_arrow_right_moves_focus_forward()
    {
        // Arrange
        _focusProvider.RegisterPanel("settings", new[] { "a", "b", "c" });
        _navigation.HandlePanelOpened("settings");
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.NavigateDirection("settings", NavigationDirection.Right);

        // Assert
        Assert.That(focused, Is.EqualTo("b"));
        Assert.That(_focusProvider.LastDirection, Is.EqualTo(NavigationDirection.Right));
    }

    [Test]
    public void test_arrow_left_moves_focus_backward()
    {
        // Arrange
        _focusProvider.RegisterPanel("settings", new[] { "a", "b", "c" });
        _navigation.HandlePanelOpened("settings");
        _navigation.NavigateDirection("settings", NavigationDirection.Right); // now on b
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.NavigateDirection("settings", NavigationDirection.Left);

        // Assert
        Assert.That(focused, Is.EqualTo("a"));
    }

    [Test]
    public void test_arrow_down_at_last_element_does_not_move()
    {
        // Arrange — at last element, Down should not move (Unity decides, mock clamps)
        _focusProvider.RegisterPanel("settings", new[] { "a", "b" });
        _navigation.HandlePanelOpened("settings");
        _navigation.NavigateDirection("settings", NavigationDirection.Down); // a→b
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.NavigateDirection("settings", NavigationDirection.Down);

        // Assert
        Assert.That(focused, Is.Null,
            "Arrow Down at last element should not fire focus event (clamped by mock)");
    }

    [Test]
    public void test_arrow_up_at_first_element_does_not_move()
    {
        // Arrange
        _focusProvider.RegisterPanel("settings", new[] { "a", "b" });
        _navigation.HandlePanelOpened("settings"); // focuses a
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.NavigateDirection("settings", NavigationDirection.Up);

        // Assert
        Assert.That(focused, Is.Null,
            "Arrow Up at first element should not fire focus event");
    }

    // =========================================================================
    // AC-3: PopPanel restores last focus position
    // =========================================================================

    [Test]
    public void test_pop_panel_restores_last_focus()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });
        _focusProvider.RegisterPanel("settings",
            new[] { "slider-master", "slider-music", "btn-back" });

        // Open pause menu, navigate to "btn-settings"
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);
        Assert.That(_focusProvider.CaptureCurrentFocus("pause-menu"),
            Is.EqualTo("btn-settings"), "Setup: focus should be on btn-settings");

        // Close pause menu (push settings on top)
        _navigation.HandlePanelClosing("pause-menu");

        // Later: pop settings, re-open pause menu
        _navigation.HandlePanelOpened("pause-menu");
        string focusedElement = null;
        FocusNavigationCore.OnElementFocused += (_, elementId) => focusedElement = elementId;
        _focusProvider.ResetTracking();

        // Re-open again — should restore
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(focusedElement, Is.EqualTo("btn-settings"),
            "Re-opening panel should restore last focused element");
    }

    [Test]
    public void test_focus_restore_fires_restored_event()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings" });
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.HandlePanelClosing("pause-menu");

        string restoredPanel = null;
        FocusNavigationCore.OnFocusRestored += (panelId) => restoredPanel = panelId;

        // Act
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(restoredPanel, Is.EqualTo("pause-menu"),
            "OnFocusRestored should fire when saved focus is restored");
    }

    [Test]
    public void test_focus_restore_does_not_fire_for_first_open()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings" });
        string restoredPanel = null;
        FocusNavigationCore.OnFocusRestored += (panelId) => restoredPanel = panelId;

        // Act — first open, no saved focus
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(restoredPanel, Is.Null,
            "OnFocusRestored should NOT fire on first open (auto-focus instead)");
    }

    [Test]
    public void test_close_panel_without_any_focus_clears_stale_state()
    {
        // Edge case from QA AC-3: No element ever focused → no focus state saved
        // Arrange
        _focusProvider.SetUnfocusable("empty-panel");
        _navigation.HandlePanelOpened("empty-panel");

        // Act
        _navigation.HandlePanelClosing("empty-panel");

        // Assert
        Assert.That(_navigation.GetLastFocused("empty-panel"), Is.Null,
            "Panel with no focus should not save any focus state");
    }

    [Test]
    public void test_focus_restore_after_pop_and_repush()
    {
        // Scenario: pause-menu opened, settings pushed, settings popped, pause-menu re-shown
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });

        // Initial open, navigate to btn-settings
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);

        // Settings pushed on top — close pause first
        _navigation.HandlePanelClosing("pause-menu");

        // Settings popped — re-open pause
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;
        _navigation.HandlePanelOpened("pause-menu");

        // Assert
        Assert.That(focused, Is.EqualTo("btn-settings"),
            "After PopPanel, focus should restore to last position");
    }

    // =========================================================================
    // AC-4: Enter/Confirm triggers button action
    // =========================================================================

    [Test]
    public void test_confirm_triggers_element_activation()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings" });
        _navigation.HandlePanelOpened("pause-menu");
        string confirmedPanel = null;
        FocusNavigationCore.OnConfirmed += (panelId) => confirmedPanel = panelId;

        // Act
        _navigation.Confirm("pause-menu");

        // Assert
        Assert.That(_focusProvider.WasActivateCalled, Is.True,
            "Confirm should call ActivateFocused on the provider");
        Assert.That(confirmedPanel, Is.EqualTo("pause-menu"),
            "OnConfirmed should fire with the panel ID");
    }

    [Test]
    public void test_confirm_on_empty_panel_does_not_fire_confirmed()
    {
        // Arrange
        _focusProvider.RegisterPanel("empty", new string[0]);
        // No element focused
        _focusProvider.SetCurrentFocus("empty", null);
        string confirmed = null;
        FocusNavigationCore.OnConfirmed += (_) => confirmed = "fired";

        // Act
        _navigation.Confirm("empty");

        // Assert
        Assert.That(confirmed, Is.Null,
            "Confirm on panel with no focused element should not fire OnConfirmed");
    }

    [Test]
    public void test_confirm_activates_button_element()
    {
        // Arrange — "btn-continue" is the focused button
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu"); // focuses btn-continue

        // Act
        _navigation.Confirm("pause-menu");

        // Assert
        Assert.That(_focusProvider.LastActivatedElement, Is.EqualTo("btn-continue"),
            "Confirm should activate the currently focused element");
    }

    // =========================================================================
    // AC-5: Gamepad hints visibility
    // =========================================================================

    [Test]
    public void test_keyboard_hints_always_shown()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = false;

        // Assert
        Assert.That(_hintsManager.ShowKeyboardHints, Is.True,
            "Keyboard hints should always be shown in menus");
    }

    [Test]
    public void test_gamepad_hints_hidden_when_no_gamepad()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = false;

        // Assert
        Assert.That(_hintsManager.ShowGamepadHints, Is.False,
            "Gamepad hints should be hidden when no gamepad is connected");
    }

    [Test]
    public void test_gamepad_hints_shown_when_gamepad_connected()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = true;

        // Assert
        Assert.That(_hintsManager.ShowGamepadHints, Is.True,
            "Gamepad hints should be shown when gamepad is connected");
    }

    [Test]
    public void test_refresh_hints_fires_connection_changed_event()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = true;
        bool? eventValue = null;
        GamepadHintsManager.OnGamepadConnectionChanged += (connected) => eventValue = connected;

        // Act
        _hintsManager.RefreshHints();

        // Assert
        Assert.That(eventValue, Is.True,
            "RefreshHints should fire OnGamepadConnectionChanged with current state");
    }

    [Test]
    public void test_refresh_hints_fires_visibility_changed_show()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = true;
        string visibility = null;
        GamepadHintsManager.OnHintsVisibilityChanged += (v) => visibility = v;

        // Act
        _hintsManager.RefreshHints();

        // Assert
        Assert.That(visibility, Is.EqualTo("Show"),
            "Gamepad connected → hints visibility should be 'Show'");
    }

    [Test]
    public void test_refresh_hints_fires_visibility_changed_hide()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = false;
        string visibility = null;
        GamepadHintsManager.OnHintsVisibilityChanged += (v) => visibility = v;

        // Act
        _hintsManager.RefreshHints();

        // Assert
        Assert.That(visibility, Is.EqualTo("Hide"),
            "Gamepad disconnected → hints visibility should be 'Hide'");
    }

    [Test]
    public void test_handle_gamepad_connection_changed_calls_refresh()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = false;
        bool? eventValue = null;
        GamepadHintsManager.OnGamepadConnectionChanged += (connected) => eventValue = connected;

        // Act
        _hintsManager.HandleGamepadConnectionChanged();

        // Assert
        Assert.That(eventValue, Is.False,
            "HandleGamepadConnectionChanged should refresh and fire events");
    }

    [Test]
    public void test_gamepad_connected_property_reads_provider()
    {
        // Arrange
        _gamepadState.IsGamepadConnected = false;
        Assert.That(_hintsManager.IsGamepadConnected, Is.False);

        // Act
        _gamepadState.IsGamepadConnected = true;

        // Assert
        Assert.That(_hintsManager.IsGamepadConnected, Is.True,
            "IsGamepadConnected should reflect the provider's current state");
    }

    // =========================================================================
    // AC-6: Tab/Shift+Tab focus switching
    // =========================================================================

    [Test]
    public void test_tab_moves_focus_to_next_group()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "slider-volume", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu"); // focuses btn-continue
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.FocusNextGroup("pause-menu");

        // Assert
        Assert.That(focused, Is.EqualTo("btn-settings"),
            "Tab should move focus to the next focusable group");
        Assert.That(_focusProvider.FocusNextCallCount, Is.EqualTo(1));
    }

    [Test]
    public void test_shift_tab_moves_focus_to_previous_group()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.FocusNextGroup("pause-menu"); // btn-continue → btn-settings
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.FocusPreviousGroup("pause-menu");

        // Assert
        Assert.That(focused, Is.EqualTo("btn-continue"),
            "Shift+Tab should move focus to the previous group");
        Assert.That(_focusProvider.FocusPreviousCallCount, Is.EqualTo(1));
    }

    [Test]
    public void test_tab_at_last_element_wraps_to_first()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.FocusNextGroup("pause-menu"); // → settings
        _navigation.FocusNextGroup("pause-menu"); // → quit
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.FocusNextGroup("pause-menu");

        // Assert
        Assert.That(focused, Is.EqualTo("btn-continue"),
            "Tab at last element should wrap to first");
    }

    [Test]
    public void test_shift_tab_at_first_element_wraps_to_last()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });
        _navigation.HandlePanelOpened("pause-menu"); // on btn-continue
        _focusProvider.ResetTracking();
        string focused = null;
        FocusNavigationCore.OnElementFocused += (_, id) => focused = id;

        // Act
        _navigation.FocusPreviousGroup("pause-menu");

        // Assert
        Assert.That(focused, Is.EqualTo("btn-quit"),
            "Shift+Tab at first element should wrap to last");
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Test]
    public void test_navigate_on_null_panel_logs_error()
    {
        // Arrange
        string errorMsg = null;
        FocusNavigationCore.OnError += (msg) => errorMsg = msg;

        // Act
        _navigation.NavigateDirection(null, NavigationDirection.Down);

        // Assert
        Assert.That(errorMsg, Is.Not.Null.And.Contains("null"));
    }

    [Test]
    public void test_tab_next_on_null_panel_logs_error()
    {
        string errorMsg = null;
        FocusNavigationCore.OnError += (msg) => errorMsg = msg;
        _navigation.FocusNextGroup(null);
        Assert.That(errorMsg, Is.Not.Null.And.Contains("null"));
    }

    [Test]
    public void test_confirm_on_null_panel_logs_error()
    {
        string errorMsg = null;
        FocusNavigationCore.OnError += (msg) => errorMsg = msg;
        _navigation.Confirm(null);
        Assert.That(errorMsg, Is.Not.Null.And.Contains("null"));
    }

    [Test]
    public void test_cancel_fires_event()
    {
        // Arrange
        bool cancelFired = false;
        FocusNavigationCore.OnCancelled += () => cancelFired = true;

        // Act
        _navigation.Cancel();

        // Assert
        Assert.That(cancelFired, Is.True,
            "Cancel should fire OnCancelled event");
    }

    [Test]
    public void test_clear_all_focus_state_removes_saved_positions()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu", new[] { "btn-continue", "btn-settings" });
        _focusProvider.RegisterPanel("settings", new[] { "slider-master" });

        _navigation.HandlePanelOpened("pause-menu");
        _navigation.HandlePanelClosing("pause-menu");
        _navigation.HandlePanelOpened("settings");
        _navigation.HandlePanelClosing("settings");

        Assert.That(_navigation.SavedFocusCount, Is.EqualTo(2), "Setup: should have 2 saved foci");

        // Act
        _navigation.ClearAllFocusState();

        // Assert
        Assert.That(_navigation.SavedFocusCount, Is.EqualTo(0));
        Assert.That(_navigation.GetLastFocused("pause-menu"), Is.Null);
        Assert.That(_navigation.GetLastFocused("settings"), Is.Null);
    }

    [Test]
    public void test_clear_single_focus_state()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu", new[] { "btn-continue" });
        _focusProvider.RegisterPanel("settings", new[] { "slider-master" });

        _navigation.HandlePanelOpened("pause-menu");
        _navigation.HandlePanelClosing("pause-menu");
        _navigation.HandlePanelOpened("settings");
        _navigation.HandlePanelClosing("settings");

        // Act
        _navigation.ClearFocusState("pause-menu");

        // Assert
        Assert.That(_navigation.GetLastFocused("pause-menu"), Is.Null);
        Assert.That(_navigation.GetLastFocused("settings"), Is.EqualTo("slider-master"),
            "Other panel focus state should be unaffected");
    }

    [Test]
    public void test_close_panel_overwrites_previous_saved_focus()
    {
        // Arrange
        _focusProvider.RegisterPanel("pause-menu",
            new[] { "btn-continue", "btn-settings", "btn-quit" });

        // First visit: focus on btn-continue, close
        _navigation.HandlePanelOpened("pause-menu");
        Assert.That(_focusProvider.CaptureCurrentFocus("pause-menu"),
            Is.EqualTo("btn-continue"));
        _navigation.HandlePanelClosing("pause-menu");

        // Second visit: navigate to btn-quit, close
        _navigation.HandlePanelOpened("pause-menu");
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);
        _navigation.NavigateDirection("pause-menu", NavigationDirection.Down);
        Assert.That(_focusProvider.CaptureCurrentFocus("pause-menu"),
            Is.EqualTo("btn-quit"));
        _navigation.HandlePanelClosing("pause-menu");

        // Assert — should have overwritten with latest close
        Assert.That(_navigation.GetLastFocused("pause-menu"), Is.EqualTo("btn-quit"),
            "Latest close should overwrite previous saved focus");
    }

    [Test]
    public void test_constructor_rejects_null_focus_provider()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FocusNavigationCore(null));
    }

    [Test]
    public void test_constructor_rejects_null_gamepad_state()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GamepadHintsManager(null));
    }

    [Test]
    public void test_navigate_fires_element_focused_event()
    {
        // Arrange
        _focusProvider.RegisterPanel("menu", new[] { "a", "b", "c" });
        _navigation.HandlePanelOpened("menu");
        _focusProvider.ResetTracking();
        string eventPanel = null;
        string eventElement = null;
        FocusNavigationCore.OnElementFocused += (panelId, elementId) =>
        {
            eventPanel = panelId;
            eventElement = elementId;
        };

        // Act
        _navigation.NavigateDirection("menu", NavigationDirection.Down);

        // Assert
        Assert.That(eventPanel, Is.EqualTo("menu"));
        Assert.That(eventElement, Is.EqualTo("b"));
    }
}
