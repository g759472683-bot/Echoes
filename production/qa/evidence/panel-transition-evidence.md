# Panel Transition — Visual Evidence

**Story**: ui-framework S003 — 面板过渡动画
**Type**: Visual/Feel
**Date**: 2026-05-18
**Status**: [ ] Sign-off pending

## AC-1: Fade-In Transition

**Setup**: Game in Gameplay state
**Action**: Press Escape → PushPanel("pause-menu")
**Expected**: Panel appears with opacity fading from 0 to 1 over ~0.3s
**Actual**: [ ] Verified in Unity Editor runtime
**Screenshot**: [ ]

## AC-2: Fade-Out Transition

**Setup**: Pause menu open
**Action**: Press Escape → PopPanel()
**Expected**: Panel disappears with opacity fading from 1 to 0 over ~0.2s
**Actual**: [ ] Verified in Unity Editor runtime
**Screenshot**: [ ]

## AC-3: Transitioning State Guard

**Setup**: Pause menu mid-fade-in (PushPanel just called)
**Action**: Press Escape again during fade-in
**Expected**: Second Escape ignored; fade-in completes normally; subsequent Escape triggers fade-out
**Actual**: [ ] Verified in Unity Editor runtime
**Screenshot**: [ ]

## AC-4: Transition Duration Configurable

**Setup**: Modify Theme.uss `--transition-normal: 600ms`
**Action**: PushPanel("pause-menu")
**Expected**: Fade-in takes ~0.6s (visibly longer)
**Actual**: [ ] Verified in Unity Editor runtime
**Screenshot**: [ ]

## AC-5: ReplaceTop Cross-Fade

**Setup**: Pause menu open
**Action**: ReplaceTop("settings-panel")
**Expected**: Pause menu fades out while settings fades in (cross-fade, no intermediate blank frame)
**Actual**: [ ] Verified in Unity Editor runtime
**Screenshot**: [ ]

## Sign-Off

**Reviewer**: [ ]
**Date**: [ ]
**Verdict**: [ ] APPROVED / [ ] REVISE
