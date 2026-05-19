# ADR-0016: 情感标签系统 — 标签词汇表、层级结构与查询 API

## Status

Accepted

## Date

2026-05-19

## Last Verified

2026-05-19

## Decision Makers

User + Claude Code (technical-director via /dev-story)

## Summary

情感标签系统为网状关联引擎提供碎片间相似度计算的原始词汇。决定使用 EmotionalTagCatalog ScriptableObject（15-20 标签，8 类别，最多 2 层父子层级）+ 纯函数查询 API + ModifyTagWeight 叠加层委托给 ChangeTracker。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 数据结构 + ScriptableObject，无 post-cutoff API |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0007 (ChangeTracker — ModifyTagWeight 叠加层委托), ADR-0002 (IDataManager — 碎片查询) |
| **Enables** | ADR-0009 (WebAssociationEngine — TagSimilarity 因子 A), ADR-0010 (MultiEnding — 标签条件) |
| **Blocks** | EmotionalTagSystem Epic |
| **Ordering Note** | 在 ADR-0007 之后实现；需先于 ADR-0009（关联引擎依赖标签相似度矩阵） |

## Context

### Problem Statement

网状关联引擎需要计算碎片之间的情感相似度，但"哪些标签存在、标签之间的关系是什么、如何查询带有特定标签的碎片"这些基础问题尚未有独立的架构决策。当前 ADR-0009 引用了情感标签 GDD #10 但注明"无独立 ADR"——这导致实现关联引擎时标签系统的接口定义不明确。

### Constraints

- 标签词汇表在运行时只读——设计时确定，不在游戏中新增/删除
- 标签总数 MVP 15-20，Full Vision ≤ 30
- 最多 2 层父子层级（根标签 + 1 层子标签）
- 每碎片 1-5 个标签，其中最多 1 个 IsPrimary
- 标签权重修改通过 ChangeTracker 叠加层——本系统只提供读取接口

### Requirements

- EmotionalTagCatalog SO 定义完整标签词汇表
- 标签支持可选的父子层级（查询父标签时自动包含子标签）
- 互斥规则：IncompatibleWith 标签对不能同时为 IsPrimary
- 纯函数查询 API：GetTagsForFragment, GetPrimaryTag, QueryFragmentsByTag, GetTagCategory, GetRelatedTags
- 编辑器工具：Emotional Tag Browser 窗口（树形视图、引用计数、安全重命名、孤立检测）

## Decision

**EmotionalTagCatalog ScriptableObject + 纯函数查询 API + ModifyTagWeight 叠加层委托。**

### 标签词汇表结构

```
EmotionalTagCatalog (ScriptableObject, 全局唯一)
├── Tags[]
│   ├── TagId: string              // 程序标识符，如 "nostalgia"
│   ├── DisplayName: string        // 本地化 Key (TableReference 就绪后迁移)
│   ├── Category: enum             // Joy|Sadness|Love|Fear|Anger|Wonder|Melancholy|Peace
│   ├── ParentTagId: string?       // 可选父标签 (最多 1 层)
│   ├── IncompatibleWith: string[] // 不可同时为主标签的 TagId 列表
│   ├── AssociatedColors: { primary, secondary }  // 视觉关联色
│   └── Description: string        // 设计师可读的标签含义说明
```

**8 个固定情感类别**：Joy（喜悦）、Sadness（悲伤）、Love（爱）、Fear（恐惧）、Anger（愤怒）、Wonder（惊奇）、Melancholy（愁思）、Peace（平静）

### 标签层级规则

- 根标签 ParentTagId = null
- 子标签 ParentTagId 指向根标签，最多 1 层深度
- 查询父标签时自动包含所有子标签：`QueryFragmentsByTag("love")` → 返回标记 `love` 和 `nostalgia`（子标签）的所有碎片
- 子标签权重独立——不继承父标签权重

### Key Interfaces

```csharp
public class EmotionalTagSystem : IEmotionalTagSystem
{
    // 初始化
    public void Initialize(EmotionalTagCatalog catalog);

    // 查询 — 全部纯函数，无副作用
    public List<EmotionalTag> GetTagsForFragment(string fragmentId);
    public EmotionalTag? GetPrimaryTag(string fragmentId);
    public List<string> QueryFragmentsByTag(string tagId, float minWeight = 0.0f);
    public TagCategory GetTagCategory(string tagId);
    public List<string> GetRelatedTags(string tagId);  // 同 Category + 同父的子标签
    public bool AreIncompatible(string tagIdA, string tagIdB);
    public bool IsValidTag(string tagId);

    // 标签权重（含叠加层合并——委托给 ChangeTracker）
    public float GetResolvedWeight(string fragmentId, string tagId);
}
```

### 互斥规则验证

- 互斥检查在编辑器保存时执行——运行时不验证
- `IncompatibleWith` 仅约束 IsPrimary：互斥标签可同时作为非主标签存在（复杂情感允许矛盾）
- 编辑器验证：`if (tagA.IsPrimary && tagB.IsPrimary && AreIncompatible(tagA, tagB))` → 报错

### 碎片标签分配 (MemoryFragment.EmotionalTags)

| 字段 | 类型 | 说明 |
|------|------|------|
| TagId | string | 引用 Catalog 中的标签 |
| BaseWeight | float [0.0, 1.0] | 设计时基础权重 |
| IsPrimary | bool | 每碎片最多 1 个 |

### Architecture Diagram

