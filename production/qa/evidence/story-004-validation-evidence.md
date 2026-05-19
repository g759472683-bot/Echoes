# Story 004 -- Data Validation & Exception Safety -- Evidence

> **Story**: production/epics/data-management/story-004-data-validation-exception-safety.md
> **Date**: 2026-05-15
> **Type**: Integration (automated tests + manual Editor verification)

## Evidence Checklist

### AC-1: Missing Asset throws DataLoadException with FragmentId

- [ ] `test_validation_dle_three_param_constructor_sets_fragment_id` -- DataLoadException(assetKey, fragmentId, inner) sets both properties correctly
- [ ] `test_validation_dle_two_param_constructor_fragment_id_is_null` -- Backward-compat: two-param ctor leaves FragmentId null
- [ ] `test_validation_get_illustration_with_fragment_id_failure_has_fragment_id` -- Core AC-1: GetIllustrationAsync(key, fragId) throws DataLoadException with FragmentId populated
- [ ] `test_validation_get_illustration_without_fragment_id_failure_fragment_id_null` -- Single-param overload: FragmentId is null (backward compat)
- [ ] `test_validation_get_fragment_async_missing_throws_dle_with_asset_key` -- GetFragmentAsync missing key carries correct composite AssetKey
- [ ] `test_validation_concurrent_loads_both_carry_fragment_id` -- Concurrent dedup'd loads both fault with FragmentId
- [ ] `test_validation_get_illustration_with_fragment_id_succeeds` -- Valid load path succeeds without exception

**Automated test file**: `tests/unit/data-management/validation_test.cs`
**Run**: Open Unity Test Runner, select ValidationTests, Run All.

### AC-2: try/catch coverage of all loading paths

