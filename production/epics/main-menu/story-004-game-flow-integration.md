# Story 004: 新游戏/继续/加载/退出流程

> **Epic**: 主菜单与菜单系统 (MainMenu)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/main-menu.md`
**Requirement**: `TR-main-menu-002`, `TR-main-menu-004` (full flow)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架 + ADR-0003: 存档 + ADR-0011: 跨章节状态
**ADR Decision Summary**: 新游戏流程——检查 auto_save 存在 → 弹出确认对话框（若有）→ ChapterManager.StartNewGame() → CrossChapterTracker.InitializeAllFlags() → LoadScene("InGame")。继续/加载流程——SaveManager.LoadAsync(slotId) → 校验 Checksum + 版本迁移 → ChapterManager.LoadAndRestore(saveData) → LoadScene("InGame")。退出——Application.Quit()（Editor 中 EditorApplication.ExitPlaymode()）。所有流程通过面板栈委托 UI 框架管理。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Application.Quit() 在 Editor 中无效——使用 `#if UNITY_EDITOR` 分支。SceneManager.LoadScene 触发场景过渡——异步操作，过渡动画由 SceneFader 管理（ADR-0004）。LoadAsync 是 async Task——在面板淡出期间执行。

**Control Manifest Rules (Feature Layer)**:
- Required: LIFO panel stack for flow management — source: ADR-0006
- Required: CrossChapterTracker.InitializeAllFlags() on new game — source: ADR-0011
- Required: 3-scene architecture (Boot → MainMenu → Game) — source: ADR-0004

---

## Acceptance Criteria

*From GDD `design/gdd/main-menu.md`, scoped to this story:*

- [ ] GIVEN 玩家在标题画面点击"新游戏"，WHEN auto_save 存在 → 弹出确认对话框"开始新游戏将覆盖当前进度。确定继续？" → 确认 → ChapterManager.StartNewGame() 被调用 → CrossChapterTracker.InitializeAllFlags() → SceneManager.LoadScene("InGame") → Ch01 开始。若无 auto_save → 直接 StartNewGame()（无对话框）。

- [ ] GIVEN 玩家点击"继续"（auto_save 存在），WHEN 流程执行，THEN SaveManager.LoadAsync("auto_save") 被调用 → Checksum 校验通过 → ChapterManager.LoadAndRestore(saveData) → SceneManager.LoadScene("InGame") → 恢复到存档位置。

- [ ] GIVEN "继续"点击后 auto_save 版本不兼容（迁移失败），WHEN LoadAsync 抛出异常，THEN 错误提示"存档与新版本不兼容"显示 → 返回标题画面。不崩溃。

- [ ] GIVEN 玩家点击"退出"（在标题画面），WHEN 在 Editor 中 → EditorApplication.ExitPlaymode()；WHEN 在 Release Build 中 → Application.Quit()。

- [ ] GIVEN 面板栈深度 > 1（子面板打开），WHEN 玩家连按两次 Escape，THEN 第一次 Escape PopPanel 关闭子面板，第二次 Escape 在栈只剩标题画面时弹出退出确认对话框。两次 Escape 间距 < 0.05s 时第二次被忽略（UI 框架 Transitioning 拦截）。

---

## Implementation Notes

*Derived from GDD rules 7-8 + ADR-0011:*

### 新游戏流程

```csharp
async void HandleNewGame()
{
    if (SaveManager.HasAnySave())
    {
        // Show confirmation dialog first
        ShowConfirmDialog(ConfirmScenario.NewGame, () =>
        {
            _ = StartNewGameAsync();
        });
    }
    else
    {
        await StartNewGameAsync();
    }
}

async Task StartNewGameAsync()
{
    // Fade out title screen (0.3s)
    await _titleScreen.FadeOutAsync(300);

    // Initialize new game state
    CrossChapterTracker.InitializeAllFlags(); // ADR-0011
    ChapterManager.StartNewGame();

    // Load game scene
    await SceneManager.LoadSceneAsync("InGame");
    // SceneFader handles transition (ADR-0004)
}
```

### 继续流程

```csharp
async void HandleContinue()
{
    try
    {
        await LoadGameAsync("auto_save");
    }
    catch (VersionMigrationException e)
    {
        // Show error and return to title
        ShowError("存档与新版本不兼容");
        // Stay on title screen
    }
}

async Task LoadGameAsync(string slotId)
{
    // Fade out current view (0.3s)
    await _titleScreen.FadeOutAsync(300);

    // Load save data
    SaveData saveData = await SaveManager.LoadAsync(slotId);
    // LoadAsync internally: deserialize JSON → SHA-256 checksum verify → version migration

    // Restore game state
    await ChapterManager.LoadAndRestore(saveData);

    // Load game scene
    await SceneManager.LoadSceneAsync("InGame");
    Time.timeScale = 1f; // Ensure unpaused
}
```

