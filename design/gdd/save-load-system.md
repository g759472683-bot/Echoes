# 存档系统 (Save/Load System)

> **Status**: In Design
> **Author**: 用户 + gameplay-programmer
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Pillar 3 (真正的后果) — 间接支撑——存档是将玩家选择固化为"不可撤销的后果"的物理载体

## Overview

存档系统是《回响》中玩家进度的持久化与恢复管理器。它封装 System.Text.Json 的序列化与反序列化，为游戏状态（章节进度、记忆变化叠加层、跨章节触发条件、玩家设置）提供统一的保存与加载接口。存档系统采用三槽位设计（两个手动存档槽 + 一个自动存档槽），每个存档包含版本号和校验和以确保完整性。

在技术层面，它是一个薄序列化层：从各系统收集可序列化的状态对象 → 聚合为 `SaveData` → `JsonSerializer.Serialize` → 写入 `Application.persistentDataPath`。加载时反向操作。存档系统不定义"什么值得保存"——每个系统自行定义其可序列化的状态 DTO。存档系统只负责"如何序列化、如何校验、如何恢复"。

## Player Fantasy

存档系统是基础设施——玩家不会"感受"到 JSON 序列化。但每次"继续游戏"的瞬间——上次合上画卷的地方墨迹还在，上次做出的选择仍在纸页上留着印记——那是存档系统无声的承诺：你的记忆不会被遗忘。

## Detailed Design

### Core Rules

**规则 1 — 存档槽位设计（2+1）**：

| 槽位 | ID | 触发方式 | 说明 |
|------|-----|---------|------|
| Manual Slot 1 | `save_01` | 玩家在暂停菜单手动保存 | 完整存档 |
| Manual Slot 2 | `save_02` | 玩家在暂停菜单手动保存 | 完整存档 |
| Auto Save | `auto_save` | 章节边界自动触发 + 关键选择后触发 | 完整存档，无玩家操作 |

- 两个手动槽让玩家可以在关键分叉点保留不同路线——不强制单一存档线
- 自动存档始终在章节边界和关键选择后覆盖写入——作为安全网
- 每个槽位对应一个独立的 `.sav` 文件
- 文件路径：`Application.persistentDataPath / Saves / [slot_id].sav`

**规则 2 — 存档数据结构（SaveData）**：

```csharp
[Serializable]
public struct SaveData
{
    public int Version;                    // 存档格式版本号
    public string Timestamp;               // ISO 8601 UTC 时间戳
    public string LocaleCode;              // 当前语言 (e.g. "zh-Hans")
    public int PlayTimeSeconds;            // 累计游玩时间

    // 章节进度
    public string CurrentChapterKey;       // 当前章节
    public string CurrentFragmentId;       // 当前碎片 ID
    public int CurrentFragmentIndex;       // 当前碎片序号
    public string[] CompletedChapters;     // 已完成章节列表
    public string[] UnlockedChapters;      // 已解锁章节列表

    // 变化叠加层 (from Memory Change Tracking #12)
    public Dictionary<string, string> ChangeOverlay; // key: "fragmentId:choiceId", value: JSON of ContentOverrides

    // 跨章节状态 (from Cross-Chapter State Tracking #16)
    public Dictionary<string, bool> CrossChapterFlags; // key: flagId, value: flag state

    // 玩家设置（仅语言和音量——其他设置由 PlayerPrefs 管理）
    public float MasterVolume;
    public float SFXVolume;
    public float MusicVolume;
    public float AmbienceVolume;

    // 结局触发条件
    public string[] TriggeredEndingConditionIds; // 已触发的结局条件 ID 列表

    // 校验和 (SHA-256 hash of all fields above except Checksum itself)
    public string Checksum;
}
```

- `Version`：存档格式版本号。当前为 `1`。如果未来存档结构改变，版本号递增，加载时按版本迁移
- `ChangeOverlay` 和 `CrossChapterFlags` 的 Key 格式由各自系统定义——存档系统不解析内容，只负责序列化
- `Checksum` 覆盖上述所有字段的 JSON 字符串的 SHA-256 哈希——检测文件损坏和篡改
- 存档系统本身不持有 `SaveData` 的"当前状态"——各系统持有自己的状态。保存时收集；加载时分发

**规则 3 — 保存流程**：

