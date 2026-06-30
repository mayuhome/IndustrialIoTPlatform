# 数据基础设施配置与启动指南

本文件用于学习和执行下一阶段的数据准备工作，涵盖 PostgreSQL、Redis、MongoDB 与 API 配置。

## 当前代码中的真实存储落位

本仓库当前已经不再使用运行时内存存储，生产路径已切换为：
- PostgreSQL：事件存储（Event Store）
- MongoDB：设备状态读模型（Read Model）
- Redis：设备状态查询缓存（Read Cache）

对应实现文件：
- [src/Infrastructure/EventSourcing/PostgresEventStore.cs](src/Infrastructure/EventSourcing/PostgresEventStore.cs)
- [src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs](src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs)
- [src/API/Program.cs](src/API/Program.cs)

## 1. 配置文件放置规则

1. 可提交默认配置（无敏感信息）
- [src/API/appsettings.json](src/API/appsettings.json)
- [src/API/appsettings.Development.json](src/API/appsettings.Development.json)
- [config/env/.env.example](config/env/.env.example)
- [docker-compose.yml](docker-compose.yml)

2. 本地私有配置（不提交）
- .env.local
- .env.secrets

3. 生产配置（不在仓库明文保存）
- CI/CD Secret Store 或云密钥管理系统

## 2. 默认配置说明

### PostgreSQL
- Host: postgres（容器内）/ localhost（宿主机）
- Port: 5432
- Database: iot_platform
- Username: iot_user
- Password: change_me_postgres

### Redis
- Host: redis（容器内）/ localhost（宿主机）
- Port: 6379
- Password: change_me_redis

### MongoDB
- Host: mongodb（容器内）/ localhost（宿主机）
- Port: 27017
- Database: iot_readmodels
- Username: iot_mongo_user
- Password: change_me_mongo

## 3. 启动步骤（开发环境）

1. 复制环境变量模板
- 将 [config/env/.env.example](config/env/.env.example) 复制为 .env.local
- 替换密码为本机值

2. 启动依赖
- 执行 `docker compose --env-file .env.local up -d`

3. 检查容器健康状态
- 执行 `docker compose ps`
- 三个服务应为 healthy

4. 启动 API
- API 启动时会校验关键配置：
  - ConnectionStrings:Postgres
  - ConnectionStrings:Redis
  - ConnectionStrings:Mongo
  - EventStore:Schema
  - Projection:MongoDatabase

## 4. 最佳实践

- 禁止将真实密码写入 appsettings 和 docker-compose。
- 使用环境变量覆盖默认值。
- 仅将 .env.example 提交到仓库。
- 如果连接串缺失，启动直接失败（Fail Fast），避免错误运行。

## 4.1 当前实现细节

### PostgreSQL
- 当前用于持久化事件流。
- API 首次访问事件存储时会自动确保表存在。
- 表名：`{schema}.device_events`
- 主键：`(stream_id, version)`
- 并发控制：按 `expectedVersion` + PostgreSQL Serializable 事务。

### MongoDB
- 当前用于持久化 `device_status_views` 集合。
- 文档按 `DeviceId` 唯一存储。
- 已为 `DeviceCode` 创建唯一索引，便于后续按设备编码查询。

### Redis
- 当前用于缓存 `GetDeviceStatus` 查询结果。
- 缓存键格式：`devices:status:{deviceId}`
- TTL 来源：`Cache:DefaultTtlSeconds`

## 4.2 当前阶段的技术权衡

- 事件表当前由应用启动后惰性创建，适合开发阶段快速闭环。
- 更成熟的生产方案应逐步迁移到独立迁移机制（Flyway / DbUp / EF Migration 任选一种）。
- 当前读模型仍由命令处理器直接 Upsert；下一阶段建议迁移为独立 Projector。

## 5. 与当前代码关系

- 配置默认值来源：
  - [src/API/appsettings.json](src/API/appsettings.json)
  - [src/API/appsettings.Development.json](src/API/appsettings.Development.json)
- 容器编排来源：
  - [docker-compose.yml](docker-compose.yml)
- 启动配置校验逻辑：
  - [src/API/Program.cs](src/API/Program.cs)
- 事件存储实现：
  - [src/Infrastructure/EventSourcing/PostgresEventStore.cs](src/Infrastructure/EventSourcing/PostgresEventStore.cs)
- 读模型与缓存实现：
  - [src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs](src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs)
