#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Simple struct representing a single validation issue found during build-time
/// or editor-time Addressables cross-reference checking.
/// </summary>
public class ValidationError
{
    /// <summary>The asset path or fragment identifier where the issue was found.</summary>
    public string FragmentPath;

    /// <summary>Human-readable description of the issue.</summary>
    public string Message;

    /// <summary>True if this issue should block the build; false for advisory warnings.</summary>
    public bool IsBlocking;

    public ValidationError(string fragmentPath, string message, bool isBlocking)
    {
        FragmentPath = fragmentPath;
        Message = message;
        IsBlocking = isBlocking;
    }

    public override string ToString()
    {
        string severity = IsBlocking ? "ERROR" : "WARNING";
        return $"[{severity}] {FragmentPath}: {Message}";
    }
}

/// <summary>
/// Build-time validation that cross-references ChapterDefinition fragment
/// AssetReferences against the Addressables catalog before every build.
///
/// Implements <see cref="IPreprocessBuildWithReport"/> so Unity invokes
/// <see cref="OnPreprocessBuild"/> automatically. Any mismatched keys (referenced
/// in a ChapterDefinition but not registered in Addressables) throw
/// <see cref="BuildFailedException"/> to block the build.
///
/// Null or empty AssetReferences produce <see cref="Debug.LogWarning"/> and do
/// NOT block the build (advisory only).
///
/// The core validation logic is extracted into the static
/// <see cref="ValidateFragments"/> method for testability without Unity Editor APIs.
/// </summary>
public class BuildValidation : IPreprocessBuildWithReport
{
    /// <summary>
    /// Callback order for the build pipeline. Runs early so validation failures
    /// surface before expensive asset processing.
    /// </summary>
    public int callbackOrder => 0;

    /// <summary>
    /// Called by Unity before every build. Finds all ChapterDefinition SOs,
    /// cross-references their fragment AssetReferences against the Addressables
    /// catalog, and blocks the build if mismatches are found.
    /// </summary>
    /// <param name="report">Build report provided by Unity.</param>
    /// <exception cref="BuildFailedException">
    /// Thrown when one or more blocking validation errors are found.
    /// </exception>
    public void OnPreprocessBuild(BuildReport report)
    {
        // Find all ChapterDefinition assets in the project
        string[] chapterGuids = AssetDatabase.FindAssets("t:ChapterDefinition");
        if (chapterGuids == null || chapterGuids.Length == 0)
        {
            Debug.Log("[BuildValidation] No ChapterDefinition assets found — skipping validation.");
            return;
        }

        // Load ChapterDefinitions
        var chapters = new List<ChapterDefinition>();
        foreach (string guid in chapterGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            if (chapter != null)
            {
                chapters.Add(chapter);
            }
        }

        if (chapters.Count == 0)
        {
            Debug.Log("[BuildValidation] No valid ChapterDefinition assets loaded.");
            return;
        }

        // Build the set of known Addressables keys
        Func<string, bool> keyExists = BuildAddressableKeyLookup();

        // Run validation
        List<ValidationError> errors = ValidateFragments(chapters, keyExists);

        // Separate advisory warnings from blocking errors
        var blockingErrors = errors.Where(e => e.IsBlocking).ToList();
        var warnings = errors.Where(e => !e.IsBlocking).ToList();

        // Log warnings (advisory, don't block build)
        foreach (var warning in warnings)
        {
            Debug.LogWarning($"[BuildValidation] {warning}");
        }

        // Block build on errors
        if (blockingErrors.Count > 0)
        {
            string message = "Build blocked — Addressables validation errors:\n"
                + string.Join("\n", blockingErrors.Select(e => e.ToString()));
            Debug.LogError($"[BuildValidation] {message}");
            throw new BuildFailedException(message);
        }

        Debug.Log($"[BuildValidation] Validation passed ({chapters.Count} ChapterDefinition(s), " +
            $"{warnings.Count} warning(s), 0 errors)");
    }

