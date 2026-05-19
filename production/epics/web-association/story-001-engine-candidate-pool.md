# Story 001: 引擎架构 + 候选池构建

> **Epic**: 网状关联引擎 (WebAssociationEngine)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/web-association-engine.md`
**Requirement**: `TR-web-association-001`, `TR-web-association-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0009: 网状关联引擎
**ADR Decision Summary**: 纯 C# 类（非 MonoBehaviour），构造函数依赖注入（IDataManager, IEmotionalTagSystem, IChangeTracker）；ComputeAssociations 纯函数；候选池 = 同章已解锁碎片，排除自身/ConditionGroup 不可达

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 数学计算，零 Unity API 依赖；可在单元测试中独立实例化

**Control Manifest Rules (Feature Layer)**:
- Required: Four-factor web association formula — source: ADR-0009
- Required: Candidate pool filtering — same chapter, unlocked, conditions met, exclude self — source: ADR-0009

---

## Acceptance Criteria

*From GDD `design/gdd/web-association-engine.md`, scoped to this story:*

- [ ] GIVEN 当前碎片在章节 Ch01 中有 10 个候选碎片（均有标签权重），WHEN SceneManager 调用 ComputeAssociations(currentFragmentId, "Ch01", recentHistory, visitedIds)，THEN 返回包含 5 个 AssociationCandidate 的列表，按 compositeScore 降序排列。第 1 个候选的 compositeScore ≥ 第 5 个。

- [ ] GIVEN 当前碎片的 EmotionalTags 为空，WHEN ComputeAssociations 执行，THEN A = 0 对所有候选。排序仅由 B（显式关联）和 C×D 驱动。不抛异常。

- [ ] GIVEN 章节仅有 2 个候选碎片（微小章节），且两者的 compositeScore 都低于 0.05，WHEN ComputeAssociations 执行，THEN 返回 2 个候选（最低候选数放宽——返回实际可用数量），Strength 标记为 Trace。

- [ ] GIVEN 候选池中某碎片 DefaultState = Locked 或 ConditionGroup 判定为不可达，WHEN 构建候选池，THEN 该碎片不出现在候选池中。

---

## Implementation Notes

*Derived from ADR-0009 Implementation Guidelines:*

```csharp
public class WebAssociationEngine
{
    private readonly IDataManager _dataManager;
    private readonly IEmotionalTagSystem _tagSystem;
    private readonly IChangeTracker _changeTracker;

    public WebAssociationEngine(
        IDataManager dataManager,
        IEmotionalTagSystem tagSystem,
        IChangeTracker changeTracker)
    {
        _dataManager = dataManager;
        _tagSystem = tagSystem;
        _changeTracker = changeTracker;
    }

    public List<AssociationCandidate> ComputeAssociations(
        string currentFragmentId,
        string chapterKey,
        List<string> recentHistory,
        HashSet<string> visitedFragmentIds)
    {
        // Step 1: Build candidate pool
        var allFragments = _dataManager.GetFragmentsByChapter(chapterKey);
        var candidates = allFragments
            .Where(f =>
                f.FragmentId != currentFragmentId &&
                f.DefaultState != FragmentState.Locked &&
                _changeTracker.EvaluateCondition(f.UnlockCondition))
            .ToList();

        // Step 2-5: Score, sort, grade (delegated to factor methods)
        // ...
    }
}

public struct AssociationCandidate
{
    public string FragmentId;
    public float CompositeScore;
    public Strength Grade;
    public DominantFactor DominantFactor;
    // Factor breakdown for debugging
    public float FactorA;
    public float FactorB;
    public float FactorC;
    public float FactorD;
}
```

候选池过滤规则：
- 同章（chapterKey 匹配）
- 排除自身（FragmentId != currentFragmentId）
- 排除 Locked 状态
- 排除 ConditionGroup 不可达（EvaluateCondition 返回 false）

微小章节保护：候选池 ≤ 3 → 0.05 排除阈值放宽，所有未被 B=-1.0 排除的候选均保留。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: Factor A — 余弦标签相似度计算
- Story 003: Factor B/C/D — 显式关联、情感节奏、发现偏向
- Story 004: 综合评分公式 + 排名 + Strength 分级 + DominantFactor

---

## QA Test Cases

- **AC-1**: 基本返回
  - Given: 10 个候选在 Ch01 中（均有标签），recentHistory 为空，visitedIds 为空
  - When: ComputeAssociations("frag_current", "Ch01", [], emptySet)
  - Then: 返回 5 个 AssociationCandidate；按 compositeScore 降序；candidates[0].CompositeScore ≥ candidates[4].CompositeScore
  - Edge cases: 候选池恰好 5 个 → 返回 5 个

- **AC-2**: 空标签碎片
  - Given: currentFragment.EmotionalTags 为空；候选池 10 个
  - When: ComputeAssociations
  - Then: 所有候选 FactorA = 0；不抛异常；排序正常执行
  - Edge cases: 候选也空标签 → 无变化

- **AC-3**: 微小章节
  - Given: 章节仅 2 个候选（均 compositeScore < 0.05），无 B=-1.0 排除
  - When: ComputeAssociations
  - Then: 返回 2 个候选（非空列表）；Strength = Trace
  - Edge cases: 1 个候选 → 返回 1 个

- **AC-4**: 候选池过滤
  - Given: 章节 10 碎片，其中 2 个 Locked，1 个 ConditionGroup 不可达（EvaluateCondition=false）
  - When: 构建候选池
  - Then: 候选池包含 7 个碎片（含排除自身则 6 个）
  - Edge cases: 全部 Locked → 空候选池 → 返回空列表

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/web-association/engine_candidate_pool_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: data-management Story 001 (IDataManager.GetFragmentsByChapter)；ChangeTracker Story 003 (EvaluateCondition)
- Unlocks: Story 002/003/004 (factor calculation depends on engine skeleton)
