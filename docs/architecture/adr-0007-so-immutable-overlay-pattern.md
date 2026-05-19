# ADR-0007: SO 不可变配置 + ChangeTracker 可变 Overlay 模式

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

MemoryFragment 等 ScriptableObject 保存设计师配置的"基础状态"。玩家选择产生的变化（文本替换、对象显隐、标签权重变化）写入 ChangeTracker 的 `_overlay` Dictionary。查询时合并 base SO + overlay。存档只序列化 overlay（不序列化 base SO）。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | MEDIUM — `[SerializeReference]` 在 IL2CPP 构建中可能存在 AOT 代码剥离问题 |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | `[SerializeReference]` (polymorphic ContentChange/Condition serialization) |
| **Verification Required** | `[SerializeReference]` 在 IL2CPP 中多态类型正确序列化/反序列化；AOT 代码剥离不丢失子类型 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (IDataManager — SO 通过 Addressables 加载) |
| **Enables** | ADR-0008 (ConditionGroup 求值 — 输入 SO 定义 + overlay 修改), ADR-0003 (存档 — overlay 是序列化目标) |
| **Blocks** | ChangeTracker + MemoryFragment Data Model Epic |
| **Ordering Note** | 在 ADR-0002 之后创建 |

## Context

### Problem Statement

MemoryFragment ScriptableObject 包含设计师配置的静态数据（基础插画、基础文本、默认标签权重）。玩家在游戏中选择会修改这些数据（替换文本、隐藏/显示对象、修改标签权重）。但 ScriptableObject 运行时修改会在 Editor 中持久化（反模式），且无法按存档槽位隔离。

### Constraints

- ScriptableObject 运行时**不可写入**（会污染 Editor 数据）
- 需要在多个存档槽位间隔离玩家修改
- 存档只应持久化**差异数据**（玩家选择产生的变化量）
- 查询性能必须 < 0.1ms（每碎片展示时查询多次）

### Requirements

- Base SO + Overlay Dictionary 两层模型
- Overlay 仅持久化（存档不包含 base SO 数据）
- 新游戏/章节重玩时 overlay 重置
- 查询接口返回合并后的"当前完整状态"
- 6 种 ContentChange 类型的 overlay 算法

## Decision

**Base SO (immutable) + ChangeTracker._overlay Dictionary (mutable) 两层模型。**

### 两层数据模型

```
┌───────────────────────────────────────────────────────┐
│  Base Layer: ScriptableObject (只读, 设计师创建)        │
│  ├─ MemoryFragment.baseIllustration                    │
│  ├─ MemoryFragment.baseText                             │
│  ├─ InteractiveObject[].defaultState                    │
│  ├─ EmotionalTag[].baseWeight                          │
│  └─ ...                                                │
├───────────────────────────────────────────────────────┤
│  Overlay Layer: _overlay Dictionary (读写, 运行时修改)  │
│  ├─ key: (fragmentId, choiceId)                        │
│  ├─ value: ContentOverrides                            │
│  │   ├─ IllustrationOverride (替换插画)                 │
│  │   ├─ TextOverride (替换文本)                         │
│  │   ├─ ObjectVisibilityOverride (显隐对象)             │
│  │   ├─ TagWeightOverride (修改标签权重)                 │
│  │   ├─ AssociationOverride (修改显式关联)              │
│  │   └─ EndingTriggerOverride (修改结局触发器)          │
└───────────────────────────────────────────────────────┘
```

### 合并查询

```csharp
public async Task<ResolvedFragmentState> GetCurrentStateAsync(string fragmentId)
{
    // 异步获取 base SO（若已缓存则同步返回，无阻塞）
    var baseFragment = await DataManager.GetFragmentAsync(chapterKey, fragmentId);
    var overlay = _overlay
        .Where(kv => kv.Key.fragmentId == fragmentId)
        .Select(kv => kv.Value);

    return new ResolvedFragmentState
    {
        Illustration = overlay.IllustrationOverride ?? baseFragment.baseIllustration,
        Text = overlay.TextOverride ?? baseFragment.baseText,
        Objects = MergeObjects(baseFragment.interactiveObjects, overlay),
        Tags = MergeTags(baseFragment.emotionalTags, overlay),
        // ...
    };
}
```

