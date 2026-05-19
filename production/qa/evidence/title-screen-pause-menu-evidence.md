# Manual Walkthrough: Title Screen + Pause Menu

> **Story**: main-menu S001 — Title Screen + Pause Menu
> **Type**: UI — Manual walkthrough
> **Date**: 2026-05-19
> **Tester**: [NAME]
> **Status**: [ ] Pass / [ ] Fail
> **Gate Level**: ADVISORY

---

## Setup

1. Build: Development Build (Unity 6.3 LTS)
2. Platform: PC (Windows 11)
3. Input: Keyboard/Mouse
4. Preparation:
   - [ ] Fresh launch environment (delete all save files)
   - [ ] Verify MainMenu scene loads correctly
   - [ ] Confirm UIDocument with `main-menu.uxml` is assigned

---

## AC-1: Title Screen -- No Save File

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 1.1 | Launch game (no save files exist) | MainMenu scene loads | [ ] |
| 1.2 | Observe title screen | `#title-screen` fades in with 0.5s fade-in animation | [ ] |
| 1.3 | Check visible buttons | `#btn-new-game`, `#btn-load-game`, `#btn-settings`, `#btn-quit` visible | [ ] |
| 1.4 | Check `#btn-continue` | NOT visible (`display:none`) | [ ] |
| 1.5 | Check keyboard focus | Focus is on `#btn-new-game` | [ ] |
| 1.6 | Check logo | `#game-logo` shows title "回响" in hand-drawn font | [ ] |
| 1.7 | Check background | `#background-painting` renders background ink painting | [ ] |

### Screenshot Placeholders

- `screenshots/ac1-title-screen-no-save.png` — [ATTACH SCREENSHOT]

### Pass Condition

All expected buttons visible, continue hidden, focus on New Game, fade-in animation plays.

---

## AC-2: Title Screen -- auto_save Exists

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 2.1 | Create a save file (play through Ch01, save, quit) | `auto_save.sav` exists in save directory | [ ] |
| 2.2 | Relaunch game | MainMenu scene loads | [ ] |
| 2.3 | Check `#btn-continue` | Visible (`display:flex`) below `#btn-new-game` | [ ] |
| 2.4 | Check keyboard focus | Focus is on `#btn-continue` | [ ] |
| 2.5 | Verify all buttons | All 5 buttons visible (new game + continue + load + settings + quit) | [ ] |

### Screenshot Placeholders

- `screenshots/ac2-title-screen-with-save.png` — [ATTACH SCREENSHOT]

### Pass Condition

Continue button visible and focused when auto_save exists.

---

## AC-3: Pause Menu Opens from Gameplay

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 3.1 | Start a new game | InGame scene loads, gameplay active | [ ] |
| 3.2 | Press Escape | `#pause-menu` opens | [ ] |
| 3.3 | Check Time.timeScale | Time.timeScale = 0 (game frozen) | [ ] |
| 3.4 | Check HUD opacity | HUD dims (opacity ~0.3) | [ ] |
| 3.5 | Check overlay | Semi-transparent ink overlay (`rgba(15,10,5,0.5)`) covers screen | [ ] |
| 3.6 | Check input mode | Input mode switched to UI (Menu) | [ ] |
| 3.7 | Check audio | Music/ambience reduced to ~0.3x volume | [ ] |
| 3.8 | Check pause buttons | Resume, Save Game, Load Game, Settings, Return to Title all visible | [ ] |
| 3.9 | Try clicking game elements behind overlay | Clicks should be blocked by pause overlay | [ ] |

### Screenshot Placeholders

- `screenshots/ac3-pause-menu-open.png` — [ATTACH SCREENSHOT]
- `screenshots/ac3-pause-menu-overlay.png` — [ATTACH SCREENSHOT]

### Pass Condition

Pause state fully engaged -- time frozen, HUD dimmed, overlay visible, input gated.

---

## AC-4: Pause Menu Closes -- Resume Gameplay

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 4.1 | From pause menu, click "继续" | Pause menu closes | [ ] |
| 4.2 | Check Time.timeScale | Time.timeScale = 1 (game resumed) | [ ] |
| 4.3 | Check HUD opacity | HUD restored to full opacity (1.0) | [ ] |
| 4.4 | Check audio | Music/ambience restored to full volume | [ ] |
| 4.5 | Check input mode | Input mode returns to Gameplay | [ ] |
| 4.6 | Repeat: press Escape, then press Escape again | Same resume behaviour -- Escape closes pause | [ ] |

### Screenshot Placeholders

- `screenshots/ac4-resumed-gameplay.png` — [ATTACH SCREENSHOT]

### Pass Condition

Full resume -- time, audio, HUD, input all restored to gameplay state.

---

## AC-5: Return to Title from Pause

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 5.1 | From pause menu, click "返回标题画面" | `#modal-dialog` opens | [ ] |
| 5.2 | Check modal message | Shows "返回标题画面？未保存的进度将丢失。" | [ ] |
| 5.3 | Check modal buttons | "确定" (confirm) and "取消" (cancel) both visible | [ ] |
| 5.4 | Check pause menu behind | Pause menu still visible behind modal (dimmed by overlay) | [ ] |
| 5.5 | Click "取消" | Modal closes, returns to pause menu | [ ] |
| 5.6 | Click "返回标题画面" again | Modal reopens | [ ] |
| 5.7 | Click "确定" | Returns to MainMenu scene (auto-save performed) | [ ] |
| 5.8 | Verify auto-save | After returning to title, "继续" button visible | [ ] |

### Screenshot Placeholders

- `screenshots/ac5-return-confirm-dialog.png` — [ATTACH SCREENSHOT]

### Pass Condition

Modal dialog on top of pause menu, correct message, cancel safe, confirm triggers return.

---

## Overall Result

| Criterion | Pass/Fail |
|-----------|-----------|
| AC-1: Title screen -- no save | [ ] |
| AC-2: Title screen -- auto_save exists | [ ] |
| AC-3: Pause menu opens | [ ] |
| AC-4: Pause menu closes | [ ] |
| AC-5: Return to title | [ ] |

## Notes

[Any observations, bugs, or improvement suggestions]

---

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | [NAME] | 2026-05-19 | |
| Lead | [NAME] | 2026-05-19 | |
