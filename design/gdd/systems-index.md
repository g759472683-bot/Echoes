# Systems Index: 回响 (Echoes)

> **Status**: Draft
> **Created**: 2026-05-11
> **Last Updated**: 2026-05-11
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

《回响》是一个2D手绘叙事探索游戏，玩家化身为游魂在记忆碎片中穿行。它的机械核心是**网状关联**——记忆碎片通过情感标签连接，玩家的选择直接改变记忆画面内容，而非简单地选择路线。相比传统动作或RPG游戏，它的系统架构更接近"交互式叙事引擎"：输入→场景→交互→选择→关联→结局，每一层都是轻量的，但层层叠加后产生丰富的叙事可能性。

24个系统分布在5个层级中：基础框架层（7个）支撑引擎运转，核心玩法层（7个）实现记忆交互与关联的核心循环，进度层（2个）管理章节与跨章节状态，界面层（5个）将玩法呈现给玩家，辅助层（3个）打磨完整体验。19个系统在MVP阶段就需要完成——对于单人4-6个月的开发周期，大部分系统设计简洁、实现轻量，复杂度集中在网状关联引擎和记忆变化追踪两个核心系统上。

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | 输入系统 (Input System) | Foundation | MVP | Designed | design/gdd/input-system.md | — |
| 2 | 数据管理系统 (Data Management) | Foundation | MVP | Designed | design/gdd/data-management.md | — |
| 3 | 音频系统 (Audio System) | Foundation | MVP | Needs Revision | design/gdd/audio-system.md | — |
| 4 | 本地化系统 (Localization) | Foundation | MVP | Designed | design/gdd/localization.md | — |
| 5 | UI框架 (UI Framework) | Foundation | MVP | Designed | design/gdd/ui-framework.md | 输入系统 |
| 6 | 场景管理系统 (Scene Management) | Foundation | MVP | Needs Revision | design/gdd/scene-management.md | 数据管理 |
| 7 | 存档系统 (Save/Load System) | Foundation | MVP | Needs Revision | design/gdd/save-load-system.md | 数据管理 |
| 8 | 记忆碎片数据模型 (Memory Fragment Data Model) | Gameplay | MVP | Designed | design/gdd/memory-fragment-data-model.md | 数据管理 |
| 9 | 微动画系统 (Micro-Animation System) | Gameplay | MVP | Designed | design/gdd/micro-animation-system.md | 场景管理 |
| 10 | 情感标签系统 (Emotional Tag System) | Gameplay | MVP | Designed | design/gdd/emotional-tag-system.md | 数据模型 |
| 11 | 记忆画卷交互系统 (Scroll Interaction) | Gameplay | MVP | Needs Revision | design/gdd/scroll-interaction-system.md | 输入 + 场景管理 + 微动画 |
| 12 | 记忆变化追踪 (Memory Change Tracking) | Gameplay | MVP | Needs Revision | design/gdd/memory-change-tracking.md | 数据模型 + 交互系统 |
| 13 | 网状关联引擎 (Web Association Engine) | Gameplay | MVP | Designed | design/gdd/web-association-engine.md | 情感标签 + 数据模型 |
| 14 | 多结局系统 (Multi-Ending System) | Gameplay | MVP | Designed | design/gdd/multi-ending-system.md | 数据模型 + 变化追踪 + 关联引擎 |
| 15 | 章节管理系统 (Chapter Management) | Progression | MVP | Designed | design/gdd/chapter-management.md | 场景管理 + 多结局 + 存档 |
| 16 | 跨章节状态追踪 (Cross-Chapter State Tracking) | Progression | MVP | Designed | design/gdd/cross-chapter-state-tracking.md | 存档 + 变化追踪 |
| 17 | 游戏内HUD (In-Game HUD) | UI | MVP | Designed | design/gdd/in-game-hud.md | UI框架 + 交互系统 |
| 18 | 交互反馈系统 (Interaction Feedback) | UI | MVP | Not Started | — | UI框架 + 音频 + 交互系统 |
| 19 | 主菜单与菜单系统 (Main Menu & Menus) | UI | MVP | Designed | design/gdd/main-menu.md | UI框架 + 存档 + 章节管理 |
| 20 | 结局呈现 (Ending Presentation) | UI | Vertical Slice | Not Started | — | UI框架 + 多结局 |
| 21 | 章节选择界面 (Chapter Select Screen) | UI | Vertical Slice | Not Started | — | UI框架 + 章节管理 |
| 22 | 无障碍系统 (Accessibility) | Meta | Vertical Slice | Not Started | — | UI框架 + 输入 |
| 23 | 成就与收集追踪 (Achievement & Collection Tracking) | Meta | Full Vision | Not Started | — | 多结局 + 存档 |
| 24 | 画廊/回忆录 (Gallery / Memoir) | Meta | Full Vision | Not Started | — | 章节管理 + 变化追踪 + UI框架 |

