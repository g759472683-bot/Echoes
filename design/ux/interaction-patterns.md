# Interaction Pattern Library — 回响 (Echoes)

**Date**: 2026-05-12
**Status**: Initialized (minimal — populated during Pre-Production)

## Core Patterns

### Pattern 1: Scroll Navigation (画卷导航)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Mouse drag on scroll plane / Arrow keys |
| **Response** | Scroll parallax (foreground/midground/background at different rates) |
| **Feedback** | Smooth deceleration (inertia), no snapping |
| **Edge Case** | Scroll boundary — gentle bounce or ink-wash edge fade |
| **ADR** | ADR-0005 (Input System Gameplay Map — Point/Drag) |

### Pattern 2: Object Hover (对象悬停)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Cursor OverlapPoint on InteractiveObject collider |
| **Response** | L1 glow (vermilion ink dot), cursor change |
| **Feedback** | MicroAnimationManager ambient → triggered transition |
| **Debounce** | 50ms (prevents flicker on boundary) |
| **ADR** | ADR-0005 (HoverDetector) + ADR-0009 (MicroAnimationTriggered) |

### Pattern 3: Object Click/Interact (对象交互)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Click on InteractiveObject |
| **Response** | Interaction → Choice/Text/Animation dispatch |
| **Feedback** | L2 glow + SFX + text/show panel |
| **Edge Case** | Drag cancelled → no interact (distinguish drag from click) |
| **ADR** | ADR-0001 (OnInteract event) + ADR-0006 (UIPanelStack) |

### Pattern 4: Choice Selection (选项选择)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Click on choice option in choice-panel |
| **Response** | ApplyChanges(ContentChange[]), transition to next fragment |
| **Feedback** | L3 glow + SFX + choice-panel close animation |
| **Edge Case** | Double-click prevention (debounce 500ms) |
| **ADR** | ADR-0007 (ChangeTracker.ApplyChanges) + ADR-0004 (SceneManager Transition) |

### Pattern 5: Panel Push/Pop (面板栈)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Menu button / Escape key / story event |
| **Response** | PushPanel / PopPanel with CSS transition |
| **Feedback** | .fade-in (300ms) / .fade-out (150ms) |
| **Input Gate** | Stack non-empty → UI Action Map, empty → Gameplay Action Map |
| **ADR** | ADR-0006 (UIPanelStack) |

### Pattern 6: Keyboard Navigation (键盘导航)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Tab / Arrow keys |
| **Response** | FocusController moves focus to next Focusable |
| **Feedback** | Focus visual style (ink outline + glow) |
| **Edge Case** | Wrapping: last → first, first → last |
| **ADR** | ADR-0005 (UI Map — Navigate/TabNext/TabPrevious) + ADR-0006 (FocusController) |

### Pattern 7: Save/Load Confirmation (存档确认)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Save/Load button in slot panel |
| **Response** | Modal dialog: "Overwrite save slot [N]?" |
| **Feedback** | Modal overlay dims background, CSS transition |
| **Edge Case** | Empty slot → instant write (no confirmation needed) |
| **ADR** | ADR-0003 (ISaveManager) |

### Pattern 8: Chapter Transition (章节转场)

| Attribute | Value |
|-----------|-------|
| **Trigger** | Chapter complete / chapter select |
| **Response** | SceneFader ink-mask expand → load → contract |
| **Feedback** | Full-screen ink-wash animation (~500ms total) |
| **Input Gate** | SwitchToInactive() during transition |
| **ADR** | ADR-0004 (SceneManager Transition FSM) |

## Pattern Matrix

| Pattern | Input | Feedback | State Change | Accessibility |
|---------|-------|----------|-------------|---------------|
| Scroll Nav | Drag/Keys | Parallax + inertia | Scroll offset | Keyboard alt: Arrow keys |
| Object Hover | Cursor only | L1 glow | HoverDetector state | N/A (not critical info) |
| Object Click | Click/Enter | L2 + SFX | Interaction dispatch | Keyboard: Enter on focused object |
| Choice Select | Click | L3 + SFX | ContentChange[] apply | Keyboard: Tab to choice + Enter |
| Panel Push/Pop | Button/Escape | CSS transition | Stack depth | Full keyboard nav |
| Keyboard Nav | Tab/Arrows | Focus outline | FocusController | Core accessibility feature |
| Save Confirm | Click | Modal dialog | SaveData write | Keyboard: Enter/Cancel |
| Chapter Transition | Event-driven | Ink-wash fade | Scene load | Visual + optional audio cue |

## Related

- `design/accessibility-requirements.md` — Tier: Comprehensive
- `docs/architecture/architecture.md` §3.2 — Event subscription table
- `design/ux/hud.md` — HUD layout (to be created)
