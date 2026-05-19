# Story 002: GetCurrentState 状态合并算法

> **Epic**: 记忆变化追踪 (ChangeTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-change-tracking.md`
**Requirement**: `TR-memory-change-tracking-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: GetCurrentState 合并 base SO + 所有 overlay 条目（按 OrderIndex 升序），返回 ResolvedFragmentState 不可变快照；6 种 ContentChange 各有独立合并策略

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ResolvedFragmentState 为 struct（栈分配，零 GC）；合并结果不可变——消费者无法修改 base SO 或 overlay

**Control Manifest Rules (Feature Layer)**:
- Required: GetCurrentState returns ResolvedFragmentState struct — immutable snapshot — source: ADR-0007
- Forbidden: Never use `Task.Result` in GetCurrentState — must be async with `await` — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/memory-change-tracking.md`, scoped to this story:*

- [ ] GIVEN 碎片 B 的 VisualLayer "layer_rain" 的 SO 默认值为 Visible=false，且叠加层中有一条 ToggleVisualLayer 将其设为 true，WHEN 调用 `GetCurrentState("frag_B")`，THEN 返回的 ResolvedFragmentState 中 "layer_rain" 的 Visible = true。其他未被修改的图层保持 SO 默认值。

- [ ] GIVEN 碎片 C 的 EmotionalTag "nostalgia" BaseWeight = 0.5，叠加层中有 ModifyTagWeight (delta=+0.3, Add) 和 ModifyTagWeight (delta=×0.8, Multiply)，WHEN GetCurrentState 合并，THEN nostalgia 的运行时权重 = Clamp(Clamp(0.5 + 0.3) × 0.8) = Clamp(0.8 × 0.8) = 0.64。

- [ ] GIVEN 叠加层中已有 Key (frag_A, choice_1)，WHEN 同一选择被再次触发 (IsRepeatable = true) 并传入不同的 ContentChanges，THEN 叠加层中该 Key 的 ContentOverrides 被覆盖为新的变更内容。_changeLog 中两条日志条目各自独立保留。

- [ ] GIVEN UnlockAssociation 的目标关联已在之前的选择中被解锁，WHEN 第二个选择也触发 UnlockAssociation 同一目标，THEN 幂等跳过——ResolvedFragmentState 中该关联仍为已解锁状态，不重复添加。

- [ ] GIVEN ModifyTagWeight 的 delta 和 operation 组合将权重推出 [0.0, 1.0]，WHEN GetCurrentState 合并，THEN 权重被 Clamp 强制夹紧到 0.0 或 1.0。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

```csharp
public ResolvedFragmentState GetCurrentState(string fragmentId)
{
    // 1. Get base SO from IDataManager
    MemoryFragment baseFragment = _dataManager.GetFragment(fragmentId);
    if (baseFragment == null) return null;
    
    // 2. Find all overlay entries targeting this fragment
    var overlays = _overlay
        .Where(kv => kv.Key.targetFragmentId == fragmentId)
        .OrderBy(kv => kv.Value.OrderIndex)
        .Select(kv => kv.Value);
    
    // 3. Create ResolvedFragmentState copy from base
    var resolved = new ResolvedFragmentState(baseFragment);
    
    // 4. Apply each overlay in OrderIndex ascending order
    foreach (var overlay in overlays)
    {
        // ToggleVisualLayer: resolved.VisualLayers[layerId].Visible = value
        //   Skip if layer.IsMutable == false → LogWarning
        // SetObjectState: resolved.InteractiveObjects[objectId].State = newState
        // SetTextContent: resolved.TextFields[fieldId] = newTextKey
        // ModifyTagWeight: apply operation (Add/Multiply/Set) → Clamp [0.0, 1.0]
        // UnlockAssociation: add to resolved.Associations (skip if exists)
    }
    
    return resolved; // immutable struct
}
```

6 种 ContentChange 合并策略：

| ChangeType | 合并策略 | 冲突解决 |
|---|---|---|
| ToggleVisualLayer | 直接覆盖 Visible 值 | 后发生覆盖先发生 |
| SetObjectState | 直接覆盖 State 值 | 后发生覆盖先发生 |
| SetTextContent | 直接覆盖文本字段 | 后发生覆盖先发生 |
| ModifyTagWeight | 按 Operation 在已有权重上操作 → Clamp | 顺序叠加 |
| UnlockAssociation | 添加到集合（幂等） | 重复跳过 |
| SetFlag | 直接写入 _flags（不在 ResolvedState 中） | 后发生覆盖先发生 |

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: _overlay 数据结构、ApplyChanges 算法
- Story 003: Flag 系统、条件求值入口
- Story 004: 存档序列化

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: VisualLayer Toggle 合并
  - Given: 碎片 B 的 SO 定义 layer_rain.Visible=false, layer_sun.Visible=true；_overlay 中有 1 条 ToggleVisualLayer("layer_rain", true)
  - When: 调用 GetCurrentState("frag_B")
  - Then: ResolvedState 中 layer_rain.Visible=true；layer_sun.Visible=true（保持 SO 默认值）
  - Edge cases: 多条 overlay 修改同一 layer → 最后 OrderIndex 的生效

- **AC-2**: ModifyTagWeight 链式叠加
  - Given: 碎片 C 的 nostalgia BaseWeight=0.5；_overlay 中有 2 条 ModifyTagWeight：(+0.3, Add, OrderIndex=1) 和 (×0.8, Multiply, OrderIndex=2)
  - When: GetCurrentState("frag_C")
  - Then: nostalgia 权重 = 0.64（0.5+0.3=0.8, 0.8×0.8=0.64）
  - Edge cases: 推至范围外 → Clamp 到 0.0 或 1.0；负权重 → Clamp 到 0.0

- **AC-3**: Repeatable 选择覆盖
  - Given: _overlay[(frag_A, choice_1)] = ContentOverrides{ ToggledLayers: [("layer_x", true)] }；_changeLog 已有 1 条
  - When: 再次 ApplyChanges("frag_A", "choice_1", newChanges with ToggledLayers: [("layer_x", false)])
  - Then: _overlay[(frag_A, choice_1)] 的 ToggledLayers 变为 [("layer_x", false)]（覆盖）；_changeLog 有 2 条独立日志
  - Edge cases: IsRepeatable=false 的选择理论上不会重复触发（交互系统控制）——ChangeTracker 不判断 IsRepeatable

- **AC-4**: UnlockAssociation 幂等
  - Given: _overlay 中已有 UnlockAssociation("frag_Z")；第二次 ApplyChanges 也包含 UnlockAssociation("frag_Z")
  - When: GetCurrentState 合并
  - Then: ResolvedState.Associations 中 "frag_Z" 仅出现 1 次
  - Edge cases: 多次解锁同一关联 + 后续其他变更 → 仅解锁部分幂等，其他变更正常

- **AC-5**: 权重 Clamp
  - Given: nostalgia BaseWeight=0.9, ModifyTagWeight(+0.3, Add) → 预期 1.2
  - When: GetCurrentState
  - Then: 返回 1.0（夹紧）
  - Edge cases: BaseWeight=-0.1（无效 SO 数据）→ Clamp 到 0.0；Multiply delta=2.0 → Clamp；NaN → Clamp 到 0.0

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/memory-change-tracking/state_merge_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (_overlay 数据结构和 ApplyChanges 必须就绪)
- Unlocks: Story 003 (条件求值需要 GetCurrentState 进行 ObjectStateIs 查询)

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 5/5 passing (all auto-verified by 27 unit tests)
**Deviations**: None
**Test Evidence**: `tests/unit/memory-change-tracking/state_merge_test.cs` — 27 tests covering AC-1 (VisualLayer toggle, unmodified layer keeps SO default, multiple overlays last-wins, no-overlay base state), AC-2 (ModifyTagWeight sequential Add+Multiply=0.64, Set replaces, unmodified tag keeps base), AC-3 (repeatable choice overwrite, replaced overlay reverts to base SO), AC-4 (UnlockAssociation idempotent, multiple targets), AC-5 (weight clamp overflow/underflow/base-below-zero/multiply-by-two/NaN)
**Code Review**: Skipped (lean mode)
