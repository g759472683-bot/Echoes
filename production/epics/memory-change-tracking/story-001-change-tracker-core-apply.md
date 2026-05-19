# Story 001: ChangeTracker 核心数据结构 + ApplyChanges

> **Epic**: 记忆变化追踪 (ChangeTracker)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-change-tracking.md`
**Requirement**: `TR-memory-change-tracking-001`, `TR-memory-change-tracking-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: Base SO (immutable) + ChangeTracker._overlay (mutable) 两层模型；_overlay Dictionary<(fragmentId, choiceId), ContentOverrides>；ApplyChanges 批量应用 ContentChange[] 并触发 OnOverlayChanged 事件

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ChangeTracker 为 Game 场景持久 MonoBehaviour；`[SerializeReference]` 多态序列化需 link.xml 保留 6 个 ContentChange 子类；IL2CPP AOT 构建验证

**Control Manifest Rules (Feature Layer)**:
- Required: Base SO (immutable) + ChangeTracker._overlay (mutable) two-layer model — source: ADR-0007
- Forbidden: Never directly modify ScriptableObject fields at runtime — source: ADR-0007
- Forbidden: Never use `Task.Result` in query methods — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/memory-change-tracking.md`, scoped to this story:*

- [ ] GIVEN 碎片 A 的一个 ChoiceOption 包含 2 个 ContentChange (ToggleVisualLayer + SetObjectState)，WHEN 玩家选择该选项，THEN ChangeTracker.ApplyChanges 被调用，_overlay 中新增 Key (targetFragmentId, choiceId)，ContentOverrides 包含 2 条变更。OverlayVersion 递增 1。_changeLog 新增 1 条日志。

- [ ] GIVEN ContentChange 的 TargetFragmentId 指向不存在的碎片，WHEN ApplyChanges 被调用，THEN LogWarning 输出，整个调用被跳过，_overlay 和 _changeLog 均无变化。

- [ ] GIVEN ContentChange 尝试修改 IsMutable = false 的图层，WHEN GetCurrentState 合并到该变更，THEN 该条 ToggleVisualLayer 被跳过，LogWarning 输出，同批次其他有效变更正常应用。

- [ ] GIVEN ApplyChanges 传入空的 ContentChange 数组，THEN 不修改 _overlay，但 _changeLog 中仍记录一条日志条目，OverlayVersion 递增，OnOverlayChanged 触发。

- [ ] GIVEN 碎片 A 的选择修改了碎片 B 的内容且碎片 B 是当前显示的碎片，WHEN ApplyChanges 完成，THEN OnOverlayChanged(fragmentId_B) 在同一帧内触发。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

ChangeTracker 作为 Game 场景持久 MonoBehaviour 单例：

```csharp
public class ChangeTracker : MonoBehaviour, IChangeTracker
{
    // Rule 1: _overlay Dictionary
    private Dictionary<(string targetFragmentId, string choiceId), ContentOverrides> _overlay;
    
    // Rule 1: _changeLog append-only
    private List<ChangeLogEntry> _changeLog;
    
    // Rule 1: OverlayVersion
    public int OverlayVersion { get; private set; }
    
    // Rule 2: ApplyChanges algorithm
    public void ApplyChanges(string targetFragmentId, string choiceId, ContentChange[] changes)
    {
        // 1. Validate targetFragmentId exists in fragment registry → else LogWarning + skip
        // 2. Convert changes to ContentOverrides (group by type)
        //    - ToggleVisualLayer → validate LayerId exists → validate IsMutable
        //    - SetObjectState → validate ObjectId exists
        //    - SetTextContent → validate textFieldId exists
        //    - ModifyTagWeight → validate TagId exists in EmotionalTagCatalog
        //    - UnlockAssociation → validate AssociationTargetId exists
        //    - SetFlag → write directly to _flags (Story 003)
        // 3. _overlay[(targetFragmentId, choiceId)] = contentOverrides
        //    (if key exists → overwrite)
        // 4. _changeLog.Add(new ChangeLogEntry { ... })
        // 5. OverlayVersion++
        // 6. OnOverlayChanged?.Invoke(targetFragmentId)
    }
    
    // ADR-0001 static event pattern
    public static event Action<string> OnOverlayChanged;
}
```

`ContentOverrides` 结构体：
```csharp
[Serializable]
public struct ContentOverrides
{
    public List<ToggleLayerEntry> ToggledLayers;
    public List<ObjectStateEntry> ObjectStates;
    public List<TextOverrideEntry> TextOverrides;
    public List<TagWeightModEntry> TagWeightMods;
    public List<string> UnlockedAssociations;
    public List<FlagSetEntry> SetFlags;
}
```

验证失败不阻塞 ApplyChanges——仅跳过无效条目，有效条目仍然应用。无效 targetFragmentId 则跳过整个调用。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: GetCurrentState 状态合并算法
- Story 003: Flag 系统 (_flags Dictionary)、_visitedFragments/_completedChapters 集合、条件求值
- Story 004: 存档序列化与恢复

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: ApplyChanges 基本流程
  - Given: ChangeTracker 已初始化，_overlay 为空，OverlayVersion=0；碎片 A 有 choice_X 产生 2 个 ContentChange
  - When: 调用 ApplyChanges("frag_A", "choice_X", [ToggleVisualLayer, SetObjectState])
  - Then: _overlay.ContainsKey(("frag_A", "choice_X")) = true；ContentOverrides 包含 2 条变更；OverlayVersion = 1；_changeLog.Count = 1
  - Edge cases: 重复调用同一 (frag_A, choice_X) → _overlay 被覆盖，OverlayVersion 递增到 2，_changeLog 有 2 条

- **AC-2**: 无效 TargetFragmentId
  - Given: ChangeTracker 已初始化，碎片注册表中无 "nonexistent_frag"
  - When: 调用 ApplyChanges("nonexistent_frag", "choice_1", [validChange])
  - Then: LogWarning 被记录；_overlay 无变化；_changeLog 无变化；OverlayVersion 不变
  - Edge cases: 空字符串 fragmentId → 同上述处理；null fragmentId → 同上述处理

- **AC-3**: IsMutable=false 图层拒绝
  - Given: 碎片 B 的 "layer_static" IsMutable=false；ContentChange 尝试 ToggleVisualLayer("layer_static", true)；同批次另有一个有效 SetObjectState
  - When: ApplyChanges 处理该批次
  - Then: ToggleVisualLayer 被跳过 + LogWarning；SetObjectState 正常应用；_overlay 中 ContentOverrides 包含 1 条变更（仅 SetObjectState）
  - Edge cases: 批次中所有变更都针对不可变图层 → 所有被跳过，但 ApplyChanges 仍完成，OverlayVersion 不递增（无有效变更）

- **AC-4**: 空 ContentChange 数组
  - Given: ChangeTracker 已初始化，OverlayVersion = N
  - When: 调用 ApplyChanges("frag_A", "choice_X", [])
  - Then: _overlay 无变化；_changeLog 新增 1 条日志；OverlayVersion = N + 1；OnOverlayChanged 事件触发
  - Edge cases: null changes 数组 → 与空数组同处理

- **AC-5**: 跨碎片即时生效
  - Given: 碎片 B 是当前显示碎片；碎片 A 的选择包含 ContentChange 目标为 frag_B
  - When: ApplyChanges("frag_B", "choice_A1", [ToggleVisualLayer("layer_rain", true)])
  - Then: OnOverlayChanged("frag_B") 事件触发；事件在同一帧内触发（同步）
  - Edge cases: 目标碎片尚未被访问 → OnOverlayChanged 仍触发，GetCurrentState 下次调用返回已变更状态

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/memory-change-tracking/change_tracker_core_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: memory-fragment-data-model Story 001 (MemoryFragment SO + ContentChange 类型定义)；data-management Story 001 (IDataManager 碎片注册表)
- Unlocks: Story 002 (GetCurrentState — needs _overlay populated)

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 5/5 passing (all auto-verified by 30 unit tests)
**Deviations**: None
**Test Evidence**: `tests/unit/memory-change-tracking/change_tracker_core_test.cs` — 30 tests covering AC-1 (ApplyChanges with 2 changes, version++, log, duplicate overwrite, independent fragments), AC-2 (invalid/null/empty TargetFragmentId), AC-3 (immutable/missing layer skip, invalid object/tag skip, valid ModifyTagWeight conversion), AC-4 (empty/null changes array), AC-5 (OnOverlayChanged synchronous fire)
**Code Review**: Skipped (lean mode)
