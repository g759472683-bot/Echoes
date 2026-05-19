# 情感标签系统 (Emotional Tag System)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 3 (关联的网络) — 直接支撑——情感标签是碎片之间"联想"的 DNA

## Overview

情感标签系统是《回响》中记忆碎片的"嗅觉系统"。它定义了一个有限的标签词汇表——每一种标签代表一种人类情感的原色（怀念、遗憾、希望、恐惧、温柔、孤独……），并为每个碎片分配一个或多个标签及权重。当网状关联引擎 (#13) 需要在碎片之间建立联想时，它首先查询这个系统——"哪些碎片和这个碎片闻起来相似？"

标签词汇表是游戏中最稳定的数据之一——在设计初期确定，后续碎片创作时从这个词汇表中选择标签。标签之间的层级关系（如"怀念"是"爱"的子标签）和互斥规则（同一碎片上"希望"和"绝望"不能同时为主标签）构成了情感逻辑的骨架。这个系统不定义"如何关联"——它只提供"用什么关联"的原始词汇。

## Player Fantasy

如果每个记忆碎片是一幅画，情感标签就是这幅画散发的**气味**。你看不见它——但当你站在画前，某种说不清的熟悉感穿过画面飘过来。你在一幅童年院子的画中闻到雨后的泥土味，又在另一幅青年雨夜的窗前重逢了同样的气息。两个碎片之间没有任何文字或线索相连——只有这种气味在暗中牵引你。你不需要理解"标签系统"——你只需要相信：**那些闻起来相似的东西，在你的记忆里本来就是连在一起的。**

## Detailed Design

### Core Rules

**规则 1 — 标签词汇表：有限且稳定的情感词典**

`EmotionalTagCatalog`（ScriptableObject，全局唯一）定义游戏中所有可用的情感标签：

```
EmotionalTagCatalog
├── Tags[]
│   ├── TagId: "nostalgia"
│   ├── DisplayName: TableReference ("怀念")
│   ├── Category: Joy | Sadness | Love | Fear | Anger | Wonder | Melancholy | Peace
│   ├── ParentTagId: "love" (可选——层级关系)
│   ├── IncompatibleWith: ["despair"] (不可同时为主标签)
│   ├── AssociatedColors: { primary: "#D4A574", secondary: "#8B7355" }
│   └── Description: "对过去某人或某物的温暖回忆，带有失去的底色"
```

- 标签总数：MVP 15-20 个，Full Vision ≤ 30 个
- Category 固定为 8 类——Joy, Sadness, Love, Fear, Anger, Wonder, Melancholy, Peace
- 标签词汇表是设计时资产——运行时不新增或删除标签
- TagId 为字符串常量——在代码和数据中引用

**8 个情感类别（Category）**:

| Category | 中文 | 示例标签 |
|----------|------|---------|
| Joy | 喜悦 | 欢乐、满足、童真 |
| Sadness | 悲伤 | 失去、遗憾、孤独 |
| Love | 爱 | 怀念、温柔、依恋 |
| Fear | 恐惧 | 不安、焦虑、惊恐 |
| Anger | 愤怒 | 怨恨、不甘、反抗 |
| Wonder | 惊奇 | 好奇、敬畏、梦幻 |
| Melancholy | 愁思 | 乡愁、怀旧、感伤 |
| Peace | 平静 | 安宁、接纳、释然 |

**规则 2 — 标签层级：最多 2 层**

标签支持可选的父子关系——ParentTagId 为 null 则为根标签：

| 层级 | 示例 | 说明 |
|------|------|------|
| **根标签 (Root)** | `love` (爱) | 顶层情感类别。ParentTagId = null |
| **子标签 (Child)** | `nostalgia` (怀念) → 父: `love` | 更精细的情感色调。最多 1 层深度 |

- 查询父标签时自动包含其所有子标签：`QueryFragmentsByTag("love")` → 返回所有标记了 `love` 或 `nostalgia` 的碎片
- 子标签权重独立——`nostalgia` 权重不继承 `love` 的权重

**规则 3 — 碎片的情感标签分配**

每个 MemoryFragment（数据模型 #8）通过 `EmotionalTags` 列表携带标签：

| 字段 | 类型 | 说明 |
|------|------|------|
| `TagId` | `string` | 引用 EmotionalTagCatalog 中的标签 |
| `BaseWeight` | `float` [0.0, 1.0] | 标签在该碎片上的基础权重 |
| `IsPrimary` | `bool` | 是否为主导标签——每碎片最多 1 个 |

- 每碎片至少 1 个标签（编辑器验证）
- 每碎片最多 5 个标签
- `IsPrimary` 为 true 的标签作为"这个碎片的气味主调"——关联引擎 (#13) 用于情感节奏控制
- 同一碎片上不能有互斥标签对（IncompatibleWith）同时为 IsPrimary

**规则 4 — 标签权重：定义、查询与修改**

- `BaseWeight` 是设计时设定的默认权重——代表这个标签在该碎片上的"强度"
- 运行时权重 = `BaseWeight × 玩家选择产生的 ModifyTagWeight 叠加效果`
- 叠加效果由记忆变化追踪 (#12) 的叠加层管理——本系统仅提供读取接口

**查询 API**:

| 方法 | 返回 | 说明 |
|------|------|------|
| `GetTagsForFragment(string fragmentId)` | `List<EmotionalTag>` | 返回碎片的所有标签（含当前权重） |
| `GetPrimaryTag(string fragmentId)` | `EmotionalTag?` | 返回碎片的主标签，无则为 null |
| `QueryFragmentsByTag(string tagId, float minWeight = 0.0)` | `List<string>` (FragmentId列表) | 查找带有指定标签且权重 ≥ minWeight 的所有碎片。包含子标签 |
| `GetTagCategory(string tagId)` | `Category` | 返回标签所属的情感类别 |
| `GetRelatedTags(string tagId)` | `List<string>` | 返回同一 Category 下的兄弟标签 + 同父的子标签 |

**规则 5 — 标签互斥规则**

`IncompatibleWith` 列表定义不可同时为主标签的标签对：

| 规则 | 示例 | 原因 |
|------|------|------|
| 同一碎片的两个标签如互斥 → 不能都设为 IsPrimary | `hope` 和 `despair` | 情感逻辑矛盾 |
| 互斥标签仍然可以同时存在为**非主标签** | 碎片可以同时携带 `hope` (权重 0.3) 和 `despair` (权重 0.7) | 复杂情感——矛盾是人之常情。只有"主调"不能分裂 |
| 编辑器验证：保存时检查 IsPrimary 互斥 | — | 运行时不验证 |

**规则 6 — 情感节奏控制的输入**

网状关联引擎 (#13) 使用本系统提供的数据实现情感节奏控制——本系统只提供查询，不实现节奏算法：

- `GetPrimaryTag(fragmentId)` → 关联引擎获取当前碎片的情感主调
- `QueryFragmentsByTag(tagId)` → 关联引擎找到同情感的候选碎片
- `Category` → 关联引擎防止连续同类别碎片（如连续 3 个 Sadness 类别——情感节奏的"反重复"机制）

**规则 7 — 编辑器中的标签浏览器**

Unity Editor 中提供 `Window > 回响 > Emotional Tag Browser` 工具窗口：
- 以树形视图展示完整标签词汇表（Group by Category）
- 显示每个标签被多少碎片引用
- 支持重命名标签（自动更新所有引用碎片的 TagId）
- 检测孤立标签（无碎片引用）——标记为"未使用"

### States and Transitions

本系统在运行时无状态机。标签词汇表加载后只读。标签查询是纯函数——输入 fragmentId → 输出标签列表。

唯一的状态变化：`EmotionalTagCatalog` 在 Boot 场景加载 → 进入 Ready 状态。加载失败 → Error 状态（不可恢复——标签词汇表是基础数据）。

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **记忆碎片数据模型 (#8)** | `MemoryFragment.EmotionalTags` | 读取每个碎片的标签分配 |
| 上游 | **记忆变化追踪 (#12)** | 叠加层中的 `ModifyTagWeight` 效果 | 读取玩家选择对标签权重的修改（通过叠加层合并到 BaseWeight） |
| 下游 | **网状关联引擎 (#13)** | `GetPrimaryTag`, `QueryFragmentsByTag`, `GetTagCategory` | 关联计算的原始输入——情感相似度、节奏控制 |
| 下游 | **微动画系统 (#9)** | `GetPrimaryTag` → 情绪类型映射 | 情绪驱动的动画参数选择 |
| 下游 | **多结局系统 (#14)** | `QueryFragmentsByTag` | 结局条件可能涉及"玩家是否访问过带有特定标签的碎片" |

## Formulas

本系统不含自定义数学公式。标签相似度计算和情感节奏控制归网状关联引擎 (#13)。

**本系统定义的数值与范围**:

| 字段 | 类型 | 范围 | 说明 |
|------|------|------|------|
| `BaseWeight` | float | [0.0, 1.0] | 标签在碎片上的基础权重 |
| 运行时权重 | float | [0.0, 1.0] | BaseWeight × 叠加层效果，夹紧到 [0.0, 1.0] |
| 标签总数 (MVP) | int | 15-20 | 词汇表规模 |
| 标签总数 (Full Vision) | int | ≤ 30 | |
| 每碎片标签数 | int | 1-5 | |

## Edge Cases

- **如果碎片没有任何 EmotionalTag（空列表）**: 编辑器验证报错——"每个碎片必须至少有一个情感标签"。运行时若发生（构建阶段漏检）→ 该碎片在关联网络中为孤立节点，只能通过 ExplicitAssociations 到达。关联引擎跳过此碎片。

- **如果 TagId 引用不存在于 Catalog 中的标签**: 编辑器验证报错——"标签 [id] 不存在于 EmotionalTagCatalog"。运行时跳过该无效标签，记录 Warning。

- **如果碎片有两个标签互为 IncompatibleWith，且都被设为 IsPrimary**: 编辑器验证报错——"标签 [A] 和 [B] 不能同时为主标签"。运行时不验证（信任构建时检查）。

- **如果 ModifyTagWeight 将权重推到 [0.0, 1.0] 范围外**: 运行时夹紧。Category 不受权重影响（Category 是标签的固有属性）。

- **如果设计师在 Catalog 中删除了一个正在被 10+ 碎片引用的标签**: 编辑器提供"安全删除"——先列出所有引用碎片，确认后自动清理这些碎片中的该标签。不允许静默删除导致运行时断裂。

- **如果 ParentTagId 形成循环（A 的父是 B，B 的父是 A）**: 编辑器验证检测循环引用——"标签层级存在循环"。运行时不验证。

- **如果 Catalog 加载失败**: 进入 Error 状态——标签词汇表是基础数据，不可降级运行。显示"情感标签数据加载失败"，返回主菜单。

## Dependencies

**硬依赖**:

| 系统 | 性质 | 接口 |
|------|------|------|
| **记忆碎片数据模型 (#8)** | 硬依赖 | `MemoryFragment.EmotionalTags` — 标签分配的唯一数据源 |

**软依赖**:

| 系统 | 性质 | 接口 |
|------|------|------|
| **记忆变化追踪 (#12)** | 软依赖 | 叠加层中的 ModifyTagWeight 效果 — 若 #12 未就绪，使用 BaseWeight 作为最终权重 |

**下游系统** (全部硬依赖): 网状关联引擎 (#13)、微动画系统 (#9)、多结局系统 (#14)

**双向一致性**: 数据模型 GDD (#8) 的 Interactions 表中已列出"下游: 情感标签系统 (#10)"——方向匹配 ✅

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 标签词汇表大小 (MVP) | 18 | 15–20 | 太少关联网络单调，太多设计师选择困难 |
| 标签词汇表大小 (Full Vision) | 25 | 20–30 | |
| 每碎片最大标签数 | 5 | 3–7 | 过多标签稀释每个标签的区分力 |
| BaseWeight 默认值 | 0.5 | 0.3–0.7 | 新标签在没有特殊理由时的默认权重 |
| 层级最大深度 | 2 | 1–2 | 当前固定为 2——根标签 + 1 层子标签 |

## Visual/Audio Requirements

本系统不产生视觉或音频输出。编辑器工具（Emotional Tag Browser 窗口）属于开发工具。

## UI Requirements

本系统不包含玩家可见 UI。调试用途的标签显示（开发阶段在 HUD 上显示当前碎片的情感标签）由游戏内 HUD (#17) 可选实现——不属于本系统范围。

## Acceptance Criteria

- **GIVEN** EmotionalTagCatalog 定义了 18 个标签，**WHEN** 游戏启动完成，**THEN** Catalog 加载到内存——所有标签的 TagId、Category、DisplayName 可查询。加载时间 < 100ms。

- **GIVEN** 碎片 A 有 3 个标签（`nostalgia` weight=0.8, `peace` weight=0.4, `loss` weight=0.6），**WHEN** 调用 `GetTagsForFragment("frag_A")`，**THEN** 返回 3 个标签及各自权重。`GetPrimaryTag("frag_A")` 返回 IsPrimary=true 的那个。

- **GIVEN** `nostalgia` 的父标签是 `love`，**WHEN** 调用 `QueryFragmentsByTag("love")`，**THEN** 返回所有标记了 `love` **和** 所有标记了 `nostalgia` 的碎片 ID 列表。`nostalgia` 碎片的权重不受父标签查询影响。

- **GIVEN** 碎片 B 的 `hope` 和 `despair` 都被设为 IsPrimary=true，**WHEN** 编辑器验证运行，**THEN** 报错——"标签 hope 和 despair 互斥，不能同时为主标签"。

- **GIVEN** 标签 `anxiety` 在 Catalog 中被 8 个碎片引用，**WHEN** 设计师在 Emotional Tag Browser 中重命名为 `fear_subtle`，**THEN** 所有 8 个碎片的 TagId 自动更新。引用完整性保持。

- **GIVEN** 玩家在碎片 C 中做了一个选择触发了 ModifyTagWeight（`nostalgia` +0.2），**WHEN** 调用 `GetTagsForFragment("frag_C")`，**THEN** `nostalgia` 的返回权重 = BaseWeight + 0.2（夹紧到 [0.0, 1.0]）。

- **GIVEN** 碎片 D 的 EmotionalTags 列表为空（设计错误），**WHEN** 编辑器验证运行，**THEN** 报错"碎片 [D] 无情感标签"。运行时若发生 → 记录 Warning，关联引擎跳过此碎片。

## Open Questions

- **MVP 标签词汇表的具体内容**: 当前定义了 8 个 Category 但未确定具体的 15-20 个标签。需 game-designer + narrative-director 在碎片内容创作前确定最终标签列表。建议 Workshop 形式——列出候选标签，每章分配标签频次目标。（Owner: game-designer）

- **标签颜色映射的使用方**: `AssociatedColors` 字段定义了每个标签的视觉颜色——但这个颜色具体由哪个系统使用？可能是过渡动画 (#6) 的色调渐变，或 HUD (#17) 的情感指示器。当前先定义字段，使用方由后续 GDD 决定。（Owner: art-director + game-designer）

- **跨语言标签名称**: TagId 使用英文字符串常量（如 `nostalgia`），DisplayName 通过 TableReference 本地化。这是否意味着 TagId 不可本地化？——是。TagId 是程序标识符，DisplayName 是面向设计师和调试工具的本地化显示。（Owner: localization-lead）

