# ADR-0005: 输入系统架构 — Input System 包 + Action Map 互斥门控

## Status

Accepted

## Date

2026-05-12

## Last Verified

2026-05-12

## Decision Makers

User + Claude Code (technical-director via /create-architecture)

## Summary

游戏需要键盘/鼠标输入，需支持 UI 导航和 Gameplay 交互两个模式，互斥切换。决定使用 Unity New Input System 包 + `InputActionAsset` 生成 C# 包装类 + 两个 Action Map (Gameplay/UI) 互斥门控 + `PlayerPrefs` 键位重绑定持久化。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Input |
| **Knowledge Risk** | HIGH — 整个 Input System 包 API 在 LLM 知识截止后（Legacy Input Manager 已弃用） |
| **References Consulted** | `VERSION.md`, `breaking-changes.md`, `deprecated-apis.md`, `modules/input.md` |
| **Post-Cutoff APIs Used** | `InputActionAsset`, `PlayerInput`, `PerformInteractiveRebinding()`, `SaveBindingOverridesAsJson()` |
| **Verification Required** | `PerformInteractiveRebinding` 在 IL2CPP 中需验证；`InputAction.CallbackContext` 结构无变化 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0006 (UI 框架 — 键盘导航依赖 Input System) |
| **Blocks** | InteractionManager + UIPanelStack Epic |
| **Ordering Note** | 在 ADR-0006 之前创建（UI 框架消费输入） |

## Context

### Problem Statement

回响 (Echoes) 是 2D 叙事探索游戏，输入需求分两种：
- **Gameplay**: 鼠标悬停/点击/拖拽画卷上的交互对象
- **UI**: 键盘导航菜单（方向键+确认+取消）、Tab 切换焦点

两种模式必须互斥 —— 菜单打开时游戏不响应点击，反之亦然。Legacy Input Manager 已被 Unity 标记弃用，必须使用新 Input System。

### Constraints

- 仅支持键盘+鼠标（PC Steam/Epic）
- 不支持触摸/手柄（技术偏好中注明）
- 键位重绑定可保存（PlayerPrefs）

### Requirements

- 两个 Action Map 互斥切换
- 鼠标位置每帧可查询（`Vector2 Point`）
- UI 键盘导航：Navigate, Confirm, Cancel, TabNext, TabPrevious
- 设备热插拔检测（OnGamepadConnectionChanged）
- 转场中可切到 Inactive（无输入响应）

## Decision

**使用 Unity New Input System 包 + 生成 C# 包装类 + 两个 Action Map 互斥门控。**

### Action Map 结构

```
PlayerControls (InputActionAsset)
├─ Gameplay Map
│  ├─ Point        (Vector2)  ← Mouse.position
│  ├─ Click        (Button)   ← Mouse.leftButton
│  ├─ Scroll       (Vector2)  ← Mouse.scroll
│  └─ RightClick   (Button)   ← Mouse.rightButton
│
└─ UI Map
   ├─ Navigate     (Vector2)  ← WASD / ArrowKeys
   ├─ Confirm      (Button)   ← Enter / Space
   ├─ Cancel       (Button)   ← Escape / Backspace
   ├─ TabNext      (Button)   ← Tab
   └─ TabPrevious  (Button)   ← Shift+Tab
```

### 模式切换（互斥）

```csharp
public void SwitchToGameplayMode()
{
    _uiMap.Disable();
    _gameplayMap.Enable();
}

public void SwitchToUIMode()
{
    _gameplayMap.Disable();
    _uiMap.Enable();
}

public void SwitchToInactive()
{
    _gameplayMap.Disable();
    _uiMap.Disable();
}
```

### Architecture Diagram

```
┌─────────────────────────────────────────┐
│           PlayerControls                 │
│         (InputActionAsset)              │
│                                         │
│  ┌──────────────┐  ┌──────────────┐    │
│  │ Gameplay Map  │  │   UI Map     │    │
│  │ (鼠标交互)    │  │ (键盘导航)   │    │
│  └──────┬───────┘  └──────┬───────┘    │
│         │                 │             │
│         └────┬────────────┘             │
│              │ 互斥切换                  │
└──────────────┼──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│           IInputManager                  │
│                                         │
│  SwitchToGameplayMode()                 │
│  SwitchToUIMode()                       │
│  SwitchToInactive()                     │
│                                         │
│  Properties: Point, ClickPressed,       │
│              ClickReleased, ScrollDelta │
│                                         │
│  HoverDetector:                         │
│    Physics2D.OverlapPoint(Point)        │
│    → OnHoverEnter/OnHoverExit          │
└─────────────────────────────────────────┘
```

