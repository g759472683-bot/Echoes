# Game Concept: 回响 (Echoes)

*Created: 2026-05-11*
*Status: Draft*

---

## Elevator Pitch

> 你是一个游魂，漂浮在自己活过的一生中。记忆像散落的画卷碎片——触碰、选择、重写它们。每一次回溯，碎片重新排列，真相也随之改变。有些结局，藏在你从未踏足的关联里。

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | 叙事探索 / 记忆拼图 |
| **Platform** | PC (Steam / Epic) |
| **Target Audience** | 喜欢叙事驱动、情感沉浸的单机玩家 |
| **Player Count** | 单人 |
| **Session Length** | 30-90分钟（一章） |
| **Monetization** | 买断制 (Premium) |
| **Estimated Scope** | 中小型（4-6个月，单人开发） |
| **Comparable Titles** | 《What Remains of Edith Finch》《极乐迪斯科》《NieR: Automata》 |

---

## Core Fantasy

你不是在看一段已经写好的故事——你在**亲手重写记忆**。

每个记忆碎片像一幅手绘画卷在你面前展开。你触碰画面中的物件，画面随之改变。你的选择不是"选A还是选B"，而是**直接在记忆上留下痕迹**——让某些东西浮现，让另一些东西褪色。没有"正确"的结局，只有不同的真相。这种感觉就像在翻阅一本旧相册，却发现每次打开，照片里的人都在看着不同的方向。

---

## Unique Hook

像《What Remains of Edith Finch》的叙事步行模拟器，**AND ALSO** 记忆内容会因你的选择而改变——网状关联让每次回溯走向不同的真相，隐藏结局需要跨章节触发特定记忆。

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Narrative** (drama, story arc) | 1 | 网状关联 + 选择改写记忆，每次回溯形成不同的叙事弧 |
| **Discovery** (exploration, secrets) | 2 | 隐藏结局需跨章节触发，鼓励反复探索不同的记忆路径 |
| **Sensation** (sensory pleasure) | 3 | 手绘插画风格 + 微动画（风吹、水流、光影），画卷式的视觉沉浸 |
| **Submission** (relaxation, comfort zone) | 4 | 无时间压力，无战斗，慢节奏在记忆画卷中穿行 |
| **Fantasy** (make-believe, role-playing) | 5 | 化身为游魂，以超然的视角重新审视一段人生 |
| **Expression** (self-expression, creativity) | 6 | 选择反映玩家的价值观——优先保留什么记忆，放弃什么 |
| **Challenge** (obstacle course, mastery) | N/A | 这不是以挑战为核心的游戏 |
| **Fellowship** (social connection) | N/A | 纯单人体验 |

### Key Dynamics (Emergent player behaviors)

- 玩家会反复重玩同一章节，尝试不同的关联路径，发现上次错过的记忆碎片
- 玩家会在不同章节之间寻找关联线索，推测隐藏结局的触发条件
- 玩家会对某些记忆产生个人化的情感依附——"这个回忆我不想改"
- 玩家社区会分享隐藏结局的触发方式，形成探索者社区文化

### Core Mechanics (Systems we build)

1. **记忆画卷交互系统** — 在2D手绘场景中触碰物件、激活记忆动画、做出选择
2. **网状关联引擎** — 每个记忆碎片通过情感标签关联到其他碎片，选择影响关联权重
3. **章节与进度管理** — 线性解锁章节，但隐藏结局需跨章节触发条件满足
4. **记忆变化追踪** — 记录玩家在每个碎片中的选择，影响后续碎片的画面内容和可用选项
5. **多结局系统** — 每章2-5种结局变体（含隐藏结局），取决于关联路径和关键选择

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** (freedom, meaningful choice) | 玩家的选择直接改变记忆画面和叙事走向；网状关联让路径由玩家决定 | Core |
| **Competence** (mastery, skill growth) | 多周目中玩家越来越擅长发现隐藏线索和跨章节关联 | Supporting |
| **Relatedness** (connection, belonging) | 与记忆中的角色建立深层情感连接，跨章节的NPC命运牵动玩家 | Core |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** (goal completion, collection, progression) — How: 收集全部结局变体和隐藏结局是天然的全成就驱动力
- [x] **Explorers** (discovery, understanding systems, finding secrets) — How: 网状关联 + 隐藏内容，核心吸引力正是"我还没去过那个记忆分支"
- [ ] **Socializers** (relationships, cooperation, community) — How: 纯单机，但隐藏结局的社区分享是次要社交层
- [ ] **Killers/Competitors** (domination, PvP, leaderboards) — How: 完全不适用

