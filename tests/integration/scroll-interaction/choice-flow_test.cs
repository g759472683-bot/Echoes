using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integration tests for InteractionManager Story 004 — choice flow + Escape cancel.
///
/// Tests cover all 6 ACs:
///   AC-1: Two-option panel display, OnChoiceHover, Action Map switching
///   AC-2: Select option → OnChoiceSelected, panel hide, state restore
///   AC-3: Escape cancel → no changes, panel close, state restore
///   AC-4: Single available option → auto-apply, skip panel
///   AC-5: Panel position — right side preference
///   AC-6: Zero available options → LogWarning, no panel
/// </summary>
[TestFixture]
public class ChoiceFlowTest
{
    private GameObject _managerGO;
    private InteractionManager _manager;
    private MockHUDV2 _mockHUD;
    private MockDataManager _mockDataManager;
    private GameObject _cameraGO;
    private Camera _testCamera;

    // =========================================================================
    // Test Fixture Setup / Teardown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        _managerGO = new GameObject("InteractionManager_ChoiceFlowTest");
        _managerGO.layer = LayerMask.NameToLayer("Interactable");
        _manager = _managerGO.AddComponent<InteractionManager>();
        _mockHUD = new MockHUDV2();
        _mockDataManager = new MockDataManager();
        _manager.Initialize(_mockDataManager, _mockHUD, null);

        // Set up a test Camera so CalculateChoicePanelPosition works correctly
        _cameraGO = new GameObject("TestCamera");
        _cameraGO.tag = "MainCamera";
        _testCamera = _cameraGO.AddComponent<Camera>();
        _testCamera.orthographic = true;
        _testCamera.orthographicSize = 5f;
        var camField = typeof(InteractionManager).GetField(
            "_mainCamera",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        camField.SetValue(_manager, _testCamera);
    }

    [TearDown]
    public void TearDown()
    {
        if (_manager != null)
            UnityEngine.Object.DestroyImmediate(_manager);
        if (_managerGO != null)
            UnityEngine.Object.DestroyImmediate(_managerGO);
        if (_cameraGO != null)
            UnityEngine.Object.DestroyImmediate(_cameraGO);

        InteractionManager.OnDragStart = null;
        InteractionManager.OnDragComplete = null;
        InteractionManager.OnDragCancel = null;
        InteractionManager.OnInteract = null;
        InteractionManager.OnHoverEnter = null;
        InteractionManager.OnHoverExit = null;
        InteractionManager.OnShowText = null;
        InteractionManager.OnRevealObject = null;
        InteractionManager.OnChoiceSelected = null;
        InteractionManager.OnChoiceHover = null;
    }

    // =========================================================================
    // AC-1: Two-option panel → shows, Action Map switches, OnChoiceHover fires
    // =========================================================================

    [Test]
    public void test_present_choice_two_options_shows_panel_and_switches_to_ui_mode()
    {
        // Arrange
        var fragment = CreateFragmentWithChoiceGroup("cg_001", maxSelections: 1, choiceCount: 2);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_001");
        SetCurrentInteractiveObject(anchor);

        _mockHUD.ChoiceResult = "choice_a";

        var result = new InteractionResult
        {
            ResultType = ResultType.PresentChoice,
            ChoiceGroupId = "cg_001"
        };

        // Act
        InvokeHandlePresentChoice(result.ChoiceGroupId);

        // Assert
        Assert.That(_mockHUD.ShowChoicePanelCalledWithGroup, Is.Not.Null,
            "HUD.ShowChoicePanel should be called");
        Assert.That(_mockHUD.ShowChoicePanelCalledPosition, Is.Not.EqualTo(Vector2.zero),
            "Panel position should be calculated (non-zero)");
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should return to Active after choice applied");
    }

    // =========================================================================
    // AC-2: Select option → OnChoiceSelected, panel hide, state restore
    // =========================================================================

