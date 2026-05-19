# Story 002: 三阶段判定算法

> **Epic**: 多结局系统 (MultiEndingSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/multi-ending-system.md`
**Requirement**: `TR-multi-ending-002`, `TR-multi-ending-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: 多结局判定算法
**ADR Decision Summary**: Stage 2 — IsEssential 门控（任一未满足→取消资格）；Stage 3a+b — ContributionWeight 累加 + EmotionalAffinity 路径加分 + 阈值检查；若 qualifiedEndings 为空 → 返回默认结局

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯 C# 判定逻辑；ResolveEnding 同步完成 <1ms

**Control Manifest Rules (Feature Layer)**:
- Required: Deterministic tie-breaking — Score DESC → TriggerCount DESC → EndingId ASC — source: ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/multi-ending-system.md`, scoped to this story:*

- [ ] GIVEN 玩家在 Ch01 中做出了选择——在 frag_03 中选了 "keep_letter"（触发 ContributionWeight=0.4 给 ending_A），在 frag_07 中选了 "open_window"（触发 ContributionWeight=0.3 给 ending_A），且 ending_A 的 IsEssential 触发器已满足、MinimumScore=0.5，WHEN Chapter Management 调用 ResolveEnding("ch01")，THEN ending_A 的 score = 0.7 ≥ 0.5 → ending_A 胜出。

- [ ] GIVEN ending_B 的 IsEssential 触发器引用 FlagSet("found_secret", true)，而该 Flag 为 false，WHEN ResolveEnding 执行，THEN ending_B 在必要门被取消资格——即使其非必要条件全部满足且 ContributionWeight 很高。

- [ ] GIVEN 玩家未满足任何结局的必要门条件，WHEN ResolveEnding 执行，THEN qualifiedEndings 为空 → 返回 IsDefault=true 的默认结局。IsDefault 标志在 ResolvedEnding 中为 true。

- [ ] GIVEN 无结局达到 MinimumScore 阈值（所有 score < MinimumScore），WHEN ResolveEnding 执行，THEN 返回默认结局。

---

## Implementation Notes

*Derived from ADR-0010 Implementation Guidelines:*

```csharp
public ResolvedEnding ResolveEnding(string chapterId)
{
    // Load ending definitions
    var endingDefs = _dataManager.GetChapterDefinition(chapterId).Endings;
    
    // Stage 1: Collect triggers (from Story 001)
    var triggerGroups = CollectTriggers(chapterId);
    
    // Stage 2-3: Evaluate each ending
    var qualified = new List<(EndingDefinition Def, float Score)>();
    
    foreach (var def in endingDefs)
    {
        if (!triggerGroups.TryGetValue(def.EndingId, out var triggers))
            triggers = new List<EndingTrigger>();
        
        // Stage 2: Essential Gate
        bool essentialPassed = true;
        foreach (var trigger in triggers.Where(t => t.IsEssential))
        {
            if (!_changeTracker.EvaluateCondition(trigger.TriggerCondition))
            {
                essentialPassed = false;
                break;
            }
        }
        if (!essentialPassed) continue; // DISQUALIFIED
        
        // Stage 3a: Accumulate score
        float score = 0f;
        foreach (var trigger in triggers)
        {
            if (_changeTracker.EvaluateCondition(trigger.TriggerCondition))
                score += trigger.ContributionWeight;
        }
        score = Mathf.Clamp01(score);
        
        // Stage 3b: Path Bonus (MVP default weight=0.0 — hook only)
        if (!string.IsNullOrEmpty(def.EmotionalAffinity))
        {
            var dominantEmotion = ComputeDominantPathEmotion(chapterId);
            if (def.EmotionalAffinity == dominantEmotion)
                score *= (1.0f + _pathBonusWeight); // _pathBonusWeight default 0.0
        }
        
        // Stage 3c: Threshold check
        if (score >= def.MinimumScore)
            qualified.Add((def, score));
    }
    
    // Stage 3d: Fallback
    if (qualified.Count == 0)
    {
        var fallback = endingDefs.First(d => d.IsDefault);
        return new ResolvedEnding { EndingId = fallback.EndingId, IsDefault = true, ... };
    }
    
    // ... tie-breaking (Story 003)
}
```

`ComputeDominantPathEmotion`: 遍历章节已访问碎片，统计每碎片主导情感类别频次 → 返回最高频类别。MVP 中 `_pathBonusWeight = 0.0`（Hook 已实现但禁用）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 数据结构定义、触发器收集
- Story 003: Tie-Breaking、UnlockedEndingIds 持久化、重判
- Story 004: 隐藏结局跨章节支持

---

## QA Test Cases

- **AC-1**: 基本累加判定
  - Given: ending_A 有 2 个触发器：keep_letter(0.4) 和 open_window(0.3)；IsEssential 通过；MinimumScore=0.5
  - When: ResolveEnding("ch01")
  - Then: ending_A 胜出；score = 0.7
  - Edge cases: score exactly at threshold → 通过；score = 0.49 且 threshold = 0.5 → 不通过

- **AC-2**: Essential Gate 失败
  - Given: ending_B 有 1 个 IsEssential 触发器引用 FlagSet("found_secret", true)；_flags 中无此 Flag
  - When: ResolveEnding
  - Then: ending_B 在 Stage 2 被取消资格；不出现在 qualified 中
  - Edge cases: 多个 IsEssential → 任一个未满足即取消；IsEssential + non-essential 混合 → essential 未满足 → 全部不计算

- **AC-3**: Default fallback
  - Given: 所有结局的 IsEssential 均未满足
  - When: ResolveEnding
  - Then: 返回 IsDefault=true 的结局；ResolvedEnding.IsDefault = true；score = 0.0
  - Edge cases: 零个 IsDefault → Error + 取第一个 EndingDefinition

- **AC-4**: Threshold not met
  - Given: ending_A score=0.3, MinimumScore=0.5（essential 通过但分数不足）；ending_B score=0.6, MinimumScore=0.5（通过）
  - When: ResolveEnding
  - Then: ending_A 未达阈值被排除；ending_B 合格；若 ending_B 也低于阈值 → 返回默认结局
  - Edge cases: score = 0.0, threshold = 0.0 → 通过

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/multi-ending/resolution_algorithm_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (trigger collection)；ChangeTracker Story 003 (EvaluateCondition)
- Unlocks: Story 003 (Tie-breaking extends ResolveEnding)

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: `tests/unit/multi-ending/resolution_algorithm_test.cs` — 10 test functions
**Code Review**: Pending
