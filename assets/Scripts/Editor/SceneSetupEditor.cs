#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool to create the 3 required scenes for the 3-scene architecture
/// (ADR-0004): Boot, MainMenu, Game.
///
/// Run via menu item: 回响 > Setup > Create Required Scenes
///
/// This script lives in an Editor/ folder so it is automatically excluded from
/// builds — no additional #if UNITY_EDITOR guard required (the folder-based
/// exclusion is the guard; the directive here is for clarity).
/// </summary>
public static class SceneSetupEditor
{
    private const string SCENES_DIR = "Assets/Scenes";
    private const string BOOT_PATH = "Assets/Scenes/Boot.unity";
    private const string MAIN_MENU_PATH = "Assets/Scenes/MainMenu.unity";
    private const string GAME_PATH = "Assets/Scenes/Game.unity";

    [MenuItem("回响/Setup/Create Required Scenes")]
    public static void CreateRequiredScenes()
    {
        // Ensure directory exists
        if (!Directory.Exists(SCENES_DIR))
        {
            Directory.CreateDirectory(SCENES_DIR);
            AssetDatabase.Refresh();
        }

        bool anyCreated = false;

        anyCreated |= CreateSceneIfMissing(BOOT_PATH, "Boot", scene =>
        {
            // Add a GameObject with BootBootstrap
            GameObject bootGo = new GameObject("BootBootstrap");
            bootGo.AddComponent<BootBootstrap>();
        });

        anyCreated |= CreateSceneIfMissing(MAIN_MENU_PATH, "MainMenu", scene =>
        {
            // Add a GameObject with MainMenuController
            GameObject menuGo = new GameObject("MainMenuController");
            menuGo.AddComponent<MainMenuController>();
        });

        anyCreated |= CreateSceneIfMissing(GAME_PATH, "Game", scene =>
        {
            // Add a camera (required for any playable scene)
            GameObject cameraGo = new GameObject("MainCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraGo.transform.position = new Vector3(0, 0, -10);

            // Add Directional Light for 2D sprite rendering
            GameObject lightGo = new GameObject("DirectionalLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
        });

        if (anyCreated)
        {
            ConfigureBuildSettings();
            Debug.Log("[SceneSetup] All required scenes created and Build Settings configured.");
        }
        else
        {
            Debug.Log("[SceneSetup] All required scenes already exist. Build Settings verified.");
        }
    }

    /// <summary>
    /// Creates a scene at the given path if it doesn't already exist.
    /// Returns true if a new scene was created.
    /// </summary>
    private static bool CreateSceneIfMissing(
        string path, string sceneName, System.Action<Scene> populateScene)
    {
        if (File.Exists(path))
        {
            Debug.Log($"[SceneSetup] Scene already exists: {path} — skipping.");
            return false;
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Populate the scene with required GameObjects
        populateScene(scene);

        // Save the scene
        bool saved = EditorSceneManager.SaveScene(scene, path);
        if (!saved)
        {
            Debug.LogError($"[SceneSetup] Failed to save scene: {path}");
            return false;
        }

        Debug.Log($"[SceneSetup] Created scene: {path}");
        AssetDatabase.Refresh();
        return true;
    }

    /// <summary>
    /// Configures Build Settings with the 3 scenes in the correct order:
    /// Boot (0), MainMenu (1), Game (2).
    ///
    /// In Unity 6.3, EditorBuildSettings.scenes returns a copy — the full
    /// array must be reassigned after modification.
    /// </summary>
    private static void ConfigureBuildSettings()
    {
        // Build the definitive list of required scenes
        var requiredScenes = new[]
        {
            new EditorBuildSettingsScene(BOOT_PATH, true),
            new EditorBuildSettingsScene(MAIN_MENU_PATH, true),
            new EditorBuildSettingsScene(GAME_PATH, true),
        };

        EditorBuildSettings.scenes = requiredScenes;

        Debug.Log(
            "[SceneSetup] Build Settings configured: " +
            "0=Boot, 1=MainMenu, 2=Game");
    }
}
#endif