> **Unity 主线程约束**: 禁止使用 `Task.Result` 或 `Task.Wait()` 阻塞主线程。
> Unity 的 SynchronizationContext 在 `await` 完成后将执行续回到主线程——
> 若用 `.Result` 阻塞等待，SynchronizationContext 无法将完成信号投递回主线程，
> 形成死锁。所有异步查询必须 `await` 到调用链顶层。

### 存档隔离

```
存档不包含 base SO 数据 — 只序列化 overlay:
SaveData.Overlay = Dictionary<string, ContentOverrides>
// "chapter1_frag03_choiceA" → { TextOverride: "...", TagWeightOverride: ... }

读取存档时:
1. 反序列化 SaveData.Overlay
2. ChangeTracker.Restore(overlay)
3. 后续查询自动合并 base SO + restored overlay
```

### Architecture Diagram

```
┌─────────────────────────────────────────┐
│  Designer (Editor)                       │
│    ↓ 创建 + 修改                         │
│  ScriptableObject (MemoryFragment)       │
│    ↓ 通过 Addressables 加载 (只读)       │
│  IDataManager                            │
│    ↓ 提供 base SO 引用                   │
│  ChangeTracker.GetCurrentState(id)       │
│    ├─ 读取 base SO 字段                  │
│    ├─ 读取 _overlay[id] 覆盖             │
│    └─ 返回 ResolvedFragmentState 快照    │
│         ↓                               │
│  消费者系统 (#11, #13, #14, #17)         │
│    (只看到合并后的最终状态)               │
└─────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface IChangeTracker
{
    void ApplyChanges(ContentChange[] changes);
    ResolvedFragmentState GetCurrentState(string fragmentId);
    bool EvaluateCondition(ConditionGroup condition);

    void SetFlag(string flagId, bool value);
    bool GetFlag(string flagId);
    Dictionary<string, bool> GetAllFlags();

    // static event Action<string> OnOverlayChanged;
}

public struct ResolvedFragmentState
{
    public Sprite Illustration;
    public string Text;
    public ResolvedInteractiveObject[] Objects;
    public ResolvedEmotionalTag[] Tags;
    // 不可变快照 — 消费者无法修改 base 或 overlay
}
```

### Implementation Guidelines

1. ScriptableObject 字段通过 `[field: SerializeField]` 标记，运行时只读
2. Overlay 修改通过 `ContentChange[]` 数组批量应用（原子操作）
3. `GetCurrentState` 返回不可变 `ResolvedFragmentState` struct（防止消费者意外修改）
4. 新游戏开头 `_overlay.Clear()` 清空所有覆盖
5. 章节重玩时 overlay 保留（玩家选择累积不可逆）

## Alternatives Considered

### Alternative 1: 直接在 ScriptableObject 上修改（`Undo.RecordObject`）

- **Description**: 运行时直接修改 SO 字段，保存时通过 Editor API 持久化
- **Pros**: 数据模型简单（一层），查询快
- **Cons**: Editor 中运行后 SO 数据被污染；不支持多存档槽位；Editor API 在构建中不可用
- **Rejection Reason**: 直接修改 SO 是 Unity 开发中常见的反模式，Editor 数据污染和存档隔离问题是致命的

### Alternative 2: 每个存档槽位保存完整数据副本

- **Description**: 存档时序列化所有碎片完整状态（不区分 base 和 overlay）
- **Pros**: 实现简单（直接 JSON.Serialize 完整状态）
- **Cons**: 存档文件巨大（19 章 × ~30 碎片 × 完整数据）；base SO 修改后旧存档不一致；版本迁移困难
- **Rejection Reason**: 存档文件膨胀，且设计师修改 base SO 后旧存档出现数据不一致

## Consequences

### Positive

- SO 受保护（只读，不会 Editor 污染）
- 多存档槽位天然隔离（每个存档有独立 overlay）
- 存档文件小（只记录差异数据）
- 设计师修改 base SO 自动生效（所有存档下次查询自动合并新 base）

### Negative

