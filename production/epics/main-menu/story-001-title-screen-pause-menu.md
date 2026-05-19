# Story 001: 标题画面 + 暂停菜单

> **Epic**: 主菜单与菜单系统 (MainMenu)
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/main-menu.md`
**Requirement**: `TR-main-menu-001`, `TR-main-menu-003`, `TR-main-menu-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: MainMenu 场景拥有独立 UIDocument。所有菜单面板是此 UIDocument 内的 VisualElement 树。面板栈管理委托 UI 框架 (#5)——MainMenuController 只定义 UXML 布局和按钮行为。#title-screen 是默认可见根面板，#pause-menu 在 Game 场景中按 Escape 时 PushPanel。暂停时 Time.timeScale=0 冻结 MonoBehaviour Update，UI Toolkit 事件系统保持响应。"继续"按钮仅 auto_save 存在时可见（隐藏——不显示为灰色禁用态）。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: UI Toolkit runtime 是 Unity 6 新特性。USS transition 仅支持 opacity/transform（GPU 加速），面板淡入使用 .fade-in CSS class。Time.timeScale=0 时 UI Toolkit 事件正常运作（不受 timeScale 影响——Unity 6 确认行为）。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit (UIDocument + VisualElement) for all runtime UI — source: ADR-0006
- Required: LIFO panel stack (max depth 10) — auto input gating — source: ADR-0006
- Required: Theme.uss global CSS variables — source: ADR-0006
- Forbidden: Never use UGUI Canvas — source: ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/main-menu.md`, scoped to this story:*

- [ ] GIVEN 游戏首次启动（无存档），WHEN 标题画面显示，THEN "新游戏"、"加载游戏"、"设置"、"退出"按钮可见。"继续"按钮不可见（display:none）。键盘焦点在"新游戏"上。标题画面以 .fade-in (0.5s) 淡入。

- [ ] GIVEN auto_save 存在 (SaveManager.HasAnySave() = true)，WHEN 标题画面显示，THEN "继续"按钮可见——显示在"新游戏"下方（display:flex）。键盘焦点自动移到"继续"上。

- [ ] GIVEN 玩家在 Gameplay 状态，WHEN 按 Escape，THEN UI 框架 PushPanel(#pause-menu)。Time.timeScale = 0。HUD 降暗（opacity 降至 0.3）。音频系统环境音/音乐降低到 pausedLevel (0.3×)。半透明墨色遮罩覆盖画面。

- [ ] GIVEN 暂停菜单打开，WHEN 玩家点击"继续"或按 Escape，THEN UI 框架 PopPanel() 关闭暂停。Time.timeScale = 1。HUD 恢复全亮。音频恢复。

- [ ] GIVEN 暂停菜单打开，WHEN 玩家点击"返回标题画面"，THEN PushPanel → #modal-dialog 显示确认消息"返回标题画面？未保存的进度将丢失。"（Story 002 处理对话框按钮行为）。

---

## Implementation Notes

*Derived from ADR-0006 + GDD rules 2–3:*

### MainMenu UIDocument 结构

```
MainMenu UIDocument
├── #title-screen                 // 标题画面根 (默认可见)
│   ├── #game-logo                // 手写游戏标题
│   ├── #menu-buttons             // 菜单按钮容器
│   │   ├── #btn-new-game
│   │   ├── #btn-continue         // 条件可见
│   │   ├── #btn-load-game
│   │   ├── #btn-settings
│   │   └── #btn-quit
│   └── #background-painting      // 背景水墨画
├── #pause-menu                   // 暂停菜单根 (默认隐藏)
│   ├── #btn-resume
│   ├── #btn-save-game
│   ├── #btn-load-game-pause
│   ├── #btn-settings-pause
│   └── #btn-return-to-title
├── #settings-panel               // 设置面板 (Story 002)
├── #save-load-panel              // 存档管理面板 (Story 003)
└── #modal-dialog                 // 模态确认对话框 (Story 002)
```

### MainMenuController MonoBehaviour

```csharp
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private VisualElement _titleScreen;
    private VisualElement _btnContinue;
    private VisualElement _btnNewGame;

    void Awake()
    {
        _titleScreen = _uiDocument.rootVisualElement.Q("#title-screen");
        _btnContinue = _uiDocument.rootVisualElement.Q("#btn-continue");
        _btnNewGame = _uiDocument.rootVisualElement.Q("#btn-new-game");

        // Check save existence
        bool hasSave = SaveManager.HasAnySave();
        _btnContinue.visible = hasSave;
        // Focus: continue if save exists, else new game
        if (hasSave)
            _btnContinue.Focus();
        else
            _btnNewGame.Focus();
    }

    void OnEnable()
    {
        // Subscribe to UI button clicks via UQuery
        _uiDocument.rootVisualElement.Q<Button>("#btn-new-game")
            .clicked += HandleNewGame;
        _uiDocument.rootVisualElement.Q<Button>("#btn-continue")
            .clicked += HandleContinue;
        _uiDocument.rootVisualElement.Q<Button>("#btn-load-game")
            .clicked += HandleLoadGame;
        _uiDocument.rootVisualElement.Q<Button>("#btn-settings")
            .clicked += HandleSettings;
        _uiDocument.rootVisualElement.Q<Button>("#btn-quit")
            .clicked += HandleQuit;

        // Pause menu buttons
        _uiDocument.rootVisualElement.Q<Button>("#btn-resume")
            .clicked += HandleResume;
        _uiDocument.rootVisualElement.Q<Button>("#btn-save-game")
            .clicked += HandleSaveGame;
        // ... etc
    }
}
```

### 暂停触发 (in-game context)

```csharp
// In Game scene, InputManager detects Escape (UI Action Map Cancel)
void HandlePause()
{
    UIPanelStack.PushPanel("pause-menu");
    Time.timeScale = 0f;
    // HUD dimming via InGameHUD
    // AudioManager.PauseAll() → reduce music/ambience to pausedLevel (0.3×)
}
```

### 暂停恢复

```csharp
void HandleResume()
{
    UIPanelStack.PopPanel();
    // If stack empty → auto SwitchToGameplayMode
    Time.timeScale = 1f;
    // AudioManager.ResumeAll()
}
```

### "继续"按钮可见性

```csharp
void RefreshContinueButton()
{
    bool hasAutoSave = SaveManager.SlotExists("auto_save");
    _btnContinue.visible = hasAutoSave;
    if (hasAutoSave)
        _btnContinue.Focus();
}
```

- 不在 Awake 时做版本兼容检查——点击"继续"后若版本迁移失败再报错（GDD Edge Cases）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 设置面板 (#settings-panel) + 模态确认对话框 (#modal-dialog)
- Story 003: 存档管理面板 (#save-load-panel) — Save/Load 双模式
- Story 004: 新游戏/继续/加载/退出完整流程实现（按钮回调调用 ChapterManager/SaveManager——本 Story 只定义按钮和回调签名）
- UI 框架 (#5): PushPanel/PopPanel 实现、Theme.uss、FocusController
- 存档系统 (#7): HasAnySave、SlotExists
- 音频系统 (#3): PauseAll/ResumeAll

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Title screen — no save file
  - Setup: Fresh launch (delete all save files); MainMenu scene loaded
  - Verify: #title-screen visible with .fade-in (0.5s); #btn-new-game, #btn-load-game, #btn-settings, #btn-quit visible; #btn-continue display:none; focus on #btn-new-game; background painting rendered
  - Pass condition: Correct buttons shown, correct focus, continue hidden

- **AC-2**: Title screen — auto_save exists
  - Setup: auto_save file present from previous session; MainMenu scene loaded
  - Verify: #btn-continue visible below #btn-new-game; keyboard focus on #btn-continue
  - Pass condition: Continue button visible and focused

- **AC-3**: Pause menu opens from gameplay
  - Setup: InGame scene, gameplay active; press Escape
  - Verify: UIPanelStack.PushPanel("pause-menu") called; Time.timeScale = 0; HUD opacity = 0.3; semi-transparent ink overlay visible; music/ambience at 0.3× volume
  - Pass condition: Pause state fully engaged — time frozen, audio muffled, HUD dimmed

- **AC-4**: Pause menu closes — resume gameplay
  - Setup: Pause menu open; click "继续" or press Escape
  - Verify: PopPanel() called; Time.timeScale = 1; HUD opacity restored to 1.0; audio restored; GameplayInputActive
  - Pass condition: Full resume — time, audio, HUD, input all restored

- **AC-5**: Return to title from pause
  - Setup: Pause menu open; click "返回标题画面"
  - Verify: PushPanel → #modal-dialog appears with localized text "返回标题画面？未保存的进度将丢失。"; confirm/cancel buttons present
  - Pass condition: Modal dialog on top of pause menu; pause menu still visible behind (dimmed)

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/title-screen-pause-menu-evidence.md` — manual walkthrough doc or interaction test

**Status**: [x] Created — `production/qa/evidence/title-screen-pause-menu-evidence.md`

---

## Completion Notes

- **Completed**: 2026-05-19
- **Files**: `src/core/MainMenuController.cs` (title screen + pause menu sections), `assets/uxml/main-menu.uxml` (#title-screen + #pause-menu), `assets/uss/main-menu.uss`
- **Deviations**: None — implementation matches story spec exactly
- **Test Evidence**: Evidence template written; manual walkthrough pending QA sign-off

## Dependencies

- Depends on: UI 框架 Story 002 (UIPanelStack PushPanel/PopPanel + Theme.uss); 存档系统 Story 001 (HasAnySave, SlotExists)
- Unlocks: Story 002 (settings + modal dialog); Story 004 (game flow integration)
