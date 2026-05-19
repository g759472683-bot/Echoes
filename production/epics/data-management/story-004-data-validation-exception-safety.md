# Story 004: 数据验证 + 异常安全

> **Epic**: 数据管理系统 (DataManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/data-management.md`
**Requirement**: `TR-data-management-005`, `TR-data-management-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: 数据管理策略
**ADR Decision Summary**: Addressables 加载 + 所有加载调用 try/catch 包装（Unity 6.2+ 加载失败抛异常而非返回 null）+ 三层数据验证（Editor Inspector / Build Addressables 交叉检查 / Runtime 描述性异常）

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: Addressables 6.2+ 异常行为变更——加载失败从静默 null 变为抛 `InvalidKeyException` 等异常，必须 try/catch；需验证 IL2CPP 中的异常类型与 Editor 一致

**Control Manifest Rules (Foundation Layer)**:
- Required: `static event Action<T>` for all cross-system communication — source: ADR-0001
- Forbidden: Never use `Task.Result` or `Task.Wait()` on main thread — source: ADR-0007
- Forbidden: Never use `Resources.Load()` (except AudioMixer) — source: ADR-0002, ADR-0013

---

## Acceptance Criteria

*From GDD `design/gdd/data-management.md`, scoped to this story:*

- [x] GIVEN 碎片 SO 引用的插图 Asset Key 不在 Addressables 目录中，WHEN 尝试加载该插图，THEN 抛出 `DataLoadException`，包含碎片 ID 和缺失的 Asset Key
- [x] 所有 `Addressables.LoadAssetAsync<T>()` 调用包裹在 try/catch 中——Unity 6.2+ 加载失败抛异常而非返回 null
- [x] 编辑器 Inspector 验证：自定义 `MemoryFragment` Inspector 顶部显示绿色/黄色/红色验证状态点
- [x] 构建时验证：`IPreBuildValidation` 回调交叉检查 SO 中的 `AssetReference` 与实际 Addressables 目录
- [x] `Window > 回响 > Validate Fragments` 菜单批量扫描所有碎片
- [x] 运行时加载失败 → 抛出描述性异常（含碎片 ID + 缺失 Asset Key）→ 不做静默降级

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

DataLoadException 定义：
```csharp
public class DataLoadException : Exception
{
    public string AssetKey { get; }
    public string FragmentId { get; }
    
    public DataLoadException(string assetKey, string fragmentId, Exception inner)
        : base($"Failed to load asset '{assetKey}' for fragment '{fragmentId}': {inner.Message}", inner)
    {
        AssetKey = assetKey;
        FragmentId = fragmentId;
    }
}
```

三层验证实施：

1. **Editor Inspector**：自定义 `MemoryFragmentInspector` (Editor 脚本)
   - 顶部状态点：绿色 = 所有引用有效 / 黄色 = 警告（非关键引用缺失）/ 红色 = 错误（关键引用缺失）
   - 使用 `SerializedObject` API 检查 AssetReference 是否非空且目标有效

2. **Build 交叉检查**：实现 `IPreBuildValidation`
   - 遍历所有 ChapterDefinition SO → 收集所有 `AssetReference` RuntimeKey
   - 与 Addressables 目录中的实际 key 对比
   - 不匹配时输出 Build Error（阻止构建）

3. **Runtime 验证**：
   - `LoadAssetAsync` 的 catch 块包装为 `DataLoadException`，包含 assetKey + fragmentId
   - 叙事游戏中缺失碎片是不可恢复的硬错误 → Error 状态 → 显示错误信息 + 返回主菜单选项

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 异步加载引擎 — GetAsync 核心逻辑（本 Story 在其基础上添加验证层）
- Story 003: 章节预加载 — 预加载失败日志警告（不抛异常）
- 错误 UI 显示 — 由主菜单/场景管理系统负责

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: 缺失 Asset 抛出 DataLoadException
  - Given: MemoryFragment SO 的 AssetReference 指向 "art_ch1_nonexistent"（不在 Addressables 中）
  - When: 调用 `GetIllustrationAsync("art_ch1_nonexistent")` 并 await
  - Then: 抛出 `DataLoadException`，`exception.AssetKey == "art_ch1_nonexistent"`，`exception.FragmentId` 非空
  - Edge cases: AssetReference 为 null → 编辑器验证应在设计时捕获；Addressables 组被删除但 SO 引用仍存 → Build 交叉检查捕获

- **AC-2**: try/catch 覆盖所有加载路径
  - Given: 代码库中所有 `Addressables.LoadAssetAsync<T>()` 调用
  - When: Code Review 检查
  - Then: 每个调用点都被 try/catch 包裹；catch 块中包装为 `DataLoadException` 并 re-throw
  - Edge cases: 嵌套加载（加载 SO 后再加载其引用的 Sprite）→ 每层独立 try/catch

- **AC-3**: Editor Inspector 状态点
  - Given: 在 Unity Editor 中选中一个 MemoryFragment SO
  - When: Inspector 显示自定义 UI
  - Then: 顶部显示绿色/黄色/红色圆点；绿色 = 所有 AssetReference 有效；红色 = 关键引用缺失
  - Edge cases: AssetReference 指向错误类型的资产 → 红色状态的 tooltip 说明具体问题

- **AC-4**: Build 交叉检查阻止构建
  - Given: 一个 MemoryFragment SO 引用了不存在的 Addressable key
  - When: 触发 Addressables 构建
  - Then: `IPreBuildValidation` 检测到不匹配 → Build Error，阻止构建完成
  - Edge cases: 空的 AssetReference (null) → 警告（黄色），不阻止构建

- **AC-5**: 批量验证菜单
  - Given: 项目中存在 60-100 个 MemoryFragment SO
  - When: 点击 `Window > 回响 > Validate Fragments`
  - Then: 显示批量扫描结果——通过/警告/错误的碎片数量，列出每个问题碎片的路径和具体问题
  - Edge cases: 无碎片时显示 "No fragments found"；扫描中断（脚本异常）→ 显示已扫描进度

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: 
- `tests/unit/data-management/validation_test.cs` — unit tests for AC-1 (DataLoadException) and AC-4 (IPreBuildValidation mock)
- `production/qa/evidence/story-004-validation-evidence.md` — manual verification for AC-3 (Editor Inspector) and AC-5 (batch menu)

**Status**: [x] Created

**Performance**: No runtime impact — validation runs at editor/build time only. IPreBuildValidation adds ~1-3s to Addressables build step.

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 6/6 passing (4 automated tests, 2 manual verification docs)
**Deviations**: 
- `IPreBuildValidation` replaced with `IPreprocessBuildWithReport` — correct Unity 6.3 API (story flagged this as uncertain)
- `UnityAddressableLoader.LoadAssetAsync` delegates without direct try/catch — exceptions propagate via `await` to `LoadAndCacheAsync`'s catch block (functionally equivalent, noted in code review)
**Test Evidence**: Integration — `tests/unit/data-management/validation_test.cs` (14 test methods) + `production/qa/evidence/story-004-validation-evidence.md`
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (4 minor suggestions, no blocking issues)

---

## Dependencies

- Depends on: Story 001 (需要 ChapterDefinition + MemoryFragment SO), Story 002 (需要 GetAsync 引擎)
- Unlocks: None directly — 这是质量保障横切关注点