    /// <summary>
    /// Validates ChapterDefinition fragment references against a set of known
    /// Addressables keys.
    ///
    /// This method is static and accepts a <paramref name="addressableKeyExists"/>
    /// delegate so it can be tested without Unity Editor APIs. In production, pass
    /// <see cref="BuildAddressableKeyLookup"/>; in tests, pass a mock lambda.
    /// </summary>
    /// <param name="chapters">All ChapterDefinitions to validate.</param>
    /// <param name="addressableKeyExists">
    /// Predicate that returns true if a given key is registered in Addressables.
    /// </param>
    /// <returns>
    /// A list of <see cref="ValidationError"/> — empty if no issues found.
    /// Errors with <c>IsBlocking = true</c> must block the build.
    /// </returns>
    public static List<ValidationError> ValidateFragments(
        IEnumerable<ChapterDefinition> chapters,
        Func<string, bool> addressableKeyExists)
    {
        var errors = new List<ValidationError>();

        if (chapters == null)
            return errors;

        foreach (var chapter in chapters)
        {
            if (chapter == null)
                continue;

            string chapterPath = AssetDatabase.GetAssetPath(chapter);
            if (string.IsNullOrEmpty(chapterPath))
                chapterPath = $"[Chapter:{chapter.ChapterKey}]";

            // Null/empty Fragments array
            if (chapter.Fragments == null || chapter.Fragments.Length == 0)
            {
                errors.Add(new ValidationError(
                    chapterPath,
                    $"ChapterDefinition '{chapter.ChapterKey}' has no fragments assigned",
                    isBlocking: false));
                continue;
            }

            for (int i = 0; i < chapter.Fragments.Length; i++)
            {
                AssetReference fragRef = chapter.Fragments[i];

                // Null AssetReference (unset in Inspector)
                if (fragRef == null)
                {
                    errors.Add(new ValidationError(
                        chapterPath,
                        $"Fragment slot [{i}] is null (unset AssetReference)",
                        isBlocking: false));
                    continue;
                }

                string fragPath = $"{chapterPath} -> Fragments[{i}]";

                // Empty RuntimeKey
                if (!fragRef.RuntimeKeyIsValid())
                {
                    errors.Add(new ValidationError(
                        fragPath,
                        $"AssetReference RuntimeKey is invalid (empty or null)",
                        isBlocking: false));
                    continue;
                }

                string runtimeKey = fragRef.RuntimeKey.ToString();

                // Cross-reference against Addressables catalog
                if (!addressableKeyExists(runtimeKey))
                {
                    errors.Add(new ValidationError(
                        fragPath,
                        $"AssetReference key '{runtimeKey}' not found in Addressables catalog",
                        isBlocking: true));
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Builds a lookup function that checks whether an Addressables key exists
    /// in the project's AddressableAssetSettings.
    ///
    /// Returns a function that always returns false if AddressableAssetSettings
    /// is not configured (e.g., Addressables package not installed).
    /// </summary>
    private static Func<string, bool> BuildAddressableKeyLookup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[BuildValidation] AddressableAssetSettings not found — " +
                "all fragment keys will be treated as missing.");
            return _ => false;
        }

        // Build a HashSet of all known keys for O(1) lookup
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

                    // Add both the address and the GUID as valid keys
                    if (!string.IsNullOrEmpty(entry.address))
                        knownKeys.Add(entry.address);
                    if (!string.IsNullOrEmpty(entry.guid))
                        knownKeys.Add(entry.guid);
                }
            }
        }
        catch (Exception ex)
        {
            // IL2CPP safety: catch generic Exception — never catch specific
            // Addressables exception types as they may be stripped by the linker.
            Debug.LogWarning($"[BuildValidation] Failed to enumerate Addressables catalog: {ex.Message}");
            return _ => false;
        }

        Debug.Log($"[BuildValidation] Addressables catalog indexed: {knownKeys.Count} known key(s)");
        return key => knownKeys.Contains(key);
    }
}
#endif
