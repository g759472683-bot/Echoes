# Story 002: 设置面板 + 模态确认对话框

> **Epic**: 主菜单与菜单系统 (MainMenu)
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/main-menu.md`
**Requirement**: `TR-main-menu-001` (#settings-panel + #modal-dialog), `TR-main-menu-006`, `TR-main-menu-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: UI Toolkit 构建设置面板——4 个音量滑块 (Master/SFX/Music/Ambience) + 语言下拉框，值实时应用到 AudioManager.SetVolume/LocaleSettings。模态确认对话框 PushPanel 到栈顶，覆盖 5 种确认场景——所有消息通过本地化系统获取。完整键盘导航——Arrow Keys/Tab 移动焦点、Enter 确认、Escape 返回/Cancel。滑块聚焦时 Arrow Left/Right 调整值 ±0.05。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Slider 控件原生支持 Arrow Key 步进。DropdownField 支持 Arrow Up/Down 切换选项。音量值保存到 PlayerPrefs（Unity 标准持久化）。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit for all runtime UI — source: ADR-0006
- Required: LIFO panel stack — source: ADR-0006
- Forbidden: Never hardcode player-facing strings — use LocalizationManager.GetLocalizedString() — source: ADR-0015

---

## Acceptance Criteria

*From GDD `design/gdd/main-menu.md`, scoped to this story:*

- [ ] GIVEN 设置面板打开（从标题画面或暂停菜单），WHEN 玩家拖动"音乐音量"滑块到 0.5，THEN AudioManager.SetVolume("music", 0.5) 实时生效。值保存到 PlayerPrefs。拖动过程中音量即时变化。

- [ ] GIVEN 设置面板打开，WHEN 玩家在语言下拉中选择"English"，THEN LocaleSettings.SelectedLocale 切换到 English → 所有可见面板的 LocalizedString 文本更新为英语。设置面板自身也刷新（标签变为英语）。语言选择保存到 PlayerPrefs。

- [ ] GIVEN 设置面板打开，WHEN 玩家按 Escape 或点击"返回"，THEN PopPanel 关闭设置面板——回到标题画面或暂停菜单。设置是即时应用的——没有"保存设置"按钮。

- [ ] GIVEN 模态确认对话框打开（5 种场景任一），WHEN 玩家点击"确定"（Enter），THEN 待确认操作执行 → PopPanel ×2（对话框 + 触发面板）。点击"取消"（Escape）→ PopPanel ×1（仅关闭对话框）。

- [ ] GIVEN 任何菜单面板打开，WHEN 玩家使用 Tab 或 Arrow Keys，THEN 焦点在可交互元素间移动。所有按钮支持 Enter 确认。滑块支持 Arrow Left/Right 调整。下拉框支持 Arrow Up/Down 切换。

---

## Implementation Notes

*Derived from GDD rules 4, 6 + ADR-0006:*

### Settings Panel 结构

```
#settings-panel
├── #audio-section
│   ├── #slider-master       // Slider 0.0–1.0, 默认 0.8
│   ├── #slider-sfx          // Slider 0.0–1.0, 默认 0.7
│   ├── #slider-music        // Slider 0.0–1.0, 默认 0.6
│   └── #slider-ambience     // Slider 0.0–1.0, 默认 0.5
├── #language-section
│   └── #dropdown-language   // "中文" / "English"
└── #btn-settings-back
```

### 音量滑块实现

```csharp
void SetupVolumeSliders()
{
    SetupSlider("#slider-master", "Master", 0.8f);
    SetupSlider("#slider-sfx", "SFX", 0.7f);
    SetupSlider("#slider-music", "Music", 0.6f);
    SetupSlider("#slider-ambience", "Ambience", 0.5f);
}

void SetupSlider(string elementId, string category, float defaultValue)
{
    var slider = _uiDocument.rootVisualElement.Q<Slider>(elementId);
    // Load saved value from PlayerPrefs, or defaultValue
    slider.value = PlayerPrefs.GetFloat($"volume_{category}", defaultValue);
    slider.RegisterValueChangedCallback(evt =>
    {
        AudioManager.SetVolume(category, evt.newValue);
        PlayerPrefs.SetFloat($"volume_{category}", evt.newValue);
    });
}
```

- 音量值是线性的 0.0–1.0
- AudioManager 内部做 linear-to-dB 转换（ADR-0013）
- Arrow Key 步进 0.05（Slider 控件默认键盘步进）

### 语言切换实现

```csharp
void SetupLanguageDropdown()
{
    var dropdown = _uiDocument.rootVisualElement.Q<DropdownField>("#dropdown-language");
    dropdown.choices = new List<string> { "中文", "English" };
    dropdown.value = LocaleSettings.SelectedLocale.LocaleName;

    dropdown.RegisterValueChangedCallback(evt =>
    {
        var newLocale = evt.newValue switch
        {
            "English" => "en",
            _ => "zh-Hans"
        };
        LocaleSettings.SelectedLocale = LocalizationSettings.AvailableLocales
            .GetLocale(newLocale);
        PlayerPrefs.SetString("selected_locale", newLocale);
        // All visible panels auto-refresh via OnLocaleChanged event (ADR-0015)
    });
}
```

### 模态确认对话框

