# 第 5 周实现细节：模拟数据系统

## 实现目标

本阶段实现一个可控、可重置、可持续运行的模拟数据系统，用于开发和测试阶段的数据支撑。

## 架构落点

### Application 层

新增模拟数据领域的应用服务、命令与查询：

- `SimulatedDeviceView`
- `SimulationOptions`
- `ISimulatedDeviceRepository`
- `ISimulatedDeviceStateGenerator`
- `ISimulatedDeviceSimulationService`
- `GenerateSimulatedDevicesCommand`
- `ResetSimulatedDevicesCommand`
- `GetAllSimulatedDevicesQuery`
- 对应 handler

### Infrastructure 层

新增 Mongo 仓储：

- `MongoSimulatedDeviceRepository`
  - 负责模拟设备数据的持久化、查询与重置
  - 维护 `DeviceCode` 唯一索引

新增状态生成器：

- `DefaultSimulatedDeviceStateGenerator`
  - 负责初始模拟设备生成
  - 负责单步温度漂移与状态演进

新增后台 worker（API 长连接推送）：

- `SimulationStreamBackgroundService`
  - 启动后按配置周期执行
  - 初始化时一次性获取所有 `deviceId`
  - 自动种子模拟设备
  - 每秒为每个 `deviceId` 生成一条数据
  - 通过 SignalR 长连接实时广播给订阅组件

### API 层

新增管理接口：

- `GET /simulation/devices`
- `POST /simulation/devices/generate`
- `POST /simulation/devices/reset`
- `POST /simulation/devices/advance`

新增长连接实时接口：

- Hub 路径：`/hubs/simulation`
- 推送方法：`ReceiveSimulationData`
- 推送模型：`SimulationDataPoint`

## 模拟数据结构

每条模拟设备记录包含：

- `DeviceId`
- `DeviceCode`
- `Status`
- `CurrentTemperature`
- `MaxTemperatureThreshold`
- `LastUpdatedUtc`
- `Sequence`

约束：

- `DeviceCode` 唯一
- 温度漂移按照配置范围生成
- `Sequence` 每次推进自增，用于观察投影/刷新节奏

## 实时生成策略

后台 worker 行为：

1. 启动时检查是否启用模拟。
2. 若集合为空，则按 `AutoSeedCount` 自动生成初始设备。
3. 初始化阶段读取全量 `deviceId` 列表并缓存。
4. 每隔 `UpdateIntervalSeconds` 按 `deviceId` 集合推进一次状态。
5. 温度在 `TemperatureDriftMin/Max` 范围内随机变化。
6. 当温度超过阈值时状态标记为 `Error`。
7. 每次推进后通过 SignalR 广播数据，供其他组件消费。

默认频率：1 秒一次。

## 手动控制方式

开发者可以通过 API：

- 快速生成一批模拟设备
- 清空并重置模拟数据
- 手动触发一次推进
- 查询当前模拟设备列表

这满足“命令行工具或 API 接口”的需求中的 API 路线，并补充了长连接推送路线。

## 配置项

建议配置节点：

- `Simulation:Enabled`
- `Simulation:AutoSeedCount`
- `Simulation:DeviceCodePrefix`
- `Simulation:UpdateIntervalSeconds`
- `Simulation:StartingTemperatureMin`
- `Simulation:StartingTemperatureMax`
- `Simulation:TemperatureDriftMin`
- `Simulation:TemperatureDriftMax`
- `Simulation:MaxTemperatureThresholdMin`
- `Simulation:MaxTemperatureThresholdMax`

## 关键实现文件

- `src/Core/Application/Simulation/*`
- `src/Infrastructure/Simulation/*`
- `src/API/Controllers/SimulationController.cs`
- `src/API/Realtime/SimulationHub.cs`
- `src/API/Realtime/SimulationStreamBackgroundService.cs`
- `src/API/Program.cs`

## 设计取舍

- 使用 Mongo 保存模拟数据，避免污染真实设备读模型集合。
- 使用 API 层 SignalR + 后台 worker 实现“项目运行时实时数据”要求。
- 使用版本序列 `Sequence` 作为简单可观察的推进标识。

## 下一步建议

1. 为模拟系统增加专用测试，覆盖生成、重置、推进与重复生成约束。
2. 为后台 worker 增加节流/抖动参数，方便更真实地模拟批量设备波动。
3. 若后续需要命令行工具，可以基于同一组 Application handler 再接一层 CLI。