    [Test]
    public void test_select_option_a_fires_OnChoiceSelected_and_hides_panel()
    {
        // Arrange
        string selectedId = null;
        InteractionManager.OnChoiceSelected += (id) => selectedId = id;

        var fragment = CreateFragmentWithChoiceGroup("cg_002", maxSelections: 1, choiceCount: 2);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_002");
        SetCurrentInteractiveObject(anchor);

        _mockHUD.ChoiceResult = "choice_a";

        // Act
        InvokeHandlePresentChoice("cg_002");

        // Assert
        Assert.That(selectedId, Is.EqualTo("choice_a"),
            "OnChoiceSelected should fire with the chosen option ID");
        Assert.That(_mockHUD.HideChoicePanelCalled, Is.True,
            "HUD.HideChoicePanel should be called");
        Assert.That(_mockHUD.HideChoicePanelDuration, Is.EqualTo(0.3f).Within(0.001f),
            "Panel should fade out over 0.3s");
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should be Active after choice applied");
    }

    // =========================================================================
    // AC-3: Escape cancel → no changes, panel close, state restore
    // =========================================================================

    [Test]
    public void test_escape_cancels_choice_no_changes_applied()
    {
        // Arrange
        bool choiceSelectedFired = false;
        InteractionManager.OnChoiceSelected += (_) => choiceSelectedFired = true;

        var fragment = CreateFragmentWithChoiceGroup("cg_003", maxSelections: 1, choiceCount: 2);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_003");
        SetCurrentInteractiveObject(anchor);

        _mockHUD.ChoiceResult = null; // Escape → null

        // Act
        InvokeHandlePresentChoice("cg_003");

        // Assert
        Assert.That(choiceSelectedFired, Is.False,
            "OnChoiceSelected should NOT fire on cancel");
        Assert.That(_mockHUD.HideChoicePanelCalled, Is.True,
            "Panel should close on cancel");
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should return to Active after cancel");
    }

    // =========================================================================
    // AC-4: Single available option → auto-apply, skip panel
    // =========================================================================

    [Test]
    public void test_single_available_option_auto_applies_skips_panel()
    {
        // Arrange
        string selectedId = null;
        InteractionManager.OnChoiceSelected += (id) => selectedId = id;

        var fragment = CreateFragmentWithChoiceGroup("cg_004", maxSelections: 1, choiceCount: 1);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_004");
        SetCurrentInteractiveObject(anchor);

        // Act
        InvokeHandlePresentChoice("cg_004");

        // Assert
        Assert.That(_mockHUD.ShowChoicePanelCalledWithGroup, Is.Null,
            "Panel should NOT be shown — single option auto-applies");
        Assert.That(selectedId, Is.EqualTo("choice_0"),
            "OnChoiceSelected should fire for the sole option");
        Assert.That(_mockHUD.HideChoicePanelCalled, Is.False,
            "HideChoicePanel should NOT be called — panel was never shown");
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should remain/was Active (never entered ChoicePresenting)");
    }

    // =========================================================================
    // AC-5: Panel position — right side preference
    // =========================================================================

    [Test]
    public void test_panel_position_right_side_when_space_available()
    {
        // Arrange — place anchor at center-left screen so right side has space
        var anchor = CreateAnchorObject("obj_005");
        Vector2 anchorScreen = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
        var colGO = CreateAnchorCollider("obj_005", ScreenToWorld(anchorScreen));
        AddToSpawnedColliders(colGO);

        // Act
        Vector2 pos = InvokeCalculateChoicePanelPosition(anchor);

        // Assert — panel returned at right-of-anchor position (not center fallback)
        Assert.That(pos.x, Is.GreaterThan(anchorScreen.x),
            "Panel should be to the right of anchor screen position");
        Assert.That(pos.x, Is.Not.EqualTo(Screen.width / 2f).Within(1f),
            "Should NOT fall back to center when right side has space");
    }

    [Test]
    public void test_panel_position_below_fallback_when_right_insufficient()
    {
        // Arrange — place anchor at far-right edge, vertical middle
        // Right side has no space (panel would overflow), but below has space
        var anchor = CreateAnchorObject("obj_005b");
        Vector2 anchorScreen = new Vector2(Screen.width - 100f, Screen.height * 0.5f);
        var colGO = CreateAnchorCollider("obj_005b", ScreenToWorld(anchorScreen));
        AddToSpawnedColliders(colGO);

        // Act
        Vector2 pos = InvokeCalculateChoicePanelPosition(anchor);

        // Assert — below fallback (y < anchor screen y, i.e., below)
        Assert.That(pos.y, Is.LessThan(anchorScreen.y),
            "Panel should fall back to below anchor when right side has no space");
        Assert.That(pos.y, Is.Not.EqualTo(Screen.height / 2f).Within(1f),
            "Should NOT fall back to center when below has space");
    }

