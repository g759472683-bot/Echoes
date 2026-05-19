# 场景管理系统 (Scene Management)

> **Status**: In Design
> **Author**: 用户 + engine-programmer + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 4 (画卷中有呼吸) — 间接支撑——场景过渡是画卷展开的方式

## Overview

场景管理系统是《回响》中记忆画卷空间的加载、卸载与过渡管理器。它封装 Unity 的异步场景加载（`LoadSceneAsync` + Addressables 资产预加载），为上层系统提供一致的内容切换接口——包括章节间的记忆空间过渡、同一章节内碎片间的画面切换、以及主菜单与游戏场景之间的往返。场景管理系统不决定"加载什么内容"——它只负责"如何加载、何时过渡、过渡期间显示什么"。没有它，记忆画卷将无法翻页。

在技术层面，它采用单场景 + Addressables 内容注入的架构：一个持久的 `Game` 场景作为容器，记忆碎片的内容（插图 Sprite、音频、文本）通过 Addressables 按需加载到已存在的 GameObject 上，而非每个碎片建一个 Unity Scene。场景过渡表现为：淡出当前内容 → 卸载旧章 Addressables → 加载新章 Addressables → 淡入新内容。过渡期间使用统一的墨韵淡入淡出效果（全屏黑色遮罩 opacity 过渡）。

## Player Fantasy

场景管理系统是基础设施——玩家不会"使用"它。但每次场景过渡的节奏，决定了画卷翻页的呼吸感。当一章结束、墨色从画面边缘向中心晕开、旧记忆缓缓隐入留白、新记忆从同样的墨色中浮现——这不是"加载画面"。这是一幅画卷正在合上，另一幅正在展开。场景管理系统唯一的工作，是让这个"合上——展开"的动作永远平滑、永远不打破卷中呼吸的节奏。

## Detailed Design

### Core Rules

**规则 1 — 场景架构：单 Game 场景 + Addressables 内容注入**：不使用多 Unity Scene 管理碎片内容。

- 项目包含 3 个 Unity Scene：
  - `Boot` — 引擎启动场景。初始化 DataManager、AudioManager、InputSystem、LocalizationSettings。完成后自动加载 `MainMenu`
  - `MainMenu` — 主菜单场景。包含标题画面的 UI Document
  - `Game` — 游戏容器场景。持久存在，所有记忆碎片内容通过 Addressables 加载到此场景中已存在的 GameObject 上

- `Game` 场景在进入游戏时加载一次，在返回主菜单时卸载一次——不在章节之间重新加载
- 记忆碎片内容切换在同一 `Game` 场景内通过以下流程完成：卸载旧碎片插图 Sprite → 加载新碎片插图 Sprite → 更新场景中的 `SpriteRenderer` / UI Document → 加载新碎片音频 → 播放过渡动画
- 不在章节间切换 `Game` 场景——章节切换是内容的重新组合，不是场景的重建

**规则 2 — 场景加载 API**：

| 方法 | 返回 | 用途 |
|------|------|------|
| `LoadSceneAsync(string sceneName)` | `Task` | 异步加载 Unity Scene（用于 Boot→MainMenu、MainMenu→Game、Game→MainMenu） |
| `TransitionToFragmentAsync(string chapterKey, string fragmentId)` | `Task` | 过渡到指定记忆碎片：卸载当前碎片内容 → 加载目标碎片内容 → 播放过渡动画。全过程返回 Task，Await 完成时碎片已就绪 |
| `PreloadNextFragmentAsync(string chapterKey, string currentFragmentId)` | `Task` | 预加载下一个碎片的内容（fire-and-forget）——提前加载插图 Sprite 和音频，使碎片切换零延迟 |
| `TransitionToChapterAsync(string chapterKey)` | `Task` | 过渡到新章节：触发 Audio System 音乐交叉淡出 → 卸载旧章 Addressables（Art_Ch + Audio_Ch）→ 加载新章 Addressables → 触发 Audio System 新章音乐。全过程约 3-5 秒 |
| `PreloadChapterAsync(string chapterKey)` | `Task` | 预加载章节的插图组和音频组。在上一章最后 2-3 个碎片时触发（Data Management 规则 6 的 Preload Trigger Threshold） |

**规则 3 — 过渡效果系统**：

单一过渡组件 `SceneFader` 管理所有视觉过渡。不使用多种过渡类型——统一使用墨韵淡入淡出。

| 过渡类型 | 效果 | 时长 | 触发 |
|---------|------|------|------|
| FragmentTransition | 全屏黑色遮罩 opacity: 0→1（淡出），然后 opacity: 1→0（淡入）。中间点切换内容 | 0.5s 淡出 + 0.5s 淡入 = 1.0s 总时长 | TransitionToFragmentAsync |
| ChapterTransition | 全屏黑色遮罩 opacity: 0→1，保持遮罩覆盖，加载新章节，然后 opacity: 1→0 | 1.0s 淡出 + 2.0-4.0s 加载（保持遮罩）+ 1.0s 淡入 | TransitionToChapterAsync |
| SceneTransition | 与 ChapterTransition 相同——全屏遮罩淡入淡出 | 1.0s 淡出 + 加载时间 + 1.0s 淡入 | LoadSceneAsync（Boot→MainMenu、MainMenu→Game 等） |

- `SceneFader` 是一个持久存在于 `Game` 场景中的 `VisualElement`，层级在所有内容之上（`picking-mode: ignore`——不阻挡鼠标事件，但视觉上覆盖全屏）
- 遮罩颜色：纯黑 (`rgb(0,0,0)`)——模拟墨色晕开
- 过渡动画使用 UI Toolkit 的 `transition` 属性（`transition-property: opacity`），不编写协程动画
- 过渡期间：所有 Action Map 进入 Inactive 状态（输入系统 GDD 规则 8）——防止过渡中玩家意外触发交互

**规则 4 — 碎片内容加载流程（TransitionToFragmentAsync）**：

```
0. 触发 OnFragmentTransitionStarted(chapterKey, fragmentId) 事件 → 交互反馈 (#18) 在过渡开始前抑制所有视觉/音频反馈
1. SceneFader.FadeOut(0.5s) → 全屏黑色遮罩覆盖
2. 卸载当前碎片：
   a. 释放当前 SpriteRenderer 的 Sprite 引用（非 Addressables.Release——那由章节粒度管理）
   b. 卸载当前碎片音频 Clip（如果已预加载而尚未播放）
3. 加载目标碎片：
   a. await DataManager.GetFragmentAsync(chapterKey, fragmentId) → 获取碎片定义
   b. await DataManager.GetIllustrationAsync(fragment.illustrationKey) → 获取插图 Sprite
   c. await AudioManager.PreloadFragmentAudioAsync(fragment.audioKeys) → 预加载音频 Clip
   d. 将 Sprite 赋值给 SpriteRenderer.sprite
   e. 更新交互物件列表（Collider2D 位置来自碎片定义）
4. SceneFader.FadeIn(0.5s) → 遮罩消失，新碎片展示
5. 触发 OnFragmentTransitioned(chapterKey, fragmentId) 事件 → 交互系统开始处理新碎片的可交互物件
```

- 步骤 3 中的 Addressables 加载：碎片插图 Sprite 已在章节预加载时加载（Art_Ch 组）——`GetIllustrationAsync` 通常从 Cached 状态立即返回
- 如果插图尚未加载（NotRequested 状态）——`GetIllustrationAsync` 自动启动加载 → `TransitionToFragmentAsync` await 该 Task
- 整个过渡的 1 秒中有 0.5 秒的遮罩覆盖时间——对加载延迟有 0.5 秒缓冲

**规则 5 — 章节预加载触发机制**：

与 Data Management 的 Preload Trigger Threshold（默认 3 个碎片剩余）协调：

- 场景管理器监听碎片切换——当 `currentFragmentIndex` 达到 `chapter.totalFragments - PreloadTriggerThreshold` 时：
  1. 调用 `DataManager.PreloadChapterAsync(nextChapterKey)` → 预加载下一章的插图
  2. 调用 `AudioManager.PreloadChapterAudioAsync(nextChapterKey)` → 预加载下一章的音频
  3. 两个 Task 并行执行（`Task.WhenAll`）
- 玩家在当前章最后 2-3 个碎片期间（通常 2-5 分钟游戏时间），预加载在后台完成
- 到达章节边界时：`TransitionToChapterAsync` await 预加载 Task——如果已完成则立即过渡；如果未完成则等待
- 如果玩家快速跳过最后 3 个碎片（<30 秒）：预加载可能未完成——过渡等待并显示遮罩直到加载完成

**规则 6 — 场景加载期间的状态管理**：

| 阶段 | Action Map | 音频 | UI | 说明 |
|------|-----------|------|-----|------|
| 过渡前 | Gameplay | 正常播放 | HUD 可见 | 玩家刚触发过渡 |
| 过渡中（遮罩覆盖） | Inactive | 音乐交叉淡出 | SceneFader 遮罩覆盖全屏 | 输入已屏蔽 |
| 内容加载中 | Inactive | 上一章音乐持续（或静音） | 遮罩保持 | Assets 异步加载 |
| 过渡完成 | Gameplay | 新章音乐淡入 | HUD 恢复 | 玩家开始交互 |

- 过渡期间 Audio Listener 不暂停——音乐交叉淡出在 Audio Mixer 层级完成，与场景加载并行
- 如果加载超时（Metadata Load Timeout 默认 10s——Data Management 规则 6）：显示"加载超时"消息 + 返回主菜单选项

