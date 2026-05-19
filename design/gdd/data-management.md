# 数据管理系统 (Data Management)

> **Status**: In Design
> **Author**: 用户 + game-designer + gameplay-programmer
> **Last Updated**: 2026-05-11
> **Implements Pillar**: Pillar 3 (关联的网络) — 间接支撑——数据层是碎片关联结构的物理载体

## Overview

数据管理系统是《回响》中所有游戏内容的"图书馆"。每一段记忆碎片——它的画面、情感标签、可触碰的物件、可能的变化结果——都存储在这里，像一个静静等待被翻阅的档案室。当玩家触碰记忆画卷中的一个物件，数据管理系统将那个碎片的配置加载到场景中；当玩家做出选择改变了记忆，数据管理系统提供变化所需的原始数据。

它是纯服务层——玩家永远不会"打开数据管理系统"。但当一段被遗忘的记忆因为玩家的选择重新浮现时，是这个系统在幕后找到了它。没有它，每一幅画卷都是空白的纸。

## Player Fantasy

这不是一排排整齐的书架，而是一座按情感排列的藏书阁。一封信旁边放着同一种颜色的围巾，一首歌紧挨着一扇相似的窗。当你做出选择，这座图书馆无声地重新排列——有些卷轴被推到更显眼处，有些退回深处的暗格。

每一段记忆归档时用的不是永久的墨，而是会被你的选择轻轻擦改的笔迹。这座图书馆保存的不只是画面，还有画面可能变成的每一种样子。被翻阅太多次的记忆边角已磨损，但那些藏在最深处的——墨迹反而最清晰。你渐渐明白：在这座图书馆里，最珍贵的从不摆在最显眼的地方。

## Detailed Design

### Core Rules

**规则 1 — ScriptableObject 结构**：采用 Chapter SO 清单 + 独立 Fragment SO 的两层结构。
- `ChapterDefinition`（ScriptableObject）：包含章节元数据 + `AssetReferenceT<MemoryFragment>[]` 引用数组
- `MemoryFragment`（ScriptableObject）：每个碎片一个独立 SO，包含画面引用、情感标签列表、交互物件定义、选项分支、内容变化条件、关联权重
- 总计：4 个 ChapterDefinition SO + 60-100 个 MemoryFragment SO
- 编辑时可单独编辑任一碎片而不影响其他碎片（Git 友好）

**规则 2 — 元数据加载策略**：所有碎片元数据在游戏启动时一次性加载到内存。
- 每个碎片定义约 5-10KB，全部 60-100 个总计约 500KB-1MB——可忽略不计
- 加载后所有系统随时可查询任意碎片（包括跨章节查询）
- 插图大图按章节通过 Addressables 按需异步加载

**规则 3 — 数据格式分工**：
- **ScriptableObject**：设计时创作——碎片定义、章节配置、情感标签词汇表、结局条件
- **JSON**（System.Text.Json）：运行时数据——存档状态、章节进度、关联引擎配置、本地化字符串表

**规则 4 — 运行时修改模式（不可变 SO + 变化叠加层）**：
- ScriptableObject 在运行时为只读——永不修改
- 记忆变化追踪系统维护 `Dictionary<(fragmentId, choiceId), ContentOverrides>` 叠加层
- 数据查询时合并基础 SO 数据 + 叠加层——读操作对调用方透明
- 序列化存档时只需保存叠加层（小而确定的增量），而非整个碎片状态

**规则 5 — Addressables 分组**（10 组）：

| 组名 | 内容 | 加载时机 | 驻留 |
|------|------|----------|------|
| Data_Ch01–04 | Chapter SO + Fragment SOs | 启动时（元数据） | 始终驻留 |
| Art_Ch01–04 | 插图 Sprite（每章 20-25 张） | 章节入口预加载 | 切换章节时卸载旧章 |
| Shared_UI | 字体、UI 精灵、共享 UI 资产 | 启动时 | 始终驻留 |
| Shared_Audio | 环境音、共享音效 | 启动时 | 始终驻留 |

- 章节过渡优化：当前章节最后 2-3 个碎片期间，后台预加载下一章的插图组（`DownloadDependenciesAsync`）。到达章节边界时，下一章已缓存。

**规则 6 — 异步 API（Task-based + 三态就绪模型）**：

| 状态 | 含义 | 行为 |
|------|------|------|
| Cached | 数据在内存中 | 同步访问即时返回 |
| Loading | 加载操作进行中 | Await Task 等待完成 |
| Not Requested | 未开始加载 | 自动启动加载，返回 Task |

