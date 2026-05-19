# Story 001: EmotionalTagCatalog SO 定义 + 启动加载

> **Epic**: 情感标签系统 (EmotionalTagSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/emotional-tag-system.md`
**Requirement**: `TR-emotional-tag-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: ScriptableObject 字段运行时只读；BaseWeight 来自 SO（不可变）；Catalog 是设计时资产，运行时通过 Addressables 加载

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ScriptableObject + Addressables.LoadAssetAsync 均为 Unity 6 标准路径；SO 字段需 `[field: SerializeField]` 标记以支持序列化

**Control Manifest Rules (Feature Layer)**:
- Required: Base SO (immutable) + ChangeTracker._overlay (mutable) two-layer model — source: ADR-0007
- Forbidden: Never directly modify ScriptableObject fields at runtime — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/emotional-tag-system.md`, scoped to this story:*

- [ ] GIVEN EmotionalTagCatalog 定义了 18 个标签（8 个 Category），WHEN 游戏启动完成，THEN Catalog 加载到内存——所有标签的 TagId、Category、DisplayName 可查询。加载时间 < 100ms。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

1. `EmotionalTagCatalog` 继承 `ScriptableObject`，字段通过 `[field: SerializeField]` 标记
2. 运行时只读——所有标签数据（TagId、Category、DisplayName、ParentTagId、IncompatibleWith、AssociatedColors）在 SO 中预定义
3. 通过 Addressables.LoadAssetAsync<EmotionalTagCatalog>() 加载
4. 加载失败 → Error 状态，显示"情感标签数据加载失败"，返回主菜单
5. Category 固定为 8 个枚举值：Joy, Sadness, Love, Fear, Anger, Wonder, Melancholy, Peace

```csharp
[CreateAssetMenu(menuName = "回响/Emotional Tag Catalog")]
public class EmotionalTagCatalog : ScriptableObject
{
    public List<EmotionalTagData> Tags;
}

[System.Serializable]
public struct EmotionalTagData
{
    public string TagId;
    public TableReference DisplayName;
    public EmotionCategory Category;
    public string ParentTagId;      // null for root tags
    public string[] IncompatibleWith;
    public ColorAssociation AssociatedColors;
    public string Description;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 所有 5 个查询 API（GetTagsForFragment、GetPrimaryTag、QueryFragmentsByTag 等）
- Story 003: ModifyTagWeight overlay 合并，编辑器验证规则
- Story 004: Emotional Tag Browser 编辑器工具窗口

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Catalog 启动加载
  - Given: Catalog SO 存在于 Addressables 中，包含 18 个标签
  - When: 游戏启动，Catalog 加载
  - Then: Catalog 非 null；所有标签的 TagId、Category、DisplayName 可查询；加载时间 < 100ms
  - Edge cases: Addressables 加载失败 → Error 状态，错误消息包含"情感标签数据加载失败"；Catalog 为空 → 标记为无效

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/emotional-tag/catalog_loading_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/emotional-tag/catalog_loading_test.cs` (26 tests)

---

## Dependencies

- Depends on: None（Catalog 是独立 SO 资产，无上游代码依赖）
- Unlocks: Story 002 (标签查询 API — needs Catalog loaded)

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 1/1 auto-verified (catalog loading + validation, 26 tests)
**Deviations**: None
**Test Evidence**: `tests/unit/emotional-tag/catalog_loading_test.cs` — 26 tests covering AC-1 (18 tags × 8 categories, load failure, null/empty, duplicates, missing references, ICatalogProvider)
**Code Review**: Skipped (lean mode)
