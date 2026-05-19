#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// EditorWindow that scans all MemoryFragment and ChapterDefinition assets
/// for Addressables cross-reference issues.
///
/// Access via: Window > 回响 > Validate Fragments
///
/// Displays:
///   - Summary counts (pass / warning / error) at the top.
///   - Scrollable list of problematic fragments with asset path and issue description.
///
/// Uses the same validation logic as <see cref="BuildValidation.ValidateFragments"/>.
/// </summary>
public class ValidateFragmentsWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private List<ValidationError> _errors;
    private int _totalFragments;
    private int _totalChapters;
    private bool _hasScanned;

    [MenuItem("Window/回响/Validate Fragments")]
    public static void ShowWindow()
    {
        var window = GetWindow<ValidateFragmentsWindow>("Validate Fragments");
        window.minSize = new Vector2(480, 320);
        window.Show();
    }

    private void OnEnable()
    {
        _errors = new List<ValidationError>();
        _hasScanned = false;
    }

    private void OnGUI()
    {
        // --- Header ---
        EditorGUILayout.LabelField("Fragment Validation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- Scan button ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Now", GUILayout.Width(120), GUILayout.Height(24)))
        {
            RunScan();
        }
        if (GUILayout.Button("Clear", GUILayout.Width(80), GUILayout.Height(24)))
        {
            _errors.Clear();
            _hasScanned = false;
            _totalFragments = 0;
            _totalChapters = 0;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // --- Summary ---
        if (_hasScanned)
        {
            DrawSummary();
            EditorGUILayout.Space();

            // --- Error list ---
            if (_errors.Count > 0)
            {
                EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                DrawErrorList();
                EditorGUILayout.EndScrollView();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Click \"Scan Now\" to validate all MemoryFragment and ChapterDefinition assets.",
                MessageType.Info);
        }
    }

    /// <summary>
    /// Draws the summary bar showing pass/warning/error counts.
    /// </summary>
    private void DrawSummary()
    {
        int warningCount = _errors.Count(e => !e.IsBlocking);
        int errorCount = _errors.Count(e => e.IsBlocking);
        int passCount = _totalFragments - warningCount - errorCount;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            $"Chapters: {_totalChapters}   |   Fragments: {_totalFragments}",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Pass
        var originalColor = GUI.color;
        GUI.color = Color.green;
        EditorGUILayout.LabelField($"● {passCount} Pass", EditorStyles.boldLabel,
            GUILayout.Width(100));
        GUI.color = originalColor;

        // Warnings
        GUI.color = Color.yellow;
        EditorGUILayout.LabelField($"● {warningCount} Warning{(warningCount != 1 ? "s" : "")}",
            EditorStyles.boldLabel, GUILayout.Width(120));
        GUI.color = originalColor;

        // Errors
        GUI.color = Color.red;
        EditorGUILayout.LabelField($"● {errorCount} Error{(errorCount != 1 ? "s" : "")}",
            EditorStyles.boldLabel, GUILayout.Width(100));
        GUI.color = originalColor;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws each validation error as a colored box with the asset path and message.
    /// </summary>
    private void DrawErrorList()
    {
        if (_errors.Count == 0)
        {
            EditorGUILayout.HelpBox("No issues found. All fragments are valid.", MessageType.Info);
            return;
        }

        foreach (var error in _errors)
        {
            MessageType msgType = error.IsBlocking ? MessageType.Error : MessageType.Warning;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Fragment path
            EditorGUILayout.LabelField(error.FragmentPath, EditorStyles.miniBoldLabel);

            // Issue description
            EditorGUILayout.HelpBox(error.Message, msgType);

            // Ping button
            if (GUILayout.Button("Ping Asset", GUILayout.Width(100)))
            {
                // Extract the asset path from FragmentPath (format: "Assets/... -> Fragments[N]")
                string assetPath = ExtractAssetPath(error.FragmentPath);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    /// <summary>
    /// Extracts the asset path from a FragmentPath string like
    /// "Assets/Chapters/ch01.asset -> Fragments[2]".
    /// Returns only the "Assets/Chapters/ch01.asset" portion.
    /// </summary>
    private static string ExtractAssetPath(string fragmentPath)
    {
        if (string.IsNullOrEmpty(fragmentPath))
            return null;

        int arrowIndex = fragmentPath.IndexOf("->");
        if (arrowIndex > 0)
            return fragmentPath.Substring(0, arrowIndex).Trim();

        return fragmentPath.Trim();
    }

    /// <summary>
    /// Scans all ChapterDefinition and MemoryFragment assets, cross-references
    /// against the Addressables catalog, and populates the error list.
    ///
    /// Shows a progress bar during the scan.
    /// </summary>
    private void RunScan()
    {
        _errors = new List<ValidationError>();
        _totalFragments = 0;
        _totalChapters = 0;

        try
        {
            // --- Step 1: Scan MemoryFragment SOs for missing critical fields ---
            EditorUtility.DisplayProgressBar("Validate Fragments",
                "Scanning MemoryFragment assets...", 0.0f);

            string[] fragmentGuids = AssetDatabase.FindAssets("t:MemoryFragment");
            if (fragmentGuids == null || fragmentGuids.Length == 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Validate Fragments",
                    "No MemoryFragment assets found in the project.", "OK");
                _hasScanned = true;
                return;
            }

            _totalFragments = fragmentGuids.Length;

            for (int i = 0; i < fragmentGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(fragmentGuids[i]);

                if (i % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("Validate Fragments",
                        $"Scanning MemoryFragment assets... ({i + 1}/{fragmentGuids.Length})",
                        (float)i / fragmentGuids.Length * 0.4f);
                }

                var fragment = AssetDatabase.LoadAssetAtPath<MemoryFragment>(path);
                if (fragment == null)
                    continue;

                // Check critical fields
                if (string.IsNullOrEmpty(fragment.FragmentId))
                {
                    _errors.Add(new ValidationError(path,
                        "FragmentId is missing (critical field)",
                        isBlocking: true));
                }
                if (string.IsNullOrEmpty(fragment.ChapterKey))
                {
                    _errors.Add(new ValidationError(path,
                        "ChapterKey is missing (critical field)",
                        isBlocking: true));
                }
                if (string.IsNullOrEmpty(fragment.IllustrationKey))
                {
                    _errors.Add(new ValidationError(path,
                        "IllustrationKey is missing (critical field)",
                        isBlocking: true));
                }

                // Check non-critical fields
                if (fragment.AudioKeys == null || fragment.AudioKeys.Length == 0)
                {
                    _errors.Add(new ValidationError(path,
                        "AudioKeys is empty (non-critical)",
                        isBlocking: false));
                }
                if (fragment.InteractiveObjects == null || fragment.InteractiveObjects.Length == 0)
                {
                    _errors.Add(new ValidationError(path,
                        "InteractiveObjects is empty (non-critical)",
                        isBlocking: false));
                }
                if (fragment.ChoiceGroups == null || fragment.ChoiceGroups.Length == 0)
                {
                    _errors.Add(new ValidationError(path,
                        "ChoiceGroups is empty (non-critical)",
                        isBlocking: false));
                }
            }

            // --- Step 2: Scan ChapterDefinitions and cross-reference ---
            EditorUtility.DisplayProgressBar("Validate Fragments",
                "Scanning ChapterDefinition assets...", 0.4f);

            string[] chapterGuids = AssetDatabase.FindAssets("t:ChapterDefinition");
            _totalChapters = chapterGuids != null ? chapterGuids.Length : 0;

            if (_totalChapters > 0)
            {
                var chapters = new List<ChapterDefinition>();
                for (int i = 0; i < chapterGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(chapterGuids[i]);
                    var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
                    if (chapter != null)
                        chapters.Add(chapter);
                }

                EditorUtility.DisplayProgressBar("Validate Fragments",
                    "Cross-referencing against Addressables catalog...", 0.7f);

                // Build Addressables key lookup
                Func<string, bool> keyExists = BuildAddressableKeyLookup();

                // Run BuildValidation.ValidateFragments
                var crossRefErrors = BuildValidation.ValidateFragments(chapters, keyExists);
                _errors.AddRange(crossRefErrors);
            }
        }
        catch (Exception ex)
        {
            // IL2CPP safety: catch generic Exception
            Debug.LogError($"[ValidateFragmentsWindow] Scan failed: {ex.Message}");
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Validate Fragments",
                $"Scan failed: {ex.Message}", "OK");
            _hasScanned = true;
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        _hasScanned = true;

        // Show completion dialog if no errors
        if (_errors.Count == 0)
        {
            EditorUtility.DisplayDialog("Validate Fragments",
                $"Scan complete. All {_totalFragments} fragment(s) across {_totalChapters} chapter(s) are valid.",
                "OK");
        }
        else
        {
            int errorCount = _errors.Count(e => e.IsBlocking);
            int warningCount = _errors.Count(e => !e.IsBlocking);
            Debug.Log($"[ValidateFragmentsWindow] Scan complete: {errorCount} error(s), {warningCount} warning(s)");
        }
    }

    /// <summary>
    /// Builds an Addressables key lookup function from the project's
    /// AddressableAssetSettings. Returns a function that always returns false
    /// if the settings are not configured.
    /// </summary>
    private static Func<string, bool> BuildAddressableKeyLookup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[ValidateFragmentsWindow] AddressableAssetSettings not found.");
            return _ => false;
        }

        var knownKeys = new HashSet<string>();
        try
        {
            foreach (var group in settings.groups)
            {
                if (group == null || group.entries == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry == null)
                        continue;

                    if (!string.IsNullOrEmpty(entry.address))
                        knownKeys.Add(entry.address);
                    if (!string.IsNullOrEmpty(entry.guid))
                        knownKeys.Add(entry.guid);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ValidateFragmentsWindow] Failed to enumerate Addressables catalog: {ex.Message}");
            return _ => false;
        }

        Debug.Log($"[ValidateFragmentsWindow] Addressables catalog indexed: {knownKeys.Count} known key(s)");
        return key => knownKeys.Contains(key);
    }
}
#endif
