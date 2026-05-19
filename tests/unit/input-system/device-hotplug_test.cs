using NUnit.Framework;

/// <summary>
/// Unit tests for DeviceHotplugDetector — covers S004 acceptance criteria.
///
/// Uses a mock IDeviceChangeProvider to verify hot-plug detection logic
/// and gamepad input gating without Unity runtime.
/// </summary>
public class DeviceHotplugDetectorTest
{
    // =========================================================================
    // Mock Implementation
    // =========================================================================

    private class MockDeviceChangeProvider : IDeviceChangeProvider
    {
        public event System.Action<bool> OnGamepadConnectionChanged;
        public bool IsGamepadConnected { get; set; }

        /// <summary>Simulates a gamepad being connected.</summary>
        public void SimulateConnect()
        {
            IsGamepadConnected = true;
            OnGamepadConnectionChanged?.Invoke(true);
        }

        /// <summary>Simulates a gamepad being disconnected.</summary>
        public void SimulateDisconnect()
        {
            IsGamepadConnected = false;
            OnGamepadConnectionChanged?.Invoke(false);
        }
    }

    // =========================================================================
    // Test Fixture State
    // =========================================================================

    private MockDeviceChangeProvider _provider;
    private DeviceHotplugDetector _detector;

    [SetUp]
    public void SetUp()
    {
        _provider = new MockDeviceChangeProvider();
        _detector = new DeviceHotplugDetector(_provider);
    }

    [TearDown]
    public void TearDown()
    {
        _detector.Shutdown();
        DeviceHotplugDetector.ResetStaticEvents();
    }

    // =========================================================================
    // AC-1: No Gamepad — No Hints
    // =========================================================================

    [Test]
    public void test_initialState_noGamepad_notConnected()
    {
        // Given: no gamepad connected
        _provider.IsGamepadConnected = false;

        // When: Initialize
        _detector.Initialize();

        // Then: not connected, no hints
        Assert.IsFalse(_detector.IsGamepadConnected);
        Assert.IsFalse(_detector.ShouldShowGamepadHints);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_noGamepad_menuState_noGamepadHints()
    {
        // Given: no gamepad, menu open
        _provider.IsGamepadConnected = false;
        _detector.Initialize();
        _detector.SetInputState(InputState.Menu);

        // Then: no hints (keyboard prompts only)
        Assert.IsFalse(_detector.ShouldShowGamepadHints);
    }

    [Test]
    public void test_noGamepad_gameplayState_noGamepadInput()
    {
        // Given: no gamepad
        _provider.IsGamepadConnected = false;
        _detector.Initialize();

        // Then: no gamepad input in any state
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
        _detector.SetInputState(InputState.Gameplay);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
        _detector.SetInputState(InputState.Menu);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    // =========================================================================
    // AC-2: Gamepad Inserted While Running
    // =========================================================================

    [Test]
    public void test_gamepadConnected_firesEvent()
    {
        // Given: no gamepad
        _provider.IsGamepadConnected = false;
        _detector.Initialize();
        Assert.IsFalse(_detector.IsGamepadConnected);

        bool eventFired = false;
        bool wasConnected = false;
        DeviceHotplugDetector.OnGamepadConnectionChanged += (connected) =>
        {
            eventFired = true;
            wasConnected = connected;
        };

        // When: gamepad is plugged in
        _provider.SimulateConnect();

        // Then: event fires with true, state updated
        Assert.IsTrue(eventFired);
        Assert.IsTrue(wasConnected);
        Assert.IsTrue(_detector.IsGamepadConnected);
    }

    [Test]
    public void test_gamepadConnected_menuState_showsHints()
    {
        // Given: gamepad connected, Menu state
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        _detector.SetInputState(InputState.Menu);

        // Then: gamepad hints shown
        Assert.IsTrue(_detector.ShouldShowGamepadHints);
        Assert.IsTrue(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_gamepadAlreadyConnected_atStartup_detected()
    {
        // Given: gamepad already connected at startup
        _provider.IsGamepadConnected = true;

        // When: Initialize
        _detector.Initialize();

        // Then: connected state detected without event
        Assert.IsTrue(_detector.IsGamepadConnected);
        Assert.IsTrue(_detector.IsInitialized);
    }

    [Test]
    public void test_Initialize_idempotent()
    {
        _detector.Initialize();
        bool initState = _detector.IsInitialized;

        // Calling Initialize again should not throw or double-subscribe
        Assert.DoesNotThrow(() => _detector.Initialize());
        Assert.IsTrue(_detector.IsInitialized);
    }

    // =========================================================================
    // AC-3: Gamepad Removed While Running
    // =========================================================================

    [Test]
    public void test_gamepadDisconnected_firesEvent()
    {
        // Given: gamepad connected
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        Assert.IsTrue(_detector.IsGamepadConnected);

        bool eventFired = false;
        bool wasConnected = false;
        DeviceHotplugDetector.OnGamepadConnectionChanged += (connected) =>
        {
            eventFired = true;
            wasConnected = connected;
        };

        // When: gamepad is unplugged
        _provider.SimulateDisconnect();

        // Then: event fires with false, state updated
        Assert.IsTrue(eventFired);
        Assert.IsFalse(wasConnected);
        Assert.IsFalse(_detector.IsGamepadConnected);
    }

    [Test]
    public void test_gamepadDisconnected_menuState_hidesHints()
    {
        // Given: gamepad connected, Menu state
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        _detector.SetInputState(InputState.Menu);
        Assert.IsTrue(_detector.ShouldShowGamepadHints);

        // When: gamepad unplugged
        _provider.SimulateDisconnect();

        // Then: hints hidden
        Assert.IsFalse(_detector.ShouldShowGamepadHints);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_gamepadDisconnected_gameplayState_noChange()
    {
        // Given: gamepad connected, Gameplay state (hints not shown in Gameplay)
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        _detector.SetInputState(InputState.Gameplay);
        Assert.IsFalse(_detector.ShouldShowGamepadHints);

        // When: gamepad unplugged during gameplay
        _provider.SimulateDisconnect();

        // Then: still no hints (gameplay doesn't use gamepad anyway)
        Assert.IsFalse(_detector.ShouldShowGamepadHints);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    // =========================================================================
    // AC-4: Gamepad Only Works in Menu State
    // =========================================================================

    [Test]
    public void test_gamepadConnected_gameplayState_noInput()
    {
        // Given: gamepad connected
        _provider.IsGamepadConnected = true;
        _detector.Initialize();

        // When: in Gameplay state
        _detector.SetInputState(InputState.Gameplay);

        // Then: gamepad input disabled (Gameplay map has no gamepad bindings)
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
        Assert.IsFalse(_detector.ShouldShowGamepadHints);
    }

    [Test]
    public void test_gamepadConnected_menuState_inputEnabled()
    {
        // Given: gamepad connected
        _provider.IsGamepadConnected = true;
        _detector.Initialize();

        // When: switching to Menu
        _detector.SetInputState(InputState.Menu);

        // Then: gamepad input enabled
        Assert.IsTrue(_detector.IsGamepadInputEnabled);
        Assert.IsTrue(_detector.ShouldShowGamepadHints);
    }

    [Test]
    public void test_gamepadConnected_rebindingState_noInput()
    {
        // Given: gamepad connected
        _provider.IsGamepadConnected = true;
        _detector.Initialize();

        // When: Rebinding state (all input suspended)
        _detector.SetInputState(InputState.Rebinding);

        // Then: gamepad input disabled
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_gamepadConnected_inactiveState_noInput()
    {
        // Given: gamepad connected
        _provider.IsGamepadConnected = true;
        _detector.Initialize();

        // When: Inactive state (scene transition)
        _detector.SetInputState(InputState.Inactive);

        // Then: gamepad input disabled
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_stateTransition_menuToGameplay_inputGated()
    {
        // Given: gamepad in Menu
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        _detector.SetInputState(InputState.Menu);
        Assert.IsTrue(_detector.IsGamepadInputEnabled);

        // When: closing menu → Gameplay
        _detector.SetInputState(InputState.Gameplay);

        // Then: gamepad input gated
        Assert.IsFalse(_detector.IsGamepadInputEnabled);
    }

    [Test]
    public void test_stateTransition_gameplayToMenu_inputEnabled()
    {
        // Given: gamepad in Gameplay (no input)
        _provider.IsGamepadConnected = true;
        _detector.Initialize();
        _detector.SetInputState(InputState.Gameplay);
        Assert.IsFalse(_detector.IsGamepadInputEnabled);

        // When: opening menu (Escape)
        _detector.SetInputState(InputState.Menu);

        // Then: gamepad input enabled
        Assert.IsTrue(_detector.IsGamepadInputEnabled);
    }

    // =========================================================================
    // AC-5: Event Subscription + Shutdown
    // =========================================================================

    [Test]
    public void test_multipleSubscribers_allNotified()
    {
        // Given: two subscribers
        _provider.IsGamepadConnected = false;
        _detector.Initialize();
        int sub1Count = 0, sub2Count = 0;
        DeviceHotplugDetector.OnGamepadConnectionChanged += (c) => sub1Count++;
        DeviceHotplugDetector.OnGamepadConnectionChanged += (c) => sub2Count++;

        // When: gamepad connected
        _provider.SimulateConnect();

        // Then: both notified once
        Assert.AreEqual(1, sub1Count);
        Assert.AreEqual(1, sub2Count);
    }

    [Test]
    public void test_shutdown_unsubscribesProvider()
    {
        // Given: initialized detector
        _provider.IsGamepadConnected = false;
        _detector.Initialize();

        // When: Shutdown
        _detector.Shutdown();

        // Then: not initialized
        Assert.IsFalse(_detector.IsInitialized);

        // Simulate device change — detector should not react (event filter tests this)
        // Verify no exception on subsequent connect
        Assert.DoesNotThrow(() => _provider.SimulateConnect());
    }

    [Test]
    public void test_tearDown_resetsStaticEvents()
    {
        // Given: event has subscriber
        DeviceHotplugDetector.OnGamepadConnectionChanged += (c) => { };

        // When: TearDown resets
        DeviceHotplugDetector.ResetStaticEvents();

        // Then: event is null
        Assert.IsNull(DeviceHotplugDetector.OnGamepadConnectionChanged);
    }
}
