# Story 004: 综合评分 + 排名 + Strength 分级

> **Epic**: 网状关联引擎 (WebAssociationEngine)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/web-association-engine.md`
**Requirement**: `TR-web-association-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0009: 网状关联引擎
**ADR Decision Summary**: compositeScore = (A × 0.6 + B × 0.4) × C × D；排除规则（B=-1.0 / compositeScore < 0.05）；Top-5 降序；Strength 4 级 + DominantFactor 标记

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 排序 + 阈值比较

**Control Manifest Rules (Feature Layer)**:
- Required: Visual grading thresholds — Strong (≥0.8), Medium (0.5-0.79), Faint (0.25-0.49), Trace (<0.25) — source: ADR-0009

---

## Acceptance Criteria

*From GDD `design/gdd/web-association-engine.md`, scoped to this story:*

- [ ] GIVEN 某个候选的 compositeScore = 0.72，WHEN 确定 Strength 分级，THEN Strength = Strong (≥0.60)。DominantFactor 标记为对 (tagScore+explicitScore)×C×D 贡献最大的单项因子。

- [ ] GIVEN 候选池有 10 个经过评分的候选，WHEN 排序并截取，THEN 返回 Top-5（compositeScore 最高的 5 个），按降序排列。

- [ ] GIVEN 候选的 compositeScore = 0.03 且候选池 > 3，WHEN 应用排除阈值，THEN 该候选被排除。若排除后候选不足 3 个 → 放宽阈值保留至少 3 个。

---

## Implementation Notes

*Derived from GDD formulas + ADR-0009:*

### Composite Score

```csharp
public List<AssociationCandidate> ComputeAssociations(...)
{
    var candidates = BuildCandidatePool(currentFragmentId, chapterKey);

    var scored = new List<AssociationCandidate>();
    foreach (var candidate in candidates)
    {
        float a = ComputeFactorA(current, candidate);
        float b = ComputeFactorB(current, candidate);
        if (b < 0f) continue; // B=-1.0 exclusion

        float c = ComputeFactorC(GetDominantCategory(candidate), recentHistory);
        float d = ComputeFactorD(candidate.FragmentId, visitedFragmentIds);

        float tagScore = a * 0.6f;
        float explicitScore = b * 0.4f;
        float compositeScore = (tagScore + explicitScore) * c * d;

        var result = new AssociationCandidate
        {
            FragmentId = candidate.FragmentId,
            CompositeScore = compositeScore,
            FactorA = a, FactorB = b, FactorC = c, FactorD = d,
            Strength = DetermineStrength(compositeScore),
            DominantFactor = DetermineDominantFactor(tagScore, explicitScore, c, d)
        };
        scored.Add(result);
    }

    // Sort descending
    scored.Sort((x, y) => y.CompositeScore.CompareTo(x.CompositeScore));

    // Apply exclusion threshold (with minimum candidate protection)
    float threshold = 0.05f;
    var filtered = scored.Where(s => s.CompositeScore >= threshold).ToList();
    if (filtered.Count < 3)
        filtered = scored.Take(Mathf.Max(3, scored.Count)).ToList();

    return filtered.Take(5).ToList();
}
```

### Strength Grading

```csharp
private Strength DetermineStrength(float score)
{
    if (score >= 0.60f) return Strength.Strong;
    if (score >= 0.30f) return Strength.Medium;
    if (score >= 0.10f) return Strength.Faint;
    return Strength.Trace;
}
```

### DominantFactor

```csharp
private DominantFactor DetermineDominantFactor(
    float tagScore, float explicitScore, float c, float d)
{
    float tagContrib = tagScore;
    float explicitContrib = explicitScore;
    float rhythmContrib = Mathf.Abs((tagScore + explicitScore) * (c - 1.0f));
    float discoveryContrib = Mathf.Abs((tagScore + explicitScore) * c * (d - 1.0f));

    float max = Mathf.Max(tagContrib, explicitContrib, rhythmContrib, discoveryContrib);
    if (max == tagContrib) return DominantFactor.TagSimilarity;
    if (max == explicitContrib) return DominantFactor.ExplicitAssociation;
    if (max == rhythmContrib) return DominantFactor.RhythmBoost;
    return DominantFactor.DiscoveryBoost;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 引擎骨架、候选池
- Story 002: Factor A
- Story 003: Factor B/C/D

---

## QA Test Cases

- **AC-1**: Strength 分级
  - Given: compositeScore = 0.72
  - When: DetermineStrength(0.72)
  - Then: Strength = Strong
  - Edge cases: 0.60 → Strong（边界值包含）；0.59 → Medium；0.30 → Medium；0.29 → Faint；0.10 → Faint；0.09 → Trace

- **AC-2**: Top-5 排序
  - Given: 10 个候选评分完成，compositeScore 各不相同
  - When: 排序并取 Top-5
  - Then: 返回 5 个候选；降序排列；第 1 个分数最高
  - Edge cases: 平局分数 → 保持原始顺序（稳定排序）；候选 < 5 → 返回所有

- **AC-3**: 排除阈值
  - Given: 10 个候选中 8 个 compositeScore < 0.05
  - When: 应用排除阈值
  - Then: 8 个低分候选被排除，返回 2 个高分候选（若 ≥ 3 则仅返回高分；不足 3 → 放宽保留至少 3）
  - Edge cases: 所有候选 < 0.05 且候选池 ≥ 3 → 返回 Top-3（放宽阈值后）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/web-association/composite_score_ranking_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001/002/003 — all factor computations
- Unlocks: SceneManager (#6) integration (calling ComputeAssociations on fragment transitions)；HUD (#17) integration (displaying AssociationCandidate[])
