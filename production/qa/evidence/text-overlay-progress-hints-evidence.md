# Text Overlay + Chapter Progress + Interaction Hints — Manual Walkthrough Evidence

> **Story**: story-003-text-overlay-progress-hints
> **Type**: UI (Manual Walkthrough)
> **Date**: 2026-05-19
> **Tester**: [TO BE FILLED]

## Test Environment

- **Engine**: Unity 6.3 LTS
- **Scene**: Game
- **UIDocument**: Game scene UIDocument with in-game-hud.uxml
- **USS**: in-game-hud.uss + Theme.uss

## Walkthrough: Fragment Text Overlay

### AC-1: Fragment text overlay display and auto-fade

**Setup**:
1. InGameHUD is initialized
2. InteractionManager fires OnShowText with text "一封泛黄的信落在桌上，墨迹已经褪去大半"

**Steps**:
1. Call InGameHUD.ShowFragmentText(content, new Vector2(600, 400))
2. Observe the text overlay over 5 seconds

**Expected Results**:
- [ ] Text "一封泛黄的信落在桌上..." appears at position (600, 400)
- [ ] Text is rendered in hand-drawn font style (semi-transparent ink color)
- [ ] Text has no background box (transparent)
- [ ] pickingMode is Ignore — mouse clicks pass through to objects beneath
- [ ] After 4.0 seconds: text begins fade-out (opacity transitions to 0 over 0.5s)
- [ ] After 4.5 seconds: text is fully hidden
- [ ] Clicking anywhere during display dismisses overlay immediately

**Actual Results**:
- [ ] PASS / FAIL

**Sub-test: Click-to-dismiss**

| Action | Expected Result | Pass? |
|--------|----------------|-------|
| ShowFragmentText("test", center) | Text appears | [ ] |
| Click anywhere on screen | Text disappears immediately | [ ] |
| ShowFragmentText("test2", center) | New text appears | [ ] |
| Wait 4.5s | Text auto-fades and disappears | [ ] |

**Screenshots**:
- Text visible: [ATTACH]
- Text mid-fade: [ATTACH]
- Text dismissed: [ATTACH]

---

## Walkthrough: Chapter Progress

### AC-2: Chapter progress updates on fragment change

**Setup**:
1. Chapter has 8 fragments total
2. Currently visited 4 fragments
3. Current fragment is the 4th one (index 4)

**Steps**:
1. Call InGameHUD.UpdateChapterProgress("第一章", 4, 8)
2. Observe the #chapter-progress element at the bottom of the screen

**Expected Results**:
- [ ] 8 horizontal dots are rendered in #fragment-count
- [ ] First 4 dots are solid vermilion (#C04040) — visited
- [ ] Dots 5-8 are hollow (border only, transparent fill) — unvisited
- [ ] The 4th dot has "dot-current" class — L2 pulse animation
- [ ] Chapter name displays "第一章" in the chapter-name label
- [ ] Dots are 6px circles with 4px spacing
- [ ] Progress bar is centered at the bottom of the screen

**Sub-tests**:

| Fragments | Visited | Current | Expected Visual | Pass? |
|-----------|---------|---------|-----------------|-------|
| 8 | 1 | 1st | 1 solid + 7 hollow; 1st dot pulsing | [ ] |
| 8 | 4 | 4th | 4 solid + 4 hollow; 4th dot pulsing | [ ] |
| 8 | 8 | 8th | All 8 solid; 8th dot pulsing (chapter complete) | [ ] |
| 3 | 1 | 1st | 1 solid + 2 hollow | [ ] |
| 3 | 3 | 3rd | All 3 solid; 3rd dot pulsing | [ ] |

**Screenshots**:
- Mid-chapter (4/8): [ATTACH]
- Chapter complete (8/8): [ATTACH]

---

### AC-3: HUD hides during fragment transition

**Setup**:
1. HUD is visible with text overlay active
2. GameSceneManager fires OnFragmentTransitionStarted

**Steps**:
1. Trigger a fragment transition
2. Observe HUD during fade-out phase
3. Observe HUD during fade-in phase

**Expected Results**:
- [ ] When OnFragmentTransitionStarted fires: HUD becomes fully hidden
- [ ] #fragment-text-overlay closes immediately (if open)
- [ ] #interaction-hint closes immediately (if open)
- [ ] During the fade-out/fade-in: NO HUD elements are visible
- [ ] When OnFragmentTransitioned fires: HUD restores to previous visibility
- [ ] Text overlay does NOT restore (it was dismissed, not hidden)

**Actual Results**:
- [ ] PASS / FAIL

**Screenshots**:
- Before transition (HUD visible): [ATTACH]
- During transition (HUD hidden, fade overlay): [ATTACH]
- After transition (HUD restored): [ATTACH]

---

## Walkthrough: Interaction Hint

### AC-4: Interaction hint appears after 0.5s hover delay

**Setup**:
1. InteractiveObject "泛黄的信封" (Touch type) exists on current fragment
2. Cursor is not over any object

**Steps**:
1. Move cursor over "泛黄的信封"
2. Keep cursor stationary for 0.5s+
3. Move cursor away

**Expected Results**:
- [ ] After exactly 0.5s (not immediately): interaction hint appears
- [ ] Hint text reads "泛黄的信封" (Touch type — object name only)
- [ ] Hint is positioned 20px above the cursor
- [ ] Hint text uses small hand-drawn font (12px)
- [ ] Hint is semi-transparent (opacity ~0.85)
- [ ] When cursor moves away: hint disappears immediately (no fade delay)

**Interaction Type Hint Text Tests**:

| Interaction Type | Object Name | Expected Hint Text | Pass? |
|-----------------|-------------|-------------------|-------|
| Touch | 泛黄的信封 | 泛黄的信封 | [ ] |
| Drag | 墨迹卷轴 | 拖拽 墨迹卷轴 | [ ] |
| Hover | 褪色的照片 | 褪色的照片... | [ ] |
| Examine | 古旧的怀表 | 细看 古旧的怀表 | [ ] |

**Actual Results**:
- [ ] PASS / FAIL

**Screenshots**:
- Hint visible above cursor: [ATTACH]
- Hint dismissed after cursor leaves: [ATTACH]

---

### Hover Exit Behavior

| Action | Expected Result | Pass? |
|--------|----------------|-------|
| Cursor enters object A | 0.5s timer starts | [ ] |
| Cursor leaves before 0.5s | No hint shown | [ ] |
| Cursor enters object A, stays 0.5s | Hint shows | [ ] |
| Cursor moves from A to B | Hint for A hides; 0.5s timer for B starts | [ ] |

## Visual Quality Checklist

- [ ] Text overlay uses hand-drawn font (semi-transparent ink, no background)
- [ ] Chapter dots are crisp 6px circles with consistent spacing
- [ ] Dot pulse animation is smooth (2.5s sine wave, opacity 100%<->75%)
- [ ] Interaction hint is readable but unobtrusive
- [ ] All elements respect the ink-wash visual theme (vermilion, cinnabar, ink black palette)

## Sign-off

- **Tester**: __________________
- **Date**: __________________
- **Verdict**: [ ] PASS / [ ] FAIL (with issues)
- **Issues**:
