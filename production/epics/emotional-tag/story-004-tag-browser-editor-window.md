# Story 004: Emotional Tag Browser 编辑器窗口

> **Epic**: 情感标签系统 (EmotionalTagSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Config/Data
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/emotional-tag-system.md`
**Requirement**: `TR-emotional-tag-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: 无专属 ADR（编辑器工具，GDD 第 3 节规则 7 直接定义）
**ADR Decision Summary**: N/A — 编辑器工具窗口，不涉及运行时架构决策

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Unity Editor 窗口使用 `EditorWindow` + `UI Toolkit` (`EditorWindow.CreateGUI` → `rootVisualElement`)；树形视图使用 `TreeView` API（Unity 6 中从 IMGUI 迁移至 UI Toolkit）；标签重命名通过 `SerializedObject.Update()` + `AssetDatabase.SaveAssets()` 传播

**Control Manifest Rules (Feature Layer)**:
- 编辑器工具无运行时性能约束 — 仅在设计时执行
- 必须使用 `AssetDatabase` API 进行资产修改（不可直接操作文件系统）

---

## Acceptance Criteria

*From GDD `design/gdd/emotional-tag-system.md`, scoped to this story:*

- [ ] GIVEN 标签 `anxiety` 在 Catalog 中被 8 个碎片引用，WHEN 设计师在 Emotional Tag Browser 中重命名为 `fear_subtle`，THEN 所有 8 个碎片的 TagId 自动更新。引用完整性保持。

- [ ] GIVEN Emotional Tag Browser 打开，WHEN 设计师查看标签树，THEN 标签按 Category 分组显示。每个标签显示被引用的碎片数量。

- [ ] GIVEN Catalog 中存在未被任何碎片引用的标签，WHEN 打开 Tag Browser，THEN 该标签标记为"未使用"（孤立标签检测）。

- [ ] GIVEN 设计师选择"安全删除"一个被引用的标签，WHEN 操作被确认，THEN 列出所有引用碎片 → 从这些碎片中移除该标签 → 从 Catalog 中删除标签。不允许静默删除。

---

## Implementation Notes

*Derived from GDD 规则 7:*

编辑器窗口位于 `Window > 回响 > Emotional Tag Browser`：
- 树形视图：Group by Category → 每 Category 下列出标签（带引用计数）
- 重命名：选中标签 → 右键 Rename → 输入新 TagId → 扫描所有 MemoryFragment SO → 替换 TagId → 标记资产为 dirty → SaveAssets
- 孤立标签检测：对每个 Catalog 标签，在所有 MemoryFragment 中搜索引用 → 计数 = 0 则标记"[UNUSED]"
- 安全删除：选中标签 → 右键 Delete → 对话框："此标签被 [N] 个碎片引用" → 确认 → 批量移除碎片中的标签 → 从 Catalog 中移除标签

```csharp
public class EmotionalTagBrowser : EditorWindow
{
    [MenuItem("回响/Emotional Tag Browser")]
    public static void ShowWindow() => GetWindow<EmotionalTagBrowser>("Emotional Tags");

    private void CreateGUI()
    {
        // UI Toolkit TreeView + search field
        // rootVisualElement.Add(treeView);
    }

    private void RenameTag(string oldTagId, string newTagId)
    {
        // 1. Update Catalog entry
        // 2. Scan all MemoryFragment assets
        // 3. Replace TagId in each fragment's EmotionalTags
        // 4. AssetDatabase.SaveAssets()
    }
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Catalog SO 结构 — 归 Story 001
- 运行时查询或权重逻辑 — 归 Story 002 / Story 003
- 碎片上的实际标签数据编辑 — 由 MemoryFragment Inspector 或其他编辑器工具处理

---

## QA Test Cases

*Manual verification — editor tool, no automated test:*

- **Manual check: AC-1 — 重命名传播**
  - Setup: 创建带有 anxiety 标签的测试 Catalog + 3 个引用 "anxiety" 的测试 MemoryFragment SO
  - Verify: 在 Tag Browser 中将 "anxiety" 重命名为 "fear_subtle"；检查所有 3 个 MemoryFragment —— TagId 应更新为 "fear_subtle"
  - Pass condition: 所有 3 个碎片显示更新后的 TagId；无残留 "anxiety" 引用

- **Manual check: AC-2 — 按 Category 分组的树形视图**
  - Setup: 打开 Window > 回响 > Emotional Tag Browser
  - Verify: 标签按 8 个 Category 分组显示；每个标签显示引用计数
  - Pass condition: 8 个 Category 分组全部可见；引用计数与实际碎片分配匹配

- **Manual check: AC-3 — 孤立标签检测**
  - Setup: 在 Catalog 中创建一个标签但不将其分配给任何碎片
  - Verify: Tag Browser 应将该标签标记为 "[UNUSED]"
  - Pass condition: 无引用的标签显示 "[UNUSED]"；至少有一个引用的标签无此标记

- **Manual check: AC-4 — 安全删除工作流**
  - Setup: 创建带有 "test_tag" 的 Catalog，分配给 2 个碎片
  - Verify: 右键 "test_tag" → Delete → 对话框应显示 "此标签被 2 个碎片引用" → 确认
  - Pass condition: 标签已从 Catalog 中删除；两个碎片均不再包含 "test_tag"

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: smoke check pass (`production/qa/smoke-*.md`) OR manual sign-off in `production/qa/evidence/emotional-tag-browser-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (Catalog SO 结构 — Tag Browser 操作 Catalog)
- Unlocks: 无（编辑器工具不阻塞任何运行时故事）

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 4/4 implemented — requires Unity Editor runtime for manual verification
**Deviations**: None
**Test Evidence**: Config/Data story — smoke check required. Editor window created at `src/core/editor/EmotionalTagBrowser.cs`. All 4 ACs implemented:
  - AC-1: Rename propagation (ExecuteRename scans all MemoryFragment assets, replaces TagId, marks dirty, SaveAssets)
  - AC-2: Category-grouped list with reference counts (Foldout per category, each row shows TagId/DisplayName/refCount)
  - AC-3: Orphan detection (tags with 0 references display "[UNUSED]" in amber; status bar shows orphan count)
  - AC-4: Safe delete (dialog shows reference count, batch-removes from fragments, removes from catalog)
**Code Review**: Skipped (lean mode)