核心接口：
- `Task<ChapterDefinition> GetChapterAsync(string key)` — 获取章节定义
- `Task<MemoryFragment> GetFragmentAsync(string chapterKey, string fragmentId)` — 获取碎片定义
- `Task<Sprite> GetIllustrationAsync(string assetKey)` — 获取插图
- `Task PreloadChapterAsync(string key)` — 预加载章节（fire-and-forget）
- `bool IsReady(string assetKey)` — 同步查询加载状态

**规则 7 — 数据验证（三层）**：
- **编辑器层**：自定义 `MemoryFragment` Inspector 顶部显示绿色/黄色/红色验证状态点。`Window > 回响 > Validate Fragments` 菜单批量扫描
- **构建层**：Addressables 构建回调——交叉检查 SO 中的 `AssetReference` 与实际 Addressables 目录
- **运行时**：加载失败时抛出描述性异常，不做静默降级——叙事游戏中缺失碎片是不可恢复的硬错误

**规则 8 — 热重载**：
- DataManager 持有对已加载 SO 的直接引用，不缓存数据副本
- 编辑器中修改 SO → Play Mode 即时反映
- JSON 运行时数据通过 `MenuItem "回响/Reload Runtime Data"` 手动重载
- 仅在 Editor 的 Addressables Fast Mode 下有效（默认模式）

**规则 9 — 异常安全**：
- Unity 6.2+ Addressables 加载失败抛出异常（非返回 null）——所有加载调用包裹在 try/catch 中
- 加载失败 → 显示明确错误信息 → 返回主菜单

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Uninitialized** | 游戏刚启动，数据层未初始化 | 引擎启动 |
| **LoadingMetadata** | 正在加载所有碎片元数据 | Uninitialized → 自动进入 |
| **Ready** | 元数据就绪，可处理查询。插图按需加载 | 元数据加载完成 |
| **PreloadingChapter** | 正在后台预加载下一章的插图 | Ready 状态下触发预加载 |
| **Error** | 关键数据加载失败 | 任意状态的加载异常 |

