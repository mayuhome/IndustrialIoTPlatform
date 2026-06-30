第 1 周：DDD + 聚合与不变量

目标：把业务规则从接口层彻底收回聚合。
任务：
识别 Device 聚合不变量（例如温度阈值、重复启动、维护态限制）。
把 DeviceCode、TemperatureThreshold 升级为值对象。
给每条不变量补领域测试。
产出：
值对象类与测试。
更新后的聚合行为文档（可写在 skill references）。
第 2 周：CQRS 命令面增强

目标：命令模型可扩展、可追踪、可并发控制。
任务：
为命令统一引入 CorrelationId、CausationId。
给 Append 增加更清晰的并发异常模型。
新增停止设备、维护模式切换等命令。
产出：
命令处理器模式统一。
命令侧测试覆盖关键状态转换。
第 3 周：Event Sourcing 深化（投影与回放）

目标：让读模型真正独立演进。
任务：
从处理器内 Upsert 迁移到独立 Projector（订阅事件更新 read model）。
定义投影幂等策略（按事件序号或版本保护）。
提供回放入口（重建 read model）。
产出：
Projector 组件。
投影回放测试与重复投递测试。
第 4 周：架构治理与生产化

目标：让“好架构”可持续执行，不依赖个人自觉。
任务：
扩展 Architecture Tests：命名约定、目录约束、禁用跨层引用。
增加集成测试：事件追加、读侧最终一致性。
为错误路径增加统一 ProblemDetails 映射。
产出：
CI gate（build + test + architecture test）。
架构决策记录（ADR）2-3 篇。

当前数据基础设施阶段已新增：
- PostgreSQL 真实事件存储
- MongoDB 真实读模型存储
- Redis 查询缓存
- Docker Compose 本地依赖编排
- API 启动配置校验

对应学习文档：
- [docs/database-prepare.md](docs/database-prepare.md)
- [docs/infra-setup.md](docs/infra-setup.md)

下一优先级建议：
1. 为 PostgreSQL 引入正式 migration 机制。
2. 将读模型更新迁移为独立 Projector。
3. 增加真实存储集成测试。