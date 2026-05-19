# Story 001: 章节状态机 + 碎片导航

> **Epic**: 章节管理 (ChapterManager)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/chapter-management.md`
**Requirement**: `TR-chapter-management-001`, `TR-chapter-management-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0001 (事件总线)
**ADR Decision Summary**: ChapterManager 是轻量级进度状态机和协调器——持有当前章节/碎片引用，3 状态状态机（IDLE/IN_CHAPTER/TRANSITIONING），ChapterDefinition ScriptableObject 运行时只读；碎片导航由关联引擎驱动（非 SequenceIndex 线性），TransitionToFragment 协调 SceneManager + 更新追踪

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: MonoBehaviour + C# 状态机；ChapterDefinition SO 遵循 ADR-0007 运行时只读

**Control Manifest Rules (Feature Layer)**:
- Required: Static event 声明在生产者系统 — OnChapterStarted/OnFragmentChanged — source: ADR-0001
- Forbidden: Never 轮询检查状态变化 — 所有过渡由事件驱动

---

## Acceptance Criteria

*From GDD `design/gdd/chapter-management.md`, scoped to this story:*

- [ ] GIVEN 游戏首次启动，WHEN 玩家选择"新游戏"，THEN ChapterManager 进入 TRANSITIONING → 加载 Ch01 → 进入 Ch01 的 EntryFragmentId → CurrentState = IN_CHAPTER。OnChapterStarted("ch01") 触发。

- [ ] GIVEN 玩家在 Ch01 的碎片 A 中，HUD 渲染了关联引擎返回的候选列表，WHEN 玩家选择碎片 B，THEN ChapterManager.TransitionToFragment("B") 被调用 → SceneManager 执行碎片过渡 → _chapterVisitedFragments 包含 B → OnFragmentChanged 触发。

- [ ] GIVEN 章节过渡进行中 (CurrentState = TRANSITIONING)，WHEN 其他系统尝试调用 TransitionToFragment，THEN 调用被忽略 + LogWarning。转换期间不允许碎片切换。

- [ ] GIVEN 玩家在 Ch01 中途保存并退出，WHEN 从主菜单加载存档，THEN CurrentChapterKey="ch01", CurrentFragmentId 恢复到保存时的碎片，_completedChapters 和 _unlockedChapters 恢复。

---

## Implementation Notes

*Derived from GDD rules 1-4, 10-11 + ADR-0001:*

### ChapterDefinition SO

```csharp
// Runtime read-only SO (ADR-0007):
ChapterDefinition : ScriptableObject
├── ChapterKey : string              // "ch01", "ch02"
├── DisplayNameKey : TableReference
├── OrderIndex : int                 // 0-based, drives linear unlock
├── EntryFragmentId : string         // First fragment on new game / replay
├── Endings : EndingDefinition[]     // Consumed by MultiEndingSystem (#14)
├── CompletionRatio : float          // [0.0, 1.0], default 0.6
└── AllowReplay : bool               // default true
```

No explicit fragment list — each MemoryFragment declares ChapterId. Single source of truth.

### 3-State Machine

```
IDLE → TRANSITIONING (StartNewGame / LoadAndRestore)
IN_CHAPTER → TRANSITIONING (Chapter completion detected)
TRANSITIONING → IN_CHAPTER (Chapter + entry fragment loaded, fade-in done)
TRANSITIONING → IDLE (Load failure or return to main menu)
IN_CHAPTER → IDLE (Player exits via pause menu)
```

### ChapterManager Skeleton

```csharp
public class ChapterManager : MonoBehaviour
{
    public ChapterState CurrentState { get; private set; } = ChapterState.Idle;
    public string CurrentChapterKey { get; private set; }
    public string CurrentFragmentId { get; private set; }
    
    private HashSet<string> _chapterVisitedFragments;
    private HashSet<string> _sessionVisitedFragments;
    private List<string> _recentHistory;
    private HashSet<string> _completedChapters;
    private HashSet<string> _unlockedChapters;
    private int _totalFragmentsInChapter;
    private bool _preloadNotYetTriggered;
    
    // Static events (ADR-0001)
    public static event Action<string> OnChapterStarted;
    public static event Action<string, string> OnFragmentChanged;
}
```