### Key Interfaces

```csharp
public interface IInputManager
{
    void SwitchToGameplayMode();
    void SwitchToUIMode();
    void SwitchToInactive();

    Vector2 Point { get; }
    bool ClickPressed { get; }
    bool ClickReleased { get; }
    Vector2 ScrollDelta { get; }

    // static event Action<bool> OnGamepadConnectionChanged;
}
```

### Implementation Guidelines

1. `InputActionAsset` 在 `Resources/` 或 Addressables 中加载
2. 键位重绑定使用 `PerformInteractiveRebinding()` + `SaveBindingOverridesAsJson()` 持久化
3. HoverDetector 每帧一次 `Physics2D.OverlapPoint`（非分配版本）
4. UIPanelStack 根据栈状态自动切换 Action Map
5. 转场中 SceneManager 调用 `SwitchToInactive()`

## Alternatives Considered

### Alternative 1: Legacy Input Manager (`Input.GetMouseButton`, `Input.mousePosition`)

- **Description**: 使用 Unity 传统 Input API
- **Pros**: 简单，文档多，代码短
- **Cons**: Unity 官方已弃用（deprecated）；不支持 Action Map 概念（需手动管理输入模式）；不支持运行时键位重绑定
- **Rejection Reason**: 已弃用 API，不符合 Unity 6.3 LTS 最佳实践

### Alternative 2: Unity Input System + `PlayerInput` 组件

- **Description**: 使用 `PlayerInput` MonoBehaviour 自动管理 Action Map 和事件广播
- **Pros**: Inspector 可配置 SendMessage/BroadcastMessage 行为
- **Cons**: `PlayerInput` 使用 `SendMessage` 或 `UnityEvent`（有 GC 分配）；与手动控制 Action Map 相比不够灵活
- **Rejection Reason**: `SendMessage` 有性能开销且不类型安全。手动控制 Action Map 更干净

## Consequences

### Positive

- 互斥门控保证输入不冲突
- 运行时键位重绑定
- 设备热插拔检测
- 新 Input System 是官方推荐路径

### Negative

- Input System 包是外部依赖（版本升级有风险）
- 比 Legacy Input Manager 代码量大
- 整个 API 在 LLM 知识截止后（需完全依赖 engine-reference docs）

### Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Input System 包升级 breaking change | Low | Medium | 锁定包版本；升级前完整测试 |
| `PerformInteractiveRebinding` IL2CPP 兼容性 | Low | Medium | Pre-Production IL2CPP 验证 |
| Action Map 切换竞态 | Low | Medium | 互斥切换逻辑简单，单元测试覆盖 |

## Performance Implications

| Metric | Value |
|--------|-------|
| CPU (InputActionAsset 状态更新) | ~0.05ms/frame |
| CPU (HoverDetector OverlapPoint) | ~0.1ms/frame |
| Memory (InputActionAsset) | ~2-5KB |
| GC Allocation | 0 (启用后不分配) |

## Migration Plan

新建项目，无迁移需求。从 Legacy Input 迁移到 Input System 的步骤如需要参见 Unity 官方迁移指南。

## Validation Criteria

- [ ] Gameplay Map 启用时 UI Map 不响应（反之亦然）
- [ ] Inactive 状态下无任何输入响应
- [ ] 键位重绑定持久化（重启后保持）
- [ ] HoverDetector 正确检测交互对象悬停/离开

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `input-system.md` (#1) | 输入系统 | 两种输入模式（Gameplay/UI） | 两个 Action Map 互斥切换 |
| `input-system.md` (#1) | 输入系统 | 鼠标悬停检测 | HoverDetector + Physics2D.OverlapPoint |
| `input-system.md` (#1) | 输入系统 | 键位重绑定持久化 | PlayerPrefs + SaveBindingOverridesAsJson |
| `ui-framework.md` (#5) | UI 框架 | 键盘导航支持 | UI Map: Navigate/Confirm/Cancel/TabNext/TabPrevious |
| `scroll-interaction-system.md` (#11) | 画卷交互 | 点击/拖拽/悬停输入 | Gameplay Map: Point/Click/Scroll |
| `scene-management.md` (#6) | 场景管理 | 转场中禁用输入 | SwitchToInactive() |

## Related

- ADR-0006 — UI 框架使用 UI Map 进行键盘导航
- `docs/engine-reference/unity/modules/input.md` — Input System API 参考
- `docs/architecture/architecture.md` §4.2 — IInputManager API 边界
