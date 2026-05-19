using System;

/// <summary>
/// Abstraction over PlayerPrefs for key rebinding persistence.
/// Enables pure C# testing of InputRebindingManager without Unity runtime.
/// </summary>
public interface IBindingStore
{
    /// <summary>Loads the saved binding overrides JSON. Returns empty string if none.</summary>
    string LoadOverrides();

    /// <summary>Saves the binding overrides JSON persistently.</summary>
    void SaveOverrides(string json);
}

/// <summary>
/// Abstraction over InputAction lookups for rebinding operations.
/// Provides action metadata and binding path read/write access.
/// </summary>
public interface IInputActionLookup
{
    /// <summary>Returns true if an action with this name exists.</summary>
    bool ActionExists(string actionName);

    /// <summary>Returns the current binding path for the named action.</summary>
    string GetBindingPath(string actionName);

    /// <summary>Sets the binding path override for the named action.</summary>
    void SetBindingPath(string actionName, string bindingPath);

    /// <summary>Returns the names of all rebindable actions.</summary>
    string[] GetAllActionNames();
}
