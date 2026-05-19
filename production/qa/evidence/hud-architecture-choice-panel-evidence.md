# HUD Architecture + Choice Panel — Manual Walkthrough Evidence

> **Story**: story-001-hud-architecture-choice-panel
> **Type**: UI (Manual Walkthrough)
> **Date**: 2026-05-19
> **Tester**: [TO BE FILLED]

## Test Environment

- **Engine**: Unity 6.3 LTS
- **Scene**: Game
- **UIDocument**: Game scene UIDocument with in-game-hud.uxml
- **USS**: in-game-hud.uss + Theme.uss

## Walkthrough: Choice Panel Display and Interaction

### AC-1: ShowChoicePanel renders choice panel

**Setup**:
1. InGameHUD MonoBehaviour is attached to the Game scene UIDocument GameObject
2. _choiceOptionTemplate is assigned in the Inspector
3. InteractionManager calls `_hud.ShowChoicePanel(choiceGroup, anchorPosition)`

**Steps**:
1. Trigger a PresentChoice interaction on a fragment object
2. Observe the choice panel appearance

**Expected Results**:
- [ ] #choice-panel becomes visible at anchor position + 40px right
- [ ] #choice-prompt displays the GroupLabel text
- [ ] Choice options are rendered (one per available choice)
- [ ] Each option displays the choice text
- [ ] Keyboard focus is on the first option
- [ ] InputManager.SwitchToUIMode() is called

**Actual Results**:
- [ ] PASS / FAIL

**Screenshot**: [ATTACH SCREENSHOT]

---

### AC-2: Player clicks an option -> ApplyChanges -> HideChoicePanel

**Setup**:
1. Choice panel is visible with 2+ options
2. At least one option has ContentChanges defined

**Steps**:
1. Click on an option in the choice panel
2. Observe the result

**Expected Results**:
- [ ] ChangeTracker.ApplyChanges is called with the correct ContentChanges
- [ ] OnChoiceSelected event fires with the selected ChoiceId
- [ ] #choice-panel becomes hidden
- [ ] InputManager.SwitchToGameplayMode() is called
- [ ] Game elements (#association-paths, #chapter-progress) become visible again

**Actual Results**:
- [ ] PASS / FAIL

**Screenshot**: [ATTACH SCREENSHOT]

---

### AC-3: Escape key closes panel without changes

**Setup**:
1. Choice panel is visible with 2+ options

**Steps**:
1. Press the Escape key while the choice panel is open
2. Observe the result

**Expected Results**:
- [ ] #choice-panel becomes hidden
- [ ] InputManager.SwitchToGameplayMode() is called
- [ ] ChangeTracker.ApplyChanges is NOT called
- [ ] OnChoiceSelected event does NOT fire
- [ ] No ContentChanges are applied

**Actual Results**:
- [ ] PASS / FAIL

**Screenshot**: [ATTACH SCREENSHOT]

---

### AC-4: Empty choice panel edge case

**Setup**:
1. A ChoiceGroup with 0 available choices (all filtered out by conditions)

**Steps**:
1. Call ShowChoicePanel with the empty ChoiceGroup
2. Observe the result

**Expected Results**:
- [ ] #choice-panel remains hidden
- [ ] No exception is thrown
- [ ] Debug.LogWarning is emitted
- [ ] The Task<string> completes with null

**Actual Results**:
- [ ] PASS / FAIL

---

### Panel Positioning

**Test positions**:

| Anchor (x, y) | Panel Size | Expected Position | Result |
|---------------|------------|-------------------|--------|
| (100, 300) | 300x200 | (140, 300) — right side | [ ] |
| (1800, 300) | 300x200 | (1460, 300) — flip left | [ ] |
| (1800, 900) | 300x200 | (1800, 920) — fallback below | [ ] |
| Center overflow | 300x200 | Screen center fallback | [ ] |

## Visual Quality Checklist

- [ ] Choice option text uses hand-drawn font style
- [ ] Choice options have hover highlight effect
- [ ] Focus ring visible on keyboard-focused option
- [ ] Panel has ink-wash border (rgba(180,60,50,0.4))
- [ ] Panel has warm parchment background
- [ ] Panel fade-in/fade-out transitions are smooth

## Sign-off

- **Tester**: __________________
- **Date**: __________________
- **Verdict**: [ ] PASS / [ ] FAIL (with issues)
- **Issues**:
