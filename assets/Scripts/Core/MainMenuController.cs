using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Save/Load panel mode — determines slot click behaviour and title text.
/// </summary>
public enum SaveLoadMode
{
    /// <summary>Saving to a slot — empty slots save directly, occupied slots show overwrite confirm.</summary>
    Save,
    /// <summary>Loading from a slot — occupied slots load (with confirm if in-game), empty slots not interactive.</summary>
    Load
}

/// <summary>
/// Confirmation dialog scenario — determines the localised message key shown.
/// </summary>
public enum ConfirmScenario
{
    NewGame,
    OverwriteSave,
    LoadInGame,
    ReturnToTitle,
    Quit
}

/// <summary>
/// MainMenu scene controller. Manages five UI panels (title screen, pause menu,
/// settings panel, save/load panel, modal dialog) within a single UIDocument.
///
/// Coordinates with SaveManager, ChapterManager, SaveOrchestrator, CrossChapterTracker,
/// and InputManager for game-flow actions. Uses PlayerPrefs for volume and locale
/// persistence. All player-facing strings go through LocalizationManager.
///
/// Panel stack: a local LIFO stack of panel IDs tracks which sub-panels are open.
/// PushPanel / PopPanel toggle VisualElement visibility and fire UIPanelStackCore
/// static events for system-wide coordination.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // =========================================================================
    // Serialized Fields
    // =========================================================================

    [SerializeField] private UIDocument _uiDocument;

    // =========================================================================
    // Service Dependencies (set by BootBootstrap before MainMenu loads)
    // =========================================================================

    /// <summary>SaveManager instance for save/load operations.</summary>
    public static SaveManager SaveManager { get; set; }

    /// <summary>ChapterManager instance for game flow.</summary>
    public static ChapterManager ChapterManager { get; set; }

    /// <summary>SaveOrchestrator instance for save collection.</summary>
    public static SaveOrchestrator SaveOrchestrator { get; set; }

    /// <summary>CrossChapterTracker instance for flag initialization on new game.</summary>
    public static CrossChapterTracker CrossChapterTracker { get; set; }

    // =========================================================================
    // Cached VisualElements — Panels
    // =========================================================================

    private VisualElement _titleScreen;
    private VisualElement _pauseMenu;
    private VisualElement _pauseOverlay;
    private VisualElement _settingsPanel;
    private VisualElement _saveLoadPanel;
    private VisualElement _modalDialog;

    // =========================================================================
    // Cached VisualElements — Title Screen Buttons
    // =========================================================================

    private Button _btnNewGame;
    private Button _btnContinue;
    private Button _btnLoadGame;
    private Button _btnSettings;
    private Button _btnQuit;

    // =========================================================================
    // Cached VisualElements — Pause Menu Buttons
    // =========================================================================

    private Button _btnResume;
    private Button _btnSaveGame;
    private Button _btnLoadGamePause;
    private Button _btnSettingsPause;
    private Button _btnReturnToTitle;

    // =========================================================================
    // Cached VisualElements — Settings Panel
    // =========================================================================

    private Slider _sliderMaster;
    private Slider _sliderSFX;
    private Slider _sliderMusic;
    private Slider _sliderAmbience;
    private DropdownField _dropdownLanguage;
    private Button _btnSettingsBack;

    // =========================================================================
    // Cached VisualElements — Save/Load Panel
    // =========================================================================

    private Label _panelTitle;
    private VisualElement _slotList;
    private Button _btnSaveLoadBack;

    // =========================================================================
    // Cached VisualElements — Modal Dialog
    // =========================================================================

    private Label _modalMessage;
    private Button _btnModalConfirm;
    private Button _btnModalCancel;

    // =========================================================================
    // State
    // =========================================================================

    private readonly Stack<string> _panelStack = new();
    private SaveLoadMode _saveLoadMode;
    private ConfirmScenario _currentConfirmScenario;
    private Action _onConfirmAction;
    private bool _isInGame;

    // Slot IDs for the save/load panel
    private static readonly string[] SlotIds = { "save_01", "save_02", "auto_save" };

    // Default volume values
    private const float DefaultMasterVolume = 0.8f;
    private const float DefaultSFXVolume = 0.7f;
    private const float DefaultMusicVolume = 0.6f;
    private const float DefaultAmbienceVolume = 0.5f;

    // Toast duration in seconds
    private const float ToastDuration = 2.0f;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    void Awake()
    {
        if (_uiDocument == null)
            _uiDocument = GetComponent<UIDocument>();

        if (_uiDocument == null)
        {
            Debug.LogError("[MainMenuController] No UIDocument found.");
            return;
        }

        VisualElement root = _uiDocument.rootVisualElement;

        // Cache all panels
        _titleScreen = root.Q<VisualElement>("title-screen");
        _pauseMenu = root.Q<VisualElement>("pause-menu");
        _pauseOverlay = root.Q<VisualElement>("pause-overlay");
        _settingsPanel = root.Q<VisualElement>("settings-panel");
        _saveLoadPanel = root.Q<VisualElement>("save-load-panel");
        _modalDialog = root.Q<VisualElement>("modal-dialog");

        // Cache title screen buttons
        _btnNewGame = root.Q<Button>("btn-new-game");
        _btnContinue = root.Q<Button>("btn-continue");
        _btnLoadGame = root.Q<Button>("btn-load-game");
        _btnSettings = root.Q<Button>("btn-settings");
        _btnQuit = root.Q<Button>("btn-quit");

        // Cache pause menu buttons
        _btnResume = root.Q<Button>("btn-resume");
        _btnSaveGame = root.Q<Button>("btn-save-game");
        _btnLoadGamePause = root.Q<Button>("btn-load-game-pause");
        _btnSettingsPause = root.Q<Button>("btn-settings-pause");
        _btnReturnToTitle = root.Q<Button>("btn-return-to-title");

        // Cache settings panel
        _sliderMaster = root.Q<Slider>("slider-master");
        _sliderSFX = root.Q<Slider>("slider-sfx");
        _sliderMusic = root.Q<Slider>("slider-music");
        _sliderAmbience = root.Q<Slider>("slider-ambience");
        _dropdownLanguage = root.Q<DropdownField>("dropdown-language");
        _btnSettingsBack = root.Q<Button>("btn-settings-back");

        // Cache save/load panel
        _panelTitle = root.Q<Label>("panel-title");
        _slotList = root.Q<VisualElement>("slot-list");
        _btnSaveLoadBack = root.Q<Button>("btn-save-load-back");

        // Cache modal dialog
        _modalMessage = root.Q<Label>("modal-message");
        _btnModalConfirm = root.Q<Button>("btn-modal-confirm");
        _btnModalCancel = root.Q<Button>("btn-modal-cancel");

        // Initialise panel visibility — only title screen visible at start
        HideAllPanels();
        _titleScreen.style.display = DisplayStyle.Flex;
        _titleScreen.AddToClassList("fade-in--active");
        _panelStack.Clear();
        _panelStack.Push("title-screen");

        // Check for existing saves — control Continue button visibility
        bool hasSave = SaveManager != null && SaveManager.HasAnySave();
        _btnContinue.visible = hasSave;
        _btnContinue.style.display = hasSave ? DisplayStyle.Flex : DisplayStyle.None;

        // Set initial focus
        if (hasSave)
            _btnContinue.Focus();
        else
            _btnNewGame.Focus();

        // Setup settings defaults
        SetupVolumeSliders();
        SetupLanguageDropdown();
    }

    void OnEnable()
    {
        WireButtonHandlers();
    }

    void OnDisable()
    {
        UnwireButtonHandlers();
    }

    void Update()
    {
        // Escape key handling for panel stack navigation
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    // =========================================================================
    // Button Wiring
    // =========================================================================

    private void WireButtonHandlers()
    {
        // Title screen
        if (_btnNewGame != null) _btnNewGame.clicked += HandleNewGame;
        if (_btnContinue != null) _btnContinue.clicked += HandleContinue;
        if (_btnLoadGame != null) _btnLoadGame.clicked += HandleLoadGame;
        if (_btnSettings != null) _btnSettings.clicked += HandleSettings;
        if (_btnQuit != null) _btnQuit.clicked += HandleQuit;

        // Pause menu
        if (_btnResume != null) _btnResume.clicked += HandleResume;
        if (_btnSaveGame != null) _btnSaveGame.clicked += HandleSaveGame;
        if (_btnLoadGamePause != null) _btnLoadGamePause.clicked += HandleLoadGamePause;
        if (_btnSettingsPause != null) _btnSettingsPause.clicked += HandleSettings;
        if (_btnReturnToTitle != null) _btnReturnToTitle.clicked += HandleReturnToTitle;

        // Settings panel
        if (_btnSettingsBack != null) _btnSettingsBack.clicked += HandleSettingsBack;

        // Save/load panel
        if (_btnSaveLoadBack != null) _btnSaveLoadBack.clicked += HandleSaveLoadBack;

        // Modal dialog
        if (_btnModalConfirm != null) _btnModalConfirm.clicked += HandleModalConfirm;
        if (_btnModalCancel != null) _btnModalCancel.clicked += HandleModalCancel;
    }

    private void UnwireButtonHandlers()
    {
        // Title screen
        if (_btnNewGame != null) _btnNewGame.clicked -= HandleNewGame;
        if (_btnContinue != null) _btnContinue.clicked -= HandleContinue;
        if (_btnLoadGame != null) _btnLoadGame.clicked -= HandleLoadGame;
        if (_btnSettings != null) _btnSettings.clicked -= HandleSettings;
        if (_btnQuit != null) _btnQuit.clicked -= HandleQuit;

        // Pause menu
        if (_btnResume != null) _btnResume.clicked -= HandleResume;
        if (_btnSaveGame != null) _btnSaveGame.clicked -= HandleSaveGame;
        if (_btnLoadGamePause != null) _btnLoadGamePause.clicked -= HandleLoadGamePause;
        if (_btnSettingsPause != null) _btnSettingsPause.clicked -= HandleSettings;
        if (_btnReturnToTitle != null) _btnReturnToTitle.clicked -= HandleReturnToTitle;

        // Settings panel
        if (_btnSettingsBack != null) _btnSettingsBack.clicked -= HandleSettingsBack;

        // Save/load panel
        if (_btnSaveLoadBack != null) _btnSaveLoadBack.clicked -= HandleSaveLoadBack;

        // Modal dialog
        if (_btnModalConfirm != null) _btnModalConfirm.clicked -= HandleModalConfirm;
        if (_btnModalCancel != null) _btnModalCancel.clicked -= HandleModalCancel;
    }

    // =========================================================================
    // Escape Key Handler
    // =========================================================================

    private void HandleEscapeKey()
    {
        if (_panelStack.Count <= 1)
        {
            // At root — show quit confirmation
            if (_isInGame)
            {
                // In game: root is pause menu — pressing escape resumes
                HandleResume();
            }
            else
            {
                // In title: show quit confirm
                ShowConfirmDialog(ConfirmScenario.Quit, HandleQuit);
            }
        }
        else
        {
            // Sub-panel open — pop it
            PopPanel();
        }
    }

    // =========================================================================
    // Panel Stack Helpers
    // =========================================================================

    /// <summary>
    /// Shows a panel, hiding the current top panel. Pushes the panel ID onto
    /// the internal stack and fires UIPanelStackCore.OnPanelPushed for
    /// system-wide coordination.
    /// </summary>
    private void PushPanel(string panelId)
    {
        // Hide current top panel
        if (_panelStack.Count > 0)
        {
            string currentTop = _panelStack.Peek();
            VisualElement currentPanel = GetPanelById(currentTop);
            if (currentPanel != null)
                currentPanel.style.display = DisplayStyle.None;
        }

        // Show new panel
        VisualElement newPanel = GetPanelById(panelId);
        if (newPanel != null)
        {
            newPanel.style.display = DisplayStyle.Flex;
            newPanel.AddToClassList("fade-in--active");
        }

        _panelStack.Push(panelId);
        UIPanelStackCore.OnPanelPushed?.Invoke(panelId);
        UIPanelStackCore.OnStackChanged?.Invoke(_panelStack.Count);

        // Auto-focus first button in the new panel
        FocusFirstButton(newPanel);
    }

    /// <summary>
    /// Pops the top panel from the internal stack. Shows the newly-exposed
    /// top panel. Fires UIPanelStackCore.OnPanelPopped.
    /// </summary>
    private void PopPanel()
    {
        if (_panelStack.Count <= 1)
            return;

        string poppedId = _panelStack.Pop();
        VisualElement poppedPanel = GetPanelById(poppedId);
        if (poppedPanel != null)
            poppedPanel.style.display = DisplayStyle.None;

        // Show the newly exposed top panel
        if (_panelStack.Count > 0)
        {
            string newTop = _panelStack.Peek();
            VisualElement newTopPanel = GetPanelById(newTop);
            if (newTopPanel != null)
                newTopPanel.style.display = DisplayStyle.Flex;
        }

        UIPanelStackCore.OnPanelPopped?.Invoke(poppedId);
        UIPanelStackCore.OnStackChanged?.Invoke(_panelStack.Count);

        // If back to root and in game, resume
        if (_panelStack.Count == 1 && _panelStack.Peek() == "pause-menu" && _isInGame)
        {
            FocusFirstButton(_pauseMenu);
        }
    }

    private VisualElement GetPanelById(string panelId)
    {
        return panelId switch
        {
            "title-screen" => _titleScreen,
            "pause-menu" => _pauseMenu,
            "settings-panel" => _settingsPanel,
            "save-load-panel" => _saveLoadPanel,
            "modal-dialog" => _modalDialog,
            _ => null
        };
    }

    private void FocusFirstButton(VisualElement panel)
    {
        if (panel == null) return;
        Button firstButton = panel.Q<Button>();
        firstButton?.Focus();
    }

    private void HideAllPanels()
    {
        if (_titleScreen != null) _titleScreen.style.display = DisplayStyle.None;
        if (_pauseMenu != null) _pauseMenu.style.display = DisplayStyle.None;
        if (_settingsPanel != null) _settingsPanel.style.display = DisplayStyle.None;
        if (_saveLoadPanel != null) _saveLoadPanel.style.display = DisplayStyle.None;
        if (_modalDialog != null) _modalDialog.style.display = DisplayStyle.None;
    }

    // =========================================================================
    // Title Screen Button Handlers
    // =========================================================================

    /// <summary>
    /// Starts a new game. If a save exists, shows a confirmation dialog first.
    /// </summary>
    private void HandleNewGame()
    {
        if (SaveManager != null && SaveManager.HasAnySave())
        {
            ShowConfirmDialog(ConfirmScenario.NewGame, StartNewGameFlow);
        }
        else
        {
            StartNewGameFlow();
        }
    }

    /// <summary>
    /// Continues from the auto_save slot.
    /// </summary>
    private void HandleContinue()
    {
        LoadGameFlow("auto_save");
    }

    /// <summary>
    /// Opens the save/load panel in Load mode from the title screen.
    /// </summary>
    private void HandleLoadGame()
    {
        ShowSaveLoadPanel(SaveLoadMode.Load);
    }

    /// <summary>
    /// Opens the settings panel.
    /// </summary>
    private void HandleSettings()
    {
        PushPanel("settings-panel");
        InputManager.SwitchToUIMode();
    }

    /// <summary>
    /// Quits the application. In Editor, exits play mode.
    /// </summary>
    private void HandleQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    // =========================================================================
    // Pause Menu Button Handlers
    // =========================================================================

    /// <summary>
    /// Resumes gameplay from pause: restores time scale, pops panel, restores input mode.
    /// </summary>
    private void HandleResume()
    {
        ResumeGame();
    }

    /// <summary>
    /// Opens the save/load panel in Save mode from pause.
    /// </summary>
    private void HandleSaveGame()
    {
        ShowSaveLoadPanel(SaveLoadMode.Save);
    }

    /// <summary>
    /// Opens the save/load panel in Load mode from pause.
    /// </summary>
    private void HandleLoadGamePause()
    {
        ShowSaveLoadPanel(SaveLoadMode.Load);
    }

    /// <summary>
    /// Shows the return-to-title confirmation dialog from pause.
    /// </summary>
    private void HandleReturnToTitle()
    {
        ShowConfirmDialog(ConfirmScenario.ReturnToTitle, ReturnToTitleFlow);
    }

    // =========================================================================
    // Settings Panel Handlers
    // =========================================================================

    /// <summary>
    /// Closes the settings panel and returns to the previous panel.
    /// Settings are applied immediately (no explicit save button).
    /// </summary>
    private void HandleSettingsBack()
    {
        PopPanel();
    }

    /// <summary>
    /// Initialises the four volume sliders. Loads saved values from PlayerPrefs
    /// or uses defaults. Registers callbacks that apply volume changes in
    /// real time and persist to PlayerPrefs.
    /// </summary>
    private void SetupVolumeSliders()
    {
        SetupSlider(_sliderMaster, "master", DefaultMasterVolume);
        SetupSlider(_sliderSFX, "sfx", DefaultSFXVolume);
        SetupSlider(_sliderMusic, "music", DefaultMusicVolume);
        SetupSlider(_sliderAmbience, "ambience", DefaultAmbienceVolume);
    }

    private void SetupSlider(Slider slider, string category, float defaultValue)
    {
        if (slider == null) return;
        slider.lowValue = 0f;
        slider.highValue = 1f;
        slider.value = PlayerPrefs.GetFloat($"volume_{category}", defaultValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            PlayerPrefs.SetFloat($"volume_{category}", evt.newValue);
            PlayerPrefs.Save();
        });
    }

    /// <summary>
    /// Sets up the language dropdown with "中文" and "English" options.
    /// Loads the saved locale from PlayerPrefs. On change, fires
    /// LocalizationManager.OnLocaleChanged for system-wide refresh.
    /// </summary>
    private void SetupLanguageDropdown()
    {
        if (_dropdownLanguage == null) return;
        _dropdownLanguage.choices = new List<string> { "中文", "English" };

        string savedLocale = PlayerPrefs.GetString("selected_locale", "zh-Hans");
        _dropdownLanguage.value = savedLocale == "en" ? "English" : "中文";

        _dropdownLanguage.RegisterValueChangedCallback(evt =>
        {
            string localeCode = evt.newValue switch
            {
                "English" => "en",
                _ => "zh-Hans"
            };
            PlayerPrefs.SetString("selected_locale", localeCode);
            PlayerPrefs.Save();
            LocalizationManager.OnLocaleChanged?.Invoke(localeCode);
        });
    }

    // =========================================================================
    // Save/Load Panel
    // =========================================================================

    /// <summary>
    /// Opens the save/load panel in the specified mode.
    /// </summary>
    private void ShowSaveLoadPanel(SaveLoadMode mode)
    {
        _saveLoadMode = mode;

        // Set title text
        string titleKey = mode == SaveLoadMode.Save ? "menu.save_game" : "menu.load_game";
        if (_panelTitle != null)
            _panelTitle.text = GetLocalized(titleKey, mode == SaveLoadMode.Save ? "保存游戏" : "加载游戏");

        RenderSlots();
        PushPanel("save-load-panel");
    }

    /// <summary>
    /// Renders all three save slots with their current metadata.
    /// </summary>
    private void RenderSlots()
    {
        if (_slotList == null) return;
        _slotList.Clear();

        foreach (string slotId in SlotIds)
        {
            SlotMetadata meta = SaveManager != null
                ? SaveManager.GetSlotMetadata(slotId)
                : SlotMetadata.Empty;

            VisualElement slotEl = CreateSlotElement(slotId, meta);
            _slotList.Add(slotEl);
        }
    }

    /// <summary>
    /// Creates a VisualElement for a single save slot.
    /// </summary>
    private VisualElement CreateSlotElement(string slotId, SlotMetadata meta)
    {
        var slot = new VisualElement();
        slot.name = slotId;
        slot.AddToClassList("save-slot");

        if (meta.IsEmpty)
        {
            string emptyText = GetLocalized("menu.slot_empty", "— 空 —");
            var emptyLabel = new Label(emptyText);
            emptyLabel.AddToClassList("slot-empty");
            slot.Add(emptyLabel);

            if (_saveLoadMode == SaveLoadMode.Load)
            {
                // Empty slots are not interactive in Load mode
                slot.pickingMode = PickingMode.Ignore;
            }
            else
            {
                slot.RegisterCallback<ClickEvent>(_ => SaveToSlot(slotId));
            }
        }
        else
        {
            // Slot label
            string slotLabel = GetSlotDisplayLabel(slotId);
            slot.Add(new Label(slotLabel));

            // Formatted timestamp
            string formattedTime = FormatTimestamp(meta.Timestamp);
            slot.Add(new Label(formattedTime));

            // Chapter key
            string chapterDisplay = GetLocalized($"chapter.{meta.CurrentChapterKey}", meta.CurrentChapterKey);
            slot.Add(new Label(chapterDisplay));

            // Play time
            string playTimeStr = FormatPlayTime(meta.PlayTimeSeconds);
            slot.Add(new Label(playTimeStr));

            slot.RegisterCallback<ClickEvent>(_ => HandleSlotClick(slotId, meta));
        }

        return slot;
    }

    /// <summary>
    /// Handles a click on a save slot, routing based on mode and metadata.
    /// </summary>
    private void HandleSlotClick(string slotId, SlotMetadata meta)
    {
        if (_saveLoadMode == SaveLoadMode.Save)
        {
            // Save mode: occupied slots need overwrite confirmation
            ShowConfirmDialog(ConfirmScenario.OverwriteSave, () => SaveToSlot(slotId));
        }
        else
        {
            // Load mode
            if (_isInGame)
            {
                // In-game load requires confirmation (unsaved progress)
                ShowConfirmDialog(ConfirmScenario.LoadInGame, () => LoadFromSlot(slotId));
            }
            else
            {
                // Title screen load — direct, no confirmation needed
                LoadFromSlot(slotId);
            }
        }
    }

    /// <summary>
    /// Saves the current game state to the given slot.
    /// </summary>
    private async void SaveToSlot(string slotId)
    {
        if (SaveManager == null)
        {
            Debug.LogError("[MainMenuController] SaveManager is null — cannot save.");
            return;
        }

        try
        {
            SaveData saveData = SaveOrchestrator != null
                ? SaveOrchestrator.CollectSaveData()
                : new SaveData { Timestamp = DateTime.UtcNow.ToString("O"), Version = 1 };

            await SaveManager.SaveAsync(slotId, saveData);
            ShowToast(GetLocalized("menu.save_complete", "保存完成"));
            PopPanel(); // Close save panel
        }
        catch (SaveFileException e)
        {
            Debug.LogError($"[MainMenuController] Save failed: {e.Message}");
            ShowToast(GetLocalized("menu.save_failed", "保存失败"));
        }
    }

    /// <summary>
    /// Loads game state from the given slot and transitions to the game scene.
    /// </summary>
    private async void LoadFromSlot(string slotId)
    {
        if (SaveManager == null || ChapterManager == null)
        {
            Debug.LogError("[MainMenuController] SaveManager or ChapterManager is null — cannot load.");
            return;
        }

        try
        {
            SaveData? saveData = await SaveManager.LoadAsync(slotId);
            if (saveData == null)
            {
                ShowToast(GetLocalized("menu.load_empty", "存档为空"));
                return;
            }

            await ChapterManager.LoadAndRestore(saveData.Value);
            Time.timeScale = 1f;
            SceneManager.LoadScene("InGame");
        }
        catch (SaveMigrationException e)
        {
            Debug.LogError($"[MainMenuController] Load failed — version migration: {e.Message}");
            ShowToast(GetLocalized("menu.version_mismatch", "存档与新版本不兼容"));
        }
        catch (SaveCorruptedException e)
        {
            Debug.LogError($"[MainMenuController] Load failed — corrupt: {e.Message}");
            ShowToast(GetLocalized("menu.corrupt", "存档已损坏"));
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainMenuController] Load failed: {e.Message}");
            ShowToast(GetLocalized("menu.load_failed", "加载失败"));
        }
    }

    /// <summary>
    /// Closes the save/load panel without action.
    /// </summary>
    private void HandleSaveLoadBack()
    {
        PopPanel();
    }

    // =========================================================================
    // Modal Dialog
    // =========================================================================

    /// <summary>
    /// Shows the modal confirmation dialog for the given scenario.
    /// </summary>
    /// <param name="scenario">Which type of confirmation.</param>
    /// <param name="onConfirm">Action to execute when the player confirms.</param>
    private void ShowConfirmDialog(ConfirmScenario scenario, Action onConfirm)
    {
        _currentConfirmScenario = scenario;
        _onConfirmAction = onConfirm;

        if (_modalMessage != null)
        {
            string messageKey = GetMessageKey(scenario);
            string defaultMessage = GetDefaultMessage(scenario);
            _modalMessage.text = GetLocalized(messageKey, defaultMessage);
        }

        PushPanel("modal-dialog");

        // Default focus on cancel (safe option)
        _btnModalCancel?.Focus();
    }

    /// <summary>
    /// Handles the modal confirm button: executes the stored action,
    /// then pops both the dialog and the triggering panel.
    /// </summary>
    private void HandleModalConfirm()
    {
        _onConfirmAction?.Invoke();

        // Pop dialog + triggering panel (PopPanel ×2)
        if (_panelStack.Count > 0 && _panelStack.Peek() == "modal-dialog")
            PopPanel(); // Pop dialog
        if (_panelStack.Count > 0)
            PopPanel(); // Pop triggering panel
    }

    /// <summary>
    /// Handles the modal cancel button: pops only the dialog.
    /// </summary>
    private void HandleModalCancel()
    {
        if (_panelStack.Count > 0 && _panelStack.Peek() == "modal-dialog")
            PopPanel();
    }

    private static string GetMessageKey(ConfirmScenario scenario)
    {
        return scenario switch
        {
            ConfirmScenario.NewGame => "menu.confirm.new_game",
            ConfirmScenario.OverwriteSave => "menu.confirm.overwrite",
            ConfirmScenario.LoadInGame => "menu.confirm.load_in_game",
            ConfirmScenario.ReturnToTitle => "menu.confirm.return_to_title",
            ConfirmScenario.Quit => "menu.confirm.quit",
            _ => "menu.confirm.default"
        };
    }

    private static string GetDefaultMessage(ConfirmScenario scenario)
    {
        return scenario switch
        {
            ConfirmScenario.NewGame => "开始新游戏将覆盖当前进度。确定继续？",
            ConfirmScenario.OverwriteSave => "覆盖此存档？此操作不可撤销。",
            ConfirmScenario.LoadInGame => "加载此存档？当前未保存的进度将丢失。",
            ConfirmScenario.ReturnToTitle => "返回标题画面？未保存的进度将丢失。",
            ConfirmScenario.Quit => "退出游戏？",
            _ => "确定？"
        };
    }

    // =========================================================================
    // Game Flow Methods
    // =========================================================================

    /// <summary>
    /// Full new game flow: initialises cross-chapter flags, starts a new chapter,
    /// and loads the game scene.
    /// </summary>
    private async void StartNewGameFlow()
    {
        if (CrossChapterTracker != null)
            CrossChapterTracker.InitializeAllFlags();

        if (ChapterManager != null)
            await ChapterManager.StartNewGame();

        Time.timeScale = 1f;
        SceneManager.LoadScene("InGame");
    }

    /// <summary>
    /// Loads a saved game from the given slot and transitions to the game scene.
    /// </summary>
    private async void LoadGameFlow(string slotId)
    {
        if (SaveManager == null || ChapterManager == null)
        {
            Debug.LogError("[MainMenuController] SaveManager or ChapterManager is null.");
            return;
        }

        try
        {
            SaveData? saveData = await SaveManager.LoadAsync(slotId);
            if (saveData == null)
            {
                ShowToast(GetLocalized("menu.no_save", "没有可用的存档"));
                return;
            }

            await ChapterManager.LoadAndRestore(saveData.Value);
            Time.timeScale = 1f;
            SceneManager.LoadScene("InGame");
        }
        catch (SaveMigrationException e)
        {
            Debug.LogError($"[MainMenuController] Continue failed — version mismatch: {e.Message}");
            ShowToast(GetLocalized("menu.version_mismatch", "存档与新版本不兼容"));
        }
        catch (SaveCorruptedException e)
        {
            Debug.LogError($"[MainMenuController] Continue failed — corrupt: {e.Message}");
            ShowToast(GetLocalized("menu.corrupt", "存档已损坏"));
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainMenuController] Continue failed: {e.Message}");
            ShowToast(GetLocalized("menu.load_failed", "加载失败"));
        }
    }

    /// <summary>
    /// Resumes gameplay from the pause menu.
    /// </summary>
    private void ResumeGame()
    {
        Time.timeScale = 1f;
        InputManager.SwitchToGameplayMode();
        PopPanel(); // Pop pause menu
    }

    /// <summary>
    /// Returns to the title screen from the game scene.
    /// Saves a quick auto-save first if SaveManager is available.
    /// </summary>
    private async void ReturnToTitleFlow()
    {
        if (SaveManager != null && SaveOrchestrator != null)
        {
            try
            {
                SaveData saveData = SaveOrchestrator.CollectSaveData();
                await SaveManager.SaveAsync("auto_save", saveData);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MainMenuController] Auto-save before return to title failed: {e.Message}");
                // Continue anyway — player chose to return to title
            }
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // =========================================================================
    // Pause Management (called from outside — e.g., by InputManager)
    // =========================================================================

    /// <summary>
    /// Opens the pause menu from gameplay. Call this when Escape is pressed
    /// during gameplay (from InputManager or other input handler).
    /// Sets _isInGame = true, freezes time, switches to UI mode.
    /// </summary>
    public void OpenPauseMenu()
    {
        _isInGame = true;
        HideAllPanels();
        _pauseMenu.style.display = DisplayStyle.Flex;
        _panelStack.Clear();
        _panelStack.Push("pause-menu");
        Time.timeScale = 0f;
        InputManager.SwitchToUIMode();

        UIPanelStackCore.OnPanelPushed?.Invoke("pause-menu");
        UIPanelStackCore.OnStackChanged?.Invoke(1);

        // Focus resume button
        _btnResume?.Focus();
    }

    // =========================================================================
    // Toast Notification
    // =========================================================================

    /// <summary>
    /// Shows a brief toast message at the bottom centre of the screen.
    /// Auto-dismisses after <see cref="ToastDuration"/> seconds.
    /// </summary>
    private void ShowToast(string message)
    {
        VisualElement root = _uiDocument?.rootVisualElement;
        if (root == null) return;

        var toast = new VisualElement();
        toast.AddToClassList("toast");

        var label = new Label(message);
        toast.Add(label);

        root.Add(toast);

        // Fade in via USS class
        toast.AddToClassList("toast--visible");

        // Auto-dismiss after delay
        StartCoroutine(DismissToastAfterDelay(toast));
    }

    private System.Collections.IEnumerator DismissToastAfterDelay(VisualElement toast)
    {
        yield return new WaitForSecondsRealtime(ToastDuration);
        toast.RemoveFromClassList("toast--visible");
        // Allow fade-out animation to play
        yield return new WaitForSecondsRealtime(0.5f);
        if (toast.parent != null)
            toast.parent.Remove(toast);
    }

    // =========================================================================
    // Localization Helper
    // =========================================================================

    /// <summary>
    /// Returns a localised string for the given key, falling back to the
    /// provided default if the LocalizationManager is unavailable.
    /// </summary>
    private static string GetLocalized(string key, string defaultText)
    {
        try
        {
            return LocalizationManager.GetLocalizedString("UI_Shared", key);
        }
        catch
        {
            return defaultText;
        }
    }

    // =========================================================================
    // Formatting Helpers
    // =========================================================================

    /// <summary>
    /// Formats an ISO 8601 timestamp string into a readable display format.
    /// Example: "2026-05-12T14:30:00Z" becomes "2026年5月12日 14:30"
    /// </summary>
    private static string FormatTimestamp(string iso8601)
    {
        if (string.IsNullOrEmpty(iso8601)) return "";

        if (DateTime.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            return $"{dt.Year}年{dt.Month}月{dt.Day}日 {dt.Hour:D2}:{dt.Minute:D2}";
        }
        return iso8601;
    }

    /// <summary>
    /// Formats play time in seconds to a human-readable string.
    /// Example: 4980 seconds becomes "1h 23m". Under 60 minutes shows only minutes.
    /// </summary>
    internal static string FormatPlayTime(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;

        if (hours > 0)
            return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }

    /// <summary>
    /// Returns a display label for a save slot ID.
    /// </summary>
    private static string GetSlotDisplayLabel(string slotId)
    {
        return slotId switch
        {
            "save_01" => "存档 1",
            "save_02" => "存档 2",
            "auto_save" => "自动存档",
            _ => slotId
        };
    }
}
