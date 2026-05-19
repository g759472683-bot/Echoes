# Story 003: 因子 B + C + D — 显式关联、情感节奏、发现偏向

> **Epic**: 网状关联引擎 (WebAssociationEngine)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/web-association-engine.md`
**Requirement**: `TR-web-association-004`, `TR-web-association-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0009: 网状关联引擎
**ADR Decision Summary**: Factor B = ExplicitWeight（双向加成 +0.15, B=-1.0 设计师排除）；Factor C = RhythmPenalty（K=4 滑动窗口, position-based penalty）；Factor D = DiscoveryBoost（未访问 ×1.30, 重访衰减, pending changes 保底 0.70）

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 计算；_visitedFragments + visitCount 来自 ChangeTracker

**Control Manifest Rules (Feature Layer)**:
- Required: Rhythm penalty sliding window K=4 — source: ADR-0009
- Required: Discovery boost for unvisited fragments — source: ADR-0009
- Forbidden: Never present more than 5 association paths simultaneously — source: ADR-0009

---

## Acceptance Criteria

*From GDD `design/gdd/web-association-engine.md`, scoped to this story:*

- [ ] GIVEN 当前碎片的 ExplicitAssociation 中候选 X 的 Weight = -1.0，WHEN ComputeAssociations 执行，THEN 候选 X 不出现在返回列表中——即使其标签相似度很高。

- [ ] GIVEN 当前碎片有 ExplicitAssociation 指向候选 Z (Weight=0.8, Direction=Bidirectional)，且候选 Z 也有 ExplicitAssociation 指向当前碎片 (Weight=0.9)，WHEN 计算 B 因子，THEN B = min(0.8 + 0.15, 1.0) = 0.95。

- [ ] GIVEN recentHistory = [碎片A(Sadness), 碎片B(Sadness), 碎片C(Sadness)]，候选的主导类别 = Sadness，WHEN 计算 C 因子，THEN C < 1.0（有惩罚）。具体值: 最近位(pos=1 碎片C) ×0.70, 第2位(pos=2 碎片B) ×0.55 → C = 0.70 × 0.55 = 0.385。

- [ ] GIVEN 候选 X 在 visitedFragmentIds 中（已访问 1 次，无 pending changes），候选 Y 不在 visitedFragmentIds 中，且 A、B、C 因子相同，WHEN 计算 D 因子，THEN D(Y) = 1.30, D(X) = 0.70。Y 的 compositeScore > X。

- [ ] GIVEN recentHistory 为空（冷启动），visitedFragmentIds 为空，WHEN ComputeAssociations 执行，THEN 所有候选的 C = 1.0, D = 1.30。仅 A 和 B 产生区分。

---

## Implementation Notes

*Derived from GDD formulas + ADR-0009:*

### Factor B — Explicit Association

```csharp
private float ComputeFactorB(MemoryFragment current, MemoryFragment candidate)
{
    // Check if candidate is in current's ExplicitAssociations
    var explicit = current.ExplicitAssociations
        .FirstOrDefault(e => e.TargetFragmentId == candidate.FragmentId
            && _changeTracker.EvaluateCondition(e.Condition));

    if (explicit == null) return 0f;

    // Designer exclusion
    if (explicit.Weight < 0f) return -1.0f;

    float b = explicit.Weight;

    // Bidirectional bonus
    var reverse = candidate.ExplicitAssociations
        .FirstOrDefault(e => e.TargetFragmentId == current.FragmentId
            && _changeTracker.EvaluateCondition(e.Condition));
    if (reverse != null && reverse.Weight >= 0f)
        b = Mathf.Min(b + 0.15f, 1.0f);

    return b;
}
```

### Factor C — Rhythm Penalty

```csharp
private float ComputeFactorC(string dominantCategory, List<string> recentHistory)
{
    float c = 1.0f;
    int candidatePoolSize = /* from context */;

    for (int i = 0; i < Mathf.Min(recentHistory.Count, 4); i++)
    {
        var histFrag = _dataManager.GetFragment(recentHistory[i]);
        if (GetDominantCategory(histFrag) == dominantCategory)
        {
            float penalty = (i) switch {
                0 => 0.70f,  // position 1
                1 => 0.55f,  // position 2
                2 => 0.40f,  // position 3
                3 => 0.25f,  // position 4
                _ => 1.0f
            };

            // Adaptive halving for small candidate pools
            if (candidatePoolSize <= 5)
                penalty = 1.0f - (1.0f - penalty) * 0.5f;

            c *= penalty;
        }
    }

    // Peace category boost
    if (dominantCategory == "Peace")
        c *= 1.30f;

    return Mathf.Clamp(c, 0.10f, 1.30f);
}
```

### Factor D — Discovery Boost

```csharp
private float ComputeFactorD(string candidateId, HashSet<string> visitedIds)
{
    if (!visitedIds.Contains(candidateId))
        return 1.30f;

    int visitCount = _changeTracker.GetVisitCount(candidateId);
    float d = Mathf.Max(0.30f, 1.0f - (visitCount * 0.30f));

    // Revisit incentive
    if (_changeTracker.HasPendingChanges(candidateId))
        d = Mathf.Max(d, 0.70f);

    return Mathf.Clamp(d, 0.30f, 1.30f);
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 引擎骨架、候选池构建
- Story 002: Factor A（标签相似度）
- Story 004: 综合评分公式、排名、分级、DominantFactor

---

## QA Test Cases

- **AC-1**: B=-1.0 排除
  - Given: 候选 X 在 current.ExplicitAssociations 中 Weight=-1.0
  - When: ComputeFactorB(current, X)
  - Then: 返回 -1.0 → 候选被排除
  - Edge cases: 多个候选 B=-1.0 → 全部排除

- **AC-2**: 双向加成
  - Given: current→Z Weight=0.8；Z→current Weight=0.9
  - When: ComputeFactorB(current, Z)
  - Then: B = 0.95（0.8 + 0.15, min with 1.0）
  - Edge cases: 单向 → 无加成；B 已 0.9 + 0.15 = 1.05 → Clamp 1.0

- **AC-3**: 情感节奏惩罚
  - Given: recentHistory = [A(Sadness), B(Sadness), C(Sadness)]；候选 dominant=Sadness
  - When: ComputeFactorC("Sadness", recentHistory)
  - Then: C = 0.70 × 0.55 = 0.385
  - Edge cases: 4 个同类别 → C = 0.70×0.55×0.40×0.25 = 0.0385 → Clamp 0.10

- **AC-4**: 发现偏向
  - Given: X 未访问，Y 已访问 1 次（无 pending changes）
  - When: ComputeFactorD(X) vs ComputeFactorD(Y)
  - Then: D(X)=1.30, D(Y)=0.70
  - Edge cases: Y 有 pending changes → D(Y)=0.70（already ≥0.70）；Z visited 3 次 + pending → D=0.70

- **AC-5**: 冷启动
  - Given: recentHistory=[]，visitedIds=[]
  - When: ComputeFactorC + ComputeFactorD
  - Then: C=1.0（所有候选），D=1.30（所有候选）
  - Edge cases: 仅一个因子冷启动 → 另一个正常计算

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/web-association/factor_bcd_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (引擎骨架)；ChangeTracker Story 003 (_flags, _visitedFragments, visit count, HasPendingChanges)
- Unlocks: Story 004 (composite scoring uses all four factors)