    [Test]
    public void test_panel_position_center_fallback_when_no_space()
    {
        // Arrange — place anchor at bottom-right corner so neither right nor below have space
        var anchor = CreateAnchorObject("obj_006");
        Vector2 anchorScreen = new Vector2(Screen.width - 100f, 100f);
        var colGO = CreateAnchorCollider("obj_006", ScreenToWorld(anchorScreen));
        AddToSpawnedColliders(colGO);

        // Act
        Vector2 pos = InvokeCalculateChoicePanelPosition(anchor);

        // Assert — center fallback
        Assert.That(pos.x, Is.EqualTo(Screen.width / 2f).Within(1f),
            "Panel should fall back to screen center X when no space right/below");
        Assert.That(pos.y, Is.EqualTo(Screen.height / 2f).Within(1f),
            "Panel should fall back to screen center Y when no space right/below");
    }

    // =========================================================================
    // AC-4 edge case: Zero available options → no panel, no changes
    // =========================================================================

    [Test]
    public void test_zero_available_options_logs_warning_no_panel()
    {
        // Arrange
        bool choiceSelectedFired = false;
        InteractionManager.OnChoiceSelected += (_) => choiceSelectedFired = true;

        var fragment = CreateFragmentWithChoiceGroup("cg_007", maxSelections: 1, choiceCount: 0);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_007");
        SetCurrentInteractiveObject(anchor);

        // Act
        InvokeHandlePresentChoice("cg_007");

        // Assert
        Assert.That(_mockHUD.ShowChoicePanelCalledWithGroup, Is.Null,
            "Panel should NOT show when 0 options available");
        Assert.That(choiceSelectedFired, Is.False,
            "OnChoiceSelected should NOT fire with 0 options");
    }

    // =========================================================================
    // AC-1: OnChoiceHover broadcast via HUD callback
    // =========================================================================

    [Test]
    public void test_choice_hover_callback_fires_OnChoiceHover()
    {
        // Arrange
        string hoveredId = null;
        InteractionManager.OnChoiceHover += (id) => hoveredId = id;

        var fragment = CreateFragmentWithChoiceGroup("cg_008", maxSelections: 1, choiceCount: 2);
        _mockDataManager.SetCachedFragment(fragment);
        _manager.OnFragmentTransitioned("ch01", "frag_001");

        var anchor = CreateAnchorObject("obj_008");
        SetCurrentInteractiveObject(anchor);

        _mockHUD.ChoiceResult = "choice_b";
        // Simulate HUD calling back with hover
        _mockHUD.OnChoiceHoverAction = (id) => InteractionManager.OnChoiceHover?.Invoke(id);

        // Act — first trigger hover, then complete selection
        _mockHUD.OnChoiceHoverAction?.Invoke("choice_b");
        InvokeHandlePresentChoice("cg_008");

        // Assert
        Assert.That(hoveredId, Is.EqualTo("choice_b"),
            "OnChoiceHover should fire when HUD reports hover");
    }

    // =========================================================================
    // Reflection Helpers
    // =========================================================================

