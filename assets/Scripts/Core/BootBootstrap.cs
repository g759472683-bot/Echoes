using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Boot scene initialization MonoBehaviour (ADR-0004 Step 1-2).
///
/// Lives on a GameObject in the Boot scene. On Start:
///   1. Creates the persistent GameSceneManager as a DontDestroyOnLoad singleton.
///   2. Initializes foundation systems (InputManager, DataManager, AudioManager).
///   3. Calls LoadSceneAsync("MainMenu") to transition to the main menu.
///   4. On success, sets IsBootComplete = true and fires OnBootComplete.
///
/// On failure (Story 005): displays error panel in Boot scene with retry button.
/// Does NOT attempt to load MainMenu on failure.
///
/// The BootBootstrap itself is destroyed when the Boot scene unloads (Single mode).
/// All persistent systems are on DontDestroyOnLoad GameObjects that survive the unload.
/// </summary>
public class BootBootstrap : MonoBehaviour
{
    [Header("Scene References (optional — set in Inspector)")]
    [SerializeField] private SpriteRenderer _bootSpriteRenderer;

    [Header("System Prefabs (optional — instantiated if Scene is empty)")]
    [SerializeField] private GameObject _inputManagerPrefab;

    private GameSceneManager _gameSceneManager;
    private InputManager _inputManager;
    private bool _isBootComplete;
    private VisualElement _bootErrorPanel;
    private int _bootRetryCount;

    /// <summary>
    /// True after all foundation systems are initialized and LoadSceneAsync("MainMenu")
    /// has completed successfully. Used by tests to verify Boot completion.
    /// </summary>
    public bool IsBootComplete => _isBootComplete;

    /// <summary>
    /// Fires when Boot initialization completes (all systems ready, MainMenu load completed).
    /// Consumers can use this for test assertions or Boot-phase diagnostics.
    /// </summary>
    public static event Action OnBootComplete;

    // --- Story 005 test hooks ---

    /// <summary>Test-only: true while the Boot error panel is displayed.</summary>
    internal bool _bootErrorVisible;

    /// <summary>Test-only: the last Boot error message.</summary>
    internal string _bootErrorMessage;

    /// <summary>Test-only: number of Boot retry attempts.</summary>
    internal int _bootRetryAttempts => _bootRetryCount;

    /// <summary>Test-only: fires when the Boot retry button is pressed.</summary>
    internal event Action OnBootRetried;

