# Story 003: 存档管理面板（Save/Load 双模式）

> **Epic**: 主菜单与菜单系统 (MainMenu)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/main-menu.md`
**Requirement**: `TR-main-menu-001` (#save-load-panel), `TR-main-menu-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架 + ADR-0003: 存档
**ADR Decision Summary**: 存档管理面板是同一个 VisualElement 树，通过 `_saveLoadMode` 枚举切换 Save/Load 两种模式——同一面板，不同标题和按钮行为。3 个槽位 (save_01/save_02/auto_save)，元数据通过 SaveManager.GetSlotMetaData(slotId) 获取（不加载完整存档）。Save 模式：空槽直接保存，已有存档弹出覆盖确认。Load 模式：空槽不可交互，已有存档点击后确认加载。槽位显示时间戳、章节名、游玩时长。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: SlotMetaData 是纯 C# struct——不依赖 Unity 特定序列化。时间戳格式化使用 C# 标准 DateTime。游玩时长格式化："1h 23m"（<1h 时省略小时部分）。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit for all runtime UI — source: ADR-0006
- Required: JSON + SHA-256 checksum for save files — source: ADR-0003
- Forbidden: Never hardcode player-facing strings — use LocalizationManager — source: ADR-0015

---

## Acceptance Criteria

*From GDD `design/gdd/main-menu.md`, scoped to this story:*

- [ ] GIVEN 暂停菜单中进入 Save 模式（点击"保存游戏"），WHEN 存档管理面板打开，THEN 标题显示"保存游戏"。3 个槽位展示各自状态——空槽显示"— 空 —"（淡墨色），已有存档槽位显示时间戳 + 章节名 + 游玩时长。

- [ ] GIVEN Save 模式面板中，WHEN 玩家点击空槽位，THEN 直接保存（SaveManager.SaveAsync(slotId) 执行）。保存完成显示"保存完成"提示 → PopPanel 回到暂停菜单。

- [ ] GIVEN Save 模式面板中，WHEN 玩家点击已有存档的槽位，THEN PushPanel → #modal-dialog "覆盖此存档？此操作不可撤销。" → 确认 → SaveAsync 执行 → PopPanel ×2 回到暂停菜单。

- [ ] GIVEN 标题画面中进入 Load 模式（点击"加载游戏"），WHEN 面板打开，THEN 标题显示"加载游戏"。空槽显示"— 空 —"且不可交互（pickingMode: Ignore）。已有存档槽位可点击——直接调用 LoadAsync（无"未保存进度"问题）。

- [ ] GIVEN 暂停菜单中进入 Load 模式，WHEN 玩家点击已有存档槽位，THEN PushPanel → #modal-dialog "加载此存档？当前未保存的进度将丢失。" → 确认 → LoadAsync 执行。

---

## Implementation Notes

*Derived from GDD rule 5 + ADR-0003:*

### Save/Load 双模式

```csharp
public enum SaveLoadMode { Save, Load }

public class SaveLoadPanelController
{
    private SaveLoadMode _mode;
    private VisualElement _panel;
    private string[] _slotIds = { "save_01", "save_02", "auto_save" };

    public void Show(SaveLoadMode mode)
    {
        _mode = mode;
        _panel = _uiDocument.rootVisualElement.Q("#save-load-panel");

        // Set title
        var titleLabel = _panel.Q<Label>("#panel-title");
        titleLabel.text = mode == SaveLoadMode.Save ? "保存游戏" : "加载游戏";

        // Render slots
        RenderSlots();

        UIPanelStack.PushPanel("save-load-panel");
    }

    void RenderSlots()
    {
        var slotList = _panel.Q("#slot-list");
        slotList.Clear();

        foreach (var slotId in _slotIds)
        {
            var meta = SaveManager.GetSlotMetaData(slotId);
            var slotEl = CreateSlotElement(slotId, meta);
            slotList.Add(slotEl);
        }
    }

    VisualElement CreateSlotElement(string slotId, SlotMetaData meta)
    {
        var slot = new VisualElement();
        slot.AddToClassList("save-slot");

        if (meta.IsEmpty)
        {
            var emptyLabel = new Label("— 空 —");
            emptyLabel.AddToClassList("slot-empty");
            slot.Add(emptyLabel);

            if (_mode == SaveLoadMode.Load)
                slot.pickingMode = PickingMode.Ignore; // 空槽不可交互
            else
                slot.RegisterCallback<ClickEvent>(_ => SaveToSlot(slotId));
        }
        else
        {
            // 已占用槽位
            slot.Add(new Label($"槽位: {GetSlotLabel(slotId)}"));
            slot.Add(new Label(meta.Timestamp));          // "2026年5月12日 14:30"
            slot.Add(new Label(meta.ChapterNameKey));      // 本地化章节名
            slot.Add(new Label(FormatPlayTime(meta.PlayTimeSeconds))); // "1h 23m"
            slot.RegisterCallback<ClickEvent>(_ => HandleSlotClick(slotId, meta));
        }

        return slot;
    }
}
```

