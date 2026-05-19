# Story 005: JSON 序列化桥接

> **Epic**: 数据管理系统 (DataManager)
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/data-management.md`
**Requirement**: GDD Acceptance Criteria #6, #7 (SerializeState/DeserializeState)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: 数据管理策略 (序列化格式), ADR-0003: 存档序列化策略 (JSON 格式 + 版本迁移)
**ADR Decision Summary**: ADR-0002 — DataManager 提供 SerializeState<T>/DeserializeState<T> 的 JSON 序列化桥接；ADR-0003 — System.Text.Json + SHA-256 校验 + 原子写入 + 版本迁移链

**Engine**: Unity 6.3 LTS | **Risk**: LOW (纯 C# I/O，不依赖 Unity 特定 API)
**Engine Notes**: System.Text.Json 在 IL2CPP 中需验证多态类型序列化正确性；.NET 运行时版本待确认 (.NET Standard 2.1 vs .NET 8)

**Control Manifest Rules (Foundation Layer)**:
- Required: JSON + SHA-256 checksum for save files — System.Text.Json, atomic write (.tmp → .sav), version migration chain — source: ADR-0003
- Forbidden: Never serialize base SO data into save files — save only overlay + flags — source: ADR-0007
- Guardrail: Save file write: <200ms (JSON serialize + SHA-256 + atomic write) — source: ADR-0003

---

## Acceptance Criteria

*From GDD `design/gdd/data-management.md`, scoped to this story:*

- [ ] GIVEN DataManager 处于 Ready 状态，WHEN 调用 `SerializeState(playerProgress)`，THEN 返回有效的 JSON 字符串——包含所有需要持久化的运行时数据
- [ ] GIVEN 一份有效的 JSON 存档字符串，WHEN 调用 `DeserializeState<PlayerProgress>(json)`，THEN 返回正确填充的 PlayerProgress 对象
- [ ] JSON 反序列化失败时（格式无效/字段缺失/类型不匹配）→ 抛出 `DataLoadException`，包含描述性错误信息
- [ ] `SerializeState` / `DeserializeState` 使用 `System.Text.Json`，不引入第三方 JSON 库

---

## Implementation Notes

*Derived from ADR-0002 + ADR-0003:*

接口定义：
```csharp
public string SerializeState<T>(T state) where T : class;
public T DeserializeState<T>(string json) where T : class;
```

序列化配置：
- 使用 `System.Text.Json.JsonSerializer` with `JsonSerializerOptions`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `WriteIndented = false`（生产环境压缩）
- 注册自定义 `JsonConverter` 用于 Unity 类型（Vector2、Color 等）

反序列化安全：
```csharp
public T DeserializeState<T>(string json) where T : class
{
    try
    {
        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        if (result == null)
            throw new DataLoadException("deserialize", "", 
                new InvalidOperationException($"Deserialization returned null for {typeof(T).Name}"));
        return result;
    }
    catch (JsonException ex)
    {
        throw new DataLoadException("json_parse", "", ex);
    }
}
```

注意：
- 此 Story 仅实现 DataManager 端的序列化/反序列化桥接
- 存档文件的写入/读取/校验/槽位管理由存档系统 (#7) 负责
- DataManager 不关心存档文件路径——它只处理 JSON 字符串

---

## Out of Scope

*Handled by neighbouring stories or systems — do not implement here:*

- 存档槽位管理、SHA-256 校验、原子写入、版本迁移 — 由存档系统 (#7) 负责
- SaveData 结构体定义 — 由存档系统 (#7) 的 GDD 定义
- 序列化哪些运行时状态（overlay, flags, visitedFragments 等）— 由 ChangeTracker (#12) 和存档系统协调

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: SerializeState 返回有效 JSON
  - Given: 一个填充了测试数据的 PlayerProgress 对象
  - When: 调用 `SerializeState(playerProgress)`
  - Then: 返回非空 JSON 字符串；字符串可被 `JsonDocument.Parse` 解析；JSON 包含所有 PlayerProgress 字段
  - Edge cases: 空对象 → 返回 "{}"；包含 null 字段 → JSON 中字段值为 null

- **AC-2**: DeserializeState 正确还原
  - Given: 一份由 `SerializeState` 生成的有效 JSON 字符串
  - When: 调用 `DeserializeState<PlayerProgress>(json)`
  - Then: 返回的 PlayerProgress 对象与原始对象字段值完全相等
  - Edge cases: 空 JSON "{}" → 返回默认值填充的对象（非 null）；JSON 包含未知字段 → 忽略（不抛异常）

- **AC-3**: 损坏 JSON 抛出 DataLoadException
  - Given: 一份无效的 JSON 字符串 "{broken"
  - When: 调用 `DeserializeState<PlayerProgress>(json)`
  - Then: 抛出 `DataLoadException`，内部包含 `JsonException`；`DataLoadException.AssetKey == "json_parse"`
  - Edge cases: JSON 为 null → `DataLoadException`；JSON 类型正确但字段类型不匹配 → `DataLoadException`；JSON 反序列化返回 null → `DataLoadException`

- **AC-4**: 使用 System.Text.Json
  - Given: 代码库依赖
  - When: 检查 `SerializeState` 和 `DeserializeState` 实现
  - Then: 仅使用 `System.Text.Json` API；无 `Newtonsoft.Json` 引用；无第三方 JSON 库依赖

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/data-management/serialization_test.cs` — must exist and pass

**Status**: [x] Created — `tests/integration/data-management/serialization_test.cs` (15 tests)

---

## Dependencies

- Depends on: Story 002 (DataManager Ready 状态), ADR-0003 (存档序列化策略)
- Unlocks: 存档系统 (#7) — `SaveLoadManager` 消费 DataManager 的序列化桥接

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 4/4 passing (all auto-verified by 15 integration tests)
**Deviations**:
- FIXED (review): `DeserializeState` now catches all `Exception` types after `JsonException` — `NotSupportedException`/`ArgumentException` no longer leak unwrapped
- FIXED (review): Added `InnerException` assertion on type mismatch test
- FIXED (review): Added partial-field JSON deserialization test (forward-compatibility)
- ADVISORY: Empty string `""` used as `FragmentId` in serialization DLEs — should use `null`
- ADVISORY: Test file name `serialization_test.cs` — should be `data_management_serialization_test.cs` per convention
- ADVISORY: camelCase test uses string `Contains` instead of `JsonDocument` property enumeration
**Test Evidence**: `tests/integration/data-management/serialization_test.cs` — 15 test functions covering AC-1 through AC-4
**Code Review**: Complete — APPROVED (unity-specialist PASS, qa-tester GAPS addressed)