### 退出流程

```csharp
void HandleQuit()
{
#if UNITY_EDITOR
    EditorApplication.ExitPlaymode();
#else
    Application.Quit();
#endif
}
```

### 面板栈管理

所有子面板 Push/Pop 委托 UI 框架:

```csharp
// 标题画面打开设置
void HandleSettings()
{
    UIPanelStack.PushPanel("settings-panel");
    // Stack: [#title-screen, #settings-panel]
}

// 设置面板关闭 (Escape 或返回按钮)
void HandleSettingsBack()
{
    UIPanelStack.PopPanel();
    // Stack: [#title-screen]
    // Focus returns to the button that opened settings
}
```

面板栈状态和 Escape 键行为:

| 栈状态 | Escape 行为 |
|--------|-----------|
| [#title-screen] | 弹出退出确认对话框 |
| [#title-screen, #settings] | PopPanel → [#title-screen] |
| [#title-screen, #save-load] | PopPanel → [#title-screen] |
| [#title-screen, ..., #modal-dialog] | PopPanel (Cancel → 关闭对话框) |

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 标题画面按钮定义 + 暂停按钮回调
- Story 002: 设置面板 + 模态确认对话框渲染
- Story 003: 存档管理面板渲染 + Save/Load 槽位交互
- 章节管理 (#15): StartNewGame、LoadAndRestore 实现
- 跨章节状态 (#16): InitializeAllFlags 实现
- 存档系统 (#7): SaveAsync/LoadAsync 实现、Checksum 校验、版本迁移
- 场景管理 (#6): LoadSceneAsync、SceneFader 过渡动画

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: New game flow — with existing save
  - Given: Title screen; auto_save exists; click "新游戏"
  - When: Flow executes
  - Then: Modal dialog "开始新游戏将覆盖当前进度。确定继续？" appears; confirm → ChapterManager.StartNewGame() called; CrossChapterTracker.InitializeAllFlags() called; SceneManager.LoadScene("InGame") called; cancel → dialog closes, title screen unchanged
  - Edge cases: No save exists → StartNewGame directly, no dialog

- **AC-2**: Continue — successful load
  - Given: Title screen; auto_save exists and is valid; click "继续"
  - When: Flow executes
  - Then: SaveManager.LoadAsync("auto_save") called; ChapterManager.LoadAndRestore(saveData) called with loaded SaveData; SceneManager.LoadScene("InGame") called; game restores to saved state
  - Edge cases: auto_save missing → "继续" button hidden (Story 001 AC-2); no need to reach this handler

- **AC-3**: Continue — version migration failure
  - Given: Title screen; auto_save exists but version incompatible; LoadAsync throws VersionMigrationException
  - When: Click "继续"
  - Then: Exception caught; error text "存档与新版本不兼容" displayed; player stays on title screen; no scene load; no crash
  - Edge cases: Non-version error (e.g., corrupted file) → generic error shown, similar non-crash behavior

- **AC-4**: Quit — Editor vs Build
  - Given: Title screen; click "退出"
  - When: Handler executes
  - Then: In Editor → EditorApplication.ExitPlaymode() called; in Release build → Application.Quit() called
  - Edge cases: Neither path throws; compile-time #if ensures correct method is called

- **AC-5**: Panel stack — Escape key behavior with sub-panels
  - Given: Title screen with settings panel open (stack depth 2); press Escape
  - When: First Escape
  - Then: PopPanel → settings closes, title screen visible; press Escape again → exit confirmation dialog appears
  - Edge cases: Rapid double-Escape (<0.05s) → second Escape ignored during UI framework Transitioning state

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/main-menu/game_flow_integration_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/main-menu/game_flow_integration_test.cs` (22 tests)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Files**: `src/core/MainMenuController.cs` (game flow sections), `tests/integration/main-menu/game_flow_integration_test.cs`
- **Deviations**: None — implementation matches story spec exactly
- **Tests**: 22 integration tests covering new game with/without save, continue flow, version migration failure, quit behaviour, panel stack escape logic, confirm scenario mapping

## Dependencies

- Depends on: Story 001 (title screen + pause menu buttons); Story 002 (modal dialog); Story 003 (save/load panel); 章节管理 Story 001 (StartNewGame, LoadAndRestore); 存档系统 Story 003 (LoadAsync with checksum + migration); 场景管理 Story 002 (LoadSceneAsync)
- Unlocks: None (final story in MainMenu epic)