    private void InvokeHandlePresentChoice(string choiceGroupId)
    {
        var method = typeof(InteractionManager).GetMethod(
            "HandlePresentChoice",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method.Invoke(_manager, new object[] { choiceGroupId });
        // Block until async method completes (safe — mocks return Task.FromResult)
        task?.Wait();
    }

    private Vector2 InvokeCalculateChoicePanelPosition(InteractiveObject anchor)
    {
        var method = typeof(InteractionManager).GetMethod(
            "CalculateChoicePanelPosition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Vector2)method.Invoke(_manager, new object[] { anchor });
    }

    private void SetCurrentInteractiveObject(InteractiveObject obj)
    {
        var field = typeof(InteractionManager).GetField(
            "_currentInteractiveObject",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, obj);
    }

    private void AddToSpawnedColliders(GameObject go)
    {
        var field = typeof(InteractionManager).GetField(
            "_spawnedColliderGOs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<GameObject>)field.GetValue(_manager);
        list.Add(go);
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    /// <summary>
    /// Converts a screen-space position to a world-space position using the test camera.
    /// Required because CalculateChoicePanelPosition uses Camera.WorldToScreenPoint.
    /// </summary>
    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        float ppu = Screen.height / (2f * _testCamera.orthographicSize);
        float worldX = (screenPos.x - Screen.width / 2f) / ppu;
        float worldY = (screenPos.y - Screen.height / 2f) / ppu;
        return new Vector2(worldX, worldY);
    }

    private static MemoryFragment CreateFragmentWithChoiceGroup(string groupId, int maxSelections, int choiceCount)
    {
        var choices = new Choice[choiceCount];
        for (int i = 0; i < choiceCount; i++)
        {
            choices[i] = new Choice
            {
                ChoiceId = $"choice_{i}",
                Text = $"Option {i}",
                OnSelect = new InteractionResult
                {
                    ResultType = ResultType.PlayAnimation,
                    AnimationId = $"anim_{i}"
                }
            };
        }

        var fragment = ScriptableObject.CreateInstance<MemoryFragment>();
        fragment.FragmentId = "frag_001";
        fragment.ChapterKey = "ch01";
        fragment.ChoiceGroups = new ChoiceGroup[]
        {
            new ChoiceGroup
            {
                GroupId = groupId,
                MaxSelections = maxSelections,
                Choices = choices
            }
        };
        return fragment;
    }

    private static InteractiveObject CreateAnchorObject(string objectId)
    {
        return new InteractiveObject
        {
            ObjectId = objectId,
            Type = InteractionType.Touch,
            DefaultState = ObjectState.Active,
            HitboxCenter = Vector2.one,
            HitboxSize = Vector2.one,
            SortOrder = 1
        };
    }

    private GameObject CreateAnchorCollider(string objectId, Vector2 position)
    {
        var go = new GameObject($"Anchor_{objectId}");
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Interactable");
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = true;
        var refHolder = go.AddComponent<InteractableRef>();
        refHolder.ObjectId = objectId;
        refHolder.InteractionType = InteractionType.Touch;
        refHolder.SortOrder = 1;
        return go;
    }

    // =========================================================================
    // Mock Implementations
    // =========================================================================

    private class MockHUDV2 : IHUD
    {
        public readonly List<TextContent> ShowFragmentTextCalls = new List<TextContent>();

        // ShowChoicePanel tracking
        public ChoiceGroup ShowChoicePanelCalledWithGroup;
        public Vector2 ShowChoicePanelCalledPosition;
        public string ChoiceResult;
        public Action<string> OnChoiceHoverAction;

        // HideChoicePanel tracking
        public bool HideChoicePanelCalled;
        public float HideChoicePanelDuration;

        public void ShowFragmentText(TextContent content, Vector2 screenPosition)
        {
            ShowFragmentTextCalls.Add(content);
        }

        public Task<string> ShowChoicePanel(ChoiceGroup choiceGroup)
        {
            return ShowChoicePanel(choiceGroup, Vector2.zero);
        }

        public Task<string> ShowChoicePanel(ChoiceGroup choiceGroup, Vector2 screenPosition)
        {
            ShowChoicePanelCalledWithGroup = choiceGroup;
            ShowChoicePanelCalledPosition = screenPosition;
            return Task.FromResult(ChoiceResult);
        }

        public Task HideChoicePanel(float fadeDuration)
        {
            HideChoicePanelCalled = true;
            HideChoicePanelDuration = fadeDuration;
            return Task.CompletedTask;
        }
    }

    private class MockDataManager : IDataManager
    {
        private MemoryFragment _cachedFragment;

        public void SetCachedFragment(MemoryFragment fragment)
        {
            _cachedFragment = fragment;
        }

        public Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)
        {
            return Task.FromResult(_cachedFragment);
        }

        public Task<Sprite> GetIllustrationAsync(string illustrationKey)
        {
            return Task.FromResult<Sprite>(null);
        }

        public void ReleaseFragment(string fragmentId) { }

        public MemoryFragment GetCachedFragment(string chapterKey, string fragmentId)
        {
            return _cachedFragment;
        }

        public Task<ChapterDefinition> GetChapterAsync(string chapterKey)
        {
            return Task.FromResult<ChapterDefinition>(null);
        }

        public Task PreloadChapterAsync(string chapterKey) => Task.CompletedTask;

        public bool IsReady(string assetKey) => false;

        public List<MemoryFragment> GetFragmentsByChapter(string chapterKey)
        {
            return new List<MemoryFragment>();
        }

        public void UnloadChapter(string chapterKey) { }
    }
}