**规则 7 — 错误恢复**：

- **碎片加载失败**（插图 Addressables 异常）：SceneFader 保持遮罩覆盖，显示错误信息 "记忆碎片加载失败"，提供"返回章节开头"按钮。不自动跳转——不剥夺玩家控制
- **章节加载失败**：显示错误信息 "章节加载失败"，提供"返回主菜单"和"重试"两个选项
- **Game 场景加载失败**（LoadSceneAsync 异常）：这是最严重的故障——记录完整错误堆栈，显示"游戏场景加载失败，请验证游戏文件完整性"并退出到桌面

### States and Transitions

| 状态 | 描述 | 条件 |
|------|------|------|
| **Boot** | Boot 场景正在初始化基础系统 | 引擎启动 |
| **MainMenu** | 主菜单场景激活。Game 场景未加载 | Boot 完成 → 加载 MainMenu |
| **LoadingGame** | 正在异步加载 Game 场景 + 初始章节的 Addressables | 玩家点击"开始游戏" |
| **InGame** | Game 场景激活，碎片内容已展示。玩家可交互 | Game 场景加载 + 初始碎片加载完成 |
| **FragmentTransition** | 正在过渡到同一章的新碎片（遮罩动画 + 内容切换） | 玩家触发碎片切换 |
| **ChapterTransition** | 正在过渡到新章节（卸载旧章 + 加载新章 + 音乐交叉淡出） | 玩家到达章节边界 |
| **SceneTransition** | 正在加载新 Unity Scene（Boot→MainMenu、MainMenu→Game、Game→MainMenu） | 场景切换触发 |
| **Error** | 加载失败，显示错误信息 | 任意加载异常 |

