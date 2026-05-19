using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for HoverDetectorCore — covers S002 acceptance criteria.
///
/// Uses mock implementations of IMousePositionProvider, ICameraProvider,
/// and IPhysics2DProvider to verify hover detection logic without Unity runtime.
/// </summary>
public class HoverDetectorCoreTest
{
    // =========================================================================
    // Mock Implementations
    // =========================================================================

    private class MockMouseProvider : IMousePositionProvider
    {
        public Vector2 Position;
        public Vector2 GetMousePosition() => Position;
    }

    private class MockCameraProvider : ICameraProvider
    {
        public float Scale = 1f;
        public Vector2 ScreenToWorldPoint(Vector2 screenPoint) => screenPoint * Scale;
    }

    private class MockPhysicsProvider : IPhysics2DProvider
    {
        public string[] Result;
        public string[] OverlapPoint(Vector2 worldPoint) => Result ?? new string[0];
    }

    // =========================================================================
    // Test Fixture State
    // =========================================================================

    private MockMouseProvider _mouse;
    private MockCameraProvider _camera;
    private MockPhysicsProvider _physics;
    private HoverDetectorCore _detector;

    [SetUp]
    public void SetUp()
    {
        _mouse = new MockMouseProvider();
        _camera = new MockCameraProvider();
        _physics = new MockPhysicsProvider();
        _detector = new HoverDetectorCore(_mouse, _camera, _physics);
    }

    [TearDown]
    public void TearDown()
    {
        HoverDetectorCore.ResetStaticEvents();
    }

    // =========================================================================
    // AC-1: Hover Enter Detection
    // =========================================================================

