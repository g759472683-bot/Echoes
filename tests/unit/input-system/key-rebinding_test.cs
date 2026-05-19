using NUnit.Framework;

/// <summary>
/// Unit tests for InputRebindingManager — covers S003 acceptance criteria.
///
/// Uses mock implementations of IBindingStore, IInputActionLookup,
/// and ITimeProvider to verify rebinding logic without Unity runtime.
/// </summary>
public class InputRebindingManagerTest
{
    // =========================================================================
    // Mock Implementations
    // =========================================================================

    private class MockBindingStore : IBindingStore
    {
        public string StoredJson = "";
        public string LoadOverrides() => StoredJson;
        public void SaveOverrides(string json) => StoredJson = json;
    }

    private class MockActionLookup : IInputActionLookup
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _bindings
            = new System.Collections.Generic.Dictionary<string, string>();

        public MockActionLookup(params (string name, string binding)[] actions)
        {
            foreach (var (name, binding) in actions)
                _bindings[name] = binding;
        }

        public bool ActionExists(string actionName) => _bindings.ContainsKey(actionName);
        public string GetBindingPath(string actionName) =>
            _bindings.TryGetValue(actionName, out var p) ? p : null;
        public void SetBindingPath(string actionName, string bindingPath)
        {
            if (_bindings.ContainsKey(actionName))
                _bindings[actionName] = bindingPath;
        }
        public string[] GetAllActionNames()
        {
            var names = new string[_bindings.Count];
            _bindings.Keys.CopyTo(names, 0);
            return names;
        }
    }

    private class MockTimeProvider : ITimeProvider
    {
        public float Time = 100f;
    }

    // =========================================================================
    // Test Fixture State
    // =========================================================================

    private MockBindingStore _store;
    private MockActionLookup _actions;
    private MockTimeProvider _time;
    private InputRebindingManager _manager;

    [SetUp]
    public void SetUp()
    {
        _store = new MockBindingStore();
        _actions = new MockActionLookup(
            ("Confirm", "<Keyboard>/enter"),
            ("Cancel", "<Keyboard>/escape"),
            ("Pause", "<Keyboard>/escape"),
            ("TabNext", "<Keyboard>/tab")
        );
        _time = new MockTimeProvider { Time = 100f };
        _manager = new InputRebindingManager(_store, _actions, _time);
    }

    [TearDown]
    public void TearDown()
    {
        InputRebindingManager.ResetStaticEvents();
    }

    // =========================================================================
    // AC-1: Normal Rebinding Flow
    // =========================================================================

    [Test]
    public void test_StartRebinding_entersRebindingState()
    {
        // Given: player in settings, Confirm bound to Enter
        string startedAction = null;
        InputRebindingManager.OnRebindingStarted += (name) => startedAction = name;

        // When: StartRebinding("Confirm")
        _manager.StartRebinding("Confirm");

        // Then: rebinding started, OnRebindingStarted fires
        Assert.IsTrue(_manager.IsRebinding);
        Assert.AreEqual("Confirm", _manager.CurrentRebindingAction);
        Assert.AreEqual("Confirm", startedAction);
    }

    [Test]
    public void test_CompleteRebinding_appliesNewBinding()
    {
        // Given: rebinding Confirm from Enter
        _manager.StartRebinding("Confirm");

        string completedAction = null;
        string newBinding = null;
        InputRebindingManager.OnRebindingCompleted += (name, path) =>
        {
            completedAction = name;
            newBinding = path;
        };

        // When: player presses Space — CompleteRebinding("Confirm", "<Keyboard>/space")
        _manager.CompleteRebinding("<Keyboard>/space");

        // Then: binding updated, OnRebindingCompleted fires
        Assert.IsFalse(_manager.IsRebinding);
        Assert.AreEqual("Confirm", completedAction);
        Assert.AreEqual("<Keyboard>/space", newBinding);
        Assert.AreEqual("<Keyboard>/space", _actions.GetBindingPath("Confirm"));
    }

    [Test]
    public void test_StartRebinding_nonexistentAction_noOp()
    {
        // Given: an action that doesn't exist
        string startedAction = null;
        InputRebindingManager.OnRebindingStarted += (name) => startedAction = name;

        // When: StartRebinding("Nonexistent")
        _manager.StartRebinding("Nonexistent");

        // Then: no rebinding started
        Assert.IsFalse(_manager.IsRebinding);
        Assert.IsNull(startedAction);
    }

    [Test]
    public void test_StartRebinding_alreadyRebinding_noOp()
    {
        // Given: already rebinding Confirm
        _manager.StartRebinding("Confirm");
        int startCount = 0;
        InputRebindingManager.OnRebindingStarted += (name) => startCount++;

        // When: StartRebinding again
        _manager.StartRebinding("Cancel");

        // Then: second StartRebinding ignored
        Assert.AreEqual(0, startCount);
        Assert.AreEqual("Confirm", _manager.CurrentRebindingAction);
    }

    [Test]
    public void test_CancelRebinding_restoresOriginalBinding()
    {
        // Given: rebinding Confirm from Enter
        _manager.StartRebinding("Confirm");
        // Simulate user started rebinding but hit Escape
        string cancelledAction = null;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelledAction = name;

        // When: CancelRebinding
        _manager.CancelRebinding();

        // Then: original Enter binding restored, OnRebindingCancelled fires
        Assert.IsFalse(_manager.IsRebinding);
        Assert.AreEqual("Confirm", cancelledAction);
        Assert.AreEqual("<Keyboard>/enter", _actions.GetBindingPath("Confirm"));
    }

    // =========================================================================
    // AC-2: 30s Timeout
    // =========================================================================

    [Test]
    public void test_CheckTimeout_at29Seconds_noCancel()
    {
        // Given: rebinding started at t=100
        _manager.StartRebinding("Confirm");
        bool cancelled = false;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelled = true;

        // When: 29 seconds elapsed (t=129)
        _time.Time = 129f;
        _manager.CheckTimeout();

        // Then: still rebinding, no cancel
        Assert.IsTrue(_manager.IsRebinding);
        Assert.IsFalse(cancelled);
    }

    [Test]
    public void test_CheckTimeout_at30Seconds_cancels()
    {
        // Given: rebinding started at t=100
        _manager.StartRebinding("Confirm");
        string cancelledAction = null;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelledAction = name;

        // When: 30 seconds elapsed (t=130)
        _time.Time = 130f;
        _manager.CheckTimeout();

        // Then: rebinding cancelled, original preserved
        Assert.IsFalse(_manager.IsRebinding);
        Assert.AreEqual("Confirm", cancelledAction);
        Assert.AreEqual("<Keyboard>/enter", _actions.GetBindingPath("Confirm"));
    }

    [Test]
    public void test_CheckTimeout_at31Seconds_cancels()
    {
        // Given: rebinding started at t=100
        _manager.StartRebinding("Confirm");
        string cancelledAction = null;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelledAction = name;

        // When: 31 seconds elapsed (t=131)
        _time.Time = 131f;
        _manager.CheckTimeout();

        // Then: timeout after 30s
        Assert.IsFalse(_manager.IsRebinding);
        Assert.AreEqual("Confirm", cancelledAction);
    }

    [Test]
    public void test_CheckTimeout_notRebinding_noOp()
    {
        // Given: not in rebinding state
        Assert.IsFalse(_manager.IsRebinding);

        // When/Then: CheckTimeout is safe to call
        Assert.DoesNotThrow(() => _manager.CheckTimeout());
    }

    // =========================================================================
    // AC-3: Duplicate Binding Resolution
    // =========================================================================

    [Test]
    public void test_CompleteRebinding_duplicateBinding_clearsOldAction()
    {
        // Given: Confirm → Enter, Cancel → Escape
        // When: Confirm is rebound to Escape
        _manager.StartRebinding("Confirm");
        string duplicateCleared = null;
        InputRebindingManager.OnDuplicateCleared += (name) => duplicateCleared = name;

        _manager.CompleteRebinding("<Keyboard>/escape");

        // Then: Confirm → Escape, Cancel's Escape cleared → Cancel unbound
        Assert.AreEqual("<Keyboard>/escape", _actions.GetBindingPath("Confirm"));
        Assert.AreEqual("", _actions.GetBindingPath("Cancel"));
        Assert.AreEqual("Cancel", duplicateCleared);
    }

    [Test]
    public void test_CompleteRebinding_duplicatePause_clearsPauseBinding()
    {
        // Given: Confirm → Enter, Cancel → Escape, Pause → Escape (same as Cancel)
        // When: Confirm rebound to Escape
        _manager.StartRebinding("Confirm");

        var clearedActions = new System.Collections.Generic.List<string>();
        InputRebindingManager.OnDuplicateCleared += (name) => clearedActions.Add(name);

        _manager.CompleteRebinding("<Keyboard>/escape");

        // Then: Both Cancel and Pause lose their Escape binding
        Assert.AreEqual("<Keyboard>/escape", _actions.GetBindingPath("Confirm"));
        Assert.AreEqual("", _actions.GetBindingPath("Cancel"));
        Assert.AreEqual("", _actions.GetBindingPath("Pause"));
        Assert.AreEqual(2, clearedActions.Count);
        Assert.Contains("Cancel", clearedActions);
        Assert.Contains("Pause", clearedActions);
    }

    [Test]
    public void test_CompleteRebinding_sameAction_noSelfClear()
    {
        // Given: Confirm bound to Enter
        _manager.StartRebinding("Confirm");
        bool selfCleared = false;
        InputRebindingManager.OnDuplicateCleared += (name) =>
        {
            if (name == "Confirm") selfCleared = true;
        };

        // When: Confirm rebound to a new unique path
        _manager.CompleteRebinding("<Keyboard>/space");

        // Then: Confirm keeps its new binding, not self-cleared
        Assert.AreEqual("<Keyboard>/space", _actions.GetBindingPath("Confirm"));
        Assert.IsFalse(selfCleared);
    }

    // =========================================================================
    // AC-4: Device Disconnect (handled via CancelRebinding)
    // =========================================================================

    [Test]
    public void test_CancelRebinding_deviceDisconnect_restoresOriginal()
    {
        // Given: rebinding in progress
        _manager.StartRebinding("Confirm");
        // Simulates device disconnect detected by InputSystem → caller invokes CancelRebinding

        string cancelledAction = null;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelledAction = name;

        // When: CancelRebinding called due to device disconnect
        _manager.CancelRebinding();

        // Then: original binding restored
        Assert.AreEqual("Confirm", cancelledAction);
        Assert.AreEqual("<Keyboard>/enter", _actions.GetBindingPath("Confirm"));
        Assert.IsFalse(_manager.IsRebinding);
    }

    // =========================================================================
    // AC-5: Persistence
    // =========================================================================

    [Test]
    public void test_SaveBindingsToStore_persistsToStore()
    {
        // Given: Confirm rebound to Space
        _manager.StartRebinding("Confirm");
        _manager.CompleteRebinding("<Keyboard>/space");

        // Then: JSON stored
        var stored = _store.StoredJson;
        Assert.IsTrue(stored.Contains("Confirm=<Keyboard>/space"));
        Assert.IsTrue(stored.Contains("Cancel=<Keyboard>/escape"));
        Assert.IsTrue(stored.Contains("Pause=<Keyboard>/escape"));
    }

    [Test]
    public void test_LoadBindings_restoresFromStore()
    {
        // Given: saved overrides in store
        _store.StoredJson = "Confirm=<Keyboard>/space;Pause=<Keyboard>/p";

        // When: LoadBindings called (e.g., game restart)
        _manager.LoadBindings();

        // Then: Confirm and Pause bindings restored from save
        Assert.AreEqual("<Keyboard>/space", _actions.GetBindingPath("Confirm"));
        Assert.AreEqual("<Keyboard>/p", _actions.GetBindingPath("Pause"));
        // Cancel not in save → keeps default
        Assert.AreEqual("<Keyboard>/escape", _actions.GetBindingPath("Cancel"));
    }

    [Test]
    public void test_LoadBindings_emptyStore_noChange()
    {
        // Given: no saved overrides
        _store.StoredJson = "";

        // When: LoadBindings
        _manager.LoadBindings();

        // Then: all actions keep default bindings
        Assert.AreEqual("<Keyboard>/enter", _actions.GetBindingPath("Confirm"));
        Assert.AreEqual("<Keyboard>/escape", _actions.GetBindingPath("Cancel"));
    }

    [Test]
    public void test_LoadBindings_unrecognizedAction_ignored()
    {
        // Given: saved overrides include an action that no longer exists
        _store.StoredJson = "Confirm=<Keyboard>/space;DeletedAction=<Keyboard>/x";

        // When: LoadBindings
        Assert.DoesNotThrow(() => _manager.LoadBindings());

        // Then: valid action restored, unknown action silently skipped
        Assert.AreEqual("<Keyboard>/space", _actions.GetBindingPath("Confirm"));
    }

    [Test]
    public void test_CompleteRebinding_autosaveTriggered()
    {
        // Given: empty store
        Assert.AreEqual("", _store.StoredJson);

        // When: complete a rebinding
        _manager.StartRebinding("Confirm");
        _manager.CompleteRebinding("<Keyboard>/space");

        // Then: store now has data
        Assert.IsTrue(_store.StoredJson.Length > 0);
    }

    [Test]
    public void test_CompleteRebinding_emptyPath_noOp()
    {
        // Given: rebinding in progress
        _manager.StartRebinding("Confirm");
        bool completed = false;
        InputRebindingManager.OnRebindingCompleted += (name, path) => completed = true;

        // When: empty binding path passed
        _manager.CompleteRebinding("");

        // Then: ignored, still rebinding, original preserved
        Assert.IsTrue(_manager.IsRebinding);
        Assert.IsFalse(completed);
        Assert.AreEqual("<Keyboard>/enter", _actions.GetBindingPath("Confirm"));
    }

    [Test]
    public void test_CancelRebinding_notRebinding_noOp()
    {
        // Given: not in rebinding state
        bool cancelled = false;
        InputRebindingManager.OnRebindingCancelled += (name) => cancelled = true;

        // When: CancelRebinding called
        Assert.DoesNotThrow(() => _manager.CancelRebinding());

        // Then: no event
        Assert.IsFalse(cancelled);
    }

    [Test]
    public void test_tearDown_resetsStaticEvents()
    {
        // Given: events have subscribers
        InputRebindingManager.OnRebindingStarted += (name) => { };
        InputRebindingManager.OnRebindingCompleted += (name, path) => { };
        InputRebindingManager.OnRebindingCancelled += (name) => { };
        InputRebindingManager.OnDuplicateCleared += (name) => { };

        // When: TearDown resets
        InputRebindingManager.ResetStaticEvents();

        // Then: all events are null
        Assert.IsNull(InputRebindingManager.OnRebindingStarted);
        Assert.IsNull(InputRebindingManager.OnRebindingCompleted);
        Assert.IsNull(InputRebindingManager.OnRebindingCancelled);
        Assert.IsNull(InputRebindingManager.OnDuplicateCleared);
    }

    [Test]
    public void test_rebindingCancelled_doesNotSaveToStore()
    {
        // Given: store has original save data
        _store.StoredJson = "Confirm=<Keyboard>/enter;Cancel=<Keyboard>/escape";

        // When: start rebinding, then cancel
        _manager.StartRebinding("Confirm");
        _manager.CancelRebinding();

        // Then: store unchanged
        Assert.AreEqual("Confirm=<Keyboard>/enter;Cancel=<Keyboard>/escape", _store.StoredJson);
    }
}