**状态转换**：
- Boot → MainMenu（Boot 初始化完成）
- MainMenu → LoadingGame（玩家开始游戏）
- LoadingGame → InGame（Game 场景 + 初始碎片加载成功）/ → Error（失败）
- InGame → FragmentTransition（碎片切换）/ → ChapterTransition（到达章节边界）
- FragmentTransition → InGame（过渡完成）
- ChapterTransition → InGame（新章加载完成）/ → Error（失败）
- InGame → SceneTransition（玩家退出到主菜单）
- Any → Error（致命故障）

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **Data Management (#2)** | PreloadChapterAsync, GetFragmentAsync, GetIllustrationAsync | 内容数据的加载和查询 |
| 上游 | **Audio (#3)** | PlayMusic, StopMusic, PreloadChapterAudioAsync, UnloadChapterAudio | 章节过渡的音乐管理 |
| 上游 | **Input (#1)** | Action Map 切换到 Inactive | 过渡期间禁用输入 |
| 下游 | **微动画 (#9)** | OnFragmentTransitioned 事件 | 碎片切换后启动微动画播放 |
| 下游 | **记忆画卷交互 (#11)** | OnFragmentTransitioned(chapterKey, fragmentId) 事件 | 碎片切换后初始化交互物件 |
| 下游 | **记忆变化追踪 (#12)** | OnFragmentTransitioned 事件 | 碎片切换后更新已访问碎片记录 |
| 下游 | **交互反馈 (#18)** | OnFragmentTransitionStarted, OnFragmentTransitioned 事件 | 过渡期间抑制反馈；过渡完成后恢复 |
| 下游 | **章节管理 (#15)** | TransitionToChapterAsync, TransitionToFragmentAsync | 章节流程控制和碎片遍历 |

## Formulas

场景管理系统不包含数学公式。它的核心逻辑是异步 Task 协调和事件发送——不涉及数学计算。

## Edge Cases

- **如果预加载未完成时玩家到达章节边界**：TransitionToChapterAsync await 预加载 Task——遮罩保持覆盖直到加载完成。玩家看到的是墨色遮罩——不是加载百分比或旋转图标
- **如果玩家在同一碎片上快速点击两个不同的可交互物件**：碎片切换本身是异步的——TransitionToFragmentAsync 进行中时忽略新的 TransitionToFragmentAsync 调用。这防止了快速双击导致的竞态条件
- **如果碎片 A→B 过渡中，插图 Sprite 未缓存（首次进入该碎片）**：加载期间遮罩保持——过渡动画的 0.5s 遮罩时间可能不足以完成加载。TransitionToFragmentAsync await GetIllustrationAsync——如果加载需要 2 秒，遮罩保持 2 秒
- **如果 Game 场景加载超时**：30 秒超时。超时后显示错误信息 + 重试按钮。30 秒远超过正常 2-5 秒的加载时间
- **如果 Boot 场景初始化失败（DataManager、AudioManager 等初始化失败）**：在 Boot 场景中显示错误信息。不尝试加载 MainMenu——基础系统不可用时游戏无法运行
- **如果玩家在 FragmentTransition 期间按下 Escape（暂停）**：过渡期间 Action Map 为 Inactive——Escape 不被处理。过渡完成后 Escape 正常触发暂停

## Dependencies

**硬依赖**：

| 系统 | 依赖性质 | 接口 |
|------|----------|------|
| Data Management (#2) | 硬依赖 | PreloadChapterAsync, GetFragmentAsync, GetIllustrationAsync |
| Audio (#3) | 硬依赖 | PlayMusic, StopMusic, PreloadChapterAudioAsync, UnloadChapterAudio |
| Input (#1) | 硬依赖 | Action Map 状态控制（切换到 Inactive） |

**下游系统**（全部硬依赖）：
微动画 (#9)、记忆画卷交互 (#11)、记忆变化追踪 (#12)、章节管理 (#15)、交互反馈 (#18)

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Fragment Fade Duration | 0.5s | 0.3–1.0s | 碎片间淡出/淡入时长（各 0.5s，总计 1s） |
| Chapter Fade Duration | 1.0s | 0.5–2.0s | 章节过渡淡出/淡入时长 |
| Preload Trigger Threshold | 3 fragments | 1–5 | 剩余多少碎片时触发下一章预加载 |
| Scene Load Timeout | 30s | 15–60s | Unity Scene 加载超时 |
| Metadata Load Timeout | 10s | 5–30s | 章节数据加载超时（与 Data Management 一致） |

## Visual/Audio Requirements

- **视觉**：SceneFader 遮罩——全屏纯黑 VisualElement，opacity 过渡。无加载动画、无进度条、无文字——仅墨色覆盖
- **音频**：音乐交叉淡出由 Audio System (#3) 管理——场景管理器仅触发 `StopMusic(fadeTime)` 和 `PlayMusic(newClip, fadeTime)`。场景管理器本身不产生或处理音频

## UI Requirements

场景管理器本身没有独立 UI。其唯一的视觉元素是 SceneFader 遮罩（全屏黑色覆盖层），由 UI Toolkit 的 VisualElement 实现。错误面板（加载失败提示 + 重试/返回按钮）由场景管理器创建为临时 UI Document——但它使用 UI 框架的 Theme.uss 样式。

## Acceptance Criteria

- **GIVEN** 游戏启动，**WHEN** Boot 场景完成初始化，**THEN** DataManager + AudioManager + InputSystem 全部就绪，MainMenu 场景自动加载。Boot→MainMenu 过渡使用墨色遮罩淡入淡出
- **GIVEN** 玩家在主菜单点击"开始游戏"，**WHEN** Game 场景尚未加载，**THEN** Game 场景异步加载 + 初始章节 Addressables 预加载。加载期间全屏遮罩覆盖。加载完成后遮罩淡出，玩家看到第一个记忆碎片
- **GIVEN** 玩家在章节 1 碎片 5，**WHEN** 触发切换到碎片 6，**THEN** 遮罩淡出（0.5s）→ 内容切换 → 遮罩淡入（0.5s）。过渡期间输入被屏蔽
- **GIVEN** 章节 1 剩余 3 个碎片，**WHEN** 玩家进入倒数第 3 个碎片，**THEN** 后台触发章节 2 的插图 + 音频预加载。到达章节边界时 ChapterTransition 立即或近立即执行
- **GIVEN** 玩家到达章节 1 结尾，**WHEN** ChapterTransition 触发，**THEN** 音乐交叉淡出 → 卸载 Ch01 Addressables → 加载 Ch02 Addressables → 新章音乐淡入 → 遮罩淡出展示 Ch02 第一个碎片
- **GIVEN** 碎片插图 Addressables 加载失败，**WHEN** TransitionToFragmentAsync 捕获异常，**THEN** 遮罩保持覆盖，显示"记忆碎片加载失败"错误 + "返回章节开头"按钮

## Revision Notes

- **B2 (2026-05-19)**: ✅ OnFragmentTransitionStarted 已在 Rule 4 Step 0（第61行）和 Interactions 表（#18 行）中记录。事件签名为 `Action<string, string>`（chapterKey, fragmentId），由 GameSceneManager 触发。
- **W3 (2026-05-19)**: ✅ #12（记忆变化追踪）已在 Interactions 表（第142行）列为 OnFragmentTransitioned 的下游消费者。无需修改。

## Open Questions

- **碎片过渡是否需要方向性**：当前过渡是统一的墨色遮罩淡入淡出。如果 Vertical Slice 需要碎片间的方向性过渡（如向左滑动 = 深入记忆，向右滑动 = 退出记忆），需扩展 SceneFader 支持更多过渡效果
- **Game 场景是否需要编辑器中的基础布局**：Game 场景在编辑器中完全空白还是有基础 GameObject（SpriteRenderer、交互层根节点）？当前设计假设 Game 场景包含持久的基础 GameObject——这些在场景创建时手动放置
