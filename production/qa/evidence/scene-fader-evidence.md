# SceneFader — Manual Test Evidence

> **Story**: Story 002 — SceneFader 墨韵过渡效果
> **Date**: 2026-05-15
> **Type**: Visual/Feel — manual verification required

## Evidence Checklist

### AC-1: SceneFader VisualElement 层级

- [ ] SceneFader exists in UI Toolkit hierarchy (verify via UI Toolkit Debugger)
- [ ] Initial opacity = 0 (transparent — SceneFader not visible)
- [ ] `picking-mode: ignore` confirmed — mouse clicks pass through overlay

**Screenshot**: [Attach UI Toolkit Debugger screenshot showing SceneFader in hierarchy]

### AC-2: FragmentTransition 动画时序

- [ ] Opacity transitions from transparent to black (0.5s fade-out)
- [ ] Opacity transitions from black to transparent (0.5s fade-in)
- [ ] Total transition time ~1.0s
- [ ] No flickering, no frame drops during transition

**Screenshot**: [Attach sequence showing mid-fade state]

### AC-3: ChapterTransition 保持遮罩

- [ ] Opacity transitions 0→1 (1.0s fade-out)
- [ ] Mask holds at full opacity during content load (2-4s)
- [ ] Opacity transitions 1→0 (1.0s fade-in)
- [ ] Content switch is NOT visible during load

**Screenshot**: [Attach sequence showing held-mask state]

### AC-4: USS transition 属性驱动

- [ ] opacity transition uses `transition-property: opacity` (confirmed in Theme.uss)
- [ ] No C# coroutines (`IEnumerator` / `yield return`) used for opacity animation
- [ ] `FadeOut` / `FadeIn` use `Task.Delay` for timing

**Screenshot**: [Attach code snippet of SceneFader.cs showing Task-based implementation]

---

## Sign-Off

**Verified by**: __________________ (Creative Director / Lead)
**Date**: __________________
**Result**: [ ] APPROVED / [ ] CHANGES REQUIRED

**Notes**:
