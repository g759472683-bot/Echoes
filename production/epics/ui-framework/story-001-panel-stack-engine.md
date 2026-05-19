# Story 001: UIPanelStack 核心引擎 + 输入门控

> **Epic**: UI 框架 (UIPanelStack)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/ui-framework.md`
**Requirement**: `TR-ui-framework-001`, `TR-ui-framework-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架 — UI Toolkit 面板栈 + MVVM 数据绑定, ADR-0001: 事件总线架构
**ADR Decision Summary**: UI Toolkit 独占 (UXML + USS)；LIFO 面板栈 (max depth 10)；自动输入门控——栈非空 → UI Action Map，栈空 → Gameplay Action Map

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: UI Toolkit runtime 是 Unity 6 新特性（文档较少）；USS transition 仅支持 opacity/translate/scale/rotate

**Control Manifest Rules (Foundation Layer)**:
- Required: UI Toolkit (UIDocument + VisualElement) for all runtime UI — source: ADR-0006
- Required: LIFO panel stack (max depth 10) — auto input gating — source: ADR-0006
- Forbidden: Never use UGUI Canvas — source: ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/ui-framework.md`, scoped to this story:*

- [ ] GIVEN 游戏处于 Gameplay 状态，WHEN 玩家按下 Escape，THEN 暂停面板 PushPanel 到栈顶，Gameplay Action Map 禁用，UI Action Map 启用
- [ ] GIVEN 暂停面板打开，WHEN 玩家再次按 Escape，THEN PopPanel 关闭暂停面板，切换到 Gameplay Action Map
- [ ] GIVEN 两个面板在栈中（暂停 → 设置），WHEN 玩家按 Escape 两次，THEN 设置面板先关闭（回到暂停），暂停面板再关闭（回到 Gameplay）。焦点正确恢复到前一个面板
- [ ] GIVEN 面板栈为空，WHEN 检查 Action Map 状态，THEN Gameplay Action Map 激活，UI Action Map 禁用
- [ ] GIVEN UXML 文件缺失，WHEN 尝试 PushPanel，THEN Development Build 中记录错误并显示包含文件路径的错误信息。Release Build 中显示通用错误面板
- [ ] 面板栈深度上限为 10——超过时拒绝 PushPanel 并记录错误

---

## Implementation Notes

*Derived from ADR-0006:*

UIPanelStack 核心:
```csharp
public class UIPanelStack : IUIPanelStack
{
    private readonly Stack<PanelEntry> _stack = new();
    private const int MAX_DEPTH = 10;
    private PanelState _state = PanelState.Empty;
    
    public int StackDepth => _stack.Count;
    public string TopPanelId => _stack.Count > 0 ? _stack.Peek().PanelId : null;
    
    public void PushPanel(string panelId)
    {
        if (_state == PanelState.Transitioning)
        {
            Debug.LogWarning($"[UIPanelStack] Transitioning — PushPanel({panelId}) ignored");
            return;
        }
        
        if (_stack.Count >= MAX_DEPTH)
        {
            Debug.LogError($"[UIPanelStack] Max depth {MAX_DEPTH} reached — PushPanel({panelId}) rejected");
            return;
        }
        
        _state = PanelState.Transitioning;
        
        // 创建面板 VisualElement
        var asset = _panelRegistry[panelId];
        var ve = asset.CloneTree();
        _root.Add(ve);
        
        _stack.Push(new PanelEntry(panelId, ve));
        
        // 自动输入门控
        if (_stack.Count == 1)
            _inputManager.SwitchToUIMode();
        
        _state = PanelState.PanelOpen;
    }
    
