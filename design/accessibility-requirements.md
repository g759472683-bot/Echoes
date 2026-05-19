# Accessibility Requirements — 回响 (Echoes)

**Date**: 2026-05-12
**Tier**: Comprehensive
**Author**: User + Claude Code

## Committed Tier

**Comprehensive** = Standard + motor accessibility + full settings menu.

### Tier Breakdown

| Requirement | Basic | Standard | Comprehensive | Status |
|-------------|-------|----------|---------------|--------|
| Key rebinding | ✅ | ✅ | ✅ | ADR-0005 (Input System + PlayerPrefs) |
| Subtitles for all dialogue | ✅ | ✅ | ✅ | ADR-0006 (UI Toolkit text) |
| Colorblind modes (3 types) | — | ✅ | ✅ | TODO: Pre-Production |
| Scalable UI (text size) | — | ✅ | ✅ | Theme.uss CSS variables — USS `var()` |
| Motor — full keyboard nav | — | — | ✅ | ADR-0005 (UI Map: Navigate/Confirm/Cancel/Tab) |
| Motor — input delay tolerance | — | — | ✅ | Input System holding threshold |
| Motor — no rapid-tap requirements | — | — | ✅ | Design constraint — no time-pressure mechanics |
| Full settings menu | — | — | ✅ | ADR-0006 (Settings Panel) |
| Screen reader support | — | — | ✅ | UI Toolkit `Focusable` + accessible labels |
| External audit | — | — | — | Exemplary tier only |

## Visual Accessibility

### Colorblind Modes
- **Deuteranopia** (green-blind, ~6% male): Avoid red-green only distinction
- **Protanopia** (red-blind, ~2% male): Same palette constraints
- **Tritanopia** (blue-blind, rare): Blue-yellow palette check

**Implementation**: Post-processing LUT or Shader Graph color transform applied at URP 2D Renderer level.

**Design constraint**: No gameplay-critical information communicated by color alone. Emotional palette shifts (§4 of art bible) are atmospheric, not informational — colorblind mode preserves the emotional gradient through value/luminance rather than hue.

### Text Scaling
- Base font size configurable from 100% to 200%
- All UI Toolkit VisualElements use USS `var()` font sizes
- HUD text overlay scales within safe-area constraints

## Motor Accessibility

### Keyboard Navigation (Full)
- All UI panels navigable by keyboard (Tab/Arrow keys + Enter/Escape)
- No mouse-only interactions in core gameplay
- Drag interaction has keyboard alternative (arrow keys to move scroll)

### Input Tolerance
- Hold-to-confirm threshold configurable (default 300ms)
- No rapid-tap or time-pressure mechanics (game is narrative/atmospheric)
- Double-press prevention window: 500ms

### Scroll Interaction Alternative
- Mouse drag → Arrow keys pan the scroll
- Mouse click → Enter/Space to interact with highlighted object

## Hearing Accessibility

### Subtitles
- All dialogue/NPC speech subtitled
- Speaker name label
- Sound effect descriptions in brackets: `[door creaks open]`
- Subtitle background: semi-transparent ink-wash for contrast

### Audio Cues
- All audio cues have visual equivalents:
  - Memory transition: SceneFader ink animation
  - Interaction feedback: micro-animation + text hint
  - No audio-only puzzles or mechanics

## Settings Menu Design

### Accessibility Settings Panel
```
Settings > Accessibility
├─ Text Size: [slider, 100%-200%, step 25%]
├─ Colorblind Mode: [dropdown: Off / Deuteranopia / Protanopia / Tritanopia]
├─ Hold Confirmation Time: [slider, 0ms-1000ms, step 100ms]
├─ Subtitle Background: [toggle: On/Off]
├─ Screen Shake: [toggle: On/Off]
└─ Reset to Defaults: [button]
```

## Validation

- [ ] All UI panels navigable by keyboard alone
- [ ] Colorblind mode does not distort art bible's emotional palette intent
- [ ] Subtitles appear within 200ms of audio onset
- [ ] Text at 200% scale does not overflow VisualElement boundaries
- [ ] No gameplay-critical information conveyed by color or audio alone

## Related

- ADR-0005 — Input System + key rebinding
- ADR-0006 — UI Toolkit + Theme.uss variables
- `design/art/art-bible.md` §4 — Color System
- `design/ux/interaction-patterns.md`
