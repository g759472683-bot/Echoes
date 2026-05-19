# Manual Walkthrough: Settings Panel + Modal Dialog

> **Story**: main-menu S002 — Settings Panel + Modal Confirmation Dialog
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
   - [ ] MainMenu scene loads with UIDocument
   - [ ] Story 001 (title screen + pause menu) implemented
   - [ ] PlayerPrefs cleared (fresh settings state)

---

## AC-1: Volume Sliders -- Real-Time Adjustment

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 1.1 | From title screen, click "设置" | Settings panel opens (push from title) | [ ] |
| 1.2 | Check slider labels | "主音量", "音效", "音乐", "环境音" labels visible | [ ] |
| 1.3 | Check slider defaults | Master=0.8, SFX=0.7, Music=0.6, Ambience=0.5 | [ ] |
| 1.4 | Drag Music slider to ~0.5 | Audio volume changes in real time | [ ] |
| 1.5 | Verify PlayerPrefs | `PlayerPrefs.GetFloat("volume_music")` = 0.5 | [ ] |
| 1.6 | Adjust SFX slider to 0.2 | SFX volume immediately quieter | [ ] |
| 1.7 | Drag Master slider to 0 | All audio silent (master mutes everything) | [ ] |
| 1.8 | Drag Master back to 0.8 | Audio restores | [ ] |
| 1.9 | Exit settings, reopen | All slider values persist at last-set levels | [ ] |

### Screenshot Placeholders

- `screenshots/ac1-settings-volume-sliders.png` — [ATTACH SCREENSHOT]
- `screenshots/ac1-settings-volume-adjusted.png` — [ATTACH SCREENSHOT]

### Pass Condition

Real-time volume change on drag, PlayerPrefs persistence across panel open/close.

---

## AC-2: Language Switch -- All Panels Refresh

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 2.1 | Open settings panel | Current locale shown in dropdown (e.g., "中文") | [ ] |
| 2.2 | Click dropdown, select "English" | Dropdown value changes to "English" | [ ] |
| 2.3 | Verify settings panel refresh | Labels change: "设置" → "Settings", "主音量" → "Master Volume", etc. (if translations exist) | [ ] |
| 2.4 | Verify PlayerPrefs | `PlayerPrefs.GetString("selected_locale")` = "en" | [ ] |
| 2.5 | Close settings panel | Title screen button text updates (if localised strings exist) | [ ] |
| 2.6 | Reopen settings panel | Dropdown still shows "English" | [ ] |
| 2.7 | Switch back to "中文" | All labels revert to Chinese | [ ] |
| 2.8 | Verify fallback strings | Any untranslated strings show "……" in release, "<MISSING: key>" in dev build | [ ] |

### Screenshot Placeholders

- `screenshots/ac2-settings-language-chinese.png` — [ATTACH SCREENSHOT]
- `screenshots/ac2-settings-language-english.png` — [ATTACH SCREENSHOT]

### Pass Condition

Language switch propagates to all visible UI text, persists across panel open/close.

---

## AC-3: Settings Back -- No Save Button

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 3.1 | Open settings from title screen | Settings panel visible | [ ] |
| 3.2 | Check for "保存设置" button | No explicit save button exists | [ ] |
| 3.3 | Change Master volume to 0.3 | Volume changes immediately | [ ] |
| 3.4 | Click "返回" button | Settings panel closes, title screen visible | [ ] |
| 3.5 | Reopen settings | Master volume still at 0.3 (persisted) | [ ] |
| 3.6 | Press Escape while in settings | Same as clicking "返回" -- panel closes, settings persisted | [ ] |

### Screenshot Placeholders

- `screenshots/ac3-settings-no-save-button.png` — [ATTACH SCREENSHOT]

### Pass Condition

Settings applied immediately, persisted automatically, no explicit save action needed.

---

