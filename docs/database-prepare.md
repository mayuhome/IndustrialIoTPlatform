# 下一阶段开发文档（数据准备）

## 1. 阶段目标

本阶段聚焦“数据基础设施可用 + 配置治理可控”，为后续 Event Sourcing、投影和集成测试提供稳定底座。

核心目标：
- 本地一键拉起 PostgreSQL + Redis + MongoDB（Docker Compose）。
- 按职责拆分数据用途：
	- PostgreSQL：事件存储、事务性元数据。
	- Redis：缓存、分布式锁、幂等键。
	- MongoDB：读模型投影（查询侧文档）。
- 建立统一配置放置规范，明确“可提交配置”和“敏感配置”的边界。
- 打通健康检查、初始化脚本、数据持久化卷和基础观测。

## 2. 数据组件职责与边界

### PostgreSQL（写侧主存储）
- 用途：事件流表、快照表、幂等命令日志、Outbox。
- 原则：
	- 写侧真相来源优先放 PostgreSQL。
	- 采用版本号实现乐观并发。
	- 迁移统一使用数据库迁移工具（例如 EF Core Migration 或 Flyway，二选一并固定）。

### Redis（高性能辅助存储）
- 用途：
	- 查询缓存。
	- 分布式锁（必要时）。
	- 命令幂等去重键（短 TTL）。
- 原则：
	- Redis 不承载最终一致性真相数据。
	- 所有键名统一前缀：环境名:业务域:资源。
	- 强制设置 TTL，避免无限增长。

### MongoDB（读侧投影存储）
- 用途：CQRS 查询模型（面向 API 查询优化）。
- 原则：
	- 只存读侧模型，不直接承接写事务。
	- 投影处理器保证幂等（事件版本防重）。
	- 按查询模式建索引，而不是按关系模型建表。

## 3. 环境与配置放置规范（重点）

## 3.1 配置分层

1. 仓库可提交默认配置（无密钥）
- [src/API/appsettings.json](src/API/appsettings.json)
- [src/API/appsettings.Development.json](src/API/appsettings.Development.json)
- [docker-compose.yml](docker-compose.yml)

2. 本地开发私有配置（不提交）
- 根目录 .env.local
- 根目录 .env.secrets
- 通过 .gitignore 排除

3. 生产与测试环境配置（不在仓库明文保存）
- CI/CD Secret Store（GitHub Secrets、Azure Key Vault、AWS Secrets Manager 等）
- 容器编排平台注入环境变量

## 3.2 推荐目录结构

- docker/
- docker/postgres/init/
- docker/mongo/init/
- docker/redis/
- config/
- config/env/
- config/env/.env.example
- docs/

说明：
- 初始化脚本和容器专用配置放 docker 目录。
- 环境变量模板放 config/env/.env.example。
- 真实密钥只存在本机或密钥管理系统，不进入 Git。

## 3.3 连接配置约定

建议统一使用 ASP.NET Core 环境变量映射规则：

- ConnectionStrings__Postgres
- ConnectionStrings__Mongo
- ConnectionStrings__Redis
- EventStore__Schema
- Projection__MongoDatabase
- Cache__DefaultTtlSeconds

说明：
- appsettings 只保留安全默认值和占位符。
- 连接串、账号、密码统一由环境变量覆盖。

## 3.4 密钥治理规则

- 严禁将数据库密码、令牌、证书私钥写入：
	- appsettings.*
	- docker-compose.yml
	- docs 文档
- 本地开发可使用：
	- dotnet user-secrets（API 项目）
	- .env.secrets（仅本机）
- CI 环境必须使用 Secret 管理器注入。

## 4. Docker Compose 最佳实践清单

- 使用具名卷持久化三类数据，容器重建不丢数。
- 每个服务配置健康检查，API 依赖健康状态启动。
- 使用独立内部网络，默认不暴露非必要端口。
- 对外暴露端口仅限开发所需。
- Redis 开启持久化策略并设置内存淘汰策略。
- PostgreSQL 指定时区、字符集，并限制 max connections。
- MongoDB 启用认证和初始化用户。
- Compose 文件中不写明文密钥，改为环境变量注入。

## 5. 下一阶段里程碑（2 周）

### 第 1 周：基础设施落地
- 完成 docker-compose.yml 四服务定义与健康检查。
- 增加 postgres/mongo 初始化脚本目录。
- 明确环境变量模板与 .gitignore 规则。
- API 启动时读取三种连接配置并输出脱敏日志。

交付物：
- [docker-compose.yml](docker-compose.yml)
- docs/infra-setup.md
- config/env/.env.example

### 第 2 周：与当前应用集成
- PostgreSQL：事件流读写接口替换内存实现（已完成最小可用版）。
- MongoDB：查询侧投影仓储替换内存实现（已完成基础版）。
- Redis：引入查询缓存（已完成基础版），后续补命令幂等键。
- 增加集成测试：容器化依赖 + 最小业务闭环。

交付物：
- Infrastructure 下三类生产实现（已完成当前最小闭环）。
- 集成测试工程（或现有 tests 扩展）。
- docs/data-operations.md（备份、恢复、清理策略）。

## 5.1 当前已完成的真实存储替换

1. 事件存储已替换为 PostgreSQL：
- [src/Infrastructure/EventSourcing/PostgresEventStore.cs](src/Infrastructure/EventSourcing/PostgresEventStore.cs)

2. 查询侧已替换为 MongoDB：
- [src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs](src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs)

3. 查询缓存已接入 Redis：
- [src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs](src/Infrastructure/ReadModels/MongoCachedDeviceStatusReadRepository.cs)

4. API 依赖注入已切换到真实存储：
- [src/API/Program.cs](src/API/Program.cs)

## 5.2 下一步建议

1. 为 PostgreSQL 事件表引入正式迁移机制，替代运行时惰性建表。
2. 将读模型更新从 Command Handler 中剥离为独立 Projector。
3. 为 Redis 增加命令幂等键和缓存失效策略文档。
4. 增加基于 Docker 的真实存储集成测试。

## 6. 验收标准

- 本地执行一次命令即可启动全部依赖。
- API 成功连接 PostgreSQL、Redis、MongoDB 并可健康检查通过。
- 关键配置来源可追踪：默认值、覆盖值、敏感值分离清晰。
- 仓库扫描无明文密钥。
- 至少 1 条写侧命令与 1 条查询链路通过真实存储集成测试。

## 7. 风险与防护

- 风险：本地端口冲突。
	- 防护：统一端口清单并允许 .env.local 覆盖。
- 风险：投影重复消费导致脏数据。
	- 防护：Mongo 投影引入事件版本幂等保护。
- 风险：环境配置漂移。
	- 防护：通过 .env.example + 启动前配置校验。
- 风险：开发环境误用生产密钥。
	- 防护：环境标识强校验，禁止非 Dev 环境连接本地 Compose。

## 8. 建议立即执行的三个动作

1. 在本周内补齐 docker-compose.yml 的四服务与健康检查。
2. 新增 config/env/.env.example 并更新 .gitignore 规则。
3. 在 API 启动阶段加入配置校验（缺少关键连接串直接失败并提示）。