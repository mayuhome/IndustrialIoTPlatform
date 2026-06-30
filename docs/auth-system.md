# 用户管理与 JWT 鉴权

当前实现包含以下能力：
- 用户注册
- 用户登录
- JWT 访问令牌签发
- 受保护 API 访问
- 当前登录用户信息查询

## 1. 数据落位

- 用户账号数据：PostgreSQL `users` 表
- JWT：无状态签名令牌，不落库
- 设备接口：通过 Bearer Token 鉴权

## 2. 配置项

需要配置以下 JWT 参数：
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`
- `Jwt:ExpiryMinutes`

建议：
- `SigningKey` 长度至少 32 个字符
- 生产环境通过环境变量或 Secret Manager 注入

## 3. API 能力

- `POST /auth/register`
- `POST /auth/login`
- `GET /auth/me`

设备接口默认需要授权访问。
