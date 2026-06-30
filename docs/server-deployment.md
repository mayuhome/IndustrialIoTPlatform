# 服务器部署模式

本文件定义当前推荐的服务器部署方式：
- API 部署在服务器主机上
- PostgreSQL / Redis / MongoDB 通过 Docker Compose 运行在同一台服务器上
- 数据服务仅绑定服务器本机回环地址 `127.0.0.1`

## 1. 架构模式

当前推荐模式：
- API：systemd / dotnet 进程运行在服务器宿主机
- PostgreSQL：Docker 容器
- Redis：Docker 容器
- MongoDB：Docker 容器

该模式下，API 连接信息统一为：
- PostgreSQL：`localhost:5433`
- Redis：`localhost:6379`
- MongoDB：`localhost:27017`

原因：
- 数据服务只暴露给服务器本机，减少公网暴露面。
- API 和数据库部署在同一台服务器上时，不需要通过公网 IP 回环访问自己。

## 2. 配置文件

服务器模式建议使用：
- [src/API/appsettings.Production.json](src/API/appsettings.Production.json)
- [config/env/.env.server.example](config/env/.env.server.example)
- [docker-compose.yml](docker-compose.yml)

## 3. 启动步骤

1. 在服务器上复制环境变量模板
- 将 [config/env/.env.server.example](config/env/.env.server.example) 复制为 `.env.server.local`
- 填入真实用户名和密码

2. 启动数据服务
- 执行 `docker compose --env-file .env.server.local up -d`

3. 启动 API
- 设置 `ASPNETCORE_ENVIRONMENT=Production`
- 运行 API，使其加载 [src/API/appsettings.Production.json](src/API/appsettings.Production.json)

## 4. 安全建议

- 不要将 PostgreSQL / Redis / MongoDB 直接暴露到公网。
- 默认只绑定 `127.0.0.1`，如需远程访问，使用 SSH Tunnel 或内网访问。
- 真实密码只放在服务器本地环境文件或 Secret Manager 中。
- 生产环境不要把真实密码提交到 Git。

## 5. 常见误区

1. 把 API 配成服务器公网 IP
- 如果 API 和数据库在同一台服务器上，这通常不是最佳选择。
- 优先使用 `localhost`，更稳定，也减少网络与防火墙干扰。

2. 混用本地开发配置和生产配置
- 开发环境读取 [src/API/appsettings.Development.json](src/API/appsettings.Development.json)
- 生产环境读取 [src/API/appsettings.Production.json](src/API/appsettings.Production.json)
- 不要让生产运行继续依赖 Development 配置
