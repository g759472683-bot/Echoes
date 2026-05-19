# Epic: 游戏内HUD (InGameHUD)

> **Layer**: Feature
> **GDD**: design/gdd/in-game-hud.md
> **Architecture Module**: HUD (#17)
> **Status**: Complete
> **Stories**: 4/4 Complete

## Overview

实现《回响》中的游戏内 HUD——UI Toolkit VisualElement 树（#fragment-text-overlay / #choice-panel / #association-paths / #chapter-progress / #interaction-hint），挂载在 Game 场景 UIDocument 上。消费交互系统 (#11)、关联引擎 (#13)、章节管理 (#15) 的数据，渲染为墨迹风格的界面元素。选择面板——手写体选项 + 朱砂墨点，面板定位在锚点物件旁；关联路径可视化——5 条从画面中心辐射的墨线，Strength 分级（浓墨/半透明/淡墨/虚线）；碎片文本浮层——手写体，picking-mode:ignore，4s 自动淡出；章节进度——底部水平墨点（实心朱砂=已访问，空心=未访问，脉动=当前）。MVVM 数据绑定（INotifyBindablePropertyChanged）。HUD 是 UI 框架面板栈之下的持久层——Gameplay 可见，UI 面板打开时隐藏。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: UI 框架 | UI Toolkit (UXML/USS), PanelStack LIFO, Theme.uss | LOW |
| ADR-0001: 事件总线 | HUD 订阅 OnFragmentChanged, OnChoiceSelected 等 static events | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-in-game-hud-001 | UI Toolkit VisualElement 树（5 个子元素） | ADR-0006 ✅ |
| TR-in-game-hud-002 | MVVM 数据绑定（INotifyBindablePropertyChanged） | ADR-0006 ✅ |
| TR-in-game-hud-003 | 关联路径视觉分级（Strong/Medium/Faint/Trace 墨线） | ⚠️ No dedicated ADR |
| TR-in-game-hud-004 | 章节进度（水平墨点——已访问/未访问/当前） | ⚠️ No dedicated ADR |
| TR-in-game-hud-005 | 文本浮层 auto-fade 4.0s, picking-mode:ignore | ⚠️ No dedicated ADR |
| TR-in-game-hud-006 | HUD 可见性规则表（Gameplay/ChoicePanel/Transitioning/UIPanel） | ADR-0006 ✅ |

Note: ⚠️ items are covered by GDD directly — no dedicated ADR. Implementation follows GDD rules verbatim.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/in-game-hud.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | HUD 架构 + 选择面板 | UI | Complete | ADR-0006 |
| 002 | 关联路径可视化 | UI | Complete | ADR-0006 |
| 003 | 文本浮层 + 章节进度 + 交互提示 | UI | Complete | ADR-0006 |
| 004 | MVVM 数据绑定 + 显示/隐藏规则 | Logic | Complete | ADR-0006, ADR-0001 |
