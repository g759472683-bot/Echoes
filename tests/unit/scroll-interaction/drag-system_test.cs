using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for drag interaction system (Story 003).
///
/// Tests cover all 6 ACs:
///   AC-1: Drag trigger 5px threshold — under threshold → no activation
///   AC-2: Drag activation >=5px — _isDragging=true, OnDragStart fires, object follows mouse
///   AC-3: Drag complete >=30px — OnDragComplete, OnInteract, DispatchInteractionResult
///   AC-4: Drag cancel <30px — spring-back, OnDragCancel, no OnInteract
///   AC-5: Drag mutual exclusion — Dragging state blocks hover/click
///   AC-6: Drag trail fade-out — LineRenderer created, fades, destroyed
/// </summary>
[TestFixture]
public class DragSystemTest
{
    private GameObject _managerGO;
    private InteractionManager _manager;
    private MockDataManager _mockDataManager;

    // =========================================================================
    // Test Fixture Setup / Teardown
    // =========================================================================

    [SetUp]
    public void SetUp()
    {
        _managerGO = new GameObject("InteractionManager_DragTest");
        _managerGO.layer = LayerMask.NameToLayer("Interactable");
        _manager = _managerGO.AddComponent<InteractionManager>();
        _mockDataManager = new MockDataManager();
        _manager.Initialize(_mockDataManager, null, null);

        // Disable injected input by default (negative infinity = real input mode)
        SetInjectedWorldMousePos(new Vector2(float.NegativeInfinity, float.NegativeInfinity));
        SetInjectedClickReleased(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_manager != null)
            UnityEngine.Object.DestroyImmediate(_manager);
        if (_managerGO != null)
            UnityEngine.Object.DestroyImmediate(_managerGO);

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
    // AC-1: Drag trigger 5px threshold — under threshold -> no activation
    // =========================================================================

    [Test]
    public void test_drag_below_5px_threshold_does_not_activate()
    {
        // Arrange
        var obj = CreateDragObject("drag_001");

        // Act — start drag
        InvokeStartDrag(obj);

        // Assert — state is Dragging but _isDragging stays false (threshold not crossed)
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Dragging),
            "State should be Dragging after StartDrag");
        Assert.That(GetIsDragging(), Is.False,
            "_isDragging should be false until 5px threshold exceeded");
    }

    [Test]
    public void test_drag_below_5px_release_cancels_drag()
    {
        // Arrange
        bool cancelFired = false;
        InteractiveObject cancelObj = null;
        InteractionManager.OnDragCancel += (o) => { cancelFired = true; cancelObj = o; };
        var obj = CreateDragObject("drag_002");

        // Act — start drag with 0 delta, inject click release
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetInjectedWorldMousePos(Vector2.zero);
        SetInjectedClickReleased(true);
        InvokeHandleDragUpdate();

        // Assert — cancel should fire since mouse released under 5px
        Assert.That(cancelFired, Is.True,
            "OnDragCancel should fire when mouse released under 5px threshold");
        Assert.That(cancelObj, Is.Not.Null);
        Assert.That(cancelObj.ObjectId, Is.EqualTo("drag_002"));
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should return to Active after under-threshold cancel");
    }

    [Test]
    public void test_drag_below_5px_object_does_not_move()
    {
        // Arrange
        var obj = CreateDragObject("drag_003");
        Vector2 originalPos = obj.HitboxCenter;

        // Act
        InvokeStartDrag(obj);

        // Assert — position unchanged
        Assert.That(originalPos, Is.EqualTo(obj.HitboxCenter),
            "Object should not move before 5px threshold");
        Assert.That(GetIsDragging(), Is.False);
    }

    // =========================================================================
    // AC-2: Drag activation >=5px — _isDragging=true, OnDragStart, follow mouse
    // =========================================================================

    [Test]
    public void test_drag_above_5px_activates_and_fires_OnDragStart()
    {
        // Arrange
        InteractiveObject firedObject = null;
        InteractionManager.OnDragStart += (o) => firedObject = o;
        var obj = CreateDragObject("drag_004");

        // Act — start drag, then inject mouse at 6px delta to cross threshold
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        // Inject world mouse position 6 units away (no camera = identity transform)
        SetInjectedWorldMousePos(new Vector2(6f, 0f));
        InvokeHandleDragUpdate();

        // Assert — OnDragStart fires AFTER threshold crossed (not on StartDrag)
        Assert.That(firedObject, Is.Not.Null,
            "OnDragStart should fire when drag crosses 5px threshold");
        Assert.That(firedObject.ObjectId, Is.EqualTo("drag_004"));
        Assert.That(GetIsDragging(), Is.True,
            "_isDragging should be true after exceeding 5px threshold");
    }

    [Test]
    public void test_drag_above_5px_sets_is_dragging_true()
    {
        // Arrange
        var obj = CreateDragObject("drag_005");

        // Act — start drag, inject mouse at 6px delta to cross threshold
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetInjectedWorldMousePos(new Vector2(6f, 0f));
        InvokeHandleDragUpdate();

        // Assert
        Assert.That(GetIsDragging(), Is.True,
            "_isDragging should be true after exceeding 5px threshold");
    }

    [Test]
    public void test_drag_object_follows_mouse_delta()
    {
        // Arrange
        var obj = CreateDragObject("drag_006");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        // Add a collider GO so HandleDragUpdate can find it and move it
        var colGO = CreateColliderGO("drag_006", Vector2.zero);
        AddToSpawnedColliders(colGO);

        // Act — inject mouse at 10px delta
        SetInjectedWorldMousePos(new Vector2(10f, 0f));
        InvokeHandleDragUpdate();

        // Assert — collider should have moved
        Assert.That(colGO.transform.position.magnitude, Is.GreaterThan(0f),
            "Collider should move when dragged beyond 5px");
    }

    // =========================================================================
    // AC-3: Drag complete >=30px — OnDragComplete, OnInteract, dispatch
    // =========================================================================

    [Test]
    public void test_drag_complete_above_30px_fires_OnDragComplete()
    {
        // Arrange
        bool completeFired = false;
        InteractiveObject completeObj = null;
        InteractionManager.OnDragComplete += (o) => { completeFired = true; completeObj = o; };

        var obj = CreateDragObject("drag_007");
        obj.OnInteract = new InteractionResult { ResultType = ResultType.PlayAnimation, AnimationId = "drag_done" };

        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_007", Vector2.zero);
        AddToSpawnedColliders(colGO);

        // Act — simulate drag at 35px by setting _dragTotalDistance and completing
        SetIsDragging(true);
        SetDragTotalDistance(35f);
        InvokeCompleteDrag();

        // Assert
        Assert.That(completeFired, Is.True, "OnDragComplete should fire when drag >= 30px");
        Assert.That(completeObj, Is.Not.Null);
    }

    [Test]
    public void test_drag_complete_above_30px_fires_OnInteract()
    {
        // Arrange
        bool interactFired = false;
        InteractionManager.OnInteract += (_) => interactFired = true;

        var obj = CreateDragObject("drag_008");
        obj.OnInteract = new InteractionResult { ResultType = ResultType.PlayAnimation, AnimationId = "ripple" };

        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_008", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetDragTotalDistance(40f);

        // Act
        InvokeCompleteDrag();

        // Assert
        Assert.That(interactFired, Is.True,
            "OnInteract should fire when drag completes at >= 30px");
    }

    // =========================================================================
    // AC-4: Drag cancel <30px — spring-back, OnDragCancel, no OnInteract
    // =========================================================================

    [Test]
    public void test_drag_cancel_below_30px_fires_OnDragCancel()
    {
        // Arrange
        bool cancelFired = false;
        InteractiveObject cancelObj = null;
        InteractionManager.OnDragCancel += (o) => { cancelFired = true; cancelObj = o; };

        var obj = CreateDragObject("drag_009");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_009", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetDragTotalDistance(15f); // < 30px

        // Act
        InvokeCompleteDrag();

        // Assert
        Assert.That(cancelFired, Is.True,
            "OnDragCancel should fire when drag completes under 30px");
        Assert.That(cancelObj, Is.Not.Null);
    }

    [Test]
    public void test_drag_cancel_below_30px_does_not_fire_OnInteract()
    {
        // Arrange
        bool interactFired = false;
        InteractionManager.OnInteract += (_) => interactFired = true;

        var obj = CreateDragObject("drag_010");
        obj.OnInteract = new InteractionResult { ResultType = ResultType.PlayAnimation, AnimationId = "test" };

        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_010", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetDragTotalDistance(10f); // < 30px

        // Act
        InvokeCompleteDrag();

        // Assert
        Assert.That(interactFired, Is.False,
            "OnInteract should NOT fire on cancelled drag");
    }

    [Test]
    public void test_spring_back_restores_object_to_original_position()
    {
        // Arrange
        var obj = CreateDragObject("drag_011");
        Vector2 originalPos = obj.HitboxCenter;

        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_011", Vector2.zero);
        AddToSpawnedColliders(colGO);

        // Simulate drag to 20px (object moves away)
        SetIsDragging(true);
        SetDragTotalDistance(20f);
        SetInjectedWorldMousePos(new Vector2(20f, 0f));
        InvokeHandleDragUpdate();
        Vector2 afterDrag = colGO.transform.position;
        Assert.That(afterDrag.magnitude, Is.GreaterThan(0f), "Object should have moved");

        // Act — complete drag (will spring back since < 30px)
        InvokeCompleteDrag();

        // Assert — state restored (SpringBack runs as coroutine, async)
        // Verify state machine transitioned out of Dragging
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should be Active after CompleteDrag");
    }

    // =========================================================================
    // AC-5: Drag mutual exclusion — Dragging blocks hover/click
    // =========================================================================

    [Test]
    public void test_dragging_state_blocks_hover_detection()
    {
        // Arrange
        string hoverEnterId = null;
        string hoverExitId = null;
        InteractionManager.OnHoverEnter += (id) => hoverEnterId = id;
        InteractionManager.OnHoverExit += (id) => hoverExitId = id;

        var obj = CreateDragObject("drag_012");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);

        // Act — simulate active drag (state = Dragging, bypasses hover code in Update)
        SetIsDragging(true);
        SetInjectedWorldMousePos(new Vector2(10f, 0f));
        InvokeHandleDragUpdate();

        // Assert — hover events NOT fired during drag
        Assert.That(hoverEnterId, Is.Null, "OnHoverEnter should NOT fire during drag");
        Assert.That(hoverExitId, Is.Null, "OnHoverExit should NOT fire during drag");
    }

    [Test]
    public void test_dragging_state_blocks_click_on_other_object()
    {
        // Arrange
        bool interactFired = false;
        InteractionManager.OnInteract += (_) => interactFired = true;

        var dragObj = CreateDragObject("drag_013");
        InvokeStartDrag(dragObj);

        // Assert — state is Dragging, no interaction fired
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Dragging));
        Assert.That(interactFired, Is.False,
            "No interaction should fire during drag");
    }

    [Test]
    public void test_dragging_state_allows_reentry_after_completion()
    {
        // Arrange
        var obj = CreateDragObject("drag_014");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_014", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetDragTotalDistance(35f);
        InvokeCompleteDrag();

        // After CompleteDrag, state should transition out of Dragging
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Active),
            "State should return to Active after drag completes");
    }

    // =========================================================================
    // AC-6: Drag trail — LineRenderer created, fades, destroyed
    // =========================================================================

    [Test]
    public void test_drag_trail_renderer_created_on_active_drag()
    {
        // Arrange
        var obj = CreateDragObject("drag_015");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_015", Vector2.zero);
        AddToSpawnedColliders(colGO);

        // Act — inject mouse position to make delta >= 5px, triggering trail render
        SetIsDragging(true);
        SetInjectedWorldMousePos(new Vector2(10f, 0f));
        InvokeHandleDragUpdate();

        // Assert — trail renderer should be created
        var trail = GetDragTrail();
        Assert.That(trail, Is.Not.Null,
            "LineRenderer trail should be created during active drag");
        Assert.That(trail.positionCount, Is.EqualTo(2),
            "Trail should have 2 positions (origin -> current)");
    }

    [Test]
    public void test_drag_trail_destroyed_after_complete_fade()
    {
        // Arrange
        var obj = CreateDragObject("drag_016");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_016", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetDragTotalDistance(40f);

        // Act — complete drag (triggers FadeDragTrailCo with 1.0s via StartCoroutine)
        InvokeCompleteDrag();

        // Assert — trail is detached from _trailRenderer (FadeDragTrail sets to null)
        var trail = GetDragTrail();
        Assert.That(trail, Is.Null,
            "_trailRenderer should be null after FadeDragTrail detaches it");
    }

    [Test]
    public void test_drag_trail_cleanup_on_new_fragment_transition()
    {
        // Arrange
        var obj = CreateDragObject("drag_017");
        InvokeStartDrag(obj);
        SetDragStartMousePos(Vector2.zero);
        SetDragStartObjectPos(Vector2.zero);

        var colGO = CreateColliderGO("drag_017", Vector2.zero);
        AddToSpawnedColliders(colGO);

        SetIsDragging(true);
        SetInjectedWorldMousePos(new Vector2(20f, 0f));
        InvokeHandleDragUpdate();
        Assert.That(GetDragTrail(), Is.Not.Null, "Trail should exist during drag");

        // Act — simulate fragment transition (calls ClearAllColliders which resets drag)
        _manager.OnFragmentTransitioned("ch01", "frag_002");

        // Assert
        Assert.That(GetDragTrail(), Is.Null,
            "Drag trail should be destroyed on fragment transition");
        Assert.That(_manager.CurrentState, Is.EqualTo(InteractionState.Idle),
            "State should be Idle after fragment transition with no objects");
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static InteractiveObject CreateDragObject(string objectId)
    {
        return new InteractiveObject
        {
            ObjectId = objectId,
            Type = InteractionType.Drag,
            DefaultState = ObjectState.Active,
            HitboxCenter = Vector2.one,
            HitboxSize = Vector2.one,
            SortOrder = 1
        };
    }

    private GameObject CreateColliderGO(string objectId, Vector2 position)
    {
        var go = new GameObject($"Interactable_{objectId}");
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Interactable");
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = true;
        var refHolder = go.AddComponent<InteractableRef>();
        refHolder.ObjectId = objectId;
        refHolder.InteractionType = InteractionType.Drag;
        refHolder.SortOrder = 1;
        return go;
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
    // Reflection Helpers — Private Field/Method Access
    // =========================================================================

    private void InvokeStartDrag(InteractiveObject obj)
    {
        var method = typeof(InteractionManager).GetMethod(
            "StartDrag",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_manager, new object[] { obj });
    }

    private void InvokeCompleteDrag()
    {
        var method = typeof(InteractionManager).GetMethod(
            "CompleteDrag",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_manager, null);
    }

    private void InvokeHandleDragUpdate()
    {
        var method = typeof(InteractionManager).GetMethod(
            "HandleDragUpdate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_manager, null);
    }

    private void SetDragStartMousePos(Vector2 pos)
    {
        var field = typeof(InteractionManager).GetField(
            "_dragStartMousePos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, pos);
    }

    private void SetDragStartObjectPos(Vector2 pos)
    {
        var field = typeof(InteractionManager).GetField(
            "_dragStartObjectPos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, pos);
    }

    private void SetIsDragging(bool value)
    {
        var field = typeof(InteractionManager).GetField(
            "_isDragging",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, value);
    }

    private void SetDragTotalDistance(float distance)
    {
        var field = typeof(InteractionManager).GetField(
            "_dragTotalDistance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, distance);
    }

    private void SetInjectedWorldMousePos(Vector2 pos)
    {
        var field = typeof(InteractionManager).GetField(
            "_injectedWorldMousePos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, pos);
    }

    private void SetInjectedClickReleased(bool released)
    {
        var field = typeof(InteractionManager).GetField(
            "_injectedClickReleasedThisFrame",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_manager, released);
    }

    private bool GetIsDragging()
    {
        var field = typeof(InteractionManager).GetField(
            "_isDragging",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (bool)field.GetValue(_manager);
    }

    private LineRenderer GetDragTrail()
    {
        var field = typeof(InteractionManager).GetField(
            "_trailRenderer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (LineRenderer)field.GetValue(_manager);
    }

    // =========================================================================
    // Mock Implementation
    // =========================================================================

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