```
1. SaveManager.CollectSaveData():
   a. 从 ChapterManager 收集：currentChapterKey, currentFragmentId, currentFragmentIndex, completedChapters, unlockedChapters
   b. 从 ChangeTracker 收集：ChangeOverlay (Dictionary)
   c. 从 CrossChapterTracker 收集：CrossChapterFlags (Dictionary)
   d. 从 LocaleSettings 收集：LocaleCode
   e. 从 AudioManager 收集：MasterVolume, SFXVolume, MusicVolume, AmbienceVolume
   f. 从 EndingTracker 收集：TriggeredEndingConditionIds
   g. 设置：Version=1, Timestamp=DateTime.UtcNow.ToString("O"), PlayTimeSeconds
2. SaveData.Checksum = ComputeSHA256(serialized json of all other fields)
3. string json = JsonSerializer.Serialize(saveData)  // System.Text.Json
4. File.WriteAllTextAsync($"{saveDir}/{slotId}.sav", json)
5. 更新主菜单的"继续游戏"时间戳显示
```

- 保存是异步操作（`async Task SaveAsync(string slotId)`）
- 保存期间不阻塞游戏——但保存完成前不允许退出游戏（显示"正在保存…"提示）
- 保存失败（磁盘满、权限错误）→ 显示"保存失败"提示 + 保留旧存档不变 + 允许重试

**规则 4 — 加载流程**：

```
1. SaveManager.LoadAsync(string slotId):
   a. string json = await File.ReadAllTextAsync(filePath)
   b. SaveData saveData = JsonSerializer.Deserialize<SaveData>(json)
   c. ValidateChecksum(saveData) → 如果失败: 抛出 SaveCorruptedException
   d. 检查 saveData.Version → 如果 != 当前版本: 执行版本迁移
2. SaveManager.RestoreSaveData(saveData):
   a. LocaleSettings.SelectedLocale = saveData.LocaleCode → 触发言语切换
   b. AudioManager.SetVolume("master", saveData.MasterVolume), etc. → 恢复音量
   c. ChangeTracker.RestoreFromSave(saveData.ChangeOverlay) → 恢复变化叠加层
   d. CrossChapterTracker.RestoreFromSave(saveData.CrossChapterFlags) → 恢复跨章节标记
   e. EndingTracker.RestoreFromSave(saveData.TriggeredEndingConditionIds) → 恢复结局条件
3. ChapterManager.LoadAndRestore(saveData) → 恢复章节进度 + 加载碎片场景
   （ChapterManager 内部调用 SceneManager.TransitionToFragmentAsync——存档系统不直接操作场景）
```

- 加载是异步操作——返回 `Task`，调用方 Await
- 加载期间显示墨色遮罩（由场景管理器 SceneFader 处理）
- 校验和失败 → 提示"存档文件已损坏"，不尝试部分恢复
- 版本不匹配 → 执行版本迁移逻辑（见规则 5）

**规则 5 — 校验和验证与版本迁移**：

**校验和**：
- 计算方式：`SHA256(JsonSerializer.Serialize(saveData, options: exclude Checksum field))`
- 保存时计算并写入。加载时重新计算并比对
- 不匹配 → `SaveCorruptedException`。显示"存档文件已损坏"（本地化 Key: `system.save.corrupted`）

**版本迁移**：
- `SaveData.Version` 字段标识存档格式版本。当前版本 = `1`
- 当存档结构未来改变时（如新增字段），递增版本号
- 加载时：`if (saveData.Version < currentVersion)` → 执行从 `Version` 到 `currentVersion` 的逐步迁移
- 迁移函数链：`Migrate_V1_to_V2()` → `Migrate_V2_to_V3()` → ...
- 迁移后的 SaveData 在下次保存时写为新版本格式

**规则 6 — 自动存档触发点**：

| 触发点 | 时机 | 槽位 |
|--------|------|------|
| 章节开始 | 新章节的第一个碎片展示后 | auto_save |
| 关键选择 | 玩家做出触发 ChangeOverlay 修改的选择后 | auto_save |
| 章节完成 | 章节最后一个碎片展示后 | auto_save |
| 应用退出 | `OnApplicationQuit`（如果当前在游戏中） | auto_save |

