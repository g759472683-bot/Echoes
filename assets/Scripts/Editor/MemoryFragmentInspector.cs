#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for MemoryFragment ScriptableObjects.
///
/// Draws a colored status indicator at the top of the Inspector:
///   - Green: all critical fields (FragmentId, ChapterKey, IllustrationKey) are filled.
///   - Yellow: all critical fields are filled, but one or more non-critical fields
///     (AudioKeys, InteractiveObjects, ChoiceGroups) are empty or null.
///   - Red: one or more critical fields are missing (null or empty).
///
/// The status dot includes a tooltip listing specific issues.
/// After the status indicator, the default Inspector is drawn.
///
/// Uses SerializedObject / SerializedProperty API for proper undo support
/// and dirty-tracking integration with Unity's serialization system.
/// </summary>
[CustomEditor(typeof(MemoryFragment))]
public class MemoryFragmentInspector : Editor
{
    private SerializedProperty _fragmentIdProp;
    private SerializedProperty _chapterKeyProp;
    private SerializedProperty _illustrationKeyProp;
    private SerializedProperty _audioKeysProp;
    private SerializedProperty _interactiveObjectsProp;
    private SerializedProperty _choiceGroupsProp;

    private void OnEnable()
    {
        _fragmentIdProp = serializedObject.FindProperty("FragmentId");
        _chapterKeyProp = serializedObject.FindProperty("ChapterKey");
        _illustrationKeyProp = serializedObject.FindProperty("IllustrationKey");
        _audioKeysProp = serializedObject.FindProperty("AudioKeys");
        _interactiveObjectsProp = serializedObject.FindProperty("InteractiveObjects");
        _choiceGroupsProp = serializedObject.FindProperty("ChoiceGroups");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Collect validation issues from SerializedProperty values ---
        var criticalIssues = new System.Collections.Generic.List<string>();
        var warningIssues = new System.Collections.Generic.List<string>();

        // Critical fields
        if (_fragmentIdProp != null && string.IsNullOrEmpty(_fragmentIdProp.stringValue))
            criticalIssues.Add("FragmentId is missing");
        if (_chapterKeyProp != null && string.IsNullOrEmpty(_chapterKeyProp.stringValue))
            criticalIssues.Add("ChapterKey is missing");
        if (_illustrationKeyProp != null && string.IsNullOrEmpty(_illustrationKeyProp.stringValue))
            criticalIssues.Add("IllustrationKey is missing");

        // Non-critical fields
        if (_audioKeysProp != null &&
            (_audioKeysProp.arraySize == 0 || !IsAnyArrayElementNonEmpty(_audioKeysProp)))
            warningIssues.Add("AudioKeys is empty");
        if (_interactiveObjectsProp != null && _interactiveObjectsProp.arraySize == 0)
            warningIssues.Add("InteractiveObjects is empty");
        if (_choiceGroupsProp != null && _choiceGroupsProp.arraySize == 0)
            warningIssues.Add("ChoiceGroups is empty");

        // --- Determine status ---
        FragmentStatus status;
        string tooltip;

        if (criticalIssues.Count > 0)
        {
            status = FragmentStatus.Error;
            tooltip = "Critical issues:\n" + string.Join("\n", criticalIssues);
            if (warningIssues.Count > 0)
                tooltip += "\n\nWarnings:\n" + string.Join("\n", warningIssues);
        }
        else if (warningIssues.Count > 0)
        {
            status = FragmentStatus.Warning;
            tooltip = "Warnings:\n" + string.Join("\n", warningIssues);
        }
        else
        {
            status = FragmentStatus.Ok;
            tooltip = "All fields are valid";
        }

        // --- Draw status dot ---
        DrawStatusDot(status, tooltip);

        EditorGUILayout.Space(4);

        // --- Draw default inspector (handles all field rendering) ---
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Returns true if any element in a string array SerializedProperty is non-empty.
    /// Used to detect arrays where all elements are empty strings (effectively empty).
    /// </summary>
    private static bool IsAnyArrayElementNonEmpty(SerializedProperty arrayProp)
    {
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            if (element != null && !string.IsNullOrEmpty(element.stringValue))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Draws a colored circle with a tooltip indicating the fragment's validation status.
    /// </summary>
    private static void DrawStatusDot(FragmentStatus status, string tooltip)
    {
        Color dotColor;
        string label;

        switch (status)
        {
            case FragmentStatus.Ok:
                dotColor = Color.green;
                label = "● All fields valid";
                break;
            case FragmentStatus.Warning:
                dotColor = Color.yellow;
                label = "● Warnings present";
                break;
            case FragmentStatus.Error:
            default:
                dotColor = Color.red;
                label = "● Critical fields missing";
                break;
        }

        var content = new GUIContent(label, tooltip);

        var originalColor = GUI.color;
        GUI.color = dotColor;
        EditorGUILayout.LabelField(content, EditorStyles.boldLabel);
        GUI.color = originalColor;
    }

    private enum FragmentStatus
    {
        Ok,
        Warning,
        Error
    }
}
#endif
