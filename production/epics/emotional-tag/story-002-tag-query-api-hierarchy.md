# Story 002: 标签查询 API + 层级解析

> **Epic**: 情感标签系统 (EmotionalTagSystem)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/emotional-tag-system.md`
**Requirement**: `TR-emotional-tag-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: SO 不可变 + Overlay
**ADR Decision Summary**: 查询通过 Catalog（只读 SO）+ Fragment Data 进行；GetCurrentState 返回不可变 snapshot

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: 查询为纯函数（无状态变更），零 GC 分配（List<T> 可缓存/复用）

**Control Manifest Rules (Feature Layer)**:
- Required: GetCurrentState returns immutable struct — consumers cannot modify — source: ADR-0007
- Forbidden: Never use `Task.Result` in query methods — source: ADR-0007

---

## Acceptance Criteria

*From GDD `design/gdd/emotional-tag-system.md`, scoped to this story:*

- [ ] GIVEN 碎片 A 有 3 个标签（`nostalgia` weight=0.8, `peace` weight=0.4, `loss` weight=0.6），WHEN 调用 `GetTagsForFragment("frag_A")`，THEN 返回 3 个标签及各自权重。`GetPrimaryTag("frag_A")` 返回 IsPrimary=true 的那个。

- [ ] GIVEN `nostalgia` 的父标签是 `love`，WHEN 调用 `QueryFragmentsByTag("love")`，THEN 返回所有标记了 `love` **和** 所有标记了 `nostalgia` 的碎片 ID 列表。`nostalgia` 碎片的权重不受父标签查询影响。

- [ ] GIVEN 查询请求一个不存在的 TagId，WHEN 调用任何查询方法，THEN 返回空集合（不抛出异常），记录 Debug.LogWarning。

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

查询 API 全部实现为纯函数——输入 fragmentId/tagId → 输出结果。不修改任何状态。

```csharp
public class EmotionalTagSystem : IEmotionalTagSystem
{
    private EmotionalTagCatalog _catalog;
    private IDataManager _dataManager;  // for fragment tag data

    // 规则 2: 标签层级 — QueryFragmentsByTag("love") 返回 love + nostalgia 的碎片
    public List<string> QueryFragmentsByTag(string tagId, float minWeight = 0.0f)
    {
        var allTagIds = new HashSet<string> { tagId };
        // 递归收集子标签（最多 1 层）
        foreach (var tag in _catalog.Tags)
        {
            if (tag.ParentTagId == tagId)
                allTagIds.Add(tag.TagId);
        }
        // 查询所有碎片中匹配 allTagIds 的
        ...
    }
}
```

5 个查询方法全部实现：
1. `GetTagsForFragment(string fragmentId)` → `List<EmotionalTag>`
2. `GetPrimaryTag(string fragmentId)` → `EmotionalTag?`
3. `QueryFragmentsByTag(string tagId, float minWeight = 0.0)` → `List<string>`
4. `GetTagCategory(string tagId)` → `Category`
5. `GetRelatedTags(string tagId)` → `List<string>`

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: Catalog SO 定义和加载（本故事依赖 Catalog 已加载）
- Story 003: ModifyTagWeight overlay 合并到运行时权重（本故事返回 BaseWeight，不应用 overlay）
- 标签相似度矩阵计算 — 归 ADR-0009（关联引擎）

---

## QA Test Cases

- **AC-1**: GetTagsForFragment + GetPrimaryTag
  - Given: 碎片 frag_A 有 3 个标签（nostalgia w=0.8, peace w=0.4, loss w=0.6），其中 nostalgia IsPrimary=true
  - When: 调用 GetTagsForFragment("frag_A")，然后调用 GetPrimaryTag("frag_A")
  - Then: GetTagsForFragment 返回 3 项，权重匹配；GetPrimaryTag 返回 TagId="nostalgia"
  - Edge cases: 没有 IsPrimary 标签的碎片 → GetPrimaryTag 返回 null

- **AC-2**: QueryFragmentsByTag 层级包含
  - Given: nostalgia.ParentTagId = "love"；frag_X 有标签 love，frag_Y 有标签 nostalgia
  - When: 调用 QueryFragmentsByTag("love")
  - Then: 返回包含 frag_X 和 frag_Y 的列表（子标签自动包含）
  - Edge cases: 带有 love 的碎片权重在 nostalgia 子查询中不受影响；只有子标签的碎片不因父查询而改变权重

- **AC-3**: 无效 TagId
  - Given: Catalog 中没有 "nonexistent" 标签
  - When: 调用 GetTagCategory("nonexistent") 或 QueryFragmentsByTag("nonexistent")
  - Then: 返回空集合/默认值；记录 Debug.LogWarning；不抛出异常
  - Edge cases: 空字符串/null TagId → 同上述处理

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/emotional-tag/tag_query_test.cs` — must exist and pass

**Status**: [x] Created — `tests/unit/emotional-tag/tag_query_test.cs` (28 tests)

---

## Dependencies

- Depends on: Story 001 (Catalog SO 必须已定义并加载)
- Unlocks: Story 003 (运行时权重叠加需要查询 API)

---

## Completion Notes

**Completed**: 2026-05-18
**Criteria**: 3/3 auto-verified (all 5 query methods implemented, 28 tests)
**Deviations**: None
**Test Evidence**: `tests/unit/emotional-tag/tag_query_test.cs` — 28 tests covering AC-1 (GetTagsForFragment/GetPrimaryTag), AC-2 (QueryFragmentsByTag hierarchy), AC-3 (invalid TagId warnings)
**Code Review**: Skipped (lean mode)
