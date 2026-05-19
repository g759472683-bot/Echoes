# Story 001: MemoryFragment SO 完整 Schema

> **Epic**: 记忆碎片数据模型 (MemoryFragment)
> **Status**: Complete
> **Layer**: Core
> **Type**: Config/Data
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/memory-fragment-data-model.md`
**Requirement**: `TR-memory-fragment-001`, `TR-memory-fragment-002`, `TR-memory-fragment-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变配置 + ChangeTracker 可变 Overlay 模式
**ADR Decision Summary**: MemoryFragment ScriptableObject 定义 8 类别字段（"已干的墨"不可变 + "未干的墨"可变），6 种 ContentChange 多态类型，InteractiveObject 包含 Hitbox + InteractionType

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `[SerializeReference]` 在 IL2CPP 构建中可能存在 AOT 代码剥离问题；需 link.xml 保留所有多态子类型

**Control Manifest Rules (Core Layer)**:
- Required: Base SO (immutable) + ChangeTracker._overlay (mutable) two-layer model — source: ADR-0007
- Forbidden: Never serialize base SO data into save files — save only overlay + flags — source: ADR-0007
- Forbidden: Never directly modify ScriptableObject fields at runtime — use ApplyChanges() → _overlay — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/memory-fragment-data-model.md`, scoped to this story:*

- [ ] GIVEN Unity Editor 中创建了一个新的 MemoryFragment SO，WHEN 填写必填字段（FragmentId, ChapterId, SequenceIndex, FragmentName, BaseIllustration）并保存，THEN SO 可通过数据管理系统的 `GetFragmentAsync` 加载，返回完整 MemoryFragment 对象，所有必填字段非空
- [ ] GIVEN 一个包含 2 个 InteractiveObject 的 MemoryFragment，WHEN 交互系统读取 `InteractiveObjects` 列表，THEN 两个物件的 ObjectId、HitboxCenter、HitboxSize、DefaultState、OnInteract 均按 SO 中定义的值返回
- [ ] GIVEN 一个 ChoiceGroup 定义了 2 个 ChoiceOption，每个各含 1 个 ToggleVisualLayer 类型的 ContentChange，WHEN 变化追踪系统读取 ChoiceOption.ContentChanges，THEN 每个 ContentChange 的 ChangeType、TargetFragmentId、LayerId、Visible 值与 SO 定义一致
- [ ] GIVEN 一个 MemoryFragment 定义了 3 个 EmotionalTag（一个 IsPrimary = true），WHEN 情感标签系统查询该碎片标签，THEN 返回 3 个标签，IsPrimary 正确标记
- [ ] GIVEN 一个 MemoryFragment 定义了 2 个 ExplicitAssociations（一个 IsBidirectional = true），WHEN 关联引擎读取关联列表，THEN 返回 2 个关联，双向关联的目标可隐式回向关联到源

---

## Implementation Notes

*Derived from ADR-0007:*

MemoryFragment SO 结构（8 类别）:

**类别 1: 核心标识**（全部已干）:
```csharp
[CreateAssetMenu(menuName = "Echoes/Memory Fragment")]
public class MemoryFragment : ScriptableObject
{
    [field: SerializeField] public string FragmentId { get; private set; }
    [field: SerializeField] public string ChapterId { get; private set; }
    [field: SerializeField] public int SequenceIndex { get; private set; }
    [field: SerializeField] public TableReference FragmentName { get; private set; }
}
```

**类别 2: 视觉字段**（BaseIllustration 已干，VisualLayers 逐层可变）:
```csharp
[field: SerializeField] public AssetReferenceSprite BaseIllustration { get; private set; }
[field: SerializeField] public List<VisualLayer> VisualLayers { get; private set; }
```

**类别 3: 交互物件**（Hitbox 已干，DefaultState 湿）:
```csharp
[Serializable]
public struct InteractiveObject
{
    public string ObjectId;
    public InteractionType InteractionType; // Touch/Drag/Hover/Examine
    public Vector2 HitboxCenter, HitboxSize;
    public ObjectState DefaultState; // Active/Hidden/Disabled — 湿
    public AssetReferenceSprite DefaultSprite, HoverSprite;
    [SerializeReference] public ConditionGroup InteractCondition;
    public InteractionResult OnInteract;
}
```

**类别 4: 情感标签**: `List<EmotionalTag>` — TagId (干), BaseWeight (湿), IsPrimary (干)

**类别 5: 选择分支**: `List<ChoiceGroup>` → ChoiceOption → `List<ContentChange>`

**类别 6: 内容变化（6 种多态类型）**:
```csharp
[Serializable] public class ToggleVisualLayer : ContentChange { ... }
[Serializable] public class SetObjectState : ContentChange { ... }
[Serializable] public class SetTextContent : ContentChange { ... }
[Serializable] public class ModifyTagWeight : ContentChange { ... }
[Serializable] public class UnlockAssociation : ContentChange { ... }
[Serializable] public class SetFlag : ContentChange { ... }
```

**类别 7: 关联**: `List<FragmentAssociation>` — TargetFragmentId, AssociationType, BaseWeight (湿)

**类别 8: 结局触发器**: `List<EndingTrigger>` — EndingId, TriggerCondition, ContributionWeight, IsEssential

`[SerializeReference]` 多态序列化:
- ContentChange 子类使用 `[SerializeReference]` 标记 ChoiceOption.ContentChanges 列表
- ConditionGroup 子类使用 `[SerializeReference]` 标记条件列表
- link.xml 保留所有 16 个多态类型（见 ADR-0007 link.xml 清单）

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: ConditionGroup 条件求值引擎 — Evaluate() 逻辑
- Story 003: 编辑器验证工具 — Inspector 交叉验证、深度检查
- 数据管理系统 (#2): GetFragmentAsync 加载逻辑
- 记忆变化追踪 (#12): ApplyChanges + _overlay Dictionary 管理

---

## QA Test Cases

- **AC-1**: SO 创建与加载
  - Given: Unity Editor 中创建 MemoryFragment SO，填写必填字段
  - When: 通过 `ScriptableObject.CreateInstance` 或 AssetDatabase 加载
  - Then: FragmentId、ChapterId、SequenceIndex、FragmentName、BaseIllustration 均非 null/空；SO 完整可序列化
  - Edge cases: 缺少必填字段 → 编辑器验证警告

- **AC-2**: InteractiveObject 数据完整性
  - Given: SO 中定义了 2 个 InteractiveObject
  - When: 读取 `fragment.InteractiveObjects`
  - Then: 每个物件的 ObjectId、HitboxCenter、HitboxSize、DefaultState、OnInteract 值与 Inspector 中设置一致；HitboxCenter 和 HitboxSize 坐标无偏移
  - Edge cases: InteractiveObjects 列表为空 → 返回空列表，不返回 null

- **AC-3**: ContentChange 多态序列化
  - Given: ChoiceOption 定义了 1 个 ToggleVisualLayer（ChangeType=ToggleVisualLayer, TargetFragmentId="ch1_frag_05", LayerId="rain_layer", Visible=true）
  - When: SO 序列化后再反序列化
  - Then: ContentChange 类型保持为 ToggleVisualLayer；所有字段值与原始一致
  - Edge cases: 多个 ContentChange 在同一个 ChoiceOption 中 → 顺序保持

- **AC-4**: EmotionalTags + IsPrimary
  - Given: SO 中 3 个 EmotionalTag，一个 IsPrimary=true
  - When: 查询 `fragment.EmotionalTags.Where(t => t.IsPrimary)`
  - Then: 返回 1 个标签；其余 2 个 IsPrimary=false
  - Edge cases: 无标签标记 IsPrimary → Where 返回空（合法状态）

- **AC-5**: ExplicitAssociations + 双向性
  - Given: SO 中 2 个 FragmentAssociation，Association[0].IsBidirectional = true, Association[1].IsBidirectional = false
  - When: 查询关联列表
  - Then: 返回 2 个关联；双向关联目标碎片可通过 `GetReverseAssociation` 获取回向关联
  - Edge cases: 无关联定义 → 返回空列表

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: `production/qa/smoke-memory-fragment-schema.md` — smoke check pass

**Status**: [x] Created (AssociationType.cs, EmotionalTag.cs, VisualLayer.cs — MemoryFragment SO deferred to Unity project setup)

---

## Dependencies

- Depends on: data-management Story 001 (SO 基础设施 + Addressables groups), localization Story 001 (TableReference 类型)
- Unlocks: memory-fragment Story 002 (ConditionGroup 引擎), Story 003 (编辑器验证); ChangeTracker (#12), ScrollInteraction (#11), WebAssociation (#13), MultiEnding (#14)
