# Cross-GDD Review Report

**Date**: 2026-05-12
**GDDs Reviewed**: 19 (all MVP systems)
**Systems Covered**: #1–19 (Foundation, Gameplay, Progression, UI)
**Review Mode**: full (consistency + design theory)

---

## Consistency Issues

### Blocking (must resolve before architecture begins)

#### B1. InteractionManager 缺少公共事件定义
**GDDs**: `interaction-feedback.md` → `scroll-interaction-system.md`

`interaction-feedback.md` 规则 4 (C# 代码, lines 55-63) 订阅了 `InteractionManager` 的 9 个公共事件:
```csharp
InteractionManager.OnHoverEnter, OnHoverExit, OnInteract,
OnDragStart, OnDragComplete, OnDragCancel,
OnChoiceSelected, OnChoiceHover, OnRevealObject, OnShowText
```
但 `scroll-interaction-system.md` (InteractionManager 的归属 GDD) **未将这些定义为公共 C# 事件**。交互系统将这些名称用作内部方法名（如 `OnInteract(InteractiveObject obj)` 是私有方法），而非对外发布的事件。交互反馈系统没有可订阅的事件来源。

**修复方案**: 在 `scroll-interaction-system.md` 中添加规则定义这 10 个公共静态事件（含 `OnChoiceHover`），并明确每个事件的触发时机和参数签名。或者，`interaction-feedback.md` 改为直接调用模式（在每个处理方法中被交互系统调用）。

#### B2. SceneManager.OnFragmentTransitionStarted 事件缺失
**GDDs**: `interaction-feedback.md` → `scene-management.md`

`interaction-feedback.md` 规则 4 (C# 代码, line 64) 订阅了 `SceneManager.OnFragmentTransitionStarted` 事件以在过渡期间抑制反馈。但 `scene-management.md` 的 `TransitionToFragmentAsync` 流程 (规则 4) 仅触发 `OnFragmentTransitioned(chapterKey, fragmentId)`——**未定义** `OnFragmentTransitionStarted` 事件。反馈抑制逻辑无法触发。

**修复方案**: 在 `scene-management.md` 规则 4 的 TransitionToFragmentAsync 流程中添加 `OnFragmentTransitionStarted(chapterKey, fragmentId)` 事件——在 FadeOut 开始前（Step 1 之前）触发。

---

### Warnings (should resolve, but won't block architecture)

#### W1. OnFragmentTransitioned 参数签名不一致
**GDDs**: `memory-change-tracking.md` ↔ `scene-management.md`

- `scene-management.md` (line 72): `OnFragmentTransitioned(chapterKey, fragmentId)` — **两个参数**
- `memory-change-tracking.md` (lines 215, 316): `OnFragmentTransitioned(fragmentId)` — **一个参数**

变化追踪系统只订阅了 `fragmentId` 参数，但场景管理会传递两个参数。C# 事件委托签名必须匹配，否则编译失败。

**修复方案**: 统一为 `OnFragmentTransitioned(string chapterKey, string fragmentId)` 两个参数。memory-change-tracking.md 的接口表更新参数签名。

#### W2. 音频系统下游引用错误：UI 框架不消费 PlaySFX
**GDDs**: `audio-system.md` ↔ `ui-framework.md`

- `audio-system.md` (line 182): 列出 UI 框架 (#5) 为下游，接口 `PlaySFX(clipKey)` 用于 "按钮悬停/点击、面板开关 UI 音效"
- `ui-framework.md` Visual/Audio Requirements: "音频反馈（按钮点击声、面板开关声）由交互反馈系统 (#18) 播放——UI 框架仅提供过渡动画的视觉部分"

UI 框架明确声明不播放音频。所有 UI 音效由交互反馈 (#18) 管理。音频系统的下游表应该引用 #18 而非 #5 作为 UI 音效的消费者。实际上 audio-system.md 已经列了 #18 为下游——只是 #5 的条目是错误的。

**修复方案**: 从 `audio-system.md` 的下游表中移除 UI 框架 (#5) 条目，或将其降级为"无直接接口"。

#### W3. 场景管理下游列表不完整：缺失 #12
**GDDs**: `scene-management.md` ↔ `memory-change-tracking.md`

`scene-management.md` 的下游表 (lines 139-140) 仅列出 #9 (微动画) 和 #11 (交互系统) 为 `OnFragmentTransitioned` 消费者。但 `memory-change-tracking.md` (#12) 也订阅此事件以更新 `_visitedFragments`。场景管理的下游列表应包含 #12。

**修复方案**: 在 `scene-management.md` 下游表中添加: `| 下游 | 记忆变化追踪 (#12) | OnFragmentTransitioned 事件 | 碎片切换时更新已访问记录 |`

#### W4. 存档加载绕过 ChapterManager 状态初始化
**GDDs**: `save-load-system.md` ↔ `chapter-management.md`

- `save-load-system.md` 规则 4 (line 114): 加载时直接调用 `SceneManager.TransitionToFragmentAsync()` 恢复碎片位置
- `chapter-management.md` 规则 10 (line 194): 加载时通过 `EnterChapterAtFragment()` 恢复，此方法初始化 `_chapterVisitedFragments`、`_sessionVisitedFragments`、`_recentHistory`

两条代码路径冲突。如果存档系统直接调用 SceneManager，ChapterManager 的追踪变量不会被初始化——导致完成检测和关联引擎的 discovery boost / rhythm penalty 使用空数据。

**修复方案**: `save-load-system.md` 规则 4 应改为调用 `ChapterManager.LoadAndRestore(saveData)`（章节管理已定义此公开方法），而非直接调用 SceneManager。

---

### Info (worth noting)

#### I1. 前向引用：4 个下游系统未设计
**GDDs**: Multiple → #20, #21, #22, #24

以下系统被现有 GDD 引用为下游，但尚未设计——无法验证依赖双向性:
- #20 结局呈现 (Vertical Slice) — 被 #14, #15 引用
- #21 章节选择 (Vertical Slice) — 被 #15 引用
- #22 无障碍 (Vertical Slice) — 被 #1, #5 引用
- #24 画廊/回忆录 (Full Vision) — 被 #14 引用

**建议**: 在设计这些系统时验证双向依赖。当前引用方向正确——不影响架构。

#### I2. SceneManager.LoadScene 命名不一致
**GDDs**: `main-menu.md` ↔ `scene-management.md`

- `main-menu.md` (rules 3, 7, 8): 引用 `SceneManager.LoadScene("MainMenu")` 和 `SceneManager.LoadScene("InGame")`
- `scene-management.md` (line 37): 方法定义为 `LoadSceneAsync(string sceneName)`

缺少 "Async" 后缀。设计层面的命名应保持一致。

**建议**: 将 `main-menu.md` 中的引用更新为 `SceneManager.LoadSceneAsync()`。

---

## Game Design Issues

### Blocking

None. No game design issues rise to the blocking level.

### Warnings

#### D1. 章节完成检测存在边缘情况竞态
**GDDs**: `chapter-management.md` + `web-association-engine.md`

章节完成检测 (chapter-management.md 规则 6) 在 `TransitionToFragment` 完成后执行——比较 `bestCandidateScore < 0.30`。但刚进入新碎片时，关联引擎可能尚未为当前碎片计算候选（候选计算在 HUD 请求关联路径时才触发）。这意味着完成检测可能使用**上一个碎片**的 bestCandidateScore 而非当前碎片的。

**严重程度**: Warning — 在快速碎片切换时可能导致提前/延迟完成。需要明确：完成检测使用"刚进入碎片后，为当前碎片计算的候选分数"还是"上一个碎片的候选分数"。

**建议**: 完成检测应在 HUD 请求并渲染关联路径后执行（确保关联引擎已为当前碎片计算）。或者完成检测从 `ComputeAssociations` 改为在 `TransitionToFragment` 完成后主动调用一次。

#### D2. 交互反馈的音频键名未在音频系统中注册
**GDDs**: `interaction-feedback.md` → `audio-system.md`

`interaction-feedback.md` 规则 5 定义了 8 个 SFX audio key（`sfx_touch_generic`, `sfx_drag_start` 等）。但 `audio-system.md` 的音频分组表中仅给出了命名规范（`sfx_` 前缀）和示例（`sfx_ui_hover_01`, `sfx_interact_touch_01`），**未包含这 8 个具体的 key**。

**严重程度**: Warning — 不会导致设计矛盾，但音频系统需要在 Audio_UI Addressables 组中明确包含这些资产。交互反馈定义了需求，音频系统需要在资产清单中响应。

**建议**: 在 `audio-system.md` 的 Audio Groups 表中明确列出这 8 个 SFX key，或添加引用指向 `interaction-feedback.md` 规则 5。

---

### Info

#### D3. Player Fantasy 高度一致
所有 19 个 GDD 的 Player Fantasy 描述形成了一致的玩家身份：一个在记忆画卷中穿行的游魂，触碰改变画面，选择留下不可磨灭的痕迹。基础设施系统的 Fantasy 正确标记为"间接——玩家感受其效果而非系统本身"。无冲突。

#### D4. Pillar 对齐完整
所有 19 个 MVP 系统都明确服务于至少一个设计支柱。无 Pillar 漂移。无 Anti-Pillar 违规。
- Pillar 1 (选择即重写): #8, #11, #12, #17
- Pillar 2 (不完美才是力量): #14, #16
- Pillar 3 (关联的网络): #10, #13, #15, #17
- Pillar 4 (画卷中有呼吸): #1-7 (间接), #9, #11, #17, #18, #19

#### D5. 认知负荷评估
核心循环中同时活跃的系统约 2 个（交互悬停 + 关联路径导航）。其余系统为被动或后台运行。远低于 3-4 的认知负荷上限。设计合理。

#### D6. 无显性主导策略
网状关联引擎的多因子评分（Tag 0.6 + Explicit 0.4）× Rhythm × Discovery 不会产生固定的"最优路径"。章节重玩保留 overlay/flags 确保路径多样性。完成检测的双条件（全访问 OR Ratio+阈值）防止玩家"卡"在某个碎片。

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|-----|--------|------|----------|
| scroll-interaction-system.md | 缺少 10 个公共 C# 事件定义 (B1) | Consistency | Blocking |
| scene-management.md | 缺少 OnFragmentTransitionStarted 事件 (B2)；下游表缺失 #12 (W3) | Consistency | Blocking |
| interaction-feedback.md | 引用未定义的事件 (B1, B2) | Consistency | Blocking |
| memory-change-tracking.md | OnFragmentTransitioned 参数签名不一致 (W1) | Consistency | Warning |
| audio-system.md | 下游表错误引用 UI 框架为 PlaySFX 消费者 (W2) | Consistency | Warning |
| save-load-system.md | 加载路径绕过 ChapterManager (W4) | Consistency | Warning |
| chapter-management.md | 完成检测的 bestCandidateScore 竞态 (D1) | Design | Warning |
| main-menu.md | LoadScene vs LoadSceneAsync 命名 (I2) | Consistency | Info |

---

## Verdict: CONCERNS (was FAIL — blockers resolved 2026-05-12)

**2 blocking consistency issues resolved:**
1. **B1** ✅: `scroll-interaction-system.md` 规则 1.1 — 10 个 InteractionManager 公共事件已定义
2. **B2** ✅: `scene-management.md` 规则 4 Step 0 — `OnFragmentTransitionStarted` 事件已添加
3. **B1 extra** ✅: `interaction-feedback.md` 规则 4 — `OnChoiceHover` 订阅已补充

**4 warnings remain** — recommended for resolution before or early in architecture phase:
- W1: OnFragmentTransitioned 参数签名 (2 vs 1)
- W2: audio-system 下游表 UI 框架条目
- W3: ✅ 已修复 (scene-management 下游表已补 #12, #18)
- W4: 存档绕过 ChapterManager

**Gate-readiness**: Blockers cleared. Warnings are design clarifications, not structural issues. Ready to proceed to Technical Setup.