### Flow State Design

- **Onboarding curve**: 第一个记忆碎片作为教程——自然地引导玩家触碰画面中的第一件物品，激活第一段记忆动画
- **Difficulty scaling**: 不是传统难度曲线，而是关联网络的复杂度逐渐展开——前期碎片关联少，后期网状结构更丰富
- **Feedback clarity**: 画面中的物件在可交互时有微妙的视觉提示（微光、颜色变化）；选择后画面立即产生可见变化
- **Recovery from failure**: 没有"失败"——每次选择都是一种有效的叙事路径。重玩即"重新探索"

---

## Core Loop

### Moment-to-Moment (30 seconds)
玩家在一幅手绘记忆画卷中飘荡，触碰画面中发光的物件（一封信、一扇窗、一个人的背影），激活一段短动画，或面对一个微小的选择（拿起信 / 不拿，开窗 / 不开）。

### Short-Term (5-15 minutes)
几个记忆碎片通过**网状关联**串联——一个画面中的物件引出另一个相关联的记忆。玩家在一条"记忆链"中穿行，直到该链条自然收束（一个阶段性记忆场景的结束）。

### Session-Level (30-120 minutes)
一次完整的章节体验。从一个固定的章节入口开始，经历15-25个记忆碎片，最终抵达该章节的一个结局（取决于选择的路径）。玩家可以重新进入同一章节尝试不同路径。

### Long-Term Progression
- 完成一章解锁下一章（线性推进）
- 隐藏结局需要跨章节触发条件——例如在第一章保存了某封信，第三章某个NPC才会出现
- 总共有3个隐藏结局分散在多周目探索中

### Retention Hooks
- **Curiosity**: "如果我当时选了另一个选项会怎样？"；"我还漏了什么？"
- **Investment**: 对角色的情感投入——想知道他们完整的故事
- **Mastery**: 逐渐理解关联网络的结构，能更有目的性地探索特定路径
- **Social**: 社区分享隐藏结局触发条件（次要）

---

## Game Pillars

### Pillar 1: 选择即重写
玩家的选择不是"选哪条路"，而是**直接改变记忆本身的内容**。同一个场景，不同的选择会让画面上出现不同的东西、不同的人物反应、不同的结局碎片。

*Design test*: 如果我们纠结"这个场景该让玩家做什么"，支柱说——**给一个会改变画面的选择，而不是给一段只能看的过场**。

### Pillar 2: 不完美才是力量
这个游戏不以"圆满"为目标。主线结局可以是平和的，但真正打动人的是那些触发条件苛刻的隐藏结局——它们可能更破碎、更遗憾、也更真实。

*Design test*: 如果我们纠结"这个结局够不够好"，支柱说——**它不需要好，它需要真**。

### Pillar 3: 关联的网络，不是线性的书
记忆碎片之间通过**联想**连接——一个画面中的物件、一句话、一种颜色，都可能引出另一个碎片。玩家在网状结构中穿行，而不是翻页。

*Design test*: 如果我们纠结"这个碎片应该接到哪里"，支柱说——**接在情感上相关的地方，而不是时间顺序上相邻的地方**。

### Pillar 4: 画卷中有呼吸
视觉必须是**手绘插画式的**。记忆画面不是写实3D场景，而是像《黑神话悟空》的画卷动画——有留白、有意境、有"未完成"的美。画面中的元素会有微动画（风吹树叶、水流、光影移动），但核心是**静中的动**。

*Design test*: 如果我们纠结"这个场景视觉怎么做"，支柱说——**能用一幅画表达的，不要建一个房间**。