- [ ] `LoadAndCacheAsync` in DataManager.cs wraps `_loader.LoadAssetAsync<T>` in try/catch (line 297-308)
- [ ] Catch block uses `catch (Exception ex)` -- NOT specific Addressables types (IL2CPP stripping safety)
- [ ] Catch block wraps in `DataLoadException(key, fragmentId, ex)` when fragmentId is provided
- [ ] `PreloadChapterInternalAsync` in DataManager.cs wraps download in try/catch (line 476-498)
- [ ] UnityAddressableLoader.LoadAssetAsync delegates to real Addressables (caller's try/catch is the safety net)

**Code review**: All `Addressables.LoadAssetAsync<T>()` call sites verified. No direct `.Result` or `.Wait()` usage.

### AC-3: Editor Inspector status dot (MANUAL -- requires Unity Editor)

- [ ] Select a valid MemoryFragment SO in Project view
- [ ] Inspector shows green dot "Valid" at top
- [ ] Select a MemoryFragment with empty IllustrationKey
- [ ] Inspector shows red dot "Errors" with HelpBox listing the issue
- [ ] Select a MemoryFragment with valid IllustrationKey but unknown audio key
- [ ] Inspector shows yellow dot "Warnings" with specific issue listed
- [ ] Tooltip on the status dot shows issue summary

**Screenshot**: [Attach Inspector screenshot showing green/yellow/red states]

**Custom Inspector file**: `src/core/editor/MemoryFragmentInspector.cs`

### AC-4: Build cross-check (MANUAL -- requires Unity build or Window tool)

- [ ] Open Window > 回响 > Validate Fragments
- [ ] Click Refresh
- [ ] Summary shows total/passed/warnings/errors counts
- [ ] Each fragment entry shows status dot, fragment ID, chapter, asset path
- [ ] Fragments with missing references show specific issue texts
- [ ] Build-time validation: PreBuildValidator.OnPreprocessBuild runs before every build
- [ ] Missing fragment AssetReference keys produce BuildFailedException (blocks build)
- [ ] Null/empty AssetReferences produce warnings (do NOT block build)

**Screenshot**: [Attach Fragment Validator window screenshot showing results]

**Build Validator file**: `src/core/editor/PreBuildValidator.cs`

### AC-5: Batch validation menu (MANUAL -- requires Unity Editor)

- [ ] `Window > 回响 > Validate Fragments` menu item exists
- [ ] Opens Fragment Validator window titled "Fragment Validator"
- [ ] If no fragments exist, shows "No MemoryFragment assets found" message
- [ ] "Refresh" button re-scans all fragments
- [ ] Results list is scrollable (handles 60-100 fragments)
- [ ] Console log shows summary: "Scanned X fragments: Y passed, Z warnings, W errors."

**Screenshot**: [Attach window screenshot with batch results for all project fragments]

**Window file**: `src/core/editor/FragmentValidatorWindow.cs`

---

## Automated Test Summary

| Test Function | AC | Verdict |
|---|---|---|
| `test_validation_dle_three_param_constructor_sets_fragment_id` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_dle_two_param_constructor_fragment_id_is_null` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_dle_three_param_constructor_null_inner_is_valid` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_dle_three_param_constructor_empty_fragment_id_is_preserved` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_get_illustration_with_fragment_id_failure_has_fragment_id` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_get_illustration_without_fragment_id_failure_fragment_id_null` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_get_fragment_async_missing_throws_dle_with_asset_key` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_concurrent_loads_both_carry_fragment_id` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_get_illustration_with_fragment_id_succeeds` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_cross_reference_finds_missing_keys` | AC-4 | [ ] Pass / [ ] Fail |
| `test_validation_cross_reference_all_keys_known_passes` | AC-4 | [ ] Pass / [ ] Fail |
| `test_validation_cross_reference_empty_catalog_all_missing` | AC-4 | [ ] Pass / [ ] Fail |
| `test_validation_null_asset_references_are_warnings_not_errors` | AC-4 | [ ] Pass / [ ] Fail |
| `test_validation_dle_message_format_contains_key_and_fragment` | AC-1 | [ ] Pass / [ ] Fail |
| `test_validation_dle_two_param_message_format_unchanged` | AC-1 | [ ] Pass / [ ] Fail |

**Total**: 15 automated tests
**AC-1 tests**: 10 (DataLoadException constructors + integration)
**AC-4 mock tests**: 4 (cross-reference logic)
**Message format tests**: 2

---

## Engine Compliance Verification

| Requirement | Status |
|---|---|
| `catch (Exception ex)` -- NOT specific Addressables types (IL2CPP stripping, Finding 4) | Confirmed |
| `BuildFailedException` uses `UnityEditor.Build.BuildFailedException` namespace (Unity 6.3) | Confirmed |
| `IPreprocessBuildWithReport` from `UnityEditor.Build.Reporting` | Confirmed |
| `AddressableAssetSettingsDefaultObject.Settings` for editor key enumeration | Confirmed |
| `#if UNITY_EDITOR` wrapping on all editor-only files | Confirmed |
| No `.Result` or `.Wait()` on main thread | Confirmed |
| `SerializedObject`/`SerializedProperty` for custom Inspector | Confirmed |
| `[CreateAssetMenu]` and `[CustomEditor]` attributes are stable Unity 6.3 APIs | Confirmed |

---

## Files Modified / Created

| File | Action | Lines |
|---|---|---|
| `src/core/DataLoadException.cs` | Modified | +27 |
| `src/core/IDataManager.cs` | Modified | +11 |
| `src/core/DataManager.cs` | Modified | +19 |
| `src/core/editor/MemoryFragmentInspector.cs` | Created | ~180 |
| `src/core/editor/PreBuildValidator.cs` | Created | ~140 |
| `src/core/editor/FragmentValidatorWindow.cs` | Created | ~260 |
| `tests/unit/data-management/validation_test.cs` | Created | ~380 |
| `production/qa/evidence/story-004-validation-evidence.md` | Created | ~130 |

---

## Sign-Off

**Verified by**: __________________ (Lead Programmer)
**Date**: __________________
**Result**: [ ] APPROVED / [ ] CHANGES REQUIRED

**Notes**:
