# Story 001: EndingDefinition 数据结构 + 触发器收集

> **Epic**: 多结局系统 (MultiEndingSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/multi-ending-system.md`
**Requirement**: `TR-multi-ending-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: 多结局判定算法
**ADR Decision Summary**: EndingDefinition 数据结构（EndingId/EndingType/MinimumScore/IsDefault/EmotionalAffinity）存储在 ChapterDefinition 中；ResolveEnding 遍历章节所有碎片收集 EndingTrigger 并按 EndingId 分组

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 逻辑；EndingDefinition 作为 ChapterDefinition SO 的子数组

**Control Manifest Rules (Feature Layer)**:
- Required: 3-stage multi-ending resolution — collect triggers → IsEssential gate → accumulate ContributionWeight + EmotionalAffinity → threshold → tie-breaking — source: ADR-0010
- Required: Fallback ending guaranteed per chapter — source: ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/multi-ending-system.md`, scoped to this story:*

- [ ] GIVEN 章节 EndingDefinition[] 中有 1 个 IsDefault=true、MinimumScore=0.0 的结局，WHEN 游戏启动，THEN 多结局系统验证通过。若零个或多个 IsDefault=true → 系统记录 Error。

- [ ] GIVEN EndingTrigger.EndingId 引用了一个不存在的 EndingId（与任何 EndingDefinition 不匹配），WHEN ResolveEnding 执行，THEN 该触发器被忽略 + LogWarning。其他结局仍正常评估。

- [ ] GIVEN ChapterDefinition 中有 3 个 EndingDefinition，且章节碎片中散布着 8 个 EndingTrigger（匹配这 3 个 EndingId），WHEN 触发器收集阶段执行，THEN 触发器按 EndingId 分组为 3 组。引用未知 EndingId 的触发器被过滤掉。

---

## Implementation Notes

*Derived from ADR-0010 Implementation Guidelines:*

### Data Structures

```csharp
[Serializable]
public class EndingDefinition
{
    public string EndingId;
    public EndingType EndingType; // ChapterEnding, HiddenEnding
    public string ChapterId;
    public float MinimumScore;
    public bool IsDefault;
    public string EmotionalAffinity; // nullable — dominant emotion category match
    public TableReference DisplayNameKey;
}

[Serializable]
public class EndingTrigger
{
    public string EndingId; // matches EndingDefinition.EndingId
    public float ContributionWeight; // [0.0, 1.0]
    public bool IsEssential;
    [SerializeReference] public ConditionGroup TriggerCondition;
}

public struct ResolvedEnding
{
    public string EndingId;
    public EndingType EndingType;
    public float Score;
    public bool IsDefault;
    public bool IsNewUnlock;
    public List<(string EndingId, float Score)> QualifiedEndings;
    public string DominantPathEmotion;
}
```

### MultiEndingSystem Class

```csharp
public class MultiEndingSystem
{
    private readonly IDataManager _dataManager;
    private readonly IChangeTracker _changeTracker;
    private readonly IEmotionalTagSystem _tagSystem;
    private HashSet<string> _unlockedEndingIds;

    public MultiEndingSystem(
        IDataManager dataManager,
        IChangeTracker changeTracker,
        IEmotionalTagSystem tagSystem)
    { ... }

    // Step 1-2: Collect triggers for a chapter
    private Dictionary<string, List<EndingTrigger>> CollectTriggers(string chapterId)
    {
        var fragments = _dataManager.GetFragmentsByChapter(chapterId);
        var grouped = new Dictionary<string, List<EndingTrigger>>();

        foreach (var frag in fragments)
        {
            foreach (var trigger in frag.EndingTriggers)
            {
                if (!grouped.ContainsKey(trigger.EndingId))
                    grouped[trigger.EndingId] = new List<EndingTrigger>();
                grouped[trigger.EndingId].Add(trigger);
            }
        }
        return grouped;
    }
}
```

### Default Ending Validation

Editor 验证（`[MenuItem("回响/Validate/Default Endings")]`）：
- 每章恰好 1 个 IsDefault=true 的 EndingDefinition
- 默认结局 MinimumScore = 0.0

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 判定算法（Essential gate + score accumulation + threshold + default fallback）
- Story 003: Tie-Breaking + 结局持久化 + 重判
- Story 004: 隐藏结局跨章节支持 + Path Bonus Hook

---

## QA Test Cases

- **AC-1**: Default ending validation
  - Given: ChapterDefinition.Endings[] 有 1 个 IsDefault=true, MinimumScore=0.0
  - When: Editor 验证运行
  - Then: 通过（无 Error）
  - Edge cases: 0 个 IsDefault → Error；2+ 个 IsDefault → Error

- **AC-2**: Orphan trigger
  - Given: EndingTrigger.EndingId = "nonexistent"；EndingDefinition[] 中无此 ID
  - When: CollectTriggers
  - Then: LogWarning "触发器引用未知 EndingId: 'nonexistent'——已忽略"；其他组正常收集
  - Edge cases: 多个 orphan triggers → 每个警告一次

- **AC-3**: Trigger collection
  - Given: 3 个 EndingDefinition；8 个 EndingTriggers 分布在 5 个碎片中，其中 1 个是 orphan
  - When: CollectTriggers("ch01")
  - Then: 返回 3 组（每组对应有效 EndingId）；orphan 被排除；各组触发器来自正确的碎片
  - Edge cases: 零触发器 → 所有分组为空

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/multi-ending/data_structures_trigger_collection_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: memory-fragment-data-model Story 001 (EndingTrigger 定义在 MemoryFragment 中)；chapter-management Story 001 (ChapterDefinition.Endings[])
- Unlocks: Story 002 (判定算法 needs trigger groups)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 3/3 passing
**Deviations**: None
**Test Evidence**: `tests/unit/multi-ending/data_structures_trigger_collection_test.cs` — 11 test functions
**Code Review**: Pending