### Anti-Pillars (What This Game Is NOT)

- **NOT 战斗系统**: 这不是动作游戏。冲突存在于选择中，不在武器中。加入战斗会破坏沉静的氛围。
- **NOT "唯一真结局"**: 不存在"选对了就能幸福"的路线。每条路线都失去一些东西。如果有完美结局，就背叛了"不完美才是力量"。
- **NOT 开放世界**: 这是结构化的章节。关联是丰富的，但世界是有边界的。开放世界会摧毁叙事节奏。
- **NOT 全程配音**: 体量不允许，且画卷风格更适合文字+画面+环境音。关键记忆可以有短配音作为情绪高点。

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| 《What Remains of Edith Finch》 | 以记忆为舞台的叙事探索玩法；每个记忆片段的独特视觉呈现 | 记忆内容可被玩家选择改变；网状关联而非线性顺序 | 验证了"纯叙事探索"可以成为独立游戏的完整体验 |
| 《极乐迪斯科》 | 无战斗的深度叙事；选择塑造角色和世界 | 2D画卷式视觉；章节制而非开放区域 | 证明了无战斗的叙事游戏有忠实的市场 |
| 《NieR: Automata》 | 多周目隐藏结局；不同路线揭示不同真相 | 更小的体量；手绘风格 | 验证了"跨周目解锁隐藏内容"的吸引力 |
| 《黑神话悟空》 | 画卷式插画动画的视觉语言 | 整套游戏都是画卷风格，而非仅过场 | 视觉方向的直接参考——"画卷中有呼吸"的来源 |

**Non-game inspirations**:
- **中国传统水墨画**：留白、意境、情感在画面之外
- **王家卫电影**：记忆的不确定性、时间的非线性、不完美的情感结局
- **《追忆似水年华》**（普鲁斯特）：一个物件触发一段记忆——这正是我们网状关联的文学原型

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18-40 |
| **Gaming experience** | Mid-core — 玩过一些独立游戏，欣赏叙事驱动的体验 |
| **Time availability** | 每次30-90分钟，能够沉浸式体验一个章节 |
| **Platform preference** | PC (Steam) |
| **Current games they play** | 《赛博朋克2077》《荒野大镖客2》《极乐迪斯科》《What Remains of Edith Finch》《去月球》 |
| **What they're looking for** | 有情感深度、不追求"圆满"、会让人事后回味的叙事体验 |
| **What would turn them away** | 战斗系统、过长的跑图、没有情感重量的"纯玩法"游戏 |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity — 用户偏好；2D插画+动画管线成熟；Steam发布支持完善 |
| **Key Technical Challenges** | 网状关联引擎（情感标签匹配+选择权重）、手绘动画系统、跨章节触发条件追踪 |
| **Art Style** | 2D手绘插画 + 微动画层（风吹、水流、光影） |
| **Art Pipeline Complexity** | Medium — 自定义2D资产为主，插图量约40-100张 |
| **Audio Needs** | Moderate — 环境音+氛围音乐，关键场景有短配音 |
| **Networking** | 无 |
| **Content Volume** | 4章，60-100个记忆碎片，80-100张插图，4-5种结局/章，3个隐藏结局 |
| **Procedural Systems** | 关联网络对每个碎片使用情感标签权重来生成关联，非纯随机——是"结构化随机" |

---

## Risks and Open Questions

### Design Risks
- **叙事碎片化**: 网状关联能否始终产生有情感冲击力的叙事弧？需要加入"情感节奏控制"——限制连续同情绪碎片的数量
- **选择疲劳**: 过多的记忆选择可能导致玩家感到决策负担，需要在"有意义的选项"和"节奏流畅"之间平衡

### Technical Risks
- **关联网络复杂度**: 每个碎片需要情感标签、关联条件、选择权重——数据结构设计需要仔细规划
- **跨章节状态追踪**: 隐藏结局需要跨章节触发——需要持久化的状态管理系统

### Market Risks
- **叙事独立游戏竞争激烈**: 每年大量叙事独立游戏发布——需要独特视觉风格和隐藏结局作为差异化
- **受众规模有限**: 纯叙事无战斗的游戏有忠实但有限的受众——定价和预期销量需合理