- 自动存档在后台静默完成——不显示"保存中"提示，不弹出通知
- 关键选择触发自动存档时，距上一个自动存档 < 30 秒则跳过（防抖——防止连续快速选择导致频繁写入）

**规则 7 — 存档文件的物理安全**：

- 文件操作使用原子写入模式：
  1. 序列化到临时文件：`[slotId].sav.tmp`
  2. `File.Move(tmpPath, finalPath, overwrite: true)` — 原子替换
- 这确保：如果在序列化过程中崩溃/断电，旧存档文件完好无损——不会出现半写入的损坏存档
- 保存失败处理：
  - 磁盘满 → `IOException` 捕获，显示"磁盘空间不足"提示
  - 权限错误 → `UnauthorizedAccessException` 捕获，显示"无法写入存档文件"提示
  - 序列化异常 → 显示内部错误提示——这不应发生（所有数据为简单类型）

**规则 8 — 存档与 PlayerPrefs 的职责分工**：

| 数据 | 存储位置 | 原因 |
|------|---------|------|
| 语言设置 | 存档 (.sav) + PlayerPrefs | 存档恢复时保持语言。PlayerPrefs 让主菜单在新游戏前也能显示正确语言 |
| 音量设置 | 存档 (.sav) + PlayerPrefs | 同上——存档恢复时恢复音量。PlayerPrefs 让主菜单在新游戏前也有音量设置 |
| 输入绑定 | PlayerPrefs only | 输入绑定是全局设备偏好，不属于游戏存档——不应该随不同存档槽变化 |
| 游戏进度 | 存档 (.sav) only | 游戏进度与存档槽绑定——不同槽位有不同进度 |

### States and Transitions

| 状态 | 描述 | 触发 |
|------|------|------|
| **Idle** | 无保存或加载操作进行中 | 默认状态 |
| **Saving** | 正在序列化并写入存档文件 | SaveAsync() 调用 |
| **Loading** | 正在读取、反序列化、分发存档数据 | LoadAsync() 调用 |
| **Error** | 上次操作失败（校验和不匹配、IO 错误、版本不兼容） | 保存或加载异常 |

保存和加载是瞬时操作——在 PC 上 JSON 序列化 200KB 数据 + SSD 写入 < 50ms。状态仅用于防止并发操作（保存期间不可以加载，加载期间不可以保存）。

### Interactions with Other Systems