```
┌──────────────────────────────────────────┐
│       EmotionalTagCatalog (SO)           │
│       Tags: EmotionalTagDef[]            │
│       运行时只读，设计时资产              │
└──────────────────────────────────────────┘
              │
              ▼
┌──────────────────────────────────────────┐
│       EmotionalTagSystem                  │
│       纯 C# 类，依赖注入                   │
│                                          │
│  查询 API (纯函数):                       │
│  ├─ GetTagsForFragment(id)               │
│  ├─ GetPrimaryTag(id)                    │
│  ├─ QueryFragmentsByTag(tagId, minW)     │
│  ├─ GetTagCategory(tagId)                │
│  └─ GetRelatedTags(tagId)                │
│                                          │
│  权重查询 (委托 ChangeTracker):           │
│  └─ GetResolvedWeight(fragId, tagId)     │
└──────────────────────────────────────────┘
         │                    │
         ▼                    ▼
┌──────────────────┐  ┌──────────────────┐
│ ChangeTracker    │  │ DataManager      │
│ _overlay 叠加层  │  │ GetFragment()    │
│ ModifyTagWeight  │  │ 碎片 → 标签列表  │
└──────────────────┘  └──────────────────┘
```

### Implementation Guidelines

1. EmotionalTagCatalog 通过 Addressables 加载（不通过 Resources.Load）
2. 查询方法为纯函数——相同输入始终返回相同结果（权重查询除外——依赖叠加层状态）
3. TagId 为英文常量（snake_case），DisplayName 通过 LocalizationManager 本地化
4. 标签重命名编辑器工具自动更新所有引用碎片的 TagId
5. 父标签查询的子标签展开在初始化时预计算索引（`Dictionary<string, List<string>>`）——避免每次查询遍历全表
6. 不使用 Update()——纯查询服务，按需调用

## Alternatives Considered

### Alternative 1: 标签内嵌在碎片中，无独立 Catalog

- **Description**: 每个碎片的 EmotionalTag 直接写 TagId 字符串，无集中词汇表
- **Pros**: 实现简单
- **Cons**: 无法保证标签名一致性（拼写错误静默失效）；无法查询"哪些碎片用了这个标签"；重命名困难；关联引擎无法构建 TagSimilarityMatrix
- **Rejection Reason**: 集中 Catalog 是关联引擎和编辑器工具的前提

### Alternative 2: 运行时可变标签词汇表

- **Description**: 允许游戏过程中动态新增/删除标签
- **Pros**: 理论灵活性
- **Cons**: 标签相似度矩阵需要重建；存档兼容性复杂；叙事一致性风险（同一标签在前后含义不同）
- **Rejection Reason**: 标签是游戏的情感词汇基础——运行时稳定是设计意图

## Consequences

### Positive

- 集中词汇表：设计师可在单一位置查看和管理所有情感标签
- 纯函数查询：完全可单元测试
- 层级查询：父标签自动包含子标签——简化关联引擎的标签查询逻辑
- 编辑器工具支持安全重命名和引用追踪

### Negative

- 标签数量固定为 15-30——如果需要更多标签需要更新 Catalog 并重新构建
- ModifyTagWeight 的叠加逻辑委托给 ChangeTracker——本系统不直接控制权重修改

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 标签不足导致关联网络单调 | Low | Medium | 15-20 标签 × 8 类别已覆盖主要情感色谱；Full Vision 可扩展到 30 |
| 父标签查询性能（大量碎片） | Low | Low | 预计算子标签索引（O(1) 查找） |
| Catalog 加载失败 | Low | High | Error 状态 + 显示错误提示，不可降级运行 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (QueryFragmentsByTag, 40 碎片) | < 0.5ms |
| CPU (GetTagsForFragment) | < 0.05ms (Dictionary lookup) |
| Memory (EmotionalTagCatalog, 20 标签) | ~5KB |
| Memory (子标签预计算索引) | ~1KB |
| GC Allocation | 0 (复用列表引用) |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] EmotionalTagCatalog 定义 15-20 个标签，启动时加载成功
- [ ] QueryFragmentsByTag("love") 返回所有 love 和 nostalgia 标记的碎片
- [ ] GetPrimaryTag 返回 IsPrimary=true 的标签，无则为 null
- [ ] IncompatibleWith 标签对在编辑器中报错
- [ ] 标签重命名后所有引用碎片的 TagId 自动更新
- [ ] GetResolvedWeight 返回 BaseWeight + 叠加层效果（夹紧到 [0, 1]）
- [ ] 子标签权重不继承父标签权重

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `emotional-tag-system.md` (#10) | 情感标签 | EmotionalTagCatalog 词汇表 | ScriptableObject 集中定义 |
| `emotional-tag-system.md` (#10) | 情感标签 | 标签层级（最多 2 层） | ParentTagId + 子标签预计算索引 |
| `emotional-tag-system.md` (#10) | 情感标签 | 查询 API | GetTagsForFragment, QueryFragmentsByTag, etc. |
| `emotional-tag-system.md` (#10) | 情感标签 | 互斥规则 (IncompatibleWith) | AreIncompatible + 编辑器验证 |
| `emotional-tag-system.md` (#10) | 情感标签 | 编辑器工具 | Emotional Tag Browser 窗口 |
| `web-association-engine.md` (#13) | 关联引擎 | TagSimilarity 计算 | 因子 A — 标签余弦相似度输入 |
| `memory-fragment-data-model.md` (#8) | 数据模型 | EmotionalTags 列表 | MemoryFragment.EmotionalTags 消费 |

## Related

- ADR-0007 — ChangeTracker 管理 ModifyTagWeight 叠加层
- ADR-0009 — WebAssociationEngine 消费标签相似度（因子 A）
- ADR-0010 — MultiEnding 使用标签条件
- ADR-0002 — IDataManager 提供碎片查询
- `design/gdd/emotional-tag-system.md` — 完整 GDD