---

## Categories

| Category | Description | Systems |
|----------|-------------|---------|
| **Foundation** | 引擎和框架基础——所有其他系统的运行前提 | 输入、数据管理、音频、本地化、UI框架、场景管理、存档 |
| **Gameplay** | 核心玩法机制——记忆交互、关联、变化、结局 | 数据模型、微动画、情感标签、交互系统、变化追踪、关联引擎、多结局 |
| **Progression** | 章节推进与跨章节状态 | 章节管理、跨章节状态追踪 |
| **UI** | 玩家可见的信息展示与交互界面 | HUD、交互反馈、主菜单、结局呈现、章节选择 |
| **Meta** | 打磨与辅助系统——跨切面的完整体验 | 无障碍、成就追踪、画廊 |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | 核心循环可玩——2个完整章节，网状关联，多结局，跨章节隐藏结局 | 第一可玩原型 (2-3个月) | 优先设计 |
| **Vertical Slice** | 打磨一个完整区域——完整动画、菜单打磨、基础无障碍 | 垂直切片 / Demo (3-4个月) | 第二批设计 |
| **Full Vision** | 完整4章、全部隐藏结局、画廊、内部成就追踪 | 完整发布 (4-6个月) | 按需设计 |

---

## Dependency Map

Systems sorted by dependency order — design and build from top to bottom.

### Foundation Layer (no dependencies)

1. **输入系统** — 键盘/鼠标输入的封装，为所有交互提供原始输入数据
2. **数据管理系统** — JSON/ScriptableObject 配置加载，所有游戏数据的入口
3. **音频系统** — 环境音、氛围音乐、交互音效的播放管理
4. **本地化系统** — 字符串表管理，中文为第一语言

### Foundation Layer (light dependencies)

5. **UI框架** — depends on: 输入系统。按钮、面板、文本渲染、UI动画的基础设施
6. **场景管理系统** — depends on: 数据管理。记忆碎片场景的加载、卸载、过渡
7. **存档系统** — depends on: 数据管理。序列化格式、存档文件读写

### Gameplay Layer

8. **记忆碎片数据模型** — depends on: 数据管理。定义一个碎片是什么：画面、情感标签、可选物件、选择项、变化条件
9. **微动画系统** — depends on: 场景管理。画卷中的微动画——物件发光、风吹草动、水流、光影
10. **情感标签系统** — depends on: 数据模型。定义情感标签词汇表，支持标签的分配和查询
11. **记忆画卷交互系统** — depends on: 输入 + 场景管理 + 微动画。在2D手绘场景中触碰物件、激活记忆动画、做出选择
12. **记忆变化追踪** — depends on: 数据模型 + 交互系统。记录每次选择，计算后续碎片的内容变化
13. **网状关联引擎** — depends on: 情感标签 + 数据模型。通过情感标签权重计算碎片之间的关联，含情感节奏控制
14. **多结局系统** — depends on: 数据模型 + 变化追踪 + 关联引擎。根据关联路径和关键选择判定结局

### Progression Layer

15. **章节管理系统** — depends on: 场景管理 + 多结局 + 存档。章节入口、流程推进、完成判定
16. **跨章节状态追踪** — depends on: 存档 + 变化追踪。跨章节触发条件的持久化追踪

### UI Layer

17. **游戏内HUD** — depends on: UI框架 + 交互系统。交互提示、选择界面、章节进度指示
18. **交互反馈系统** — depends on: UI框架 + 音频 + 交互系统。物件发光、触碰响应、选择确认的视觉+听觉反馈
19. **主菜单与菜单系统** — depends on: UI框架 + 存档 + 章节管理。主菜单、暂停菜单、设置界面
20. **结局呈现** — depends on: UI框架 + 多结局。结局画面的展示
21. **章节选择界面** — depends on: UI框架 + 章节管理。已解锁章节的选择与重玩

### Meta Layer