- 查询时需要合并两层（额外 CPU）
- `ContentOverrides` 的类型需要与 SO 字段保持同步
- 6 种 ContentChange 类型的 overlay 算法各有差异
- `[SerializeReference]` 在 IL2CPP 中有 AOT 风险

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| `[SerializeReference]` IL2CPP 丢失子类型 | Medium | High | link.xml 显式保留 6 个 ContentChange 子类 + 9 个 Condition 子类（见下方 link.xml 清单）；Pre-Production IL2CPP 构建验证 |
| Overlay + base SO 合并结果不正确 | Low | High | 6 种 overlay 算法各写单元测试 |
| 合并查询性能影响帧率 | Low | Medium | ResolvedFragmentState 缓存 + 脏标记；profile 验证 |

### link.xml 类型保留清单

`[SerializeReference]` 的子类若仅通过反射/序列化器引用（未在代码中直接实例化），
IL2CPP 链接器会在 AOT 编译时剥离其类型元数据。必须将以下类型列入 link.xml：

```xml
<linker>
  <!-- 6 个 ContentChange 子类 -->
  <assembly fullname="Assembly-CSharp">
    <type fullname="ToggleVisualLayer" preserve="all" />
    <type fullname="SetObjectState" preserve="all" />
    <type fullname="SetTextContent" preserve="all" />
    <type fullname="ModifyTagWeight" preserve="all" />
    <type fullname="UnlockAssociation" preserve="all" />
    <type fullname="SetFlag" preserve="all" />
    <!-- 9 个 Condition 子类 -->
    <type fullname="ConditionAll" preserve="all" />
    <type fullname="ConditionAny" preserve="all" />
    <type fullname="ConditionNot" preserve="all" />
    <type fullname="ConditionAlways" preserve="all" />
    <type fullname="ConditionChoiceMade" preserve="all" />
    <type fullname="ConditionFlagSet" preserve="all" />
    <type fullname="ConditionObjectStateIs" preserve="all" />
    <type fullname="ConditionVisitedFragment" preserve="all" />
    <type fullname="ConditionChapterCompleted" preserve="all" />
    <type fullname="ConditionTagWeight" preserve="all" />
    <!-- Overlay 值类型 -->
    <type fullname="ContentOverrides" preserve="all" />
  </assembly>
</linker>
```

> 若新增 ContentChange 或 Condition 子类（Full Vision 系统扩展），必须同步更新
> 此清单和 `link.xml` 文件。Pre-Production 的 IL2CPP 构建验证必须覆盖这些类型。

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (GetCurrentState, 合并 1 碎片) | ~0.05ms (Dictionary lookup + struct copy) |
| CPU (ApplyChanges, 10 ContentChange) | ~0.1ms (Dictionary insert) |
| Memory (_overlay Dictionary, 50h 游戏后) | ~100-300KB |
| Memory (base SO, 1 碎片) | ~2-5KB (ScriptableObject) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 运行时修改 SO 字段不污染 Editor 数据（退出 Play Mode 后 SO 恢复原值）
- [ ] 两个存档槽位的 overlay 互不干扰
- [ ] 6 种 ContentChange overlay 算法均正确合并
- [ ] `GetCurrentState` 返回的 `ResolvedFragmentState` 不可被消费者修改
- [ ] 章节重玩后 overlay 保留（不重置）

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `memory-fragment-data-model.md` (#8) | 数据模型 | SO 模板 + 运行时可变状态 | Base SO (immutable) + Overlay (mutable) |
| `memory-change-tracking.md` (#12) | 变化追踪 | 6 种 ContentChange 类型 | 6 种 ContentOverrides 对应 overlay 算法 |
| `memory-change-tracking.md` (#12) | 变化追踪 | 存档只序列化 overlay | SaveData.Overlay Dictionary |
| `save-load-system.md` (#7) | 存档 | 存档不包含 base SO 数据 | overlay 独立序列化 |
| `chapter-management.md` (#15) | 章节管理 | 章节重玩保留玩家选择 | overlay 不随章节切换清空 |

## Related

- ADR-0002 — IDataManager 提供 base SO 加载
- ADR-0003 — 存档仅序列化 overlay
- ADR-0008 — ConditionGroup 求值消费 ResolvedFragmentState
- `docs/architecture/architecture.md` §4.2 — IChangeTracker API 边界