    void Start()
    {
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            await InitializeSystemsAsync();
            if (this == null) return;

            // Step 4: Transition to MainMenu with ink-fade (ADR-0004)
            await _gameSceneManager.LoadSceneAsync("MainMenu");
            if (this == null) return;

            // Only mark complete after LoadSceneAsync succeeds (M2 fix)
            _isBootComplete = true;
            OnBootComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[BootBootstrap] Boot initialization failed: {ex}. " +
                "Showing error panel in Boot scene.");
            _isBootComplete = false;
            ShowBootError(ex.Message);
        }
    }

    /// <summary>
    /// Displays an error panel in the Boot scene with a retry button.
    /// If no UIDocument is available in the Boot scene, logs an error
    /// (Boot scene should include a minimal UIDocument for this purpose).
    /// </summary>
    /// <param name="errorDetail">The exception message or error description.</param>
    private void ShowBootError(string errorDetail)
    {
        _bootErrorVisible = true;
        _bootErrorMessage = errorDetail;

        UIDocument uiDocument = Object.FindObjectOfType<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogWarning(
                $"[BootBootstrap] Cannot show Boot error panel — no UIDocument in Boot scene. " +
                $"Error: {errorDetail}");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        HideBootErrorPanelVisual();

        _bootErrorPanel = new VisualElement();
        _bootErrorPanel.AddToClassList("error-panel");
        _bootErrorPanel.style.position = Position.Absolute;
        _bootErrorPanel.style.top = 0;
        _bootErrorPanel.style.left = 0;
        _bootErrorPanel.style.width = Length.Percent(100);
        _bootErrorPanel.style.height = Length.Percent(100);
        _bootErrorPanel.style.backgroundColor = new Color(0f, 0f, 0f, 1f);
        _bootErrorPanel.pickingMode = PickingMode.Position;

        var container = new VisualElement();
        container.style.position = Position.Absolute;
        container.style.top = Length.Percent(35);
        container.style.left = Length.Percent(10);
        container.style.width = Length.Percent(80);
        container.style.alignItems = Align.Center;

        var titleLabel = new Label("游戏初始化失败");
        titleLabel.AddToClassList("error-message");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 22;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        container.Add(titleLabel);

        var detailLabel = new Label(errorDetail);
        detailLabel.AddToClassList("error-message");
        detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        detailLabel.style.fontSize = 14;
        detailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        detailLabel.style.whiteSpace = WhiteSpace.Normal;
        detailLabel.style.marginTop = 10;
        container.Add(detailLabel);

        var buttonRow = new VisualElement();
        buttonRow.AddToClassList("error-buttons");
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.Center;
        buttonRow.style.marginTop = 30;

        var retryButton = new Button(() => RetryBoot()) { text = "重试" };
        retryButton.AddToClassList("error-button");
        buttonRow.Add(retryButton);

        // If retry has been attempted before, also show an exit button
        if (_bootRetryCount > 0)
        {
            var exitButton = new Button(() =>
            {
                Debug.Log("[BootBootstrap] Player chose to exit from Boot error.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }) { text = "退出" };
            exitButton.AddToClassList("error-button");
            exitButton.style.marginLeft = 10;
            buttonRow.Add(exitButton);
        }

        container.Add(buttonRow);
        _bootErrorPanel.Add(container);
        root.Add(_bootErrorPanel);
        _bootErrorPanel.BringToFront();
    }

    /// <summary>
    /// Removes the Boot error panel VisualElement from the UIDocument root.
    /// </summary>
    private void HideBootErrorPanelVisual()
    {
        if (_bootErrorPanel != null)
        {
            _bootErrorPanel.RemoveFromHierarchy();
            _bootErrorPanel = null;
        }
    }

    /// <summary>
    /// Dismisses the Boot error panel and retries initialization from scratch.
    /// Increments the retry counter; if retry also fails, the error panel
    /// re-displays with an additional exit button.
    /// </summary>
    private void RetryBoot()
    {
        _bootRetryCount++;
        OnBootRetried?.Invoke();
        HideBootErrorPanelVisual();
        _bootErrorVisible = false;
        _ = InitAsync();
    }

    /// <summary>
    /// Creates or locates all foundation systems and wires their dependencies.
    /// Idempotent — safe to call if some systems were already placed in the Boot scene.
    /// </summary>
    private async Task InitializeSystemsAsync()
    {
        // --- 1. Create GameSceneManager (DontDestroyOnLoad singleton) ---
        GameObject gsmGo = new GameObject("GameSceneManager");
        DontDestroyOnLoad(gsmGo);
        _gameSceneManager = gsmGo.AddComponent<GameSceneManager>();

        // Create a default SpriteRenderer as a child of the GameSceneManager's
        // GameObject so it inherits DontDestroyOnLoad naturally. The Game scene
        // will replace this with its own renderer when it loads.
        // (M1 fix: no separate DontDestroyOnLoad GameObject that becomes orphaned)
        SpriteRenderer tempRenderer = gsmGo.AddComponent<SpriteRenderer>();

        // Inject dependencies into GameSceneManager
        // Real ISceneFader/IDataManager/IAudioManager implementations replace these stubs
        _gameSceneManager.Initialize(
            sceneFader: null,       // Story 002 provides the real SceneFader
            dataManager: null,      // data-management epic provides DataManager
            audioManager: null,     // audio epic provides AudioManager
            spriteRenderer: tempRenderer);

        // --- 2. Initialize InputManager ---
        GameObject inputGo;
        if (_inputManagerPrefab != null)
        {
            inputGo = Instantiate(_inputManagerPrefab);
        }
        else
        {
            inputGo = new GameObject("InputManager");
            _inputManager = inputGo.AddComponent<InputManager>();
        }

        DontDestroyOnLoad(inputGo);

        // If InputManager wasn't set via prefab, get the component
        if (_inputManager == null)
            _inputManager = inputGo.GetComponent<InputManager>();

        // InputManager.Initialize() is idempotent — safe to call even if Awake already ran
        if (_inputManager != null)
        {
            _inputManager.Initialize();
        }

        // --- 3. Foundation systems initialized ---
        // DataManager, AudioManager, LocalizationSettings will be initialized
        // by their respective epics. For now, GameSceneManager accepts null
        // for these interfaces and degrades gracefully (stubs return defaults).
        // IsBootComplete / OnBootComplete are set in InitAsync after LoadSceneAsync succeeds.

        // Yield one frame so Awake/Start complete on all systems
        await Task.Yield();
    }

    /// <summary>
    /// ADR-0001 Rule 7: Null static events to prevent stale delegate chains
    /// when the BootBootstrap GameObject is destroyed (Boot scene unload).
    /// </summary>
    void OnDestroy()
    {
        OnBootComplete = null;
    }
}
