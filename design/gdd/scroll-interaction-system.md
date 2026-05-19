# 记忆画卷交互系统 (Scroll Interaction System)

> **Status**: Designed (pending review)
> **Author**: 用户 + game-designer + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 1 (选择即重写) — 直接支撑——交互是"触碰记忆"的物理实现

## Overview

记忆画卷交互系统是《回响》中玩家与记忆世界之间的"手"。它将输入系统中的鼠标位置和点击转化为对记忆画卷中物件的触碰、拖拽和悬停——检测玩家光标下是什么物件、玩家的操作类型是什么、触发的交互结果是什么。它连接输入 (#1) 与数据模型 (#8)——取碎片的交互物件定义、执行交互、触发微动画 (#9)、呈现选择、并调用内容变化 (#12)。

在技术层面，它是一个集中式的交互管理器：每帧通过 Physics2D.OverlapPoint 检测鼠标下的物件，管理悬停/离开/点击/拖拽四个交互事件，根据物件的 InteractionType 和 InteractionResult 定义调度后续行为。它不渲染物件发光（那是微动画 #9 的职责），不执行内容变化（那是变化追踪 #12 的职责），不管理选择 UI（那是 HUD #17 的职责）——它只负责"检测触碰 → 分发交互结果"这一核心循环。

## Player Fantasy

记忆画卷交互系统是"触碰记忆"的物理实现——玩家伸出的手、触到的墨迹、拖开的画面。当你把鼠标光标移到画卷中的一封信上、光标自然地变成一只半透明的手（不是箭头、不是十字准星），你就知道——这段记忆可以碰。当你拖开画面表层的一角、底下的东西露出来——你不觉得这是"UI操作"，你觉得这是你亲手揭开的，这是你**在记忆上留下了指纹**。

三种触碰方式对应三种与记忆的关系：点击是"我选择面对这段记忆"；拖拽是"我亲手揭开被掩盖的东西"；悬停是"我想碰，但我在犹豫"。交互系统不催促——没有时间限制、没有快速点击的奖励、没有"错误操作"。游魂的手永远不急。

## Detailed Design

### Core Rules

**规则 1 — 集中式交互检测：InteractionManager**

```
InteractionManager (MonoBehaviour, Game 场景持久)
│
├── Update():
│   ├── 检查 Action Map 是否为 Gameplay → 否则跳过
│   ├── Vector2 mousePos = InputSystem.Get<Vector2>("Point")
│   ├── Collider2D hit = Physics2D.OverlapPoint(mousePos, layerMask: Interactable)
│   ├── 对比上一帧的 hit 对象:
│   │   ├── 不同对象 → OnHoverExit(oldObj) + OnHoverEnter(newObj)
│   │   └── 相同对象 → OnHoverStay(currentObj)
│   ├── 处理点击 (InputSystem.GetButtonDown("Click")):
│   │   └── 若悬停物件存在 → OnInteract(currentObj)
│   └── 处理拖拽 (InputSystem.GetButton("Click") + mouseDelta > threshold):
│       └── 若物件 InteractionType = Drag → OnDrag(currentObj, delta)
```

- 单个 OverlapPoint per frame——所有交互物件在 `Interactable` 物理层
- 碰撞区域使用 BoxCollider2D / CircleCollider2D，位置来自 MemoryFragment.InteractiveObject 的 HitboxCenter + HitboxSize
- 不使用 Unity EventSystem / Raycaster——这是一个纯 2D 物理检测路径

**规则 1.1 — InteractionManager 公共事件 API**

InteractionManager 通过 10 个公共静态 C# 事件对外广播所有交互行为，供交互反馈系统 (#18) 订阅：

```csharp
// InteractionManager 公共事件
public static event Action<string> OnHoverEnter;      // 参数: objectId (string)
public static event Action<string> OnHoverExit;       // 参数: objectId (string)
public static event Action<InteractiveObject> OnInteract;
public static event Action<InteractiveObject> OnDragStart;
public static event Action<InteractiveObject> OnDragComplete;
public static event Action<InteractiveObject> OnDragCancel;
public static event Action<string> OnChoiceSelected;
public static event Action<string> OnChoiceHover;
public static event Action<GameObject> OnRevealObject;
public static event Action<TextContent> OnShowText;
```

| 事件 | 触发时机 | 参数 | 订阅者 |
|------|---------|------|--------|
| `OnHoverEnter` | 光标进入物件碰撞区 | objectId (string) | #18 |
| `OnHoverExit` | 光标离开物件碰撞区 | objectId (string) | #18 |
| `OnInteract` | 玩家点击 Touch 物件或拖拽完成 | InteractiveObject | #18 |
| `OnDragStart` | 玩家开始拖拽 (移动 >5px) | InteractiveObject | #18 |
| `OnDragComplete` | 拖拽超过阈值 (30px) 释放 | InteractiveObject | #18 |
| `OnDragCancel` | 拖拽不足阈值释放 | InteractiveObject | #18 |
| `OnChoiceSelected` | 玩家在选择面板确认选项 | choiceId (string) | #18 |
| `OnChoiceHover` | 光标悬停在选项上 | choiceId (string) | #18 |
| `OnRevealObject` | RevealObject 型交互触发 | 被揭示的 GameObject | #18 |
| `OnShowText` | ShowText 型交互触发 | TextContent | #18 |

各事件的触发嵌入在规则 1 的 Update()、规则 3 的 OnInteract()、规则 4 的 OnDrag() 和规则 8 的选择面板流程中——此处仅定义接口签名。事件在交互逻辑执行完毕后触发（非事先触发）——确保订阅者收到的参数已是最新状态。

**规则 2 — 四种交互类型处理**

| InteractionType | 触发方式 | 反馈 | 典型结果 |
|-----------------|---------|------|---------|
| `Touch` | 鼠标左键点击 | L2→L3 发光 (微动画 #9) | PlayAnimation / ShowText / PresentChoice / RevealObject |
| `Drag` | 鼠标按住 + 移动 ≥ 阈值(5px) | 物件跟随鼠标 + L3 内光 | 拖拽完成后触发 RevealObject 或 PlayAnimation |
| `Hover` | 光标悬停 ≥ 0.5s | L1→L2 脉动 (微动画 #9) | ShowText（悬停提示文本） |
| `Examine` | 点击 + 进入放大查看模式 | 物件放大到画面中央 | 详细查看后按 Escape/Cancel 退出 |

**规则 3 — 交互结果分发（InteractionResult）**

```
OnInteract(InteractiveObject obj):
  读取 obj.OnInteract (InteractionResult):
  
  ├── ResultType = PlayAnimation:
  │     → MicroAnimationManager.PlayTriggered(obj.OnInteract.AnimationId)
  │
  ├── ResultType = ShowText:
  │     → HUD.ShowFragmentText(obj.OnInteract.TextContent)
  │     → (文本显示在画面上的指定位置，不打断交互)
  │
  ├── ResultType = PresentChoice:
  │     → ChoiceGroup group = fragment.GetChoiceGroup(obj.OnInteract.ChoiceGroupId)
  │     → InputSystem.SwitchToUIMode()  // 切换到 UI Action Map
  │     → HUD.ShowChoicePanel(group)    // HUD 展示选择面板
  │     → 等待玩家选择...
  │     → 玩家选择后:
  │         → ChangeTracker.ApplyChanges(chosenOption.ContentChanges)
  │         → InputSystem.SwitchToGameplayMode()
  │         → HUD.HideChoicePanel()
  │
  ├── ResultType = TransitionToFragment:
  │     → SceneManager.TransitionToFragmentAsync(chapterKey, obj.OnInteract.TargetFragmentId)
  │
  └── ResultType = RevealObject:
        → 显示之前隐藏的物件 (obj.OnInteract.TargetObjectId)
        → MicroAnimationManager.PlayTriggered("object_appear")
```

**规则 4 — 拖拽交互的特殊处理**

Drag 类型是 Pillar 1（选择即重写）在物理操作层面的直接实现：

```
OnDrag(InteractiveObject obj, Vector2 delta):
  1. obj.transform.position += delta (物件跟随鼠标)
  2. 显示拖拽轨迹——物件原始位置到当前位置之间出现淡墨色拖痕
  3. 若 delta.magnitude > dragThreshold (默认 30px):
     → 拖拽完成——触发 obj.OnInteract
     → 物件停在最终位置，拖痕在 1.0s 内淡出
  4. 若玩家在达到阈值前释放鼠标:
     → 物件弹回原位 (spring-back 动画, 0.3s EaseOutCubic)
```

- 每个碎片同一时间只能有 1 个拖拽进行中
- 拖拽过程中该物件以外的所有交互物件进入非悬停状态
- 拖拽阈值 30px 是经过 Pillar 4 设计测试的值——"3 秒内找到交互物件"：物件在阈值前的微小拖动已足够揭示它

**规则 5 — 交互优先级与状态互斥**

| 状态 | 允许的交互 | 阻断 |
|------|-----------|------|
| **正常交互** | 悬停 → 点击 / 拖拽 / 悬停等待 | — |
| **拖拽进行中** | 仅当前拖拽 | 点击、悬停其他物件、碎片切换 |
| **选择面板展示中** | 仅 ChoiceGroup 选项按钮 | 所有画面物件交互、碎片切换 |
| **文本展示中** | 正常交互 | 不阻断——文本是短暂的浮层 |
| **放大查看中 (Examine)** | 仅 Cancel 退出 | 所有其他交互 |
| **场景过渡中** | 无 | 全部交互 |

**规则 6 — 物件状态管理**

物件的 `DefaultState` 字段（数据模型 #8 类别 3）决定初始交互行为：

| State | 行为 | 视觉 |
|------|------|------|
| `Active` | 可交互——正常悬停检测和点击响应 | L1 朱砂墨点可见 |
| `Hidden` | 不可交互——碰撞体禁用。RevealObject 可将其变为 Active | 物件和墨点均不可见 |
| `Disabled` | 不可交互——碰撞体存在但不响应。视觉上"灰掉" | 物件可见但无墨点，色调降低 30% |

状态变化由记忆变化追踪 (#12) 的 ContentChange.SetObjectState 触发。

**规则 7 — 悬停提示文本**

Hover 类型物件在光标悬停 0.5s 后展示提示文本（`OnInteract.TextContent`）：

- 文本出现在光标上方 20px 处——半透明墨色文字
- 光标离开或点击后文本消失
- 提示文本是"手写体"渲染——由 UI 框架 (#5) 的 TextMeshPro 字体设置
- 提示文本不算"UI"——它在画面层渲染，不走 UI Document

**规则 8 — 选择面板的呈现方式**

当 ResultType = PresentChoice 时，HUD (#17) 展示选择面板：

- 选择面板不遮挡锚点物件：面板出现在物件旁边（智能定位——优先右侧，空间不足时下方）
- 选项以"手写墨迹"风格渲染——每个选项是一行手写体文字，前面有朱砂墨点标记
- 当前悬停的选项墨点进入 L2 脉动
- 点击选项 → 确认选择 → 触发 ContentChanges → 面板淡出 (0.3s)
- 玩家可以按 Escape 关闭选择面板——等于"不做选择"，该物件的 DefaultState 不变

**规则 9 — 过渡期间的交互保护**

场景管理器 (#6) 在过渡期间将 Action Map 切换为 Inactive：
- 碎片过渡（FadeOut → 内容加载 → FadeIn）：全程无交互
- FadeIn 完成后 → OnFragmentTransitioned → InteractionManager 读取新碎片的 InteractiveObjects 列表 → 重建碰撞体 → 启用悬停检测

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Idle** | 无碎片活跃。等待 OnFragmentTransitioned | Game 场景加载完成 |
| **Active** | 正常交互。悬停检测运行中 | 碎片切换完成 |
| **Dragging** | 玩家正在拖拽一个物件 | OnDrag 开始 |
| **ChoicePresenting** | 选择面板展示中。Gameplay 输入暂停 | PresentChoice 触发 |
| **Examining** | 放大查看模式 | Examine 交互触发 |
| **Blocked** | 过渡中、文本展示中的短暂阶段——交互暂停 | 过渡/文本触发 |

### Interactions with Other Systems

| 方向 | 系统 | 接口 | 说明 |
|------|------|------|------|
| 上游 | **输入 (#1)** | Point (Vector2), Click (Button) | 鼠标位置和点击 |
| 上游 | **场景管理 (#6)** | OnFragmentTransitioned | 碎片切换时获取新交互物件列表 |
| 上游 | **记忆碎片数据模型 (#8)** | InteractiveObjects[], ChoiceGroups[], InteractionResult | 交互物件的全部定义 |
| 上游 | **微动画 (#9)** | L2/L3 发光、触发动画播放 | 交互反馈的视觉层 |
| 下游 | **记忆变化追踪 (#12)** | ApplyChanges(ContentChange[]) | 选择完成后触发内容变化 |
| 下游 | **游戏内HUD (#17)** | ShowChoicePanel, ShowFragmentText | 选择面板和文本浮层 |
| 下游 | **存档系统 (#7)** | 选择事件 → 自动存档触发判断 | 关键选择后可能触发自动存档 |

## Formulas

本系统不含自定义数学公式。交互检测使用 Unity 2D 物理——OverlapPoint。

**交互阈值常量**:

| 参数 | 值 | 说明 |
|------|-----|------|
| 拖拽触发阈值 | 5px | 鼠标移动超过此值触发 Drag（区分点击和拖拽） |
| 拖拽完成阈值 | 30px | 拖拽超过此值视为完成——触发 OnInteract |
| 悬停提示延迟 | 0.5s | Hover 类型物件展示提示文本前的延迟 |
| 悬停检测频率 | 每帧 (60fps) | 在 Update 中轮询 |

## Edge Cases

- **如果玩家点击画面空白区域（无物件）**: 不触发任何交互。不视为"错误"。如果当前有文本浮层展示中——关闭浮层。

- **如果玩家在拖拽过程中拖到画面边缘**: 物件继续跟随鼠标——不夹紧。画面边缘外不可见但不影响交互。释放鼠标后物件弹回原位。

- **如果两个物件的碰撞区域重叠**: Physics2D.OverlapPoint 返回 Z 轴最上层（SortOrder 最高）的物件。如果 SortOrder 相同——返回 Collider 实例 ID 较小的（确定性）。

- **如果 ChoiceGroup 的 MaxSelections=1 且只有一个可用选项**: 跳过选择面板——直接触发该选项的 ContentChanges。等同于"非选择式的揭示"（数据模型 Edge Cases 中定义的 1 选项 ChoiceGroup）。

- **如果玩家在 Examine 模式中切换到下一个碎片**: Examine 模式自动退出——TransitionToFragmentAsync 的过渡流程中，InteractionManager 进入 Blocked 状态。

- **如果快速连续点击同一物件**: 第一次点击触发 OnInteract。后续点击在交互结果完成前被忽略。`_lastInteractionTime` 与当前时间的差值 < 0.3s → 跳过。

- **如果碎片有 0 个 InteractiveObjects**: 合法——纯观看碎片。InteractionManager 的悬停检测在空列表上无操作。不显示任何墨点。

## Dependencies

**硬依赖**:

| 系统 | 性质 | 接口 |
|------|------|------|
| **输入 (#1)** | 硬依赖 | Point (鼠标位置), Click (左键) |
| **场景管理 (#6)** | 硬依赖 | OnFragmentTransitioned 事件 |
| **记忆碎片数据模型 (#8)** | 硬依赖 | InteractiveObjects, ChoiceGroups, InteractionResult |

**软依赖**:

| 系统 | 性质 | 接口 |
|------|------|------|
| **微动画 (#9)** | 软依赖 | L2/L3 发光、触发动画。若 #9 未就绪——交互仍正常执行，仅无视觉反馈 |
| **游戏内HUD (#17)** | 软依赖 | ShowChoicePanel, ShowFragmentText。若 #17 未就绪——PresentChoice 和 ShowText 无效果 |

**下游**: 记忆变化追踪 (#12)、存档系统 (#7)

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| 拖拽触发阈值 | 5px | 3–10px | 区分点击和拖拽的最小移动距离 |
| 拖拽完成阈值 | 30px | 20–50px | 拖拽"完成"需要的距离 |
| 悬停提示延迟 | 0.5s | 0.3–1.0s | Hover 物件展示提示前的等待 |
| 点击防抖间隔 | 0.3s | 0.2–0.5s | 防止双击触发 |
| 拖拽弹回时长 | 0.3s | 0.2–0.5s | 未完成拖拽的回弹动画时长 |
| 选择面板淡出时长 | 0.3s | 0.2–0.5s | 选择后面板消失的过渡 |

## Visual/Audio Requirements

本系统自身不渲染视觉——交互反馈的视觉层完全由微动画 (#9) 负责（L1 墨点、L2 脉动、L3 内光、触发动画）。拖拽拖痕的墨色轨迹由本系统请求微动画渲染——属于微动画的 TriggeredAnimDef "drag_trail"。

本系统不产生音频——触碰音效、选择确认音效由音频系统 (#3) 和交互反馈系统 (#18) 负责。

## UI Requirements

本系统不包含 UI。选择面板和文本浮层由游戏内 HUD (#17) 管理——本系统只发送 `ShowChoicePanel` 和 `ShowFragmentText` 请求。

## Acceptance Criteria

- **GIVEN** 玩家进入碎片 A（3 个 InteractiveObject），**WHEN** OnFragmentTransitioned 触发，**THEN** InteractionManager 读取 3 个物件定义，为每个创建 BoxCollider2D。光标移到物件上时检测到悬停。

- **GIVEN** 光标悬停在物件上，**WHEN** 物件 InteractionType = Touch，**THEN** 光标样式变为手型指针。微动画 #9 的 L2 脉动启动。点击后触发 OnInteract。

- **GIVEN** 玩家点击一个 Touch 物件且 ResultType = PresentChoice，**WHEN** ChoiceGroup 有 2 个选项，**THEN** HUD 展示选择面板在物件旁边。Action Map 切换到 UI。点击一个选项 → ContentChanges 被应用到变化追踪 (#12) → 面板消失 → Action Map 切回 Gameplay。

- **GIVEN** 物件 InteractionType = Drag，**WHEN** 玩家按住并拖动超过 5px，**THEN** 物件跟随鼠标移动，拖痕显示。拖动超过 30px 后释放 → 拖拽完成，触发 OnInteract。拖动不足 30px 释放 → 物件弹回原位。

- **GIVEN** 选择面板展示中，**WHEN** 玩家按 Escape，**THEN** 面板关闭，Action Map 切回 Gameplay。不触发任何 ContentChanges——"不做选择"是有效的交互结果。

- **GIVEN** 碎片过渡正在进行（FadeOut/FadeIn），**WHEN** 玩家点击画面，**THEN** 点击被忽略——Action Map 为 Inactive，InteractionManager 不处理输入。

- **GIVEN** 碎片 B 有 0 个 InteractiveObject，**WHEN** 玩家进入该碎片，**THEN** InteractionManager 的悬停检测在空列表上无操作。光标保持默认样式。不显示任何墨点或提示。

- **GIVEN** 一个物件 DefaultState = Hidden，**WHEN** 碎片加载，**THEN** 该物件的碰撞体被禁用。RevealObject 类型的 OnInteract 可以启用它——启用后物件出现在画面上。

## Open Questions

- **光标样式系统**: 不同 InteractionType 是否需要不同光标样式（手型/放大镜/拖拽箭头）？光标样式由哪个系统管理——本系统、UI 框架 (#5)、还是微动画 (#9)？建议由 UI 框架统一管理光标样式表，本系统通过 CursorStyle 枚举请求切换。（Owner: ui-programmer）

- **多点触控/触摸屏**: MVP 仅支持 PC 键鼠。如果未来移植到触屏设备（平板/手机），Touch/Drag/Hover 三种交互如何映射到手指操作？Hover 在触屏上没有等价操作——是否用"长按"替代？（Owner: game-designer, Full Vision 阶段评估）

- **Examine 模式的视觉设计**: 放大查看模式下物件如何呈现？是物件 Sprite 放大填充画面中央，还是展示物件的"细节版本"插图？需要 art-director 提供视觉参考。（Owner: art-director）