```
#modal-dialog
├── #modal-message          // Label — 确认消息文本
├── #btn-modal-confirm      // Button — "确定" (墨色实心)
└── #btn-modal-cancel       // Button — "取消" (淡墨空心)
```

5 种确认场景——消息 Key 映射:

```csharp
public enum ConfirmScenario
{
    NewGame,        // menu.confirm.new_game → "开始新游戏将覆盖当前进度。确定继续？"
    OverwriteSave,  // menu.confirm.overwrite → "覆盖此存档？此操作不可撤销。"
    LoadInGame,     // menu.confirm.load_in_game → "加载此存档？当前未保存的进度将丢失。"
    ReturnToTitle,  // menu.confirm.return_to_title → "返回标题画面？未保存的进度将丢失。"
    Quit            // menu.confirm.quit → "退出游戏？"
}

void ShowConfirmDialog(ConfirmScenario scenario, Action onConfirm)
{
    var message = _uiDocument.rootVisualElement.Q<Label>("#modal-message");
    message.text = LocalizationManager.GetLocalizedString(GetMessageKey(scenario));

    var btnConfirm = _uiDocument.rootVisualElement.Q<Button>("#btn-modal-confirm");
    btnConfirm.clicked += () =>
    {
        onConfirm?.Invoke();
        UIPanelStack.PopPanel(); // Pop dialog
        UIPanelStack.PopPanel(); // Pop triggering panel
    };

    var btnCancel = _uiDocument.rootVisualElement.Q<Button>("#btn-modal-cancel");
    btnCancel.clicked += () => UIPanelStack.PopPanel(); // Pop dialog only

    UIPanelStack.PushPanel("modal-dialog");
    btnCancel.Focus(); // Default focus on cancel (safe option)
}
```

### 键盘导航

- Enter = Confirm / 触发聚焦按钮
- Escape = Cancel / PopPanel
- Tab = 在焦点组间移动
- Arrow Keys = 滑块调整 / 下拉切换 / 按钮间移动
- 面板打开时自动聚焦到第一个可交互元素

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 标题画面 (#title-screen) + 暂停菜单 (#pause-menu)
- Story 003: 存档管理面板 (#save-load-panel)
- Story 004: 游戏流程集成（新游戏/继续/加载的完整调用链）
- 音频系统 (#3): SetVolume 实现、linear-to-dB 转换
- 本地化 (#4): Unity Localization Package 集成、StringTable 构建

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Volume slider — real-time adjustment
  - Setup: Settings panel open; Music slider at default 0.6; AudioManager mock/spy ready
  - Verify: Drag slider to 0.5 → AudioManager.SetVolume("Music", 0.5) called immediately; value written to PlayerPrefs "volume_music" = 0.5
  - Pass condition: Real-time volume change + PlayerPrefs persistence

- **AC-2**: Language switch — all panels refresh
  - Setup: Settings panel open, current locale zh-Hans; select "English" from dropdown
  - Verify: LocaleSettings.SelectedLocale switches to "en"; OnLocaleChanged event fires; all visible panel text updates to English; settings panel self-refreshes; PlayerPrefs "selected_locale" = "en"
  - Pass condition: Language switch propagates to all visible panels

- **AC-3**: Settings back — no save button
  - Setup: Settings panel open from title screen; adjust Master volume to 0.3; click "返回" or Escape
  - Verify: PopPanel() called; title screen shows; Master volume stays at 0.3 (not reverted); no "保存设置" button exists
  - Pass condition: Settings applied immediately, persisted, no explicit save action

- **AC-4**: Modal confirm dialog — confirm and cancel flows
  - Setup: New game flow triggered with auto_save present → modal dialog shows "开始新游戏将覆盖当前进度"
  - Verify: Click "确定" → onConfirm action executes → PopPanel ×2 (dialog + triggering panel); Click "取消" → PopPanel ×1 (dialog only); Escape key = cancel; Enter key = confirm
  - Pass condition: Both confirm and cancel paths work; correct pop count each path

- **AC-5**: Full keyboard navigation
  - Setup: Settings panel open; only keyboard available
  - Verify: Tab/keyboard arrows move focus between all controls; Enter triggers focused button; Arrow Left/Right adjusts slider by ±0.05; Arrow Up/Down switches dropdown options; Escape = back/pop
  - Pass condition: All interactive elements reachable and operable via keyboard alone

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/settings-panel-modal-dialog-evidence.md` — manual walkthrough doc or interaction test

**Status**: [x] Created — `production/qa/evidence/settings-panel-modal-dialog-evidence.md`

---

## Completion Notes

- **Completed**: 2026-05-19
- **Files**: `src/core/MainMenuController.cs` (settings + modal dialog sections), `assets/uxml/main-menu.uxml` (#settings-panel + #modal-dialog), `assets/uss/main-menu.uss`
- **Deviations**: None — implementation matches story spec exactly
- **Test Evidence**: Evidence template written; manual walkthrough pending QA sign-off

## Dependencies

- Depends on: Story 001 (MainMenu UIDocument + title screen + pause menu); UI 框架 Story 002 (UIPanelStack PushPanel/PopPanel); 音频系统 Story 001 (AudioManager.SetVolume); 本地化 Story 001 (OnLocaleChanged event)
- Unlocks: Story 004 (game flow integration uses settings + modal)