### Save/Load 按钮行为差异

| 操作 | Save 模式 | Load 模式 (标题) | Load 模式 (暂停) |
|------|----------|-----------------|-----------------|
| 空槽 | 直接保存 → "保存完成" → PopPanel | 不可交互 | 不可交互 |
| 已占用槽 | 确认对话框 → 保存 → PopPanel ×2 | 直接 LoadAsync | 确认对话框 → LoadAsync |

### SlotMetaData 结构 (来自 ADR-0003)

```csharp
public struct SlotMetaData
{
    public string SlotId;
    public bool IsEmpty;
    public string Timestamp;          // ISO 8601
    public string ChapterNameKey;     // 本地化 Key
    public int VisitedFragmentCount;
    public int TotalFragments;
    public int PlayTimeSeconds;
}
```

### 保存/加载调用

```csharp
async void SaveToSlot(string slotId)
{
    try
    {
        await SaveManager.SaveAsync(slotId);
        // Show brief "保存完成" toast
        ShowToast("保存完成");
        UIPanelStack.PopPanel(); // Close save panel
    }
    catch (Exception e)
    {
        // Disk full or other I/O error
        ShowError($"保存失败：{e.Message}");
        // Panel stays open for retry
    }
}

async void LoadFromSlot(string slotId)
{
    var saveData = await SaveManager.LoadAsync(slotId);
    await ChapterManager.LoadAndRestore(saveData);
    SceneManager.LoadScene("InGame");
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 标题画面 + 暂停菜单按钮（"保存游戏"/"加载游戏"按钮的 PushPanel 调用）
- Story 002: 模态确认对话框 ShowConfirmDialog 实现
- Story 004: LoadAndRestore 完整流程、场景切换
- 存档系统 (#7): SaveAsync/LoadAsync/GetSlotMetaData 实现
- 本地化 (#4): 槽位标签、章节名的本地化字符串

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Save mode — slot display
  - Setup: Open save panel from pause menu; save_01 has data ("第一章 · 童年", timestamp "2026-05-12 14:30", playtime 4980s → "1h 23m"); save_02 and auto_save empty
  - Verify: Title "保存游戏"; slot 1 shows timestamp + chapter + playtime; slots 2 & 3 show "— 空 —" in light ink; empty slots are clickable
  - Pass condition: All 3 slots rendered correctly with distinct empty/occupied states

- **AC-2**: Save mode — empty slot saves directly
  - Setup: Save mode open; click empty save_02 slot
  - Verify: SaveManager.SaveAsync("save_02") called; toast "保存完成" shown; PopPanel returns to pause menu
  - Pass condition: Complete save flow without confirmation dialog

- **AC-3**: Save mode — overwrite requires confirmation
  - Setup: Save mode open; click occupied save_01 slot
  - Verify: Modal dialog opens with "覆盖此存档？此操作不可撤销。"; confirm → SaveAsync("save_01") → dialog pops → save panel pops; cancel → only dialog pops, slot remains clickable
  - Pass condition: Overwrite guard works; cancel is safe

- **AC-4**: Load mode from title — no confirmation needed
  - Setup: Load mode from title screen; save_01 has data; click save_01
  - Verify: LoadAsync("save_01") called directly (no modal dialog); no "未保存的进度将丢失" message
  - Pass condition: Direct load from title with no extra confirmation

- **AC-5**: Load mode from pause — confirmation needed
  - Setup: Load mode from pause menu; click occupied save_01
  - Verify: Modal dialog "加载此存档？当前未保存的进度将丢失。" → confirm → LoadAsync("save_01"); cancel → only dialog pops
  - Pass condition: In-game load with correct confirmation message; cancel is safe

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/main-menu/save_load_panel_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/main-menu/save_load_panel_test.cs` (20 tests)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Files**: `src/core/MainMenuController.cs` (save/load panel sections), `assets/uxml/main-menu.uxml` (#save-load-panel), `assets/uss/main-menu.uss`, `tests/integration/main-menu/save_load_panel_test.cs`
- **Deviations**: None — implementation matches story spec exactly
- **Tests**: 20 integration tests covering slot rendering, Save/Load dual mode, overwrite confirm, empty slot interaction, metadata formatting

## Dependencies

- Depends on: Story 001 (MainMenu UIDocument + title/pause menu); Story 002 (modal dialog); 存档系统 Story 002 (GetSlotMetaData, SaveAsync, LoadAsync)
- Unlocks: Story 004 (full game flow integration — new game/continue/load)

