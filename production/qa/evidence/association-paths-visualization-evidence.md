# Association Paths Visualization — Manual Walkthrough Evidence

> **Story**: story-002-association-paths-visualization
> **Type**: UI (Manual Walkthrough)
> **Date**: 2026-05-19
> **Tester**: [TO BE FILLED]

## Test Environment

- **Engine**: Unity 6.3 LTS
- **Scene**: Game
- **UIDocument**: Game scene UIDocument with in-game-hud.uxml
- **USS**: in-game-hud.uss + Theme.uss

## Walkthrough: Association Paths Rendering and Interaction

### AC-1: Association paths render with correct visual grading

**Setup**:
1. InGameHUD is initialized
2. Association engine returns 3 candidates:
   - Strong: Score 0.85, fragment "ch01_frag_03"
   - Medium: Score 0.62, fragment "ch01_frag_07"
   - Faint: Score 0.31, fragment "ch01_frag_12"
3. Call InGameHUD.ShowAssociationPaths(candidates)

**Steps**:
1. Observe the #association-paths container

**Expected Results**:
- [ ] 3 .path-candidate elements are present in #association-paths
- [ ] Strong candidate: ink trail opacity 0.9, target indicator size 16px
- [ ] Medium candidate: ink trail opacity 0.6, target indicator size 12px
- [ ] Faint candidate: ink trail opacity 0.35, target indicator size 8px
- [ ] No .scent-label text is rendered (MVP scope)
- [ ] Paths radiate from center with even angular distribution

**Actual Results**:
- [ ] PASS / FAIL

**Screenshot**: [ATTACH SCREENSHOT]

---

### AC-2: Click on path candidate triggers fragment transition

**Setup**:
1. Association paths are rendered with a candidate targeting "ch01_frag_05"
2. ChapterManagerRef is set on InGameHUD

**Steps**:
1. Click on the .path-candidate element for "ch01_frag_05"
2. Observe the result

**Expected Results**:
- [ ] ChapterManager.TransitionToFragment("ch01_frag_05") is called
- [ ] A fragment transition begins (fade out -> load -> fade in)
- [ ] Click handler fires on the correct path-candidate

**Actual Results**:
- [ ] PASS / FAIL

---

### AC-3: Zero candidates — empty container

**Setup**:
1. Association engine returns an empty array

**Steps**:
1. Call InGameHUD.ShowAssociationPaths(empty array)

**Expected Results**:
- [ ] #association-paths container has 0 children
- [ ] No errors logged
- [ ] TransitionToFragment is NOT called
- [ ] HUD continues to function normally

**Actual Results**:
- [ ] PASS / FAIL

---

### AC-4: More than 5 candidates — only Top-5 rendered

**Setup**:
1. Association engine returns 7 candidates with decreasing scores:
   - 0.95, 0.88, 0.72, 0.65, 0.55, 0.42, 0.28
2. Call InGameHUD.ShowAssociationPaths(candidates)

**Steps**:
1. Count the .path-candidate elements in #association-paths

**Expected Results**:
- [ ] Exactly 5 .path-candidate elements are rendered
- [ ] Elements correspond to the top 5 scores (0.95, 0.88, 0.72, 0.65, 0.55)
- [ ] The 6th and 7th candidates (0.42, 0.28) are NOT rendered
- [ ] No errors or warnings about truncation (this is expected behavior)

**Actual Results**:
- [ ] PASS / FAIL

---

### Keyboard Navigation

**Test**:

| Action | Expected Result | Pass? |
|--------|----------------|-------|
| Tab to first .path-candidate | Focus ring visible on first path | [ ] |
| Arrow Right to next path | Focus moves to second path | [ ] |
| Arrow Left to previous path | Focus returns to first path | [ ] |
| Enter on focused path | TransitionToFragment called | [ ] |

## Visual Quality Checklist

- [ ] Ink trails are semi-transparent black
- [ ] Target indicators are vermilion (#C04040) circles
- [ ] Strong paths are visually prominent (thick, dark)
- [ ] Faint/Trace paths are barely visible but still perceptible
- [ ] Hover effect: ink trail brightens, target indicator glows
- [ ] Focus ring visible for keyboard navigation
- [ ] Path directions are evenly distributed around the center

## Sign-off

- **Tester**: __________________
- **Date**: __________________
- **Verdict**: [ ] PASS / [ ] FAIL (with issues)
- **Issues**:
