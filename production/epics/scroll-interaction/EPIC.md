# Epic: 记忆画卷交互系统 (InteractionManager)

> **Layer**: Feature
> **GDD**: design/gdd/scroll-interaction-system.md
> **Architecture Module**: InteractionManager (#11)
> **Status**: Ready
> **Stories**: 4 created — 2 Logic, 2 Integration

## Overview

实现《回响》中玩家与记忆世界之间的"手"——将输入系统中的鼠标位置和点击转化为对记忆画卷中物件的触碰、拖拽和悬停。核心是一个集中式 InteractionManager MonoBehaviour（Game 场景持久）：每帧通过单次 Physics2D.OverlapPoint 检测 Interactable 图层上的鼠标悬停物件，管理悬停/离开/点击/拖拽四个交互事件，通过 10 个静态 C# 事件对外广播，根据物件的 InteractionType（Touch/Drag/Hover/Examine）和 InteractionResult（PlayAnimation/ShowText/PresentChoice/TransitionToFragment/RevealObject）调度后续行为。包含拖拽交互系统（触发阈值 5px、完成阈值 30px、弹回动画 0.3s）、交互状态机（Idle/Active/Dragging/ChoicePresenting/Examining/Blocked）、选择面板智能定位（优先右侧，空间不足时下方）、以及碎片过渡期间的交互保护（Action Map Inactive 门控）。

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 核心检测引擎 + 状态机 | Logic | Ready | ADR-0005 |
| 002 | 交互类型处理 + 事件广播 | Integration | Ready | ADR-0001 |
| 003 | 拖拽交互系统 | Logic | Ready | ADR-0005 |
| 004 | 选择流程 + Escape 取消 | Integration | Ready | ADR-0001, ADR-0007 |

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: 事件总线架构 | 10 个 static event Action 事件通过 InteractionManager 广播所有交互行为 | LOW |
| ADR-0005: 输入系统架构 | Physics2D.OverlapPoint (non-alloc) 每帧一次，Interactable 图层过滤 | HIGH |
| ADR-0007: SO 不可变 + Overlay | 读取 MemoryFragment SO 中的 InteractiveObjects[]/ChoiceGroups[]/InteractionResult 定义 | MEDIUM |

⚠️ 本系统无专属 ADR。交互类型处理（Touch/Drag/Hover/Examine）、结果分发算法、拖拽阈值、状态机由 GDD `scroll-interaction-system.md` 第 3 节直接定义。

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-scroll-interaction-001 | InteractionManager 集中式交互检测——单次 OverlapPoint/frame，Interactable 图层，Gameplay Action Map 门控 | ADR-0005 ✅ |
| TR-scroll-interaction-002 | 四种交互类型处理（Touch/Drag/Hover/Examine）+ 5 种 InteractionResult 分发（PlayAnimation/ShowText/PresentChoice/TransitionToFragment/RevealObject） | ⚠️ No dedicated ADR |
| TR-scroll-interaction-003 | 拖拽交互系统（触发 5px/完成 30px/弹回 0.3s EaseOutCubic）+ 拖拽期间交互互斥 | ⚠️ No dedicated ADR |
| TR-scroll-interaction-004 | 10 个静态 C# 事件（OnHoverEnter/OnHoverExit/OnInteract/OnDragStart/OnDragComplete/OnDragCancel/OnChoiceSelected/OnChoiceHover/OnRevealObject/OnShowText） | ADR-0001 ✅ |
| TR-scroll-interaction-005 | 物件状态管理（Active/Hidden/Disabled）+ DefaultState 驱动碰撞体启用/禁用 | ADR-0007 ✅ |
| TR-scroll-interaction-006 | 碎片过渡期间交互保护（Inactive Action Map 门控）+ OnFragmentTransitioned 后重建碰撞体 | ADR-0004, ADR-0005 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/scroll-interaction-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/story-readiness production/epics/scroll-interaction/story-001-core-detection-engine.md` then `/dev-story` to begin implementation.