22. **无障碍系统** — depends on: UI框架 + 输入。色盲模式、文本缩放、减少动画、键盘导航
23. **成就与收集追踪** — depends on: 多结局 + 存档。内部追踪结局解锁状态
24. **画廊/回忆录** — depends on: 章节管理 + 变化追踪 + UI框架。已解锁记忆碎片的回看

---

## Recommended Design Order

Combining dependency sort and priority tiers. Systems at the same layer can be designed in parallel.

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | 输入系统 | MVP | Foundation | gameplay-programmer | S |
| 2 | 数据管理系统 | MVP | Foundation | gameplay-programmer | S |
| 3 | 音频系统 | MVP | Foundation | audio-director, gameplay-programmer | S |
| 4 | 本地化系统 | MVP | Foundation | localization-lead | S |
| 5 | UI框架 | MVP | Foundation | ui-programmer | M |
| 6 | 场景管理系统 | MVP | Foundation | engine-programmer | M |
| 7 | 存档系统 | MVP | Foundation | gameplay-programmer | S |
| 8 | 记忆碎片数据模型 | MVP | Gameplay | game-designer | M |
| 9 | 微动画系统 | MVP | Gameplay | technical-artist, gameplay-programmer | M |
| 10 | 情感标签系统 | MVP | Gameplay | game-designer | M |
| 11 | 记忆画卷交互系统 | MVP | Gameplay | game-designer, gameplay-programmer | L |
| 12 | 记忆变化追踪 | MVP | Gameplay | gameplay-programmer | M |
| 13 | 网状关联引擎 | MVP | Gameplay | game-designer, ai-programmer | L |
| 14 | 多结局系统 | MVP | Gameplay | game-designer | M |
| 15 | 章节管理系统 | MVP | Progression | game-designer | M |
| 16 | 跨章节状态追踪 | MVP | Progression | gameplay-programmer | M |
| 17 | 游戏内HUD | MVP | UI | ui-programmer | M |
| 18 | 交互反馈系统 | MVP | UI | ui-programmer | S |
| 19 | 主菜单与菜单系统 | MVP | UI | ui-programmer | M |
| 20 | 结局呈现 | Vertical Slice | UI | ui-programmer, technical-artist | M |
| 21 | 章节选择界面 | Vertical Slice | UI | ui-programmer | S |
| 22 | 无障碍系统 | Vertical Slice | Meta | accessibility-specialist | S |
| 23 | 成就与收集追踪 | Full Vision | Meta | gameplay-programmer | S |
| 24 | 画廊/回忆录 | Full Vision | Meta | ui-programmer | M |

Effort estimates: S = 1 session, M = 2-3 sessions, L = 4+ sessions.

---

## Circular Dependencies

- None found. All dependency chains are directed acyclic.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| 网状关联引擎 | Technical + Design | 情感标签权重算法未经验证；情感节奏控制（防止连续同情绪碎片）需要原型测试 | 第一个原型系统——用10个碎片的手工数据集验证关联质量 |
| 记忆变化追踪 | Technical | 跨碎片的组合式内容变化——前一个选择影响后三个画面的内容，状态空间可能爆炸 | 限制每个碎片的变化维度（最多3个变化轴），用变化规则表而非过程式生成 |
| 跨章节状态追踪 | Technical | 持久化跨章节触发条件的正确性——"第一章保存了某封信→第三章NPC出现"这类长链依赖容易出错 | 定义清晰的跨章节状态条件DSL，每个条件有明确的真值判定和单元测试 |
| 记忆画卷交互系统 | Design | "触碰记忆"的交互感受——太像点击游戏会破坏沉浸感，太隐晦玩家找不到可交互物件（Pillar 4 设计测试：3秒内找到交互物件） | 视觉提示系统（物件发光）需要在开发早期做A/B测试 |
| 微动画系统 | Scope | 手绘微动画（风吹、水流、光影）的资产制作量——每张插图需要额外的动画层 | MVP阶段仅做物件发光+简单位移动画；手绘逐帧动画留给Vertical Slice |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 24 |
| Design docs started | 19 |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 19/19 |
| Vertical Slice systems designed | 0/3 |

---

## Next Steps

- [ ] Design MVP-tier systems in dependency order — start with `/design-system 输入系统`
- [ ] Run `/design-review` on each completed GDD
- [ ] Prototype 网状关联引擎 first (highest technical risk)
- [ ] Run `/gate-check pre-production` when MVP systems are designed
- [ ] Use `/map-systems next` to always pick the highest-priority undesigned system
