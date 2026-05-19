# 章节管理系统 (Chapter Management)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 3 (关联的网络) + Pillar 4 (画卷中有呼吸) — 章节是记忆网络的容器，过渡是画卷翻页的节奏

## Overview

章节管理系统是《回响》中游戏进度的骨架。它定义每一章是什么——哪些碎片属于这一章、章节从哪里开始、什么时候算"完成"、以及完成后触发什么。它不渲染章节标题画面（那是 HUD #17 的职责），不加载碎片内容（那是场景管理 #6 的职责），不判定结局（那是多结局 #14 的职责）——它只负责**协调这些系统在正确的时机做正确的事**。

在技术层面，它是一个轻量级的进度状态机：持有当前章节和当前碎片的引用，在碎片切换时更新进度指针，在检测到章节边界时触发预加载、结局判定、和章节过渡。ChapterDefinition（章节配置 ScriptableObject）是它的核心数据——定义章节的碎片序列、入口碎片、EndingDefinition[]、章节解锁条件。存档系统 (#7) 通过它读取和写入 CurrentChapterKey、CompletedChapters、UnlockedChapters。

## Player Fantasy

在一章之内，时间不是一条线。你凭气味的牵引在记忆中漫游——向前、向后、绕回一个你已经去过两次的画面。关联的网络不问你"先后"，它只问"什么与什么相连"。

但章节边界不是网络中的另一条边。它是一道门。你在这一章中走过了那么多幅画——触碰了不该碰的东西，保留了不该保留的信，让一些画面永远改变了。现在墨色从画面边缘晕开，不是熟悉的碎片过渡——是更深、更慢、更彻底的黑色。你知道——这一章结束了。

在那一瞬间的静默中，结局被判定。你的选择被固化为不可撤销的后果。章节完成。你可以重玩它——但你不能假装这一次没有发生过。**童年结束了。青年结束了。你成为你，是通过放手。**

3-6秒的黑色不是加载画面。它是你听见门在你身后合上的瞬间——不是砰然巨响，而是墨迹干透的柔软终结。

## Detailed Design

### Core Rules

**规则 1 — ChapterDefinition ScriptableObject**

```
ChapterDefinition : ScriptableObject
├── ChapterKey : string              // "ch01", "ch02" — 匹配 MemoryFragment.ChapterId
├── DisplayNameKey : TableReference  // 本地化章节名
├── OrderIndex : int                 // 0-based。Ch01=0, Ch02=1。驱动线性解锁
├── EntryFragmentId : string         // e.g. "ch01_frag_00" — 首次进入的起始碎片
├── Endings : EndingDefinition[]     // 多结局系统 (#14) 消费——由本章拥有
├── CompletionRatio : float          // [0.0, 1.0]。玩家须访问的碎片占比才能触发章节完成。默认 0.6
└── AllowReplay : bool               // 默认 true。若 false，完成後锁定不可重玩
```

不在 ChapterDefinition 中存储显式碎片列表——每个 MemoryFragment 已声明其 ChapterId。单一事实来源，避免同步错误。

**规则 2 — 章节状态机 (3 状态)**

```
States:
  IDLE            无游戏进行中。MainMenu 活跃或游戏未开始
  IN_CHAPTER      Gameplay 活跃。玩家在章节内的某个碎片中
  TRANSITIONING   章节边界过渡进行中。淡出→卸载→加载→淡入。输入禁用

Transitions:
  IDLE → TRANSITIONING          New Game / Load Save
  IN_CHAPTER → TRANSITIONING    章节完成检测触发
  TRANSITIONING → IN_CHAPTER    章节+入口碎片加载完成，淡入结束
  TRANSITIONING → IDLE          加载失败 (Error) 或返回主菜单
  IN_CHAPTER → IDLE             玩家通过暂停菜单退出
```

**规则 3 — ChapterManager 骨架**

```csharp
public class ChapterManager : MonoBehaviour
{
    public ChapterState CurrentState { get; private set; } = ChapterState.Idle;
    public string CurrentChapterKey { get; private set; }
    public string CurrentFragmentId { get; private set; }
    
    // 本章节会话内已访问碎片 (完成检测)
    private HashSet<string> _chapterVisitedFragments;
    // 本会话已访问碎片 (关联引擎 discovery boost)
    private HashSet<string> _sessionVisitedFragments;
    // 最近 K 个碎片 (关联引擎 rhythm penalty)
    private List<string> _recentHistory;
    // 已完成 / 已解锁 (持久化)
    private HashSet<string> _completedChapters;
    private HashSet<string> _unlockedChapters;
    // 本章节碎片总数 (完成检测)
    private int _totalFragmentsInChapter;
    // 预加载是否已触发 (每章一次)
    private bool _preloadNotYetTriggered;
}
```

**规则 4 — 碎片导航 (Fragment Navigation)**

碎片导航由关联引擎 (#13) 驱动——非 SequenceIndex 线性顺序：

1. 玩家在碎片 A → 关联引擎 ComputeAssociations(A, chapterKey, recentHistory, sessionVisited) → 返回 Top-5 候选
2. HUD (#17) 渲染候选 → 玩家选择目标碎片 B
3. HUD 调用 `ChapterManager.TransitionToFragment(B)`
4. ChapterManager 调用 `SceneManager.TransitionToFragmentAsync(chapterKey, B)` → await
5. 过渡完成后更新追踪: `_chapterVisitedFragments.Add(B)`, `_sessionVisitedFragments.Add(B)`, `_recentHistory` 更新
6. 检查预加载阈值和章节完成条件

`SequenceIndex` 仅用于编辑器排序和画廊展示——不用于 gameplay 导航。

**规则 5 — 预加载触发 (使用未访问数而非线性索引)**

```
unvisitedCount = _totalFragmentsInChapter - _chapterVisitedFragments.Count
IF unvisitedCount <= PRELOAD_TRIGGER_THRESHOLD (3) AND _preloadNotYetTriggered:
  nextChapter = GetNextChapter(currentChapterKey)
  IF nextChapter != null:
    _ = SceneManager.PreloadChapterAsync(nextChapter.ChapterKey)  // fire-and-forget
    _preloadNotYetTriggered = true
```

每章仅触发一次。重玩时不触发预加载。

**规则 6 — 章节完成检测 (两部分条件)**

```
Chapter 完成 IF:
  (A) _chapterVisitedFragments.Count >= _totalFragmentsInChapter  (全部碎片已访问)
  OR
  (B) BOTH:
      1. _chapterVisitedFragments.Count >= Ceil(_totalFragmentsInChapter × CompletionRatio)
         AND
      2. 关联引擎最强候选的 compositeScore < COMPLETION_ASSOCIATION_THRESHOLD (0.30)
         (剩余碎片的情感牵引不足——"记忆自然褪去")
```

检测在每次 `TransitionToFragment` 完成后执行。自动化——无菜单提示，无"你确定要结束本章吗？"对话框。墨色自然晕开。

**规则 7 — 章节完成过渡流程**

```
Step 1: 判定结局
  ResolvedEnding ending = MultiEndingSystem.ResolveEnding(CurrentChapterKey);

Step 2: 更新进度状态
  _completedChapters.Add(CurrentChapterKey);
  nextChapter = GetNextChapter(CurrentChapterKey);
  IF nextChapter != null:
    _unlockedChapters.Add(nextChapter.ChapterKey);

Step 3: 自动存档
  await SaveManager.SaveAsync("auto_save");

Step 4: 章节过渡 (若有下一章)
  CurrentState = TRANSITIONING;
  await SceneManager.TransitionToChapterAsync(nextChapter.ChapterKey);
  → 设置 CurrentChapterKey, EntryFragmentId, 清除追踪变量
  CurrentState = IN_CHAPTER;
  → 触发 OnChapterStarted
  → 触发 auto_save (fire-and-forget)

Step 5: 最终章节完成 (若无下一章)
  CurrentState = TRANSITIONING;
  → 触发 OnAllChaptersCompleted
  → 结局呈现 (#20) → Credits → MainMenu
```

**规则 8 — 线性章节解锁**

- 新游戏: 仅 OrderIndex=0 的章节解锁
- 完成章节 N: 自动解锁章节 N+1
- 已解锁 = 永久解锁。重玩 Ch01 不会重新锁定 Ch02
- 最终章节完成后无新章节可解锁

**规则 9 — 章节重玩 (Chapter Replay)**

从章节选择 (#21) 进入已完成章节:

**持久化的（不重置）:**
- ChangeTracker._overlay: 之前周目的画面变化保留
- ChangeTracker._flags: 全局叙事标记保留
- _completedChapters, _unlockedChapters
- UnlockedEndingIds (并集)

**重置的（本次重玩会话）:**
- _chapterVisitedFragments: 清空——全新完成追踪
- _sessionVisitedFragments: 为本次重玩创建新 HashSet——关联引擎 D=1.30 对重玩中的未访问碎片仍生效
- CurrentFragmentId: 设为 EntryFragmentId
- _recentHistory: 清空
- _preloadNotYetTriggered: false (重玩不需要预加载下一章)

玩家体验: "你不能假装这一次没有发生过"——保留的视觉变化提醒玩家前次选择。但关联网络感觉新鲜——你可以走出不同的路径，触发不同的结局。

**规则 10 — 存档/读档集成**

保存时 (CollectSaveData):
```csharp
CurrentChapterKey   → saveData.CurrentChapterKey
CurrentFragmentId   → saveData.CurrentFragmentId
_completedChapters  → saveData.CompletedChapters
_unlockedChapters   → saveData.UnlockedChapters
```

读档恢复 (RestoreFromSave):
```csharp
_completedChapters = new HashSet<string>(saveData.CompletedChapters);
_unlockedChapters = new HashSet<string>(saveData.UnlockedChapters);
await EnterChapterAtFragment(saveData.CurrentChapterKey, saveData.CurrentFragmentId);
```

**规则 11 — 公开接口**

```
// 属性
ChapterState CurrentState { get; }
string CurrentChapterKey { get; }
string CurrentFragmentId { get; }

// 方法
Task StartNewGame();
Task LoadAndRestore(SaveData saveData);
Task ReplayChapter(string chapterKey);
Task TransitionToFragment(string targetFragmentId);
Task ReturnToMainMenu();

// 查询
string[] GetCompletedChapters();
string[] GetUnlockedChapters();
ChapterDefinition GetChapterDefinition(string chapterKey);
bool IsChapterCompleted(string chapterKey);
bool IsChapterUnlocked(string chapterKey);
int GetVisitedFragmentCount(string chapterKey);

// 事件
event Action<string> OnChapterStarted;
event Action<string> OnChapterCompleted;
event Action<string, string> OnFragmentChanged;
event Action OnAllChaptersCompleted;
```

**规则 12 — MVP 范围**

MVP 包含:
- 2 章线性推进 (Ch01 → Ch02)
- 自动章节完成检测
- 章节过渡协调 (SceneManager + MultiEnding + Save)
- 章节重玩（保留变化）
- 预加载触发

MVP 不包含:
- 第 3-4 章 (Full Vision)
- 章节选择界面 (#21, Vertical Slice)
- AllowReplay=false (所有章节默认可重玩)

### States and Transitions

| 状态 | 描述 | 触发 | 退出 |
|------|------|------|------|
| **Idle** | 无游戏进行中 | 游戏启动 / 主菜单 | StartNewGame / LoadAndRestore → Transitioning |
| **InChapter** | 玩家在章节内的碎片中 | Transitioning → InChapter 过渡完成 | 章节完成 → Transitioning; 暂停退出 → Idle |
| **Transitioning** | 章节边界过渡。遮罩覆盖，输入禁用 | Idle → 新游戏/读档; InChapter → 章节完成 | 过渡成功 → InChapter; 失败 → Idle |

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **场景管理 (#6)** | TransitionToFragmentAsync, TransitionToChapterAsync, PreloadChapterAsync, LoadSceneAsync | 所有内容切换的物理执行 |
| 上游 | **数据管理 (#2)** | GetFragmentsByChapter(chapterKey) → List\<MemoryFragment\> | 获取章节碎片总数、入口碎片 |
| 上游 | **存档 (#7)** | SaveAsync, 提供 CurrentChapterKey/FragmentId/CompletedChapters/UnlockedChapters | 进度持久化 |
| 下游 | **多结局 (#14)** | 调用 ResolveEnding, OnChapterStart; 提供 ChapterDefinition.Endings[] | 结局判定触发和数据提供 |
| 下游 | **关联引擎 (#13)** | ComputeAssociations(currentFragmentId, chapterKey, recentHistory, sessionVisited) | 获取候选碎片列表 |
| 下游 | **HUD (#17)** | OnFragmentChanged 事件 → HUD 更新; 候选列表传递给 HUD | 玩家看到导航选项 |
| 下游 | **结局呈现 (#20)** | OnAllChaptersCompleted 事件 | 触发最终结局画面 |
| 下游 | **章节选择 (#21)** | GetUnlockedChapters(), GetCompletedChapters() | 章节选择界面的数据源 |
| 下游 | **变化追踪 (#12)** | OnChapterCompleted 事件 → 标记 VisitedFragment/ChapterCompleted 条件 | 条件评估的状态更新 |

## Formulas

### 1. 章节完成检测 (Completion Detection)

```
isComplete =
  (visitedCount >= totalFragments)  // 全部碎片已访问
  OR
  (
    visitedCount >= Ceil(totalFragments × chapter.CompletionRatio)
    AND
    bestCandidateScore < COMPLETION_ASSOCIATION_THRESHOLD
  )

其中:
  visitedCount            = _chapterVisitedFragments.Count
  totalFragments          = DataManager.GetFragmentsByChapter(chapterKey).Count
  CompletionRatio         ∈ [0.0, 1.0], 默认 0.6
  bestCandidateScore      = 关联引擎 ComputeAssociations 返回的第一个候选的 compositeScore
  COMPLETION_ASSOCIATION_THRESHOLD = 0.30 (默认)
```

### 2. 预加载触发 (Preload Trigger)

```
shouldPreload =
  unvisitedCount <= PRELOAD_TRIGGER_THRESHOLD
  AND _preloadNotYetTriggered == true
  AND nextChapterExists == true

其中:
  unvisitedCount           = totalFragments - _chapterVisitedFragments.Count
  PRELOAD_TRIGGER_THRESHOLD = 3
```

### 3. 章节解锁 (Chapter Unlock)

```
newGame:
  unlocked = { chapter[0] }  // OrderIndex == 0

onChapterComplete(chapterN):
  unlocked = unlocked ∪ { chapter[N+1] }  // IF exists
```

解锁使用并集语义——永久累积。

## Edge Cases

- **玩家快速跳过最后 3 个碎片 (<30 秒)**: 预加载可能未完成。TransitionToChapterAsync 内部 await 预加载 Task——若未完成，玩家在遮罩黑屏上等待直到加载完成。不超时——Addressables 加载在 30 秒内必定完成。

- **玩家访问了所有碎片但没有结局被触发**: ResolveEnding 返回默认结局 (IsDefault=true)。章节正常完成——玩家进入下一章或结局呈现。

- **重玩章节时 ChangeTracker 的 overlay 为空（首次玩没有做选择就退出了）**: 合法——重玩等同于首次玩。视觉变化为空，关联网络正常计算。

- **ChapterDefinition.CompletionRatio = 1.0 且有一个碎片因 ConditionGroup 永远不可达**: 玩家永远无法达到 100%。关联阈值检测 (B.2) 作为安全阀——当最佳候选 < 0.30 时仍触发完成。若关联引擎无法返回任何候选（空列表），直接触发完成。

- **章节中碎片总数为 0**: 配置错误。ChapterManager 在 EnterChapter 时检测——若 totalFragments = 0，记录 Error，立即触发章节完成（直接过渡到下一章或结束）。

- **TransitionToFragmentAsync 失败（Addressables 加载错误）**: SceneManager 抛出异常。ChapterManager 不捕获——异常冒泡到顶层错误处理。显示错误提示"无法加载记忆碎片"，提供"重试 / 返回主菜单"选项。

- **在转换状态中调用 TransitionToFragment**: CurrentState != IN_CHAPTER → 忽略调用 + LogWarning。转换期间不允许碎片切换。

- **加载的存档引用不存在的 ChapterKey 或 FragmentId**: SaveManager 在加载时验证——若 Key 不存在，降级到该章节的第一个碎片或第一个章节的第一个碎片。记录 Warning。

- **玩家在章节完成过渡中强制退出游戏**: auto_save 在过渡流程的 Step 3 同步完成（await SaveAsync）后才进入 Step 4。若在 Step 3 之前崩溃——进度丢失（未保存）。若在 Step 3 之后崩溃——进度已持久化，下次启动时读档恢复到新章节入口。

- **同一章节多次完成（重玩后再次完成）**: _completedChapters 是 HashSet——重复添加无影响。新结局通过 MultiEndingSystem.UnlockedEndingIds 并集记录。

## Dependencies

### 硬依赖 (Hard Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **场景管理 (#6)** | 硬依赖 — 所有内容切换的物理执行 | TransitionToFragmentAsync, TransitionToChapterAsync, PreloadChapterAsync, LoadSceneAsync |
| **数据管理 (#2)** | 硬依赖 — 章节碎片查询 | GetFragmentsByChapter(chapterKey): List\<MemoryFragment\> |
| **存档系统 (#7)** | 硬依赖 — 进度持久化 | SaveAsync(slotId), 提供 CurrentChapterKey/FragmentId/CompletedChapters/UnlockedChapters |

### 软依赖 (Soft Dependencies)

| 系统 | 性质 | 接口 |
|------|------|------|
| **多结局系统 (#14)** | 软依赖 — 章节完成时判定结局 | ResolveEnding(chapterId), OnChapterStart(chapterId) |
| **网状关联引擎 (#13)** | 软依赖 — 获取候选碎片 + 完成检测中的分数检查 | ComputeAssociations(fragmentId, chapterKey, recentHistory, sessionVisited) |
| **游戏内HUD (#17)** | 软依赖 — 渲染候选列表 | OnFragmentChanged 事件订阅 |
| **结局呈现 (#20)** | 软依赖 — 最终章完成 | OnAllChaptersCompleted 事件订阅 |
| **章节选择 (#21)** | 软依赖 — 重玩入口 + 数据源 | ReplayChapter(chapterKey), GetUnlockedChapters(), GetCompletedChapters() |
| **变化追踪 (#12)** | 软依赖 — 条件评估的章节状态 | OnChapterCompleted 事件 → 标记 ChapterCompleted 条件 |

### 下游系统

| 系统 | 消费内容 |
|------|---------|
| **多结局 (#14)** | 章节生命周期事件, EndingDefinition[] |
| **HUD (#17)** | 当前章节/碎片, 候选列表 |
| **结局呈现 (#20)** | 全部章节完成事件 |
| **章节选择 (#21)** | 已解锁/已完成章节数据 |
| **存档 (#7)** | 当前进度字段 |
| **变化追踪 (#12)** | 章节完成事件 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| CompletionRatio | 0.6 | 0.4–1.0 | 触发章节完成需访问的碎片占比。每章可在 ChapterDefinition 中覆盖 |
| PRELOAD_TRIGGER_THRESHOLD | 3 | 2–5 | 剩余未访问碎片数触发下一章预加载 |
| COMPLETION_ASSOCIATION_THRESHOLD | 0.30 | 0.20–0.50 | 最佳候选分数低于此值 → "牵引力不足" → 章节可完成。调高 → 更早结束；调低 → 更晚结束 |
| HISTORY_WINDOW (关联引擎 K) | 4 | 2–6 | 传递到关联引擎的最近碎片数——此处仅记录，实际使用归 #13 |

## Visual/Audio Requirements

章节管理自身无视觉或音频输出。过渡效果和音频交叉淡出由场景管理 (#6) 和音频系统 (#3) 执行——本章系统仅协调触发时机。章节标题画面的渲染归 HUD (#17)。

## UI Requirements

无自有 UI。章节进度和标题由 HUD (#17) 渲染。章节选择界面 (#21) 消费 GetUnlockedChapters/GetCompletedChapters 数据。ChapterDefinition Editor 工具（Inspector 验证）属于开发工具，不在 GDD 范围内。

## Acceptance Criteria

- **GIVEN** 游戏首次启动，**WHEN** 玩家选择"新游戏"，**THEN** ChapterManager 进入 TRANSITIONING → 加载 Ch01 → 进入 Ch01 的 EntryFragmentId → CurrentState = IN_CHAPTER。OnChapterStarted("ch01") 触发。

- **GIVEN** 玩家在 Ch01 的碎片 A 中，HUD 渲染了关联引擎返回的候选列表，**WHEN** 玩家选择碎片 B，**THEN** ChapterManager.TransitionToFragment("B") 被调用 → SceneManager 执行碎片过渡 → _chapterVisitedFragments 包含 B → OnFragmentChanged 触发。

- **GIVEN** 玩家在 Ch01 中访问了 8/10 碎片 (CompletionRatio=0.6, 已满足 ≥6)，且关联引擎返回的最佳候选 compositeScore = 0.15 (<0.30)，**WHEN** TransitionToFragment 完成，**THEN** CheckChapterCompletion 返回 true → 章节完成流程启动。

- **GIVEN** 章节完成流程启动，**WHEN** ResolveEnding("ch01") 返回 ending_A，**THEN** _completedChapters 包含 "ch01"，_unlockedChapters 包含 "ch02"，auto_save 写入，TransitionToChapterAsync("ch02") 被调用。

- **GIVEN** 玩家已完成 Ch01，**WHEN** 从章节选择 ReplayChapter("ch01")，**THEN** EntryFragmentId 被加载，_chapterVisitedFragments 清空，_recentHistory 清空，但 ChangeTracker 的 overlay 和 flags 保留。玩家可以走出不同的关联路径。

- **GIVEN** 玩家在 Ch01 中途保存并退出，**WHEN** 从主菜单加载存档，**THEN** CurrentChapterKey="ch01", CurrentFragmentId 恢复到保存时的碎片，_completedChapters 和 _unlockedChapters 恢复。

- **GIVEN** 玩家完成 Ch02（MVP 最终章），**WHEN** 章节完成流程中 GetNextChapter 返回 null，**THEN** OnAllChaptersCompleted 触发 → 结局呈现 (#20) 接收事件。

- **GIVEN** 章节过渡进行中 (CurrentState = TRANSITIONING)，**WHEN** 其他系统尝试调用 TransitionToFragment，**THEN** 调用被忽略 + LogWarning。转换期间不允许碎片切换。

## Open Questions

- **关联阈值完成检测的 playtest 验证**: COMPLETION_ASSOCIATION_THRESHOLD = 0.30 是理论值——需在 MVP playtest 中验证玩家感受："记忆自然褪去"是否感觉自然？还是玩家觉得章节"突然结束"？可能需要调整为"最佳候选分数连续 2 次低于阈值"（防止偶然低分触发）。(Owner: game-designer, MVP playtest)

- **章节重玩时是否应显示"新游戏+"提示**: 重玩保留 overlay 和 flags——这意味着重玩体验不同于首次。是否应在重玩入口提示玩家"之前的记忆变化仍然保留"？还是让玩家自己发现？(Owner: ux-designer, Vertical Slice)

- **CompletionRatio 的章节级别调优**: 0.6 是通用默认值——但不同章节可能需要不同的比率。小章节（8-10 碎片）可能需要 1.0（必须全部访问）。建议在 MVP 两章中使用不同的 CompletionRatio 做对比测试。(Owner: game-designer)
