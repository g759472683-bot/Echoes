# ADR-0018: 章节管理系统 — 进度状态机、完成检测与重玩保护

## Status

Accepted

## Date

2026-05-19

## Last Verified

2026-05-19

## Decision Makers

User + Claude Code (technical-director via /dev-story)

## Summary

章节管理系统是游戏进度的骨架——持有当前章节/碎片引用，在检测到章节边界时协调过渡、结局判定和自动存档。决定使用 3 状态机（Idle/InChapter/Transitioning）+ 两部分完成检测（全部访问 OR 关联阈值降级）+ 章节重玩保留不可变状态 + ChapterDefinition ScriptableObject 配置。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — 纯 C# 状态机 + ScriptableObject，无 post-cutoff API |
| **References Consulted** | `VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0004 (SceneManager — 碎片/章节过渡执行), ADR-0002 (IDataManager — 碎片查询), ADR-0007 (ChangeTracker — 状态持久化), ADR-0003 (SaveManager — 进度存档) |
| **Enables** | ADR-0010 (MultiEnding — ResolveEnding 调用时机), ADR-0011 (CrossChapterTracker — 重玩时 IsImmutable 保护) |
| **Blocks** | ChapterManagement Epic |
| **Ordering Note** | 在 ADR-0004, ADR-0007, ADR-0003 之后实现；MultiEnding 和 CrossChapterTracker 依赖本系统的生命周期事件 |

## Context

### Problem Statement

回响的章节系统需要在非线性的碎片导航中检测"章节何时完成"，协调章节过渡流程（判定结局 → 自动存档 → 卸载旧章 → 加载新章），并支持章节重玩时保留不可逆的玩家选择。当前多个 ADR（0001, 0007, 0010, 0011）以碎片化方式覆盖了章节管理的部分职责，但没有独立的架构决策来定义 ChapterManager 的完整状态机、完成检测算法和重玩保护语义。

### Constraints

- MVP：2 章线性推进（Ch01 → Ch02），Full Vision 扩展到 4-5 章
- 玩家在章节内非线性导航（关联引擎驱动的网状跳转，非 SequenceIndex 线性）
- 章节完成条件需两部分检测：全部碎片访问 OR 关联牵引力不足
- 章节重玩保留 ChangeTracker 的 overlay 和 flags（"不能假装没发生过"）
- 过渡期间输入禁用

### Requirements

- ChapterDefinition ScriptableObject（ChapterKey, EntryFragmentId, Endings, CompletionRatio, AllowReplay）
- 3 状态机：Idle → InChapter ↔ Transitioning
- 两部分章节完成检测（全部访问 + 关联阈值降级）
- 预加载触发（剩余 ≤3 未访问碎片时触发下一章预加载）
- 章节重玩语义（保留 overlay/flags，重置访问追踪）
- 存档/读档集成（CurrentChapterKey, CurrentFragmentId, CompletedChapters, UnlockedChapters）
- 4 个生命周期事件：OnChapterStarted, OnChapterCompleted, OnFragmentChanged, OnAllChaptersCompleted

## Decision

**3 状态机 + 两部分完成检测 + 重玩保留不可变状态 + ChapterDefinition SO 配置。**

### ChapterDefinition ScriptableObject

```csharp
[CreateAssetMenu(menuName = "Echoes/ChapterDefinition")]
public class ChapterDefinition : ScriptableObject
{
    public string ChapterKey;          // "ch01", "ch02"
    public string DisplayNameKey;      // 本地化 Key (TableReference 就绪后迁移)
    public int OrderIndex;             // 0-based，驱动线性解锁
    public string EntryFragmentId;     // 首次进入的起始碎片
    public EndingDefinition[] Endings; // 多结局系统消费
    public float CompletionRatio;      // [0.0, 1.0]，默认 0.6
    public bool AllowReplay;           // 默认 true
}
```

> 不在 ChapterDefinition 中存储显式碎片列表——每个 MemoryFragment 已声明其 ChapterKey。单一事实来源，避免同步错误。

### 3 状态机

```
States:
  IDLE            无游戏进行中
  IN_CHAPTER      Gameplay 活跃，玩家在章节碎片中
  TRANSITIONING   章节边界过渡中（淡出→卸载→加载→淡入），输入禁用