    public void PopPanel()
    {
        if (_state == PanelState.Transitioning || _stack.Count == 0) return;
        
        _state = PanelState.Transitioning;
        
        var entry = _stack.Pop();
        _root.Remove(entry.VisualElement);
        
        if (_stack.Count == 0)
        {
            _inputManager.SwitchToGameplayMode();
            _state = PanelState.Empty;
        }
        else
        {
            _state = PanelState.PanelOpen;
        }
    }
}
```

状态机:
| 状态 | 描述 | 条件 |
|------|------|------|
| **Empty** | 无模态 UI（HUD 可见） | Gameplay Action Map 激活 |
| **PanelOpen** | 至少一个面板在栈中 | UI Action Map 激活 |
| **Transitioning** | 面板动画进行中 | 忽略重复 Push/Pop |

面板注册表:
```csharp
private Dictionary<string, VisualTreeAsset> _panelRegistry = new();
// 注册在 Boot 场景初始化时完成
```

UXML 缺失处理:
```csharp
var asset = _panelRegistry.GetValueOrDefault(panelId);
if (asset == null)
{
    #if DEVELOPMENT_BUILD
    Debug.LogError($"[UIPanelStack] UXML not found for panel '{panelId}' — path: Assets/UI/{panelId}.uxml");
    #else
    Debug.LogError($"[UIPanelStack] Panel '{panelId}' failed to load");
    #endif
    _state = _stack.Count > 0 ? PanelState.PanelOpen : PanelState.Empty;
    return;
}
```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: Theme.uss 全局样式 — CSS 变量定义
- Story 003: 面板过渡动画 — fade-in/fade-out CSS 类
- Story 004: 键盘导航 — FocusController + Tab/Arrow 焦点
- 各面板的具体 UXML 和 Controller（MainMenu、Settings、HUD 等）— 由各 UI 子系统负责
- MVVM 数据绑定 — 由具体面板 Controller 自行实现 INotifyBindablePropertyChanged

---

## QA Test Cases

- **AC-1**: PushPanel 基本流程
  - Given: 游戏在 Gameplay 状态（栈空）
  - When: PushPanel("pause-menu")
  - Then: 栈深度 = 1；TopPanelId = "pause-menu"；InputManager 切换到 UI Action Map
  - Edge cases: Transitioning 状态下 PushPanel → 忽略

- **AC-2**: PopPanel 基本流程
  - Given: 暂停面板在栈顶（栈深度 = 1）
  - When: PopPanel()
  - Then: 栈深度 = 0；InputManager 切换到 Gameplay Action Map
  - Edge cases: 栈空时 PopPanel → 无操作

- **AC-3**: 多层 PopPanel
  - Given: 栈 = [暂停菜单, 设置面板]（深度 = 2）
  - When: PopPanel() 两次
  - Then: 第一次 → 设置面板关闭，深度 = 1，焦点回到暂停菜单；第二次 → 暂停菜单关闭，深度 = 0，Gameplay Action Map 激活
  - Edge cases: 快速按 Escape 3 次 → Transitioning 状态下第 3 次被忽略

- **AC-4**: 栈深度上限
  - Given: 栈深度 = 10
  - When: PushPanel("another-panel")
  - Then: 操作被拒绝；Debug.LogError 记录；栈不变
  - Edge cases: 深度 9 时 PushPanel → 成功，深度 = 10

- **AC-5**: UXML 缺失
  - Given: "broken-panel" 的 UXML 文件不存在
  - When: PushPanel("broken-panel")
  - Then: Development Build 中显示包含文件路径的错误日志；Release Build 中显示通用错误
  - Edge cases: 缺失面板后栈状态恢复为 Push 前状态

- **AC-6**: 面板栈为空时 Action Map
  - Given: 所有面板已关闭
  - When: 检查 InputManager.CurrentState
  - Then: CurrentState = Gameplay；Gameplay Action Map 激活；UI Action Map 禁用
  - Edge cases: 初始化时栈即为空 → Gameplay 状态

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/ui-framework/panel-stack_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/ui-framework/panel-stack_test.cs` (27 tests)

---

## Dependencies

- Depends on: input-system Story 001 (InputManager.SwitchToUIMode/SwitchToGameplayMode), ADR-0001 pattern
- Unlocks: ui-framework Story 002 (Theme.uss), Story 003 (面板过渡), Story 004 (键盘导航); MainMenu (#19), InGameHUD (#17)

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 6/6 passing
**Deviations**: None
**Test Evidence**: Logic — `tests/unit/ui-framework/panel-stack_test.cs` (27 tests)
**Code Review**: Skipped (lean mode)
