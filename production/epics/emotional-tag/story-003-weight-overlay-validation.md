# Story 003: 运行时权重叠加 + 编辑器验证

> **Epic**: 情感标签系统 (EmotionalTagSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/emotional-tag-system.md`
**Requirement**: `TR-emotional-tag-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: 运行时权重 = BaseWeight × ModifyTagWeight overlay merge（ModOp: Add/Multiply/Set），Clamp [0.0, 1.0]；编辑器验证规则（IsPrimary 互斥、空标签列表、循环层级）在构建时执行

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Overlay merge 通过 Dictionary<TagId, ModOp> 实现；编辑器验证使用 `[MenuItem]` 或 `AssetPostprocessor` 触发；ModOp 枚举在 link.xml 中为 IL2CPP 显式保留

**Control Manifest Rules (Feature Layer)**:
- Required: Overlay merge by OrderIndex ascending — later changes override earlier for same fields — source: ADR-0007
- Forbidden: Never directly modify ScriptableObject fields at runtime — use overlay — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/emotional-tag-system.md`, scoped to this story:*

- [ ] GIVEN 玩家在碎片 C 中做了一个选择触发了 ModifyTagWeight（`nostalgia` +0.2），WHEN 调用 `GetTagsForFragment("frag_C")`，THEN `nostalgia` 的返回权重 = BaseWeight + 0.2（夹紧到 [0.0, 1.0]）。

- [ ] GIVEN 碎片 B 的 `hope` 和 `despair` 都被设为 IsPrimary=true（且 hope.IncompatibleWith 包含 despair），WHEN 编辑器验证运行，THEN 报错——"标签 hope 和 despair 互斥，不能同时为主标签"。

- [ ] GIVEN 碎片 D 的 EmotionalTags 列表为空（设计错误），WHEN 编辑器验证运行，THEN 报错"碎片 [D] 无情感标签"。运行时若发生 → 记录 Warning，关联引擎跳过此碎片。

- [ ] GIVEN ModifyTagWeight 将标签权重推到 [0.0, 1.0] 范围外，WHEN 查询运行时权重，THEN 结果夹紧到 0.0 或 1.0。

- [ ] GIVEN TagId 引用不存在于 Catalog 中的标签，WHEN 验证运行，THEN 报错"标签 [id] 不存在于 EmotionalTagCatalog"。运行时跳过该无效标签，记录 Warning。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

权重合并逻辑：
```csharp
public float GetEffectiveWeight(string fragmentId, string tagId, float baseWeight)
{
    float weight = baseWeight;
    var overlays = _changeTracker.GetOverlays(fragmentId)
        .Where(o => o is ModifyTagWeight)
        .Cast<ModifyTagWeight>()
        .Where(m => m.TagId == tagId)
        .OrderBy(m => m.OrderIndex);

    foreach (var mod in overlays)
    {
        weight = mod.Operation switch
        {
            ModOp.Add => weight + mod.Value,
            ModOp.Multiply => weight * mod.Value,
            ModOp.Set => mod.Value,
            _ => weight
        };
    }
    return Mathf.Clamp(weight, 0.0f, 1.0f);
}
```

编辑器验证规则（`[MenuItem("回响/Validate/Emotional Tags")]` 或 AssetPostprocessor）：
1. 每个碎片至少 1 个标签
2. IncompatibleWith 对不能同时为 IsPrimary
3. TagId 必须存在于 Catalog 中
4. ParentTagId 不能形成循环（A→B→A）
5. 标签分配不能超过每碎片最大数（5）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 基本查询 API（GetTagsForFragment 等）——本故事在其基础上叠加 overlay
- ChangeTracker overlay 存储本身 — 归 memory-change-tracking 史诗 (#12)
- Editor Tag Browser 工具窗口 — 归 Story 004

---

## QA Test Cases

- **AC-1**: ModifyTagWeight overlay 合并
  - Given: 碎片 frag_C 有标签 nostalgia BaseWeight=0.5；overlay 中有 ModifyTagWeight(nostalgia, Add, +0.2)
  - When: 调用 GetEffectiveWeight("frag_C", "nostalgia", 0.5)
  - Then: 返回 0.7
  - Edge cases: 多个 overlay 按 OrderIndex 升序应用；Clamp 测试：BaseWeight=0.9 + Add 0.3 → 1.0

- **AC-2**: IsPrimary 互斥验证
  - Given: 碎片有 hope(IsPrimary=true) 和 despair(IsPrimary=true)，且 hope.IncompatibleWith 包含 "despair"
  - When: 编辑器验证运行
  - Then: 错误消息包含"标签 hope 和 despair 互斥，不能同时为主标签"
  - Edge cases: 互斥对作为非主标签共存 → 不报错（允许复杂情感）

- **AC-3**: 空标签验证
  - Given: 碎片有空的 EmotionalTags 列表
  - When: 编辑器验证运行
  - Then: 错误消息包含"碎片 [id] 无情感标签"
  - Edge cases: 运行时遇到空标签碎片 → Warning 日志，关联引擎跳过

- **AC-4**: 权重 Clamp
  - Given: BaseWeight=1.2（超出范围）或 overlay 推出范围
  - When: GetEffectiveWeight 被调用
  - Then: 结果夹紧到 [0.0, 1.0]
  - Edge cases: 负权重 → Clamp 到 0.0；NaN → Clamp 到 0.0

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/emotional-tag/weight_overlay_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (查询 API — 权重合并扩展 GetTagsForFragment)；memory-change-tracking Story 001 (ChangeTracker overlay 存储)
- Unlocks: 下游系统（关联引擎 #13、微动画 #9）可使用运行时权重进行查询

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 4/4 passing (all auto-verified by ~30 integration tests)
**Deviations**: Proceeded with incomplete dependency (memory-change-tracking S001 — ChangeTracker overlay storage not yet built). IOverlayProvider DI abstraction makes weight merge logic independently testable with MockOverlayProvider.
**Test Evidence**: `tests/integration/emotional-tag/weight_overlay_test.cs` — ~30 tests covering AC-1 (Add/Multiply/Set/OrderIndex), AC-2 (IsPrimary incompatible pair), AC-3 (empty/null/invalid TagId), AC-4 (clamp overflow/underflow/NaN/Infinity/intermediate)
**Code Review**: Skipped (lean mode)
