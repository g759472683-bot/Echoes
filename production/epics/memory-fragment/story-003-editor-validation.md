# Story 003: 编辑器验证 + 跨碎片约束

> **Epic**: 记忆碎片数据模型 (MemoryFragment)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-fragment-data-model.md`
**Requirement**: `TR-memory-fragment-005`, `TR-memory-fragment-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变配置 + ChangeTracker 可变 Overlay 模式
**ADR Decision Summary**: 跨碎片 ContentChange 目标必须在同一章节（ChapterId 匹配）；单碎片 5-10KB，总量 <1MB，物件数 2-5

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Editor 自定义 Inspector 使用 `Editor` 类和 `CustomEditor` 属性——API 在 Unity 6 中无变化

**Control Manifest Rules (Core Layer)**:
- Required: 6 ContentChange types with defined overlay merge algorithms — source: ADR-0007
- Forbidden: Never directly modify ScriptableObject fields at runtime — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/memory-fragment-data-model.md`, scoped to this story:*

- [ ] GIVEN 一个 ContentChange 的 TargetFragmentId 与当前碎片不同（跨碎片变化），WHEN 编辑器批量验证运行，THEN 若目标碎片在同一章节内且存在——验证通过。若目标碎片不存在或属于其他章节——验证报错
- [ ] GIVEN 两个 MemoryFragment 在同一章节内使用相同的 FragmentId，WHEN 编辑器批量验证运行，THEN 验证报错"碎片 ID [fragId] 在章节 [chapterId] 中重复"
- [ ] GIVEN 一个 VisualLayer 的 IsMutable = false，WHEN 某个 ContentChange 尝试 ToggleVisualLayer 修改该图层，THEN 运行时叠加层拒绝修改，记录 Warning 日志，不抛出异常，游戏继续运行
- [ ] 每碎片物件数超过 5 时 Inspector 显示黄色警告；ChoiceGroup 数超过 2 时显示警告
- [ ] GIVEN 一个 MemoryFragment 的 TableReference 字段指向不存在的本地化 Key，THEN 编辑器验证警告"引用的本地化 Key [key] 不存在"
- [ ] 单碎片 SO 序列化大小 5-10KB；全量 60-100 碎片元数据 < 1MB

---

## Implementation Notes

*Derived from ADR-0007:*

编辑器验证器（批量验证窗口）:
```csharp
public class FragmentValidator
{
    public static List<ValidationError> ValidateAll(List<MemoryFragment> fragments)
    {
        var errors = new List<ValidationError>();
        
        // 1. FragmentId 唯一性检查
        var duplicates = fragments.GroupBy(f => f.FragmentId)
            .Where(g => g.Count() > 1);
        foreach (var dup in duplicates)
            errors.Add(new ValidationError(ErrorLevel.Error, 
                $"碎片 ID '{dup.Key}' 在章节中重复"));
        
        // 2. 跨碎片 ContentChange 目标验证
        foreach (var frag in fragments)
        foreach (var cg in frag.ChoiceGroups)
        foreach (var choice in cg.Choices)
        foreach (var change in choice.ContentChanges)
        {
            if (change.TargetFragmentId != null && 
                change.TargetFragmentId != frag.FragmentId)
            {
                var target = fragments.Find(f => f.FragmentId == change.TargetFragmentId);
                if (target == null)
                    errors.Add(new ValidationError(ErrorLevel.Error,
                        $"碎片 '{frag.FragmentId}' 的 ContentChange 目标 '{change.TargetFragmentId}' 不存在"));
                else if (target.ChapterId != frag.ChapterId)
                    errors.Add(new ValidationError(ErrorLevel.Error,
                        $"碎片 '{frag.FragmentId}' 的跨碎片目标 '{change.TargetFragmentId}' 属于不同章节"));
            }
        }
        
        // 3. 物件数 + ChoiceGroup 数目检查
        foreach (var frag in fragments)
        {
            if (frag.InteractiveObjects.Count > 5)
                errors.Add(new ValidationError(ErrorLevel.Warning,
                    $"碎片 '{frag.FragmentId}' 物件数 ({frag.InteractiveObjects.Count}) 超过 MVP 上限 5"));
            if (frag.ChoiceGroups.Count > 2)
                errors.Add(new ValidationError(ErrorLevel.Warning,
                    $"碎片 '{frag.FragmentId}' ChoiceGroup 数 ({frag.ChoiceGroups.Count}) 超过 MVP 上限 2"));
        }
        
        return errors;
    }
}
```

运行时不可变图层保护:
```csharp
// 在 ChangeTracker.ApplyChanges() 中
if (change is ToggleVisualLayer toggle)
{
    var layer = _fragment.VisualLayers.Find(l => l.LayerId == toggle.LayerId);
    if (layer != null && !layer.IsMutable)
    {
        Debug.LogWarning($"[ChangeTracker] 尝试修改不可变图层 '{toggle.LayerId}'——已跳过");
        return; // 不抛异常，静默跳过
    }
}
```

自定义 Inspector（物件计数警告）:
```csharp
[CustomEditor(typeof(MemoryFragment))]
public class MemoryFragmentInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var frag = (MemoryFragment)target;
        
        if (frag.InteractiveObjects.Count > 5)
            EditorGUILayout.HelpBox(
                $"物件数 ({frag.InteractiveObjects.Count}) 超过 MVP 上限 5", 
                MessageType.Warning);
        if (frag.ChoiceGroups.Count > 2)
            EditorGUILayout.HelpBox(
                $"ChoiceGroup 数 ({frag.ChoiceGroups.Count}) 超过 MVP 上限 2", 
                MessageType.Warning);
    }
}
```

ConditionGroup 深度 4 检测：见 Story 002 ConditionValidator.ValidateDepth——编辑器批量验证中调用。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: MemoryFragment SO 字段定义——本 Story 消费这些字段进行验证
- Story 002: ConditionGroup Evaluate 引擎——本 Story 只验证深度，不验证求值逻辑
- 构建时 Addressables 交叉验证——由 data-management Story 004 处理
- 本地化 Key 验证的完整实现——依赖 Localization (#4) StringTable 加载

---

## QA Test Cases

- **AC-1**: 跨碎片同章验证通过
  - Given: 碎片 A（ch1_frag_01）的 ContentChange 目标为 ch1_frag_03（同一章节）
  - When: 运行批量验证
  - Then: 该 ContentChange 不产生错误
  - Edge cases: 目标 = 自身 → 合法（自身变化）

- **AC-2**: 跨碎片跨章验证失败
  - Given: 碎片 A（ch1_frag_01）的 ContentChange 目标为 ch2_frag_05（不同章节）
  - When: 运行批量验证
  - Then: 验证报错——"目标碎片属于不同章节"
  - Edge cases: 目标碎片不存在 → 报错"目标碎片不存在"

- **AC-3**: FragmentId 重复检测
  - Given: 两个 SO 的 FragmentId 均为 "ch1_frag_03"
  - When: 批量验证运行
  - Then: 报错"碎片 ID 'ch1_frag_03' 在章节中重复"
  - Edge cases: 不同章节的相同 FragmentId → 合法（不同章节可以有相同 SequenceIndex）

- **AC-4**: 物件数超限警告
  - Given: 一个碎片定义了 6 个 InteractiveObject
  - When: Inspector 渲染或批量验证
  - Then: 黄色警告"物件数 (6) 超过 MVP 上限 5"
  - Edge cases: 恰好 5 个 → 无警告

- **AC-5**: 不可变图层保护
  - Given: VisualLayer "background" 的 IsMutable = false
  - When: ContentChange 尝试 ToggleVisualLayer("background", Visible=false)
  - Then: ChangeTracker 记录 Warning；叠加层不修改该图层；游戏继续
  - Edge cases: 修改不存在的 LayerId → Warning"图层 [id] 不存在"

- **AC-6**: TableReference 缺失警告
  - Given: MemoryFragment 的 FragmentName TableReference 指向不存在的 key "missing_frag_name"
  - When: 编辑器验证运行
  - Then: 警告"引用的本地化 Key 'missing_frag_name' 不存在于任何已加载的字符串表中"
  - Edge cases: 运行时降级显示 `[missing: missing_frag_name]`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/memory-fragment/editor-validation_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/memory-fragment/editor-validation_test.cs` (21 tests)

---

## Dependencies

- Depends on: memory-fragment Story 001 (SO Schema), Story 002 (ConditionGroup 深度验证)
- Unlocks: None directly（所有消费 MemoryFragment 的系统受益于验证）

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 5/6 passing (AC-6 TableReference validation deferred — requires Unity localization runtime)
**Deviations**:
- ADVISORY: `IFragmentValidationTarget.cs` created — extracted interface for pure C# testability, follows project DI pattern
- ADVISORY: `MemoryFragment.cs` — `InteractiveObjects` and `ChoiceGroups` changed from fields to auto-properties for interface conformance
- ADVISORY: AC-6 deferred — TableReference key validation needs `com.unity.localization` runtime
**Test Evidence**: Logic — `tests/unit/memory-fragment/editor-validation_test.cs` (21 tests across 5 verified ACs)
**Code Review**: Skipped (lean mode)
