# Story 002: 关联路径可视化

> **Epic**: 游戏内HUD (InGameHUD)
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/in-game-hud.md`
**Requirement**: `TR-in-game-hud-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: UI Toolkit 渲染关联路径——从画面中心辐射的墨线，Strength 分级（Strong=浓墨、Medium=半透明墨、Faint=淡墨、Trace=虚线）。不使用按钮列表样式——路径作为 VisualElement 渲染，方向由候选碎片在章节中的预设角度决定（MVP 均匀分布）。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: UI Toolkit 中动态绘制曲线墨迹路径——需使用 `generateVisualContent` 回调或预渲染纹理。USS transition 仅支持 opacity/transform，墨线颜色过渡需代码驱动。

**Control Manifest Rules (Feature Layer)**:
- Required: UI Toolkit for all runtime UI — source: ADR-0006
- Required: Visual grading thresholds — Strong (≥0.8), Medium (0.5-0.79), Faint (0.25-0.49), Trace (<0.25) — source: ADR-0009
- Forbidden: Never present more than 5 association paths simultaneously — source: ADR-0009

---

## Acceptance Criteria

*From GDD `design/gdd/in-game-hud.md`, scoped to this story:*

- [ ] GIVEN 关联引擎返回 3 个候选 (Strong: Score 0.85, Medium: 0.62, Faint: 0.31)，WHEN HUD 渲染关联路径，THEN 3 条墨线从画面中心辐射出去——Strong 候选的墨线最浓 (opacity 0.9)、目的地标记最大 (16px)。Medium 候选半透明 (opacity 0.6)、标记中等 (12px)。Faint 候选淡墨 (opacity 0.35)、标记小 (8px)。不显示 .scent-label 文字标签（MVP 范围）。

- [ ] GIVEN 玩家点击一条关联路径的 .path-candidate，WHEN 点击检测触发，THEN ChapterManager.TransitionToFragment(candidate.TargetFragmentId) 被调用。碎片过渡开始。

- [ ] GIVEN 关联引擎返回 0 个候选，WHEN HUD 更新关联路径，THEN #association-paths 容器保持空——不显示任何路径。不触发 TransitionToFragment。

- [ ] GIVEN 关联引擎返回 6 个候选 (>5)，WHEN HUD 渲染，THEN 仅渲染 Top-5 候选（按 Score DESC）。第 6 个候选不显示。

---

## Implementation Notes

*Derived from ADR-0006 + GDD rule 3:*

### Association Path Element Structure

```
#association-paths
├── .path-candidate (× Top-5)
│   ├── .ink-trail          // VisualElement — 墨迹拖痕
│   ├── .scent-label        // 文字标签 (MVP 不渲染)
│   └── .target-indicator   // 目的地标记 (朱砂圈)
```

### ShowAssociationPaths Method

```csharp
void ShowAssociationPaths(AssociationCandidate[] candidates)
{
    var container = _uiDocument.rootVisualElement.Q("#association-paths");
    container.Clear();

    // Sort by Score DESC, take Top-5
    var topCandidates = candidates
        .OrderByDescending(c => c.Score)
        .Take(5);

    foreach (var candidate in topCandidates)
    {
        var pathEl = CreatePathCandidate(candidate);
        pathEl.RegisterCallback<ClickEvent>(_ =>
        {
            // Fire fragment transition
            ChapterManager.TransitionToFragment(candidate.TargetFragmentId);
        });
        container.Add(pathEl);
    }
}
```

### Strength → Visual Mapping

| Strength | Score Range | Ink Trail Opacity | Target Indicator Size | Line Style |
|----------|------------|-------------------|----------------------|------------|
| Strong | ≥ 0.8 | 0.9 | 16px | 实线浓墨 (rgb(20,15,10)) |
| Medium | 0.5–0.79 | 0.6 | 12px | 半透明墨 |
| Faint | 0.25–0.49 | 0.35 | 8px | 淡墨 |
| Trace | < 0.25 | 0.15 | 6px (点) | 虚线极淡 |

### 墨线方向均匀分布

```csharp
float CalculateDirectionAngle(int index, int totalCandidates)
{
    // Start from top (-90°), distribute clockwise
    float angleStep = 360f / totalCandidates;
    return -90f + (angleStep * index);
}
```

- MVP 阶段：均匀分布方向——无预设角度数据
- 墨线最大长度 200px（Tuning Knob）

### 键盘导航

```
Arrow Keys 在 .path-candidate 之间移动焦点
Enter 触发聚焦路径的 TransitionToFragment
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: HUD 根 VisualElement 树创建
- Story 003: 文本浮层 + 章节进度 + 交互提示
- Story 004: MVVM 数据绑定
- .scent-label 文字标签（DominantFactor 显示 — 推迟到 Vertical Slice）
- 关联引擎 (#13): AssociationCandidate[] 数据结构、Score 计算
- 章节管理 (#15): TransitionToFragment 方法
- 画面中心到目标方向的角度预设数据（Editor 中配置——MVP 均匀分布）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Association paths render with correct visual grading
  - Setup: HUD initialized; 3 AssociationCandidates returned with Scores 0.85/0.62/0.31; call ShowAssociationPaths
  - Verify: 3 .path-candidate elements in #association-paths; opacity 0.9/0.6/0.35; target indicator sizes 16px/12px/8px; no .scent-label text
  - Pass condition: All 3 paths visible with correct strength-based visual properties

- **AC-2**: Click on path candidate triggers fragment transition
  - Setup: Association paths rendered with a candidate targeting fragment "ch01_frag_05"
  - Verify: Click on .path-candidate → ChapterManager.TransitionToFragment("ch01_frag_05") called
  - Pass condition: Transition initiated with correct TargetFragmentId

- **AC-3**: Zero candidates — empty container
  - Setup: AssociationCandidates array is empty
  - Verify: #association-paths container has 0 children; no errors; TransitionToFragment not called
  - Pass condition: Clean empty state with no visual or logical errors

- **AC-4**: More than 5 candidates — only Top-5 rendered
  - Setup: 6 AssociationCandidates returned with descending Scores
  - Verify: Only 5 .path-candidate elements rendered; candidate with lowest Score (6th) absent
  - Pass condition: Top-5 displayed, 6th excluded without error

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/association-paths-visualization-evidence.md` — manual walkthrough doc or interaction test

**Status**: [x] Created — production/qa/evidence/association-paths-visualization-evidence.md

---

## Dependencies

- Depends on: Story 001 (HUD root VisualElement tree); 关联引擎 Story 002 (ComputeAssociations returns AssociationCandidate[])
- Unlocks: Story 003 (HUD full integration)

---

## Completion Notes

- **Completed**: 2026-05-19
- **Implementation**: InGameHUD.cs (ShowAssociationPaths, ApplyStrengthVisuals, RenderPathsFromDataSource), PathCandidateData.cs, AssociationPathsDataSource.cs
- **UXML**: assets/uxml/in-game-hud.uxml (#association-paths)
- **USS**: assets/uss/in-game-hud.uss (.association-paths, .path-candidate, .ink-trail, .target-indicator)
- **Evidence**: production/qa/evidence/association-paths-visualization-evidence.md
- **Deviations**: No .scent-label text (per MVP scope — GDD Rule 9). ChapterManager.TransitionToFragment accessed via InGameHUD.ChapterManagerRef static property set by BootBootstrap.
- **Blockers**: None
