# 第 2 周实现细节：CQRS 命令面增强

## 实现目标

本阶段围绕三个目标落地：

1. 命令统一可追踪：所有命令携带 `CorrelationId`、`CausationId`。
2. 并发异常模型清晰化：事件追加冲突使用强类型异常表达。
3. 命令能力扩展：新增设备停止与维护模式切换命令。

## 架构落点

### Application 层

- 新增命令追踪抽象：
  - `Application.Abstractions.ITraceableCommand`
  - `Application.Abstractions.CommandMetadata`
  - `Application.Abstractions.TraceableCommandExtensions`
- 新增并发异常模型：
  - `Application.Abstractions.EventStoreConcurrencyException`
- `IEventStore.AppendAsync(...)` 增加 `CommandMetadata?` 入参。

### Domain 层

- 聚合 `Device` 新增行为：
  - `Stop()`
  - `SetMaintenanceMode(bool enabled)`
- 新增领域事件：
  - `DeviceStopped`
  - `DeviceMaintenanceModeChanged`
- 启动约束增强：维护态下禁止 `Start()`。

### Infrastructure 层

- Event Store 异常从通用 `InvalidOperationException` 升级为 `EventStoreConcurrencyException`。
- PostgreSQL 事件表新增追踪元数据字段：
  - `correlation_id uuid not null`
  - `causation_id uuid null`
- 启动期 schema reconcile 自动补齐/重命名上述字段。

### API 层

- 新增统一请求头解析：`X-Correlation-Id`、`X-Causation-Id`。
- 命令类接口通过 Controller 注入 trace 元数据。
- 新增设备命令接口：
  - `POST /devices/{deviceId}/stop`
  - `POST /devices/{deviceId}/maintenance`

## 命令模型统一规范

所有命令记录类型实现 `ITraceableCommand`，并保持向后兼容构造器（未显式传入 trace 时自动生成 `CorrelationId`）。

示例约束：

- `CorrelationId`：本次业务动作全链路唯一标识。
- `CausationId`：可选，指向上游触发动作。

## 并发控制模型

`EventStoreConcurrencyException` 提供统一并发冲突语义：

- `StreamId`
- `ExpectedVersion`
- `ActualVersion`（可能为空）

触发场景：

1. 预检查版本不一致。
2. 事务提交阶段发生并发写入冲突（通过再次读取版本回填 `ActualVersion`）。

## 新增命令与状态转换

### StopDeviceCommand

- 前置条件：设备必须处于 `Running`。
- 结果：写入 `DeviceStopped`，状态回到 `Active`。

### SetDeviceMaintenanceModeCommand

- 开启维护（`enabled=true`）：
  - 设备不能已在维护态。
  - 设备不能处于 `Running`。
- 关闭维护（`enabled=false`）：
  - 设备必须当前处于 `Maintenance`。
- 结果：写入 `DeviceMaintenanceModeChanged`，状态在 `Maintenance` 与 `Active` 间切换。

## API 使用示例

### 带 trace 头调用启动命令

```http
POST /devices/{deviceId}/start
X-Correlation-Id: 3f9f3db9-59f6-4f4d-8f97-6fc8d8f5f0be
X-Causation-Id: 84c7c9b0-5d78-4bfe-a4f8-4f9ca4a3aa47
Content-Type: application/json

{
  "currentTemperature": 36.5
}
```

### 维护模式切换

```http
POST /devices/{deviceId}/maintenance
X-Correlation-Id: 072e5350-1d5a-4ab6-8f1e-f40378069fa1
Content-Type: application/json

{
  "enabled": true
}
```

## 关键文件清单

- `src/Core/Application/Abstractions/ITraceableCommand.cs`
- `src/Core/Application/Abstractions/CommandMetadata.cs`
- `src/Core/Application/Abstractions/EventStoreConcurrencyException.cs`
- `src/Core/Application/Abstractions/IEventStore.cs`
- `src/Core/Application/Devices/Commands/StopDeviceCommand*.cs`
- `src/Core/Application/Devices/Commands/SetDeviceMaintenanceModeCommand*.cs`
- `src/Core/Domain/Device.cs`
- `src/Core/Domain/Events/DeviceStopped.cs`
- `src/Core/Domain/Events/DeviceMaintenanceModeChanged.cs`
- `src/Infrastructure/EventSourcing/PostgresEventStore.cs`
- `src/Infrastructure/Data/InfrastructureDatabaseInitializer.cs`
- `src/API/Controllers/DevicesController.cs`
- `src/API/Controllers/AuthController.cs`
- `src/API/Controllers/CommandTraceHeaders.cs`
