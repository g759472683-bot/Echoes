# 本地化系统 (Localization)

> **Status**: In Design
> **Author**: 用户 + localization-lead + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 间接支撑——多语言支持让不同语种的玩家都能进入记忆画卷

## Overview

本地化系统是《回响》中所有玩家可见文本的语言管理层。它基于 Unity Localization Package（`com.unity.localization`）构建——使用包的原生 StringTable、Locale、LocalizedString 架构——以简体中文（zh-Hans）为第一语言和开发基准语言，MVP 阶段额外支持英语（en）。系统为 UI 文本、记忆碎片叙述、系统消息提供键值查找（key → locale → string），通过 Unity LP 的内置 Fallback 链（en → zh-Hans）确保缺失翻译自动回退到中文原文。

在技术层面，它是一个配置与协调层：定义 Locale 集合、组织 StringTable 结构（1 个全局 UI 表 + 按章节分割的叙事表）、制定 Key 命名约定、管理 Google Sheets → CSV → StringTable 的导入流程。字符串表的加载和生命周期由 Unity LP 通过 Addressables 内部管理——本地化系统不编写自定义加载逻辑。所有 UI 文本通过 `LocalizedString` 组件在 Inspector 中绑定，运行时 Locale 切换时自动刷新，无需 UI 程序员编写本地化代码。与 Data Management (#2) 的关系修正：原 `LoadStringTable` 软依赖被移除——StringTable 不再由 DataManager 加载。

## Player Fantasy

《回响》的中文原文是投向水面的第一颗石子。本地化不是"翻译"——它是同一颗石子在不同语言的水面上，激起的层层回响。一句记忆独白在中文里沉入水底，在英语里从另一片水域浮起；在日本语里荡开，在韩语里回旋。它们是同一圈涟漪的延续，只是波长不同。

当"母亲的信"在你的母语中展开——你看到的不是"译稿"，而是同一只手、同一种墨、在不同的纸上，被同等的真挚重新落笔。当游魂的低语在你的母语中响起，那不是"别人的故事被你读了"——而是这段记忆选择了用你能听见的频率说话。在这座回声的殿堂里，没有原文与译文的等级，没有"源语言"与"目标语言"的高低——只有同一颗石子，在不同的水中，响了又响。

## Detailed Design

### Core Rules

**规则 1 — Unity Localization Package 原生架构**：系统基于 `com.unity.localization` 构建，不使用自定义 JSON 映射层。

- 使用包提供的原生类型：`Locale`、`StringTable`、`LocalizedString`、`TableReference`、`TableEntryReference`、`LocalizationSettings`
- 本地化系统的职责是**配置与协调**——设定 Locale、组织表结构、定义命名约定、管理导入流程、处理缺失——而非重新实现字符串查找和 Fallback（这些由包本身提供）
- 与 Data Management (#2) 的关系修正：Data Management 的 `LoadStringTable` 软依赖被移除——StringTable 的加载和生命周期由 Unity LP 通过 Addressables 内部管理，Data Management 不需要感知其存在

**规则 2 — Locale 配置与 Fallback 链**：

- 默认 Locale（开发基准语言）：`zh-Hans`（简体中文）
- MVP 阶段额外 Locale：`en`（英语）
- 所有 Locale 注册在 `LocalizationSettings` 资产中
- Fallback 链：`en → zh-Hans`。当英语翻译缺失时自动回退到中文原文
- Unity LP 内置 Fallback 机制处理所有回退逻辑——无需自定义 Fallback 代码

**规则 3 — StringTable 结构（1 + N 表设计）**：

| StringTable | 内容 | 加载时机 | Addressables 组 |
|-------------|------|----------|-----------------|
| `UI_Shared` | 所有 UI 文本：菜单、HUD、设置、按钮、系统消息 | 启动时 | Shared_UI（始终驻留） |
| `Narrative_Ch01` | 第 1 章全部记忆碎片叙述文本 | 章节入口预加载 | Data_Ch01 |
| `Narrative_Ch02` | 第 2 章全部记忆碎片叙述文本 | 章节入口预加载 | Data_Ch02 |

- MVP 阶段（2 章）：3 个表。叙事表按章节分割——与 Data Management 章节粒度对齐
- 同一 Table Collection 自动包含所有已配置 Locale 的翻译

**规则 4 — Key 命名约定**：三段式点分命名法：`[域].[子系统].[元素]_[属性]`。

| 域 | 前缀 | 示例 | 说明 |
|----|------|------|------|
| UI | `ui.[screen].[element]_[prop]` | `ui.menu.settings.volume_label` | 屏幕、面板、按钮标签 |
| 叙事 | `narrative.[chapter].[fragment]_[seg]` | `narrative.ch01.frag_03.line_01` | 碎片叙述行 |
| 系统 | `system.[category].[message]` | `system.save.overwrite_confirm` | 系统消息 |

- Key 全小写，下划线分隔，仅 ASCII。变量占位符使用 `{VariableName}` 语法（Unity LP Smart String 格式）
- 每个 Key 在 SharedTableData Metadata 栏中必须包含 Context Comment

**规则 5 — StringTable 创作与导入流程**：

```
Google Sheets (中英双列) → CSV → Unity LP CSV Import → StringTable .asset → Git
```

- 主创作方式：Google Sheets——翻译人员在 en 列填写译文
- CSV 导入：Sheets 更新后导出 CSV → Unity Localization CSV Import 覆盖 StringTable
- 辅助方式：Unity Editor 直接编辑（适用于少量修改和紧急修复）
- CSV 源文件和 StringTable .asset 均提交 Git

**规则 6 — 运行时 Locale 切换机制**：

- 切换入口：`LocalizationSettings.SelectedLocale = targetLocale;`
- Unity LP 自动刷新所有 `LocalizedString` 组件——文本在当帧更新，无加载画面
- 叙事表未加载时：先异步加载再完成切换；加载期间当前叙事文本保持不变
- 持久化：当前 Locale 标识符写入存档——恢复游戏时激活上次选择的语言

**规则 7 — Fallback 与缺失翻译处理**：

- 利用 Unity LP 内置 Fallback 链，不编写自定义 Fallback 逻辑
- 层级：目标 Locale Entry → SharedTableData Fallback → Locale Fallback 链（en → zh-Hans）
- 如果 Key 在所有层级都不存在：注册 `MissingTranslationEvent` 回调输出警告日志
- **禁止**向玩家显示原始 Key 名

**规则 8 — 与 UI 文本及叙事文本的集成**：

- **静态 UI**：`LocalizedString` 组件 Inspector 绑定——UI 程序员不需写本地化代码
- **动态文本**：代码中 `new LocalizedString { ... }` + `Arguments` 传参（Smart String 格式化）
- **叙事长文本**：MemoryFragment SO 中不直接存文本字符串——存储 `TableReference` + `TableEntryReference` 两个序列化字段。叙事显示系统运行时通过 `GetLocalizedString(tableRef, entryRef)` 获取当前 Locale 文本

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Uninitialized** | LocalizationSettings 尚未加载 | 引擎启动 |
| **Initializing** | UI_Shared 表异步加载中 | 自动进入 |
| **Ready** | 默认 Locale（zh-Hans）激活，UI_Shared 可查询 | 加载成功 |
| **SwitchingLocale** | 玩家选择新语言，正在激活目标 Locale | 玩家更改语言设置 |
| **LoadingNarrativeTable** | 异步加载当前章节 Narrative 表 | 章节过渡 / Locale 切换触发 |
| **Error** | UI_Shared 加载失败 | 加载异常 |

**状态转换**：
- Uninitialized → Initializing（自动）
- Initializing → Ready（成功）/ → Error（失败）
- Ready ↔ SwitchingLocale（语言切换）
- Ready ↔ LoadingNarrativeTable（章节过渡 / 切换后加载）
- Error → Uninitialized（返回主菜单重试）

**切换期间体验**：
- Locale 切换：UI 文本在当前帧同时刷新，无闪烁，无"部分翻译"中间状态
- 叙事表加载中：UI 不受影响。记忆碎片叙述文本使用省略号占位（`"……"`），完成后替换
- UI_Shared 失败（Error）：不可恢复硬错误——显示引擎内置英文提示并阻止进入主菜单

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游→修正 | **Data Management (#2)** | 无接口（移除 LoadStringTable） | 采用 Unity LP 后，字符串表不再是 JSON——由 LP 内部通过 Addressables 加载。Data Management 不需要 LoadStringTable 接口 |
| 下游 | **UI Framework (#5)** | LocalizedString 组件 Inspector 绑定 | UI 文本不直接设字符串——通过 LocalizedString 绑定 |
| 下游 | **Memory Fragment Data Model (#8)** | TableReference + TableEntryReference 字段 | SO 存引用而非原始文本，运行时 GetLocalizedString() 获取 |
| 下游 | **In-Game HUD (#17)** | LocalizedString 组件 | HUD 文本绑定 UI_Shared 表 |
| 下游 | **Main Menu (#19)** | LocalizedString 组件 + SelectedLocale API | 菜单文本 + 语言选择下拉列表 |
| 下游 | **Interaction Feedback (#18)** | LocalizedString 组件 | 交互反馈文本绑定 UI_Shared 表 |
| 下游 | **Save/Load (#7)** | SelectedLocale.Identifier.Code 字符串 | 存档保存语言标识符 |
| 平行 | **Audio (#3)** | 无直接接口 | MVP 无语音。Vertical Slice 若加语音需协调 |

## Formulas

本地化系统不包含数学公式。它是一个基于 Unity Localization Package 的配置与协调层——所有字符串查找、Fallback 和格式化均由包内部的 Smart String 引擎处理。本地化系统本身不定义或执行任何数学计算。

## Edge Cases

- **如果 UI_Shared 表加载失败**：Addressables 异常被捕获——系统进入 Error 状态，显示引擎内置英文错误提示（`"Failed to load UI text. Please verify game files."`）并阻止进入主菜单。不尝试降级运行——没有 UI 文本的游戏无法交互
- **如果 Narrative 表缺失某个 Key**：Unity LP 沿 Fallback 链回退。`Narrative_Ch01` 缺失 → 检查 SharedTableData Fallback → 回退到 zh-Hans 的对应 Entry（中文原文始终存在——因为它是基准语言）。返回中文文本作为降级结果
- **如果 Locale 切换时 Narrative 表尚未加载**：UI 文本（UI_Shared）即时切换。Narrative 表进入 LoadingNarrativeTable 状态——当前显示的记忆碎片文本保持原语言不变，加载完成后原地刷新为目标语言
- **如果玩家连续快速切换语言（中→英→中）**：第二次切换时第一次切换可能尚未完成（如果 Narrative 表在加载中）。Unity LP 的 `SelectedLocale` 赋值覆盖前次操作——以最后一次切换为最终目标。前一次未完成的加载被取消
- **如果 MemoryFragment SO 的 TableEntryReference 指向不存在的 Key**：运行时 `GetLocalizedString` 返回占位字符串 `"<MISSING: narrative.ch01.frag_03.line_01>"`——仅在 Development Build 中显示此占位符。Release Build 中显示通用省略号 `"……"`。`MissingTranslationEvent` 回调记录错误
- **如果 Google Sheets 中 Key 重复**：CSV 导入时 Unity LP 的 CSV Importer 检测到重复 Key 并报告错误——导入中止，当前 StringTable 保持不变。开发者在 Sheets 中修复后重新导入
- **如果英语翻译中某行意外为空**：Unity LP 的 Fallback 链自动回退到 zh-Hans 文本——显示中文而非空白。`MissingTranslationEvent` 回调记录警告
- **如果 1+N 表中的 "N" 增长到超出预期（全 4 章需要 5 个 Narrative 表）**：表结构按章节分割天然支持新章节——新增 `Narrative_Ch03` 和 `Narrative_Ch04`，放入对应 `Data_Ch` Addressables 组，无需修改架构

## Dependencies

**硬依赖**：无。本地化系统是 Foundation 层系统。Unity Localization Package（`com.unity.localization`）是外部包依赖——不在系统架构中。

**下游系统**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| UI Framework (#5) | 硬依赖 | LocalizedString 组件 |
| Memory Fragment Data Model (#8) | 硬依赖 | TableReference + TableEntryReference 字段，GetLocalizedString() |
| In-Game HUD (#17) | 硬依赖 | LocalizedString 组件 |
| Main Menu (#19) | 硬依赖 | LocalizedString 组件 + SelectedLocale API |
| Interaction Feedback (#18) | 硬依赖 | LocalizedString 组件 |
| Save/Load (#7) | 软依赖 | SelectedLocale.Identifier.Code 字符串 |

**跨 GDD 修正需求**：

| GDD | 修正内容 |
|-----|----------|
| Data Management (#2) | 移除规则 3 中"本地化字符串表"JSON 条目；移除 Interactions 中 `LoadStringTable` 软依赖行 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Default Locale | `zh-Hans` | 固定值 | 开发基准语言，不可更改 |
| Supported Locales (MVP) | `zh-Hans`, `en` | 固定值（MVP） | Vertical Slice 可扩展 |
| Locale Switch Timeout | 5s | 3–10s | Narrative 表加载超时——超时后显示中文作为降级 |

## Visual/Audio Requirements

本地化系统不产生视觉或音频输出。间接相关的要求：
- 中文字体（CJK）和英文字体（Latin）的选择和切换逻辑属于 UI Framework (#5) 的职责范围
- 本地化系统仅提供 `SelectedLocale` 信息——UI Framework 据此选择正确的字体资产

## UI Requirements

本地化系统唯一的 UI 入口是**语言选择下拉列表**——位于设置菜单中（Main Menu #19 实现）。该列表从 `LocalizationSettings.AvailableLocales` 动态生成，显示每个 Locale 的原生名称（如"简体中文"、"English"），而非代码标识符。

## Acceptance Criteria

- **GIVEN** 游戏启动，**WHEN** LocalizationSettings 初始化完成，**THEN** `UI_Shared` 表加载成功，默认 Locale（zh-Hans）激活。所有 UI 文本显示为中文
- **GIVEN** 游戏语言为中文（zh-Hans），**WHEN** 玩家在设置中切换到 English（en），**THEN** 所有 UI 文本在当帧刷新为英语——无闪烁、无"部分翻译"中间状态
- **GIVEN** 玩家切换到英语后查看一段记忆碎片叙述，**WHEN** Narrative 表已加载，**THEN** 碎片文本显示英语版本
- **GIVEN** 英语 StringTable 中某个 Key 未填写翻译，**WHEN** 系统获取该 Key 的英语字符串，**THEN** Fallback 链自动返回中文原文——不显示空白、不显示 Key 名
- **GIVEN** StringTable CSV 有更新，**WHEN** 导入新 CSV 到对应 StringTable，**THEN** Unity Editor 中的 StringTable 条目被覆盖更新。不需要重新构建
- **GIVEN** MemoryFragment SO 的 TableEntryReference 指向 `narrative.ch01.frag_03.line_01`，**WHEN** 调用 `GetLocalizedString(tableRef, entryRef)`，**THEN** 返回当前 Locale 的对应文本
- **GIVEN** 当前 Locale 为 en，**WHEN** 游戏存档被保存然后重新加载，**THEN** 恢复后语言保持为 en
- **GIVEN** UI_Shared 表加载失败，**WHEN** 游戏启动时 Addressables 抛出异常，**THEN** 系统进入 Error 状态，显示错误信息并阻止进入主菜单

## Open Questions

- **字体切换策略**：中英文使用同一字体文件（如支持 CJK+Latin 的字体）还是切换两个独立字体？同一字体更简单但文件更大——是否在 PC 2GB 预算下可接受？此决策属于 UI Framework (#5)
- **Vertical Slice 语言扩展**：若加入日语（ja）或韩语（ko），Fallback 链如何调整？`ja → en → zh-Hans` 还是 `ja → zh-Hans`（跳过英语）？
- **语音本地化**：Vertical Slice 若加入角色语音，是否需要按 Locale 加载对应的 AudioClip？由 Audio System (#3) 决定
