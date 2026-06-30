# 第 3 周实现细节：Event Sourcing 深化（投影与回放）

## 目标对齐

本阶段实现了以下三项：

1. 从命令处理器内 `Upsert` 迁移到独立 `Projector`。
2. 投影幂等策略按事件版本保护。
3. 提供读模型回放入口，支持重建。

## 核心设计

### 1. 事件包络模型

新增 `StoredEvent` 作为事件存储与投影之间的标准交互对象：

- `StreamId`
- `Version`
- `DomainEvent`
- `CommandMetadata`

对应文件：
- `src/Core/Application/Abstractions/StoredEvent.cs`

### 2. Event Store 扩展

`IEventStore` 新增：

- `LoadAllAsync(CancellationToken)`：按 `stream_id + version` 顺序读取全量事件，用于回放。

对应文件：
- `src/Core/Application/Abstractions/IEventStore.cs`
- `src/Infrastructure/EventSourcing/PostgresEventStore.cs`
- `src/Infrastructure/EventSourcing/InMemoryEventStore.cs`

### 3. 独立 Projector 组件

新增抽象：

- `IDeviceEventProjector`
  - `ProjectAsync(events, ct)`
  - `ResetAsync(ct)`

对应文件：
- `src/Core/Application/Abstractions/IDeviceEventProjector.cs`

Mongo 实现：

- `MongoDeviceEventProjector`
- 处理事件：`DeviceRegistered / DeviceStarted / DeviceStopped / DeviceMaintenanceModeChanged`
- 幂等策略：若 `current.LastProjectedVersion >= event.Version` 则跳过
- 追加索引：`ix_device_status_views_last_projected_version`

对应文件：
- `src/Infrastructure/ReadModels/MongoDeviceEventProjector.cs`

## 命令处理器改造

设备命令处理器改为“只负责写事件 + 调用 Projector 投影”，不再直接操作 read repository：

- `RegisterDeviceCommandHandler`
- `StartDeviceCommandHandler`
- `StopDeviceCommandHandler`
- `SetDeviceMaintenanceModeCommandHandler`

共同流程：

1. 重建聚合并执行业务行为。
2. `AppendAsync` 持久化事件。
3. 将本次新事件封装为 `StoredEvent[]`（含版本号）。
4. 调用 `IDeviceEventProjector.ProjectAsync(...)` 更新读模型。

## 回放入口

新增应用层回放处理器：

- `RebuildDeviceReadModelHandler`
  - `LoadAllAsync`
  - `ResetAsync`
  - `ProjectAsync`

新增 API 入口：

- `POST /projections/devices/rebuild`

对应文件：
- `src/Core/Application/Devices/Projections/RebuildDeviceReadModelHandler.cs`
- `src/API/Controllers/ProjectionsController.cs`

## 数据模型兼容点

为了兼容现有 Mongo 文档，read repository 中 `LastProjectedVersion` 默认值为 `-1`，避免历史文档缺字段导致读取失败。

对应文件：
- `src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs`

## DI 注册

已在组合根注册：

- `IDeviceEventProjector -> MongoDeviceEventProjector`
- `RebuildDeviceReadModelHandler`

对应文件：
- `src/API/Program.cs`

## 当前边界与下一步建议

已完成：

- 独立投影组件
- 幂等版本保护
- 回放重建入口

建议下一步：

1. 将命令处理器中的投影调用异步化为“事件订阅者/后台 worker”模式，彻底解耦写读一致性时序。
2. 增加“重复投递回归测试 + 回放测试”专用测试项目（当前仅完成编译层面的兼容与基础行为路径）。
3. 为回放接口增加权限策略与操作审计（CorrelationId、操作者、时间）。