### Scope Risks
- **插图资产量**: 单人完成40-100张高质量插图是最大工作量——考虑可变体策略减少新图数量
- **叙事文本量**: 多结局+多路径意味着故事文本量大——建议先写MVP的2章，控制文本膨胀

### Open Questions
- 关联网络的"情感节奏控制"算法具体怎么设计？（需原型验证）
- 手绘插图的艺术风格最终方向？（需 /art-bible 确定）
- 每章15-25个碎片是否过多/过少？（需MVP测试调整）

---

## MVP Definition

**Core hypothesis**: 玩家能够在网状关联的记忆碎片中，通过选择改变记忆内容，获得有情感冲击力且每次不同的叙事体验。

**Required for MVP**:
1. 2个完整章节（童年 + 青年），每章12-18个记忆碎片
2. 网状关联系统 — 记忆碎片通过情感标签关联，玩家的选择影响后续碎片
3. 每章至少2种主线结局变体，1个隐藏结局（需跨章触发）
4. 手绘插画视觉风格 + 基础微动画（至少物件发光/交互提示）
5. 章节解锁系统 + 跨章节状态追踪

**Explicitly NOT in MVP** (defer to later):
- 第3-4章（中年 + 暮年）
- 剩余2个隐藏结局
- 配音（留关键场景做短配音即可）
- Steam 成就集成

### Scope Tiers (if budget/time shrinks)

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 2章，~30碎片，~40插图，1个隐藏结局 | 核心关联系统 + 基础动画 | 2-3个月 |
| **Vertical Slice** | 2章完整打磨 + 第3章框架 | 关联系统优化 + 完整动画 | 3-4个月 |
| **Full Vision** | 4章完整 + 3隐藏结局，~80插图 | 全部系统 + 短配音 + 完整音效 | 4-6个月 |
| **Stretch** | 4章 + 配角"碎片"章节 + Steam成就 | 全套视觉特效 + 社区功能 | 6-8个月 |

---

## Visual Identity Anchor

- **Visual direction name**: 画卷中的呼吸
- **One-line visual rule**: 每一帧画面都应是一幅可以挂上墙的画——留白、意境、未完成的美
- **Supporting visual principles**:
  1. **静中有动** — 画面主体保持静态绘画感，但总有至少一个元素在微动（风、水纹、尘埃在光中）
     *Design test*: 如果一个场景停留10秒没有任何动画，它不完整
  2. **颜色即是情绪** — 不同人生阶段和记忆类型用不同的色调体系（童年暖色偏黄，青年饱和但偏冷，暮年褪色灰调）
     *Design test*: 切换章节时，色调的变化应该比UI提示更早告诉玩家"你进入了不同的人生阶段"
  3. **留白即是空间** — 不是所有东西都要画出来。重要的记忆物件精致刻画，其余部分用笔触暗示
     *Design test*: 玩家能否在3秒内找到画面中可交互的物件？如果能，说明留白和重点的对比是对的
- **Color philosophy**: 每一章有一个主色调，情绪越低沉的记忆碎片饱和度越低。隐藏结局的记忆画面使用与主线不同的色调——玩家看到颜色就隐约知道"这里不一样"

---

## Next Steps

- [ ] Run `/setup-engine` to configure Unity and populate version-aware reference docs
- [ ] Run `/art-bible` to create the visual identity specification — do this BEFORE writing GDDs
- [ ] Use `/design-review design/gdd/game-concept.md` to validate concept completeness
- [ ] Decompose the concept into individual systems with `/map-systems`
- [ ] Author per-system GDDs with `/design-system`
- [ ] Plan the technical architecture with `/create-architecture`
- [ ] Record key architectural decisions with `/architecture-decision (×N)`
- [ ] Validate readiness with `/gate-check`
- [ ] Prototype the riskiest system with `/prototype [core-mechanic]`
- [ ] Run `/playtest-report` after the prototype
- [ ] Plan the first sprint with `/sprint-plan new`
