# ADR-0006: UI 框架 — UI Toolkit 面板栈 + MVVM 数据绑定

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

游戏 UI 包括主菜单（5 个面板）、游戏内 HUD（碎片文本/选项/关联路径）、暂停菜单等。决定使用 UI Toolkit (UIDocument + VisualElement) + LIFO 面板栈 + MVVM 数据绑定 + Theme.uss 全局样式变量。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI |
| **Knowledge Risk** | HIGH — UI Toolkit runtime 是 Unity 6 新特性，LLM 训练数据覆盖极有限 |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `deprecated-apis.md`, `modules/ui.md` |
| **Post-Cutoff APIs Used** | `UIDocument`, `VisualElement`, `INotifyBindablePropertyChanged`, USS `var()` |
| **Verification Required** | MVVM 数据绑定在频繁更新时的性能；`INotifyBindablePropertyChanged` 稳定性 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (UI 事件通信), ADR-0005 (Input System UI Map 键盘导航) |
| **Enables** | ADR-0014 (交互反馈 — UI 面板事件) |
| **Blocks** | MainMenu + InGameHUD Epic |
| **Ordering Note** | 在 ADR-0001, ADR-0005 之后创建 |

## Context

### Problem Statement

游戏需要统一的 UI 框架管理多个面板（标题画面、设置、存档选择、确认对话框、游戏内 HUD）。面板之间有层级关系（对话框 > 设置 > 暂停菜单 > 游戏画面），需要自动管理输入焦点和转场动画。

### Constraints

- 必须在 PC 端运行流畅（60fps 目标）
- 必须支持键盘导航（Tab/方向键/Enter/Escape）
- 面板深度上限 10（防止无限压栈）
- 全局视觉一致性（墨迹风 / 暖色墨水色板）

### Requirements

- LIFO 面板栈管理面板层级
- 自动输入门控：栈非空 → UI Action Map, 栈空 → Gameplay Action Map
- 面板之间支持转场动画（CSS fade-in/fade-out）
- 键盘焦点自动迁移
- 全局主题变量系统

## Decision

**使用 UI Toolkit + LIFO 面板栈 + MVVM 数据绑定 + Theme.uss 全局样式变量。**

### 面板栈架构

```
┌────────────────────────────────┐
│  UIPanelStack (LIFO, max 10)   │
│                                │
│  ┌──────────┐                  │
│  │ Dialog    │ ← 栈顶 (焦点)   │
│  ├──────────┤                  │
│  │ Settings  │                  │
│  ├──────────┤                  │
│  │ Pause     │                  │
│  ├──────────┤                  │
│  │ Game HUD  │ ← 栈底          │
│  └──────────┘                  │
│                                │
│  PushPanel(id)    → 压入       │
│  PopPanel()       → 弹出       │
│  ReplaceTop(id)   → 替换栈顶   │
└────────────────────────────────┘
```

### 输入门控（自动）

```csharp
public void PushPanel(string panelId)
{
    // 创建面板 VisualElement
    // 触发 .fade-in CSS transition
    // 自动: 栈非空 → SwitchToUIMode()
}

public void PopPanel()
{
    // 触发 .fade-out CSS transition (等待 300ms)
    // 移除面板 VisualElement
    // 自动: 栈空 → SwitchToGameplayMode()
}
```

### Theme.uss 全局变量

```css
:root {
    --color-ink-primary: #2C1810;
    --color-ink-secondary: #8B7355;
    --color-ink-vermilion: #C04040;
    --color-paper-warm: #F5E6D3;
    --color-paper-cool: #E8E0D5;
    --font-display: 'handwritten-serif';
    --font-body: 'handwritten-sans';
    --spacing-xs: 4px;
    --spacing-sm: 8px;
    --spacing-md: 16px;
    --spacing-lg: 32px;
    --transition-fast: 150ms;
    --transition-normal: 300ms;
    --transition-slow: 500ms;
}
```

### MVVM 数据绑定

```csharp
// 数据源实现 INotifyBindablePropertyChanged
public class ChapterProgressDataSource : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private int _visitedCount;
    [CreateProperty]
    public int VisitedCount
    {
        get => _visitedCount;
        set { _visitedCount = value; NotifyPropertyChanged(); }
    }
}

// UXML 中绑定
// <Label text="Chapter Progress" binding-path="VisitedCount" />
```

### Architecture Diagram

```
┌──────────────────────────────────────────┐
│             IUIPanelStack                 │
│                                          │
│  PushPanel / PopPanel / ReplaceTop       │
│  TopPanelId / StackDepth                 │
│  Theme.uss (全局样式变量)                  │
│  FocusController (键盘焦点)              │
└──────────────────────────────────────────┘
         │
         ├──► #1 InputManager (Action Map 切换)
         ├──► #4 LocalizationManager (字符串)
         └──► UIDocument VisualElement 树
                    │
                    ▼
┌──────────────────────────────────────────┐
│  面板定义 (各自 Controller 管理)           │
│                                          │
│  #19 MainMenuController                  │
│    ├─ #title-screen                      │
│    ├─ #pause-menu                        │
│    ├─ #settings-panel                    │
│    ├─ #save-load-panel                   │
│    └─ #modal-dialog                      │
│                                          │
│  #17 InGameHUD                           │
│    ├─ #fragment-text-overlay             │
│    ├─ #choice-panel                      │
│    ├─ #association-paths                 │
│    ├─ #chapter-progress                  │
│    └─ #interaction-hint                  │
└──────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface IUIPanelStack
{
    void PushPanel(string panelId);
    void PopPanel();
    void ReplaceTop(string panelId);
    string TopPanelId { get; }
    int StackDepth { get; }
}
```

### Implementation Guidelines

