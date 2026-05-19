# Story 002: 因子 A — 余弦标签相似度

> **Epic**: 网状关联引擎 (WebAssociationEngine)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/web-association-engine.md`
**Requirement**: `TR-web-association-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0009: 网状关联引擎
**ADR Decision Summary**: Factor A = CosineSimilarity(currentTags, candidateTags) 使用 TagSimilarityMatrix N×N 预计算 SO；权重 0.6

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: TagSimilarityMatrix 为 ScriptableObject（仅 Editor 时修改 + 运行时只读）；余弦相似度纯数学计算

**Control Manifest Rules (Feature Layer)**:
- Required: Four-factor web association formula — Factor A weight 0.6 — source: ADR-0009

---

## Acceptance Criteria

*From GDD `design/gdd/web-association-engine.md`, scoped to this story:*

- [ ] GIVEN 当前碎片的 EmotionalTags = {Nostalgia:0.9, Rain:0.7}，候选 A 的标签 = {Rain:0.8, Solitude:0.6}，候选 B 的标签 = {Joy:1.0}，且无显式关联，WHEN 计算 A 因子，THEN A(current, 候选A) > A(current, 候选B) —— Rain 标签相似度高于 Joy（无关标签）。

- [ ] GIVEN 玩家在碎片 A 做出选择（触发 ModifyTagWeight——将候选 B 的 Nostalgia 从 0.5 变为 0.9），WHEN SceneManager 在 ApplyChanges 后重新调用 ComputeAssociations，THEN A(A, B) 的新值能够反映变化后的标签权重（Nostalgia:0.9 替代 0.5）。

- [ ] GIVEN TagSimilarityMatrix 中 M[Rain][Rain]=1.0, M[Rain][Solitude]=0.4，当前标签 {Rain:0.7}，候选标签 {Rain:0.8, Solitude:0.6}，WHEN 计算余弦相似度，THEN 结果使用矩阵值 M[i][j] 而非默认规则。

---

## Implementation Notes

*Derived from GDD formulas + ADR-0009:*

```csharp
private float ComputeFactorA(MemoryFragment current, MemoryFragment candidate)
{
    var currentTags = _changeTracker.GetEffectiveTags(current.FragmentId);
    var candidateTags = _changeTracker.GetEffectiveTags(candidate.FragmentId);

    float dotProduct = 0f;
    float normCurrent = 0f;
    float normCandidate = 0f;

    for (int i = 0; i < currentTags.Count; i++)
    {
        float wi = currentTags[i].Weight;
        normCurrent += wi * wi;
        
        for (int j = 0; j < candidateTags.Count; j++)
        {
            float wj = candidateTags[j].Weight;
            float mij = _tagSystem.GetTagSimilarity(currentTags[i].TagId, candidateTags[j].TagId);
            dotProduct += wi * wj * mij;
        }
    }

    foreach (var tag in candidateTags)
        normCandidate += tag.Weight * tag.Weight;

    if (normCurrent < 0.0001f || normCandidate < 0.0001f)
        return 0f;

    float cosine = dotProduct / (Mathf.Sqrt(normCurrent) * Mathf.Sqrt(normCandidate));
    return Mathf.Clamp01(cosine);
}
```

TagSimilarityMatrix 默认规则（无设计师覆盖时）:
- 相同标签: 1.0
- 同一父标签下: 0.6
- 同一情感类别: 0.4
- 无关标签: 0.0

使用 `_changeTracker.GetEffectiveTags()` 获取运行时标签权重（已叠加 ModifyTagWeight overlay）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 引擎架构、候选池构建
- Story 003: Factor B/C/D 计算
- Story 004: 综合评分公式、排名、分级
- TagSimilarityMatrix Editor 工具（归 emotional-tag Story 004 的 Tag Browser 或独立工具故事）

---

## QA Test Cases

- **AC-1**: 标签相似度比较
  - Given: 当前标签 {Nostalgia:0.9, Rain:0.7}；候选A {Rain:0.8, Solitude:0.6}；候选B {Joy:1.0}；Matrix 默认规则（Rain↔Solitude=0.4, Rain↔Joy=0.0）
  - When: ComputeFactorA(current, A) 和 ComputeFactorA(current, B)
  - Then: A_A > A_B
  - Edge cases: 空标签 → A=0；所有标签权重为 0 → A=0

- **AC-2**: 运行时权重变化
  - Given: 候选 B 初始 Nostalgia=0.5；ModifyTagWeight 改为 0.9
  - When: 重新调用 ComputeFactorA(A, B)
  - Then: 新的 A 值 > 旧的 A 值（Nostalgia 权重增加 → 相似度贡献增加）
  - Edge cases: ModifyTagWeight 将权重降为 0 → 标签不再参与相似度计算

- **AC-3**: 矩阵值使用
  - Given: Matrix[M1][M2] 被设计师设为 0.8（覆盖默认 0.4）
  - When: ComputeFactorA（涉及 M1↔M2）
  - Then: 使用 0.8 而非默认 0.4
  - Edge cases: 矩阵维度不匹配 → 退化到默认规则 + LogWarning

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/web-association/factor_a_tag_similarity_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (引擎骨架)；emotional-tag Story 002 (_tagSystem.GetTagSimilarity)；ChangeTracker Story 002 (GetEffectiveTags)
- Unlocks: Story 004 (composite scoring needs all factors)