| 方向 | 系统 | 数据 | 说明 |
|------|------|------|------|
| 上游 | **Data Management (#2)** | SerializeState / DeserializeState 接口 | Data Management 提供 JSON 序列化能力——但存档系统直接使用 System.Text.Json，不通过 DataManager。Data Management 的 SerializeState 接口与存档系统的 SaveData 序列化互不依赖 |
| 上游 | **Chapter Management (#15)** | CurrentChapterKey, CurrentFragmentId, CompletedChapters, UnlockedChapters | 章节进度数据的来源 |
| 上游 | **Memory Change Tracking (#12)** | ChangeOverlay Dictionary | 变化叠加层的来源 |
| 上游 | **Cross-Chapter State (#16)** | CrossChapterFlags Dictionary | 跨章节标记的来源 |
| 上游 | **Multi-Ending (#14)** | TriggeredEndingConditionIds | 结局条件的来源 |
| 上游 | **Localization (#4)** | SelectedLocale.Identifier.Code | 当前语言设置的来源 |
| 上游 | **Audio (#3)** | 音量值 | 音量设置的来源 |
| 下游 | **Main Menu (#19)** | 存档列表（槽位 + 时间戳） | 主菜单的"继续游戏"和"加载游戏"功能 |

## Formulas

存档系统不包含数学公式。校验和计算使用标准 SHA-256——不涉及自定义数学逻辑。

## Edge Cases

- **如果玩家在选择分叉点保存到槽 1，然后加载槽 2 的旧存档**：两个槽位独立——加载槽 2 后游戏进度回到分叉点之前。槽 2 的 ChangeOverlay 不包含槽 1 的选择后果。（如果后来加载槽 1，槽 1 的后果仍然存在。）
- **如果保存期间磁盘空间不足**：临时文件写入可能部分完成。`File.Move` 失败 → 捕获 `IOException` → 旧存档文件完整保留 → 显示"磁盘空间不足"提示
- **如果自动存档触发时玩家正在手动保存**：Saving 状态下忽略所有保存请求——包括自动存档。当前保存完成后，自动存档的条件已经过去——不在之后补触发
- **如果玩家在 Loading 状态期间退出应用**：加载操作不可取消——Unity 的 `OnApplicationQuit` 等待当前帧完成。加载 < 100ms，不会造成长时间等待
- **如果存档文件从外部被删除（用户手动清理）**：主菜单的存档列表在扫描目录时发现文件不存在——该槽位显示为"空槽"
- **如果旧版本存档在新版本游戏中加载**：版本迁移链逐个执行。如果某个迁移步骤失败（数据格式不可修复），显示"存档与新版本不兼容"

## Dependencies

**硬依赖**：无。存档系统是 Foundation 层系统。它串联多个 Gameplay 系统的状态，但在代码层面不依赖它们——SaveData 是纯数据 DTO，各系统通过接口填充和被恢复。

**下游系统**：Main Menu (#19, 硬依赖)

存档系统在逻辑上收集以下系统的数据，但这些系统不感知存档的存在：
Chapter Management (#15), Memory Change Tracking (#12), Cross-Chapter State (#16), Multi-Ending (#14), Localization (#4), Audio (#3)

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 说明 |
|------|--------|----------|------|
| Save Format Version | 1 | 只增不减 | 存档格式版本号 |
| Max Save Slots | 2 manual + 1 auto | 固定值 | 槽位总数 |
| Auto Save Debounce | 30s | 15–120s | 两次自动存档之间的最小间隔 |

## Visual/Audio Requirements

存档系统不产生视觉或音频输出。保存/加载期间的视觉过渡（遮罩）由 Scene Management (#6) 管理。音频反馈（保存完成提示音，如果有）由 Interaction Feedback (#18) 管理。

## UI Requirements

存档系统不包含 UI。存档相关 UI 属于 Main Menu (#19)——包含槽位列表、"继续游戏"按钮、保存/加载确认对话框。

## Acceptance Criteria

- **GIVEN** 游戏进行中（章节 1 碎片 3），**WHEN** 玩家在暂停菜单选择"保存游戏"到槽 1，**THEN** 存档文件写入 `[persistentDataPath]/Saves/save_01.sav`。校验和字段非空。保存时间 < 200ms
- **GIVEN** 槽 1 有存档，**WHEN** 玩家关闭游戏重新启动并从主菜单加载槽 1，**THEN** 恢复到章节 1 碎片 3，语言和音量设置与保存时一致
- **GIVEN** 玩家做出关键选择（如改变了一段记忆），**WHEN** 选择完成，**THEN** 自动存档触发——`auto_save.sav` 包含选择后的 ChangeOverlay
- **GIVEN** 存档文件的 Checksum 与内容不匹配（文件损坏），**WHEN** 尝试加载，**THEN** 显示"存档文件已损坏"提示，不尝试部分恢复
- **GIVEN** 玩家在章节边界，**WHEN** 章节转换完成，**THEN** 自动存档在后台触发——不显示通知
- **GIVEN** 存档格式版本为 1、游戏版本要求版本 2，**WHEN** 加载版本 1 存档，**THEN** 执行版本迁移——迁移后数据正确，下次保存写为版本 2 格式
- **GIVEN** 保存进行中（Saving 状态），**WHEN** 玩家再次触发保存或加载，**THEN** 操作被忽略——不并发执行

## Revision Notes

- **W4 (2026-05-19)**: ✅ 已验证 SaveOrchestrator 实现：加载路径使用 ChapterManager.LoadAndRestore()，内部调用 SceneManager.TransitionToFragmentAsync()。SaveManager 不直接调用 SceneManager。GDD 第113-114行已正确记录此路径。

## Open Questions

- **存档文件加密**：MVP 是否需要加密存档文件？加密可防止玩家手动修改 JSON 存档，但对单人叙事游戏而言玩家篡改进度只影响自己——是否有必要？如果加入成就系统（#23，Full Vision），可能需要加密防止作弊
- **跨平台存档同步**：PC Steam/Epic 双平台是否需要 Steam Cloud 存档同步？Steamworks.NET 的 Auto-Cloud 功能对 `.sav` 文件自动生效——不需要额外开发。但需验证 `Application.persistentDataPath` 在 Steam Cloud 配置中的路径映射
