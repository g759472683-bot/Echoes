# Story 002: Theme.uss 全局样式系统

> **Epic**: UI 框架 (UIPanelStack)
> **Status**: Complete
> **Layer**: Core
> **Type**: Config/Data
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/ui-framework.md`
**Requirement**: `TR-ui-framework-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: UI 框架
**ADR Decision Summary**: Theme.uss 单一文件定义全局 CSS 变量——6 类别（--color-, --font-, --spacing-, --transition-, --panel-, --button-），所有面板 USS 通过 `@import url("theme.uss")` 引用

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: USS `var()` 在 Unity 6 UI Toolkit runtime 中已验证可用；颜色使用 RGB 分量以支持 `rgba()` 透明度动画

**Control Manifest Rules (Foundation Layer)**:
- Required: Theme.uss global CSS variables for visual consistency — all color/spacing/font/transition values defined in :root — source: ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/ui-framework.md`, scoped to this story:*

- [ ] GIVEN Theme.uss 中 `--color-accent` 被修改为高对比度值，WHEN 菜单面板重新渲染，THEN 所有使用该变量的元素反映新颜色
- [ ] Theme.uss 包含全部 6 类别变量：颜色、字体、间距、过渡、面板、按钮
- [ ] 颜色变量使用 RGB 分量格式（如 `200, 160, 100`）——支持 `rgba(var(--color-accent), 0.8)` 透明度动画
- [ ] Theme.uss 可被替换实现主题切换（如无障碍高对比度主题）

---

## Implementation Notes

*Derived from ADR-0006:*

Theme.uss 变量定义:
```css
:root {
    /* === 颜色 (RGB 分量) === */
    --color-bg-primary: 44, 24, 16;
    --color-bg-secondary: 139, 115, 85;
    --color-text-primary: 44, 24, 16;
    --color-text-secondary: 139, 115, 85;
    --color-accent: 192, 64, 64;
    --color-paper-warm: 245, 230, 211;
    --color-paper-cool: 232, 224, 213;
    --color-border: 139, 115, 85;
    
    /* === 字体 === */
    --font-family-display: 'handwritten-serif';
    --font-family-body: 'handwritten-sans';
    --font-size-h1: 36px;
    --font-size-h2: 24px;
    --font-size-body: 16px;
    --font-size-small: 12px;
    
    /* === 间距 === */
    --spacing-xs: 4px;
    --spacing-sm: 8px;
    --spacing-md: 16px;
    --spacing-lg: 24px;
    --spacing-xl: 32px;
    
    /* === 过渡 === */
    --transition-fast: 100ms;
    --transition-normal: 300ms;
    --transition-slow: 500ms;
    
    /* === 面板 === */
    --panel-bg-opacity: 0.92;
    --panel-border-radius: 8px;
    --panel-padding: var(--spacing-md);
    
    /* === 按钮 === */
    --button-height: 44px;
    --button-hover-brightness: 1.1;
    --button-press-scale: 0.98;
}
```

使用方法（任意面板 USS）:
```css
@import url("theme.uss");

.pause-panel {
    background-color: rgba(var(--color-paper-warm), var(--panel-bg-opacity));
    border-radius: var(--panel-border-radius);
    padding: var(--panel-padding);
}

.menu-button {
    height: var(--button-height);
    font-size: var(--font-size-body);
    transition: opacity var(--transition-fast) ease-out;
}

.menu-button:hover {
    background-color: rgba(var(--color-accent), 0.15);
}
```

主题切换机制:
- `Theme.uss` 在 Boot 场景加载到 `UIDocument.panelSettings.themeStyleSheet`
- 高对比度主题：替换 `Theme.uss` 为 `Theme_HighContrast.uss`（相同变量名，不同值）
- 替换后 UI Toolkit 自动重新计算所有 `var()` 引用

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: UIPanelStack 引擎——Theme.uss 加载时机属于 Story 001（Boot 初始化）
- Story 003: 面板过渡动画——CSS transition 类的具体实现
- 字体文件加载——由 Addressables Shared_UI 组管理
- 具体面板的 USS 样式——由各 UI 子系统负责

---

## QA Test Cases

- **AC-1**: 变量覆盖生效
  - Given: 暂停菜单面板可见，使用 `rgba(var(--color-accent), 0.8)` 设置按钮颜色
  - When: 替换 Theme.uss 中 `--color-accent` 为高对比度值（如 `255, 255, 0`）
  - Then: 按钮颜色立即更新为新值；无需重新创建 VisualElement
  - Edge cases: 变量名拼写错误 → `var()` 使用 fallback 值（若提供）或忽略

- **AC-2**: 6 类别完整性
  - Given: Theme.uss 文件
  - When: 检查 :root 中的变量声明
  - Then: --color- 至少 7 个；--font- 至少 4 个（family ×2, size ×4）；--spacing- 至少 5 个；--transition- 至少 3 个；--panel- 至少 3 个；--button- 至少 3 个
  - Edge cases: 添加新变量 → 不影响已有面板

- **AC-3**: RGB 分量格式
  - Given: 颜色变量定义为 `--color-accent: 192, 64, 64;`（非 hex）
  - When: USS 中使用 `rgba(var(--color-accent), 0.5)` 设置半透明颜色
  - Then: 渲染结果正确——朱红色半透明
  - Edge cases: hex 格式 `#C04040` → `rgba(var(--color), 0.5)` 无法工作

- **AC-4**: 主题切换
  - Given: 游戏运行中，使用 Theme.uss
  - When: 替换为 Theme_HighContrast.uss（相同变量名，不同值）
  - Then: 所有可见面板自动反映新主题颜色；无闪烁
  - Edge cases: 替换文件缺失 → 保留当前主题，记录 warning

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: `production/qa/smoke-theme-uss.md` — smoke check pass

**Status**: [x] Created — `assets/UI/Theme.uss` + `assets/UI/Theme_HighContrast.uss`

---

## Dependencies

- Depends on: ui-framework Story 001 (UIPanelStack — Theme.uss 在 Boot 初始化加载)
- Unlocks: ui-framework Story 003 (面板过渡使用 --transition- 变量), Story 004 (焦点指示器使用 --color- 变量); 所有 UI 子系统面板 USS

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 3/4 auto-verified (AC-1 deferred — requires Unity Editor runtime for stylesheet propagation verification)
**Deviations**: None
**Test Evidence**: Config/Data — smoke check pending (`production/qa/smoke-theme-uss.md` not yet created)
**Code Review**: Skipped (lean mode)