Transitions:
  IDLE → TRANSITIONING          New Game / Load Save
  IN_CHAPTER → TRANSITIONING    章节完成检测触发
  TRANSITIONING → IN_CHAPTER    章节+入口碎片加载完成，淡入结束
  TRANSITIONING → IDLE          加载失败 (Error) 或返回主菜单
  IN_CHAPTER → IDLE             玩家通过暂停菜单退出
```

### ChapterManager 骨架

```csharp
public class ChapterManager : MonoBehaviour
{
    public ChapterState CurrentState { get; private set; }
    public string CurrentChapterKey { get; private set; }
    public string CurrentFragmentId { get; private set; }

    // 本章已访问碎片（完成检测）
    private HashSet<string> _chapterVisitedFragments;
    // 本会话已访问碎片（关联引擎 discovery boost）
    private HashSet<string> _sessionVisitedFragments;
    // 最近 K 个碎片（关联引擎 rhythm penalty，K=4）
    private List<string> _recentHistory;
    // 持久化集合
    private HashSet<string> _completedChapters;
    private HashSet<string> _unlockedChapters;
    // 预加载标记（每章一次）
    private bool _preloadTriggered;

    // 公开方法
    public Task StartNewGame();
    public Task LoadAndRestore(SaveData saveData);
    public Task ReplayChapter(string chapterKey);
    public Task TransitionToFragment(string targetFragmentId);
    public Task ReturnToMainMenu();

    // 查询
    public string[] GetCompletedChapters();
    public string[] GetUnlockedChapters();
    public bool IsChapterCompleted(string chapterKey);
    public bool IsChapterUnlocked(string chapterKey);

    // 事件
    public event Action<string> OnChapterStarted;
    public event Action<string> OnChapterCompleted;
    public event Action<string, string> OnFragmentChanged;
    public event Action OnAllChaptersCompleted;
}
```

### 两部分章节完成检测

```
isComplete =
  (visitedCount >= totalFragments)  // 条件 A: 全部碎片已访问
  OR
  (
    visitedCount >= Ceil(totalFragments × CompletionRatio)  // 条件 B.1: 达到比例
    AND
    bestCandidateScore < COMPLETION_ASSOCIATION_THRESHOLD   // 条件 B.2: 关联牵引力不足
  )

其中:
  COMPLETION_ASSOCIATION_THRESHOLD = 0.30 (默认)
```

检测在每次 TransitionToFragment 完成后执行。自动化——无"是否结束本章"确认对话框。

### 章节完成过渡流程

```
Step 1: 判定结局
  ResolvedEnding ending = MultiEndingSystem.ResolveEnding(CurrentChapterKey);

Step 2: 更新进度
  _completedChapters.Add(CurrentChapterKey);
  IF nextChapter exists: _unlockedChapters.Add(nextChapter.ChapterKey);

Step 3: 自动存档
  await SaveManager.SaveAsync("auto_save");

Step 4: 章节过渡（有下一章时）
  CurrentState = TRANSITIONING;
  await SceneManager.TransitionToChapterAsync(nextChapter.ChapterKey);
  → CurrentState = IN_CHAPTER;
  → 触发 OnChapterStarted

Step 5: 最终章完成（无下一章时）
  CurrentState = TRANSITIONING;
  → 触发 OnAllChaptersCompleted;
  → 结局呈现 → Credits → MainMenu
```

### 章节重玩语义

| 保留的（不重置） | 重置的（本次重玩会话） |
|-----------------|---------------------|
| ChangeTracker._overlay | _chapterVisitedFragments |
| ChangeTracker._flags | _sessionVisitedFragments |
| _completedChapters | _recentHistory |
| _unlockedChapters | CurrentFragmentId → EntryFragmentId |
| UnlockedEndingIds（并集） | _preloadTriggered = false |

### 预加载触发

```
unvisitedCount = totalFragments - _chapterVisitedFragments.Count
IF unvisitedCount <= PRELOAD_TRIGGER_THRESHOLD (3) AND !_preloadTriggered:
  _ = SceneManager.PreloadChapterAsync(nextChapter.ChapterKey);  // fire-and-forget
  _preloadTriggered = true;
```

每章仅触发一次。重玩不触发预加载。

### 线性章节解锁

- 新游戏：仅 OrderIndex=0 解锁
- 完成章节 N：自动解锁章节 N+1
- 已解锁 = 永久（重玩 Ch01 不重新锁定 Ch02）

### Architecture Diagram

```
┌──────────────────────────────────────────────┐
│          ChapterDefinition (SO)               │
│          ChapterKey, EntryFragmentId,         │
│          Endings[], CompletionRatio           │
└──────────────────────────────────────────────┘
              │
              ▼
┌──────────────────────────────────────────────┐
│          ChapterManager                       │
│          (MonoBehaviour, Game 场景持久)        │
│                                              │
│  状态机: IDLE | IN_CHAPTER | TRANSITIONING   │
│                                              │
│  追踪: _chapterVisitedFragments              │
│        _sessionVisitedFragments               │
│        _completedChapters / _unlockedChapters │
│        _recentHistory (K=4)                   │
│                                              │
│  事件: OnChapterStarted                      │
│        OnChapterCompleted                     │
│        OnFragmentChanged                      │
│        OnAllChaptersCompleted                 │
└──────────────────────────────────────────────┘
    │        │        │        │        │
    ▼        ▼        ▼        ▼        ▼
┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌──────────┐
│ #6  │ │ #14 │ │ #7  │ │ #13 │ │ #12      │
│Scene│ │Multi│ │Save │ │Web  │ │Change    │
│Mgr  │ │End  │ │Mgr  │ │Assoc│ │Tracker   │
└─────┘ └─────┘ └─────┘ └─────┘ └──────────┘
```

### Implementation Guidelines

1. 碎片导航由关联引擎 (#13) 驱动——非 SequenceIndex 线性顺序。SequenceIndex 仅用于编辑器排序和画廊展示
2. 章节完成检测在 TransitionToFragment 完成后自动执行——不弹出确认对话框
3. TransitionToFragment 在 CurrentState != IN_CHAPTER 时忽略调用 + LogWarning
4. OnChapterCompleted 事件在 Step 2（进度更新）之后、Step 3（自动存档）之前触发——确保订阅方在存档前更新状态
5. 重玩时 _preloadTriggered = false——重玩不需要预加载下一章
6. 若章节碎片总数为 0（配置错误）→ 立即触发章节完成，记录 Error
7. 若关联引擎无法返回任何候选（空列表）→ 直接触发章节完成（不依赖 COMPLETION_ASSOCIATION_THRESHOLD 比较）

## Alternatives Considered

### Alternative 1: 线性 SequenceIndex 导航 + 简单完成检测

- **Description**: 玩家按 SequenceIndex 顺序访问碎片，访问最后一个碎片 = 章节完成
- **Pros**: 实现极简
- **Cons**: 违背回响核心设计——网状关联导航是 Pillar 3 的基础；无法处理玩家绕回已访问碎片的情况
- **Rejection Reason**: SequenceIndex 仅用于编辑器排序——gameplay 导航由关联引擎驱动

### Alternative 2: 仅基于访问比例完成（无关联阈值降级）

- **Description**: 完成条件仅 = 访问了 ≥CompletionRatio 的碎片
- **Pros**: 更简单
- **Cons**: 玩家可能在情感牵引力仍然很强时被迫结束章节（"我还没探索够"）；或反之——剩余碎片情感关联很弱但玩家必须机械地访问更多碎片
- **Rejection Reason**: 关联阈值降级提供"记忆自然褪去"的叙事节奏——当剩余碎片不再有强牵引力时，墨色自然晕开结束章节

### Alternative 3: 玩家手动触发章节结束

- **Description**: 在 HUD 上提供"结束本章"按钮，玩家主动选择结束
- **Pros**: 玩家完全控制
- **Cons**: 打破沉浸感——"墨色自然晕开"是设计意图；玩家可能在关联牵引力很强时误点结束
- **Rejection Reason**: 自动化完成检测与"画卷自然合上"的 Pillar 4 审美一致

## Consequences

### Positive

- 两部分完成检测兼顾"充分探索"和"自然结束"——不会卡住也不会仓促
- 章节重玩保留不可逆状态——"你不能假装没发生过"强化 Pillar 1（选择即重写）
- 线性解锁保证叙事顺序——章节 N 必须在 N-1 之后
- 4 个生命周期事件为下游系统提供清晰的挂载点

### Negative

- COMPLETION_ASSOCIATION_THRESHOLD = 0.30 是理论值——需 playtest 验证
- 关联引擎返回空候选时直接触发完成——需确保此行为符合设计意图
- 重玩时的状态保留/重置边界需仔细测试（哪些该保留、哪些该重置）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 关联阈值太早触发完成（玩家感觉突然） | Medium | Medium | 暴露阈值参数为 Tuning Knob；playtest 验证 |
| 关联阈值太晚触发完成（玩家感觉拖沓） | Low | Low | 同上 |
| 章节重玩时 ChangeTracker overlay 与重置后访问追踪不匹配 | Low | Medium | 单元测试覆盖重玩后的 GetCurrentState 行为 |
| 最终章完成流程中 SaveAsync 失败 | Low | High | SaveAsync 内部重试 + 失败提示；不跳过 Step 3 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (CheckChapterCompletion) | < 0.5ms（关联引擎调用 + 条件评估） |
| CPU (TransitionToFragment) | < 0.1ms（状态检查 + HashSet 更新） |
| Memory (ChapterManager) | ~5KB |
| Memory (ChapterDefinition × 4) | ~2KB |
| GC Allocation | 0（复用 HashSet/List 引用） |

## Migration Plan

新建项目，无迁移需求。

## Validation Criteria

- [ ] 新游戏 → Ch01 EntryFragmentId 加载 → OnChapterStarted("ch01") 触发
- [ ] TransitionToFragment → _chapterVisitedFragments 更新 → OnFragmentChanged 触发
- [ ] 访问碎片达 CompletionRatio + 最佳候选分数 < 0.30 → 章节完成流程启动
- [ ] 章节完成 → _completedChapters 更新 → auto_save 写入 → 过渡到下一章
- [ ] 最终章完成 → OnAllChaptersCompleted 触发
- [ ] 章节重玩 → overlay/flags 保留，访问追踪重置
- [ ] 读档恢复 → CurrentChapterKey + CurrentFragmentId 正确恢复
- [ ] TRANSITIONING 状态中 TransitionToFragment 调用被忽略 + LogWarning
- [ ] 预加载在剩余 ≤3 未访问碎片时触发，每章仅一次

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `chapter-management.md` (#15) | 章节管理 | ChapterDefinition SO | ScriptableObject 配置结构 |
| `chapter-management.md` (#15) | 章节管理 | 3 状态机 | Idle/InChapter/Transitioning |
| `chapter-management.md` (#15) | 章节管理 | 两部分完成检测 | 全部访问 + 关联阈值降级 |
| `chapter-management.md` (#15) | 章节管理 | 章节完成过渡流程 | 判定结局→存档→过渡→事件 |
| `chapter-management.md` (#15) | 章节管理 | 章节重玩保留不可变状态 | overlay/flags 保留，访问追踪重置 |
| `chapter-management.md` (#15) | 章节管理 | 预加载触发 | 剩余 ≤3 未访问碎片 + fire-and-forget |
| `chapter-management.md` (#15) | 章节管理 | 线性章节解锁 | OrderIndex 驱动，永久并集 |
| `chapter-management.md` (#15) | 章节管理 | 存档/读档集成 | CurrentChapterKey/FragmentId + CompletedChapters/UnlockedChapters |
| `cross-chapter-state.md` (#16) | 跨章状态 | IsImmutable 保护触发 | OnChapterReplayStarted → CrossChapterTracker |
| `save-load-system.md` (#7) | 存档 | 进度字段 | SaveData.CurrentChapterKey/FragmentId/CompletedChapters/UnlockedChapters |

## Related

- ADR-0004 — SceneManager 执行实际碎片/章节过渡
- ADR-0007 — ChangeTracker 管理 _overlay 和 _flags（重玩保留）
- ADR-0003 — SaveManager 序列化进度数据
- ADR-0010 — MultiEnding 在章节完成时判定结局
- ADR-0011 — CrossChapterTracker IsImmutable 保护（重玩时调用）
- ADR-0009 — WebAssociationEngine 提供候选列表 + 完成检测的分数检查
- `design/gdd/chapter-management.md` — 完整 GDD