    [Test]
    public void test_UpdateHover_mouseEntersObject_firesOnHoverEnter()
    {
        // Given: mouse over empty space, an object with Collider2D on Interactable layer ahead
        _mouse.Position = new Vector2(100f, 200f);
        _physics.Result = new string[0];

        _detector.UpdateHover();
        Assert.IsNull(_detector.CurrentHoveredId);

        // When: mouse moves onto the object
        _physics.Result = new[] { "ink_brush_01" };
        string enteredId = null;
        Vector2 enteredPos = default;
        HoverDetectorCore.OnHoverEnter += (id, pos) => { enteredId = id; enteredPos = pos; };

        _detector.UpdateHover();

        // Then: OnHoverEnter fires with object ID and screen coordinates
        Assert.AreEqual("ink_brush_01", enteredId);
        Assert.AreEqual(new Vector2(100f, 200f), enteredPos);
        Assert.AreEqual("ink_brush_01", _detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_noCollider_noOnHoverEnter()
    {
        // Given: mouse over empty space, object has no Collider2D
        _physics.Result = new string[0];
        bool eventFired = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => eventFired = true;

        // When: UpdateHover runs
        _detector.UpdateHover();

        // Then: no event fires
        Assert.IsFalse(eventFired);
        Assert.IsNull(_detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_wrongLayer_noOnHoverEnter()
    {
        // Given: object exists but not on Interactable layer
        // (Physics provider only returns Interactable layer objects)
        _physics.Result = new string[0];
        bool eventFired = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => eventFired = true;

        _detector.UpdateHover();

        // Then: no event — wrong layer objects are filtered by physics
        Assert.IsFalse(eventFired);
    }

    // =========================================================================
    // AC-2: Hover Exit Detection
    // =========================================================================

    [Test]
    public void test_UpdateHover_mouseLeavesObject_firesOnHoverExit()
    {
        // Given: mouse hovering over object A
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();
        Assert.AreEqual("object_a", _detector.CurrentHoveredId);

        // When: mouse moves to empty space
        _physics.Result = new string[0];
        string exitedId = null;
        HoverDetectorCore.OnHoverExit += (id) => exitedId = id;

        _detector.UpdateHover();

        // Then: OnHoverExit fires with object A's ID
        Assert.AreEqual("object_a", exitedId);
        Assert.IsNull(_detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_mouseMovesBetweenObjects_firesExitThenEnter()
    {
        // Given: mouse hovering object A
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();

        // When: mouse moves to object B
        _physics.Result = new[] { "object_b" };
        var events = new System.Collections.Generic.List<string>();
        HoverDetectorCore.OnHoverExit += (id) => events.Add("exit:" + id);
        HoverDetectorCore.OnHoverEnter += (id, pos) => events.Add("enter:" + id);

        _detector.UpdateHover();

        // Then: exit(A) then enter(B) fire in order
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("exit:object_a", events[0]);
        Assert.AreEqual("enter:object_b", events[1]);
        Assert.AreEqual("object_b", _detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_continuousNoCollision_exitsOnceOnly()
    {
        // Given: mouse hovering object A
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();

        // When: mouse moves to empty and stays there for multiple frames
        _physics.Result = new string[0];
        int exitCount = 0;
        HoverDetectorCore.OnHoverExit += (id) => exitCount++;

        _detector.UpdateHover(); // Frame 1: exit fires
        _detector.UpdateHover(); // Frame 2: no change, no event
        _detector.UpdateHover(); // Frame 3: no change, no event

        // Then: OnHoverExit fires only once (first frame)
        Assert.AreEqual(1, exitCount);
    }

    // =========================================================================
    // AC-3: Click Events
    // =========================================================================

    [Test]
    public void test_ProcessClick_hoveringObject_firesOnClick()
    {
        // Given: mouse hovering object A in Gameplay state
        _mouse.Position = new Vector2(150f, 300f);
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();

        // When: player presses left mouse button
        string clickedId = null;
        Vector2 clickedPos = default;
        HoverDetectorCore.OnClick += (id, pos) => { clickedId = id; clickedPos = pos; };

        _detector.ProcessClick();

        // Then: OnClick fires with object ID and screen position
        Assert.AreEqual("object_a", clickedId);
        Assert.AreEqual(new Vector2(150f, 300f), clickedPos);
    }

    [Test]
    public void test_ProcessClick_notHovering_noOnClick()
    {
        // Given: mouse over empty space (no hovered object)
        _physics.Result = new string[0];
        _detector.UpdateHover();
        bool clickFired = false;
        HoverDetectorCore.OnClick += (id, pos) => clickFired = true;

        // When: player clicks
        _detector.ProcessClick();

        // Then: no click event (nothing to click on)
        Assert.IsFalse(clickFired);
    }

    [Test]
    public void test_ProcessClick_menuState_noOnClick()
    {
        // Given: hovering object A but in Menu state
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();
        _detector.SetInputState(InputState.Menu);
        bool clickFired = false;
        HoverDetectorCore.OnClick += (id, pos) => clickFired = true;

        // When: player clicks
        _detector.ProcessClick();

        // Then: click suppressed
        Assert.IsFalse(clickFired);
    }

    // =========================================================================
    // AC-4: UI State Suppression
    // =========================================================================

    [Test]
    public void test_UpdateHover_menuState_noDetection()
    {
        // Given: hovering object A, then menu opens
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();
        Assert.AreEqual("object_a", _detector.CurrentHoveredId);

        // When: menu state, mouse moves over a different object
        _detector.SetInputState(InputState.Menu);
        // SetInputState should fire OnHoverExit for the current hovered
        // Reset for clean test — re-establish
        _detector = new HoverDetectorCore(_mouse, _camera, _physics);
        _detector.SetInputState(InputState.Menu);
        _physics.Result = new[] { "object_b" };
        bool enterFired = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => enterFired = true;

        _detector.UpdateHover();

        // Then: no hover events during Menu state
        Assert.IsFalse(enterFired);
        Assert.IsNull(_detector.CurrentHoveredId);
    }

    [Test]
    public void test_SetInputState_leavingGameplay_firesExit()
    {
        // Given: hovering object A
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();

        // When: switching to Menu state
        string exitedId = null;
        HoverDetectorCore.OnHoverExit += (id) => exitedId = id;
        _detector.SetInputState(InputState.Menu);

        // Then: OnHoverExit fires, hover is cleared
        Assert.AreEqual("object_a", exitedId);
        Assert.IsNull(_detector.CurrentHoveredId);
    }

    [Test]
    public void test_SetInputState_returnToGameplay_resumesDetection()
    {
        // Given: was in Menu, now returning to Gameplay
        _detector.SetInputState(InputState.Menu);
        _physics.Result = new[] { "object_a" };

        _detector.SetInputState(InputState.Gameplay);
        string enteredId = null;
        HoverDetectorCore.OnHoverEnter += (id, pos) => enteredId = id;

        _detector.UpdateHover();

        // Then: detection resumes, hover enter fires
        Assert.AreEqual("object_a", enteredId);
    }

    // =========================================================================
    // AC-5: No Collision Detection (Empty Space)
    // =========================================================================

    [Test]
    public void test_UpdateHover_emptySpace_atStart_noEvents()
    {
        // Given: game started, mouse in empty space
        _physics.Result = new string[0];
        bool anyEvent = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => anyEvent = true;
        HoverDetectorCore.OnHoverExit += (id) => anyEvent = true;

        // When: first UpdateHover
        _detector.UpdateHover();

        // Then: no events, no hovered object
        Assert.IsFalse(anyEvent);
        Assert.IsNull(_detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_sameHoveredObject_noDuplicateEnter()
    {
        // Given: hovering object A
        _physics.Result = new[] { "object_a" };
        int enterCount = 0;
        HoverDetectorCore.OnHoverEnter += (id, pos) => enterCount++;
        _detector.UpdateHover();
        Assert.AreEqual(1, enterCount);

        // When: still hovering same object next frame
        _detector.UpdateHover();
        _detector.UpdateHover();

        // Then: no additional enter events
        Assert.AreEqual(1, enterCount);
        Assert.AreEqual("object_a", _detector.CurrentHoveredId);
    }

    [Test]
    public void test_UpdateHover_rebindingState_noDetection()
    {
        // Given: Rebinding state
        _detector.SetInputState(InputState.Rebinding);
        _physics.Result = new[] { "object_a" };
        bool eventFired = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => eventFired = true;

        _detector.UpdateHover();

        // Then: no detection in Rebinding state
        Assert.IsFalse(eventFired);
    }

    [Test]
    public void test_UpdateHover_inactiveState_noDetection()
    {
        // Given: Inactive state (scene transition)
        _detector.SetInputState(InputState.Inactive);
        _physics.Result = new[] { "object_a" };
        bool eventFired = false;
        HoverDetectorCore.OnHoverEnter += (id, pos) => eventFired = true;

        _detector.UpdateHover();

        // Then: no detection in Inactive state
        Assert.IsFalse(eventFired);
    }

    [Test]
    public void test_SetInputState_sameState_noExitEvent()
    {
        // Given: hovering object A in Gameplay
        _physics.Result = new[] { "object_a" };
        _detector.UpdateHover();

        // When: setting same state (no change)
        bool exitFired = false;
        HoverDetectorCore.OnHoverExit += (id) => exitFired = true;
        _detector.SetInputState(InputState.Gameplay);

        // Then: no exit event, hovered persists
        Assert.IsFalse(exitFired);
        Assert.AreEqual("object_a", _detector.CurrentHoveredId);
    }

    [Test]
    public void test_tearDown_resetsStaticEvents()
    {
        // Given: events have subscribers
        HoverDetectorCore.OnHoverEnter += (id, pos) => { };
        HoverDetectorCore.OnHoverExit += (id) => { };
        HoverDetectorCore.OnClick += (id, pos) => { };

        // When: TearDown resets
        HoverDetectorCore.ResetStaticEvents();

        // Then: all events are null
        Assert.IsNull(HoverDetectorCore.OnHoverEnter);
        Assert.IsNull(HoverDetectorCore.OnHoverExit);
        Assert.IsNull(HoverDetectorCore.OnClick);
    }

    [Test]
    public void test_multipleSubscribers_allNotified()
    {
        // Given: multiple subscribers
        _physics.Result = new[] { "object_a" };
        int sub1Count = 0, sub2Count = 0;
        HoverDetectorCore.OnHoverEnter += (id, pos) => sub1Count++;
        HoverDetectorCore.OnHoverEnter += (id, pos) => sub2Count++;

        // When: hover detected
        _detector.UpdateHover();

        // Then: all subscribers notified
        Assert.AreEqual(1, sub1Count);
        Assert.AreEqual(1, sub2Count);
    }
}