1. 每个面板由各自的 Controller 类管理 VisualElement 树
2. UIPanelStack 只管理栈结构和输入门控，不关心面板内部
3. Theme.uss 在 Boot 场景加载一次，全局生效
4. CSS transition 类 (.fade-in / .fade-out) 预定义在 Theme.uss
5. 面板注册表用 `Dictionary<string, VisualTreeAsset>` 存储 UXML 模板

### MVVM 更新节流策略

`INotifyBindablePropertyChanged` 在每次属性变化时触发完整的 VisualElement
重新测量/布局——HUD 频繁更新场景下可能超过 0.1ms CPU 预算。

节流规则：
```csharp
// 批量更新模式：积累多个属性变化，单帧仅触发一次 layout
private bool _isDirty = false;
public void MarkDirty()
{
    if (!_isDirty)
    {
        _isDirty = true;
        // 利用 Unity schedule 在下个 layout pass 统一处理
        schedule.Execute(() => { _isDirty = false; /* trigger single update */ });
    }
}
```
- HUD 数据源更新频率限制在 10Hz（每 100ms 最多一次 UI 刷新）
- 多个属性变化在同一个 `schedule.Execute` 周期内批处理
- 选择面板等非连续更新的 UI 不适用此限制

### USS Transition 属性限制

Unity 6 UI Toolkit 的 USS `transition` 属性仅支持：
- `opacity` — GPU 加速
- `translate` — GPU 加速
- `scale` — GPU 加速
- `rotate` — GPU 加速

**不支持**通过 CSS transition 动画化 `color`、`width`、`height` 等属性——这些
通过 UI Toolkit 的 layout 引擎在 CPU 端重新计算，并非所有属性都是 "GPU 加速"。
面板的颜色/尺寸过渡需使用代码驱动的逐帧补间（MicroTween 或类似），而非 USS transition。

## Alternatives Considered

### Alternative 1: UGUI (Canvas + GameObject)

- **Description**: 使用 Unity 传统 UGUI 系统（Canvas, Button, Text, Image）
- **Pros**: 成熟稳定，社区资源丰富，LLM 训练数据充分
- **Cons**: Unity 官方推荐 UI Toolkit 用于新项目；不支持 CSS 样式系统；无内置 MVVM 绑定；Canvas rebuild 可能产生 GC
- **Rejection Reason**: UI Toolkit 是 Unity 6 推荐方案，USS 变量系统适合全局主题管理

### Alternative 2: 无面板栈 — 手动管理面板状态

- **Description**: 每个面板自行管理打开/关闭，无统一栈结构
- **Pros**: 灵活性最高
- **Cons**: 容易出现面板重叠；输入焦点管理复杂；Escape 键行为不一致
- **Rejection Reason**: 面板栈是经过验证的 UI 管理模式（移动端/主机端通用）

## Consequences

### Positive

- 全局主题一致（Theme.uss CSS 变量）
- 键盘导航自动管理（FocusController）
- 面板切换有 CSS transition 动画
- MVVM 数据绑定减少样板代码

### Negative

- UI Toolkit runtime 是 Unity 6 新功能（文档较少、社区资源稀缺）
- MVVM 绑定在频繁更新时性能未知（需验证）
- 团队需学习 UXML/USS 语法

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| UI Toolkit runtime 不稳定（崩溃/渲染错误） | Low | High | 关键路径降级到 UGUI；Pre-Production 压力测试 |
| MVVM 绑定在每帧更新时产生 GC | Medium | Medium | HUD 更新频率限制；Profile 验证 |
| USS 变量的浏览器兼容性差 | Low | Low | 仅 Editor 中使用，运行时不变 |
| Panel transition 动画卡顿 | Low | Medium | 使用 GPU 加速 CSS 属性（opacity, transform） |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (面板压栈/弹出) | ~1-2ms (VisualElement 构建) |
| CPU (数据绑定更新) | ~0.1ms (单属性) |
| Memory (Theme.uss) | ~1KB |
| Memory (每个面板 VisualElement 树) | ~10-50KB |
| GC Allocation (面板构建) | ~2-5KB (一次分配，复用) |

## Migration Plan

新建项目，无迁移需求。如需降级到 UGUI：面板栈逻辑可复用（接口不变），替换 UIDocument 为 Canvas。

## Validation Criteria

- [ ] Push → Pop 面板流程正确（栈深度、输入模式切换）
- [ ] 栈深 10 时 PushPanel 抛出异常
- [ ] Escape 键逐层弹出面板（Dialog → Settings → Pause → HUD）
- [ ] CSS fade-in/fade-out 动画流畅（60fps）
- [ ] 键盘焦点在 Push/Pop 后自动迁移到栈顶面板

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `ui-framework.md` (#5) | UI 框架 | 面板栈管理 | LIFO UIPanelStack |
| `ui-framework.md` (#5) | UI 框架 | 全局主题样式 | Theme.uss CSS 变量 |
| `ui-framework.md` (#5) | UI 框架 | 键盘导航 | FocusController + UI Action Map |
| `main-menu.md` (#19) | 主菜单 | 5 个面板层级 | 各自 Controller + UIPanelStack |
| `in-game-hud.md` (#17) | 游戏 HUD | MVVM 数据源 | INotifyBindablePropertyChanged |
| `in-game-hud.md` (#17) | 游戏 HUD | 关联路径视觉分级 | Ink Styles (Strong/Medium/Faint/Trace) |

## Related

- ADR-0001 — 面板事件通信
- ADR-0005 — Input System UI Map 键盘导航
- `docs/engine-reference/unity/modules/ui.md` — UI Toolkit API 参考
- `docs/architecture/architecture.md` §4.2 — IUIPanelStack API 边界