## AC-4: Modal Confirm Dialog -- Confirm and Cancel Flows

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 4.1 | Create a save file, then click "新游戏" | Modal dialog opens with "开始新游戏将覆盖当前进度。确定继续？" | [ ] |
| 4.2 | Check focus | Focus is on "取消" button (safe default) | [ ] |
| 4.3 | Press Escape | Dialog closes (cancel), title screen unchanged | [ ] |
| 4.4 | Click "新游戏" again | Dialog reopens | [ ] |
| 4.5 | Press Enter | Confirm action executes (new game starts) | [ ] |
| 4.6 | Test all 5 scenarios: | | |
| 4.6a | -- NewGame (with save) | "开始新游戏将覆盖当前进度。确定继续？" | [ ] |
| 4.6b | -- OverwriteSave (save panel) | "覆盖此存档？此操作不可撤销。" | [ ] |
| 4.6c | -- LoadInGame (pause menu load) | "加载此存档？当前未保存的进度将丢失。" | [ ] |
| 4.6d | -- ReturnToTitle (pause menu) | "返回标题画面？未保存的进度将丢失。" | [ ] |
| 4.6e | -- Quit (title screen) | "退出游戏？" | [ ] |
| 4.7 | Test PopPanel count | Confirm → stack depth decreases by 2; Cancel → decreases by 1 | [ ] |

### Screenshot Placeholders

- `screenshots/ac4-modal-new-game.png` — [ATTACH SCREENSHOT]
- `screenshots/ac4-modal-overwrite.png` — [ATTACH SCREENSHOT]
- `screenshots/ac4-modal-load-in-game.png` — [ATTACH SCREENSHOT]
- `screenshots/ac4-modal-return.png` — [ATTACH SCREENSHOT]
- `screenshots/ac4-modal-quit.png` — [ATTACH SCREENSHOT]

### Pass Condition

All 5 scenarios show correct message, confirm executes action and pops 2 panels, cancel pops 1.

---

## AC-5: Full Keyboard Navigation

### Verification Steps

| Step | Action | Expected Result | Observed |
|------|--------|-----------------|----------|
| 5.1 | Open settings panel | Mouse not used for remainder of test | [ ] |
| 5.2 | Press Tab repeatedly | Focus cycles through all controls (sliders, dropdown, back button) | [ ] |
| 5.3 | Press Arrow Down on focused slider | Slider value decreases by ~0.05 | [ ] |
| 5.4 | Press Arrow Up on focused slider | Slider value increases by ~0.05 | [ ] |
| 5.5 | Focus dropdown, press Arrow Down | Option cycles to next language | [ ] |
| 5.6 | Press Enter on "返回" button | Settings panel closes | [ ] |
| 5.7 | From title screen, press Arrow Keys | Focus moves between buttons | [ ] |
| 5.8 | Press Enter on focused button | Button action executes | [ ] |
| 5.9 | From pause menu, press Tab | Focus cycles through all 5 pause buttons | [ ] |
| 5.10 | Press Escape on sub-panel | Panel pops (back to parent) | [ ] |
| 5.11 | Press Escape on root | Modal dialog opens (quit/return confirm) | [ ] |

### Screenshot Placeholders

- `screenshots/ac5-keyboard-focus-slider.png` — [ATTACH SCREENSHOT]
- `screenshots/ac5-keyboard-focus-dropdown.png` — [ATTACH SCREENSHOT]
- `screenshots/ac5-keyboard-focus-button.png` — [ATTACH SCREENSHOT]

### Pass Condition

All interactive elements reachable and operable via keyboard alone, no mouse required.

---

## Overall Result

| Criterion | Pass/Fail |
|-----------|-----------|
| AC-1: Volume sliders -- real-time adjustment | [ ] |
| AC-2: Language switch -- all panels refresh | [ ] |
| AC-3: Settings back -- no save button | [ ] |
| AC-4: Modal confirm -- all scenarios | [ ] |
| AC-5: Full keyboard navigation | [ ] |

## Notes

[Any observations, bugs, or improvement suggestions]

---

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | [NAME] | 2026-05-19 | |
| Lead | [NAME] | 2026-05-19 | |