### Fragment Navigation Rule

Fragment navigation is driven by WebAssociationEngine (#13) — NOT SequenceIndex linear order:

1. Player in fragment A → WebAssociationEngine.ComputeAssociations(A, chapterKey, recentHistory, sessionVisited) → Top-5 candidates
2. HUD (#17) renders candidates → Player selects target B
3. HUD calls `ChapterManager.TransitionToFragment(B)`
4. ChapterManager calls `SceneManager.TransitionToFragmentAsync(chapterKey, B)` → await
5. After transition: `_chapterVisitedFragments.Add(B)`, `_sessionVisitedFragments.Add(B)`, update `_recentHistory`
6. Check preload threshold + completion condition

### Save/Load Bridge

```csharp
// Collect (called by SaveManager)
CurrentChapterKey   → saveData.CurrentChapterKey
CurrentFragmentId   → saveData.CurrentFragmentId
_completedChapters  → saveData.CompletedChapters
_unlockedChapters   → saveData.UnlockedChapters

// Restore
_completedChapters = new HashSet<string>(saveData.CompletedChapters);
_unlockedChapters = new HashSet<string>(saveData.UnlockedChapters);
await EnterChapterAtFragment(saveData.CurrentChapterKey, saveData.CurrentFragmentId);
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 章节完成检测 + 过渡流程
- Story 003: 章节重玩 + 线性解锁
- 关联引擎 (#13): ComputeAssociations 实现
- 场景管理 (#6): TransitionToFragmentAsync / TransitionToChapterAsync 物理执行
- HUD (#17): 候选列表渲染

---

## QA Test Cases

- **AC-1**: New game starts chapter
  - Given: Game first launch, no save data
  - When: StartNewGame() called
  - Then: CurrentState = TRANSITIONING → Ch01 loaded → EntryFragmentId entered → CurrentState = IN_CHAPTER; OnChapterStarted("ch01") fired
  - Edge cases: ChapterDefinition missing → Error logged, state stays IDLE

- **AC-2**: Fragment navigation via association
  - Given: Player in fragment A, HUD shows candidates, selects fragment B
  - When: TransitionToFragment("B") called
  - Then: SceneManager.TransitionToFragmentAsync invoked; _chapterVisitedFragments contains B; OnFragmentChanged(A→B) fired
  - Edge cases: Fragment B in different chapter → rejected with LogWarning; B == current fragment → no-op

- **AC-3**: TRANSITIONING blocks navigation
  - Given: CurrentState = TRANSITIONING (mid-transition)
  - When: TransitionToFragment("C") called
  - Then: Call ignored; LogWarning emitted; no state change
  - Edge cases: Rapid double-click on two candidates → second call hits TRANSITIONING guard

- **AC-4**: Save/Load round-trip
  - Given: Player in Ch01 frag_05; save game; quit; reload
  - When: LoadAndRestore(saveData) called
  - Then: CurrentChapterKey = "ch01"; CurrentFragmentId restored; _completedChapters and _unlockedChapters match saved state
  - Edge cases: Saved ChapterKey/FragmentId don't exist in current build → fallback to first chapter's EntryFragmentId + LogWarning

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/chapter-management/state_machine_navigation_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/chapter-management/state_machine_navigation_test.cs` (8 tests)

---

## Dependencies

- Depends on: 场景管理 Story 001 (SceneManager scene loading)；存档系统 Story 001 (SaveData structure)
- Unlocks: Story 002 (completion detection needs state machine + navigation)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Implementation**: `src/core/ChapterState.cs`, `src/core/IChapterManagerDependencies.cs`, `src/core/ChapterManager.cs`
**Test Evidence**: `tests/integration/chapter-management/state_machine_navigation_test.cs` (8 tests)
**Code Review**: Pending