**状态转换**：
- Uninitialized → LoadingMetadata（自动）
- LoadingMetadata → Ready（成功）/ → Error（失败）
- Ready ↔ PreloadingChapter（预加载开始/完成）
- Error → Uninitialized（返回主菜单重试）

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 下游 | **场景管理** (#6) | `PreloadChapterAsync`, `GetFragmentAsync` | 场景过渡前预加载目标章数据 |
| 下游 | **存档系统** (#7) | `SerializeState<T>`, `DeserializeState<T>` | JSON 序列化/反序列化运行时状态 |
| 下游 | **记忆碎片数据模型** (#8) | `GetFragmentAsync`, `QueryFragmentsByTag` | 提供碎片定义数据 |
| 下游 | **章节管理** (#15) | `GetChapterAsync` | 提供章节配置 |
| 下游 | **本地化** (#4) | `LoadStringTable` | 加载本地化字符串表 |

## Formulas

数据管理系统不包含数学公式。它是一个数据加载、缓存和查询的服务层。所有数据的存储格式和查询逻辑由数据本身的 Schema 决定（归记忆碎片数据模型 #8 管辖），不涉及运行时计算。

## Edge Cases

- **如果 ScriptableObject 文件在运行时损坏**：反序列化失败→抛出 `DataLoadException`，DataManager 进入 Error 状态，显示错误信息并允许返回主菜单重试
- **如果碎片引用的插图资源不在 Addressables 目录中**：抛出描述性异常（含碎片 ID 和缺失的资源 Key），构建层验证应在此前捕获此问题
- **如果 Addressables 目录与 SO 不匹配**：构建时 `IPreBuildValidation` 回调捕获。运行时版本不匹配→Error 状态
- **如果 JSON 存档文件格式无效**：`System.Text.Json` 反序列化异常被包装为 `DataLoadException`，放弃损坏的存档，提示玩家"存档文件已损坏"
- **如果两个系统同时请求同一个尚未加载的资产**：返回同一个 `Task` 引用——不发起重复加载。Task 完成时两个调用方同时收到结果
- **如果一个章节没有定义任何碎片**：ChapterDefinition 的 fragment 数组为空——加载成功，场景管理器收到空列表，显示"此章节没有记忆"占位画面
- **如果预加载下一章时玩家迅速跳过当前章最后 3 个碎片**：预加载可能未完成。场景管理器在章节过渡时 Await 预加载 Task——如果已完成则立即返回，如果未完成则等待
- **如果运行时内存紧张**：章节切换时通过 `Addressables.Release` 释放旧章插图。预加载回调中检查内存压力并决定是否保持预加载
- **如果 Fragment SO 的 AssetReference 指向另一个 Fragment SO（循环引用）**：设计上不允许——Fragment SO 之间不互相引用。编辑器验证检查 `AssetReference` 目标类型

## Dependencies

**硬依赖**：无。数据管理系统是 Foundation 层系统，无上游依赖。

**下游系统**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| 场景管理 (#6) | 硬依赖 | PreloadChapterAsync, GetFragmentAsync |
| 存档系统 (#7) | 硬依赖 | SerializeState<T>, DeserializeState<T> |
| 记忆碎片数据模型 (#8) | 硬依赖 | GetFragmentAsync, QueryFragmentsByTag |
| 章节管理 (#15) | 硬依赖 | GetChapterAsync |
| 本地化 (#4) | 软依赖 | LoadStringTable |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Preload Trigger Threshold | 3 fragments remaining | 1–5 | 当前章剩余多少碎片时触发下一章预加载 |
| Metadata Load Timeout | 10s | 5–30s | 元数据加载超时——超过此时间进入 Error 状态 |
| Max Concurrent Async Loads | 5 | 3–10 | 同时进行的 Addressables 异步加载上限 |
| Chapter Illustration Cache | Current + Next | Current Only / Current + Next / All | 内存中保留几个章节的插图 |

## Visual/Audio Requirements

数据管理系统本身不产生视觉或音频输出。加载过程中的过渡效果（淡入淡出、加载画面）由场景管理系统 (#6) 负责。

## UI Requirements

数据管理系统本身没有独立的 UI。编辑器工具窗口（`Window > 回响 > Validate Fragments`）是开发工具，不属于游戏 UI。

## Acceptance Criteria

- **GIVEN** 游戏启动，**WHEN** 引擎完成初始化，**THEN** DataManager 自动加载所有碎片元数据，进入 Ready 状态。元数据加载时间 < 2 秒。
- **GIVEN** DataManager 处于 Ready 状态，**WHEN** 任意系统调用 `GetFragmentAsync("ch1", "frag_01")`，**THEN** 返回该碎片的完整定义（包含情感标签、交互物件、选项分支），延迟 < 50ms（内存中）。
- **GIVEN** 玩家在章节 1 最后一个碎片，**WHEN** 剩余碎片数 ≤ 3，**THEN** 自动触发 `PreloadChapterAsync("ch2")`，后台加载章节 2 的插图。到达章节 2 时插图已就绪。
- **GIVEN** 某碎片的插图尚未加载，**WHEN** 调用 `GetIllustrationAsync("art_ch1_letter")`，**THEN** 返回 `Task<Sprite>`——Await 后获得 Sprite 对象。
- **GIVEN** 碎片 SO 引用的插图 Asset Key 不在 Addressables 目录中，**WHEN** 尝试加载该插图，**THEN** 抛出 `DataLoadException`，包含碎片 ID 和缺失的 Asset Key。
- **GIVEN** DataManager 处于 Ready 状态，**WHEN** 调用 `SerializeState(playerProgress)`，**THEN** 返回有效的 JSON 字符串——包含所有需要持久化的运行时数据。
- **GIVEN** 一份有效的 JSON 存档字符串，**WHEN** 调用 `DeserializeState<PlayerProgress>(json)`，**THEN** 返回正确填充的 PlayerProgress 对象。
- **GIVEN** 编辑器中使用 Play Mode + Addressables Fast Mode，**WHEN** 修改一个 MemoryFragment SO 的值并保存，**THEN** 下次查询该碎片时反映新值——无需重新启动 Play Mode。

## Open Questions

- **System.Text.Json 的 .NET 运行时版本**：Unity 6.3 的确切 .NET 运行时（.NET Standard 2.1 vs .NET 8）需要验证——System.Text.Json 的 API 层面在其中差异显著。如果仅为 .NET Standard 2.1，应考虑降级使用 Newtonsoft.Json。验证方法：运行时输出 `System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription`
- **MemoryFragment 的完整 Schema**：碎片的具体字段（情感标签结构、选项分支格式、变化条件语法）由记忆碎片数据模型 (#8) 定义。数据管理系统只负责加载和缓存——但这两个系统需协调，建议连续设计
