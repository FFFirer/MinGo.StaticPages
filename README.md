# MinGo Static Pages

自托管**版本化静态资源仓库** MVP：上传 zip → 不可变版本 → 指针发布/回滚 → 供外部 YARP 解析并直读对象存储。

技术栈：**.NET 10 · Blazor Web Auto · EF Core + SQLite · Local / S3 兼容存储**

## 功能（MVP）

| 能力 | 说明 |
|---|---|
| 项目 | 扁平唯一名的资源包命名空间 |
| 版本 | zip 上传校验、解包、文件清单、软删、不可变 |
| 发布 | `production` / `preview` 通道指针；发布、回滚、历史 |
| Resolve API | 按已发布 / 按指定版本返回存储前缀、SPA、缓存建议 |
| 存储 | 多实例（Local / S3 兼容）；项目创建时绑定且不可改；Local Prefix 与 S3 同构 |
| 鉴权 | Cookie（控制台）+ API Key（CI）；角色 admin/publisher/viewer |
| 控制台 | 登录、仪表盘、项目、版本上传/发布/回滚、存储设置、解析演示 |

**不做**：站点、域名、SSL/ACME、YARP 数据面本体（只提供契约，见 `deploy/yarp/`）。

## 快速开始

```bash
# 需要 .NET SDK 10.x
dotnet build
dotnet test

# 运行控制台 + API
dotnet run --project src/MinGo.StaticPages.Web
# 打开 https://localhost:<port>/login
# 默认账号 admin / admin123
```

对象布局（逻辑键；物理位置 = 实例 `Prefix` + 逻辑键，Local 落在 `LocalRoot` 下）：

```
projects/{projectId}/versions/{versionId}/package.zip
projects/{projectId}/versions/{versionId}/files/**{relativePath}
```

首启若无实例会种子默认 Local 实例（`local` / `storage-data`，可删）。创建项目必须指定 `storageInstanceId`，绑定后不可修改。

## API 摘要

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/auth/login` | 登录（Cookie） |
| POST | `/api/projects` | 创建项目（必填 `storageInstanceId`） |
| GET | `/api/projects` | 项目列表 |
| POST | `/api/projects/{id}/versions` | multipart zip 上传版本 |
| GET | `/api/versions/{id}` | 版本详情 + 文件清单 |
| POST | `/api/projects/{id}/publish` | 发布到通道 |
| POST | `/api/projects/{id}/rollback` | 回滚 |
| GET | `/api/resolve/{project}` | 数据面解析 |
| GET/POST/PUT/DELETE | `/api/storage-instances*` | 存储实例 CRUD + `/test` |
| GET | `/s/{project}/{**path}` | Local FileProvider（仅 Local 实例） |
| GET | `/s/{project}/_v/{version}/{**path}` | 按版本读（Local） |
| GET | `/healthz` | 健康检查 |

OpenAPI：开发环境 `/openapi/v1.json`。

### 上传示例

```bash
curl -X POST http://localhost:5218/api/projects/PROJECT_ID/versions \
  -H "X-API-Key: $KEY" \
  -F "file=@site.zip" \
  -F "number=1.0.0"
```

### 解析示例

```bash
curl http://localhost:5218/api/resolve/docs
curl "http://localhost:5218/api/resolve/docs?version=1.0.0"
```

## S3 兼容配置

系统设置 → 存储：

| 项 | MinIO | AWS S3 | R2 |
|---|---|---|---|
| Provider | S3 | S3 | S3 |
| ServiceURL | `http://127.0.0.1:9000` | `https://s3.amazonaws.com` | `https://<account>.r2.cloudflarestorage.com` |
| ForcePathStyle | true | false | true |
| Region | us-east-1 | 实际 region | auto |

## Docker

```bash
docker compose -f deploy/docker-compose.yml up
```

含 MinIO + 控制面。YARP 对接说明见 `deploy/yarp/README.md`。

## 测试

```bash
dotnet test
```

覆盖：zip 路径穿越/限额、本地存储读写、上传→发布→resolve(published/version)→回滚、版本删除守卫。

## 目录结构

```
src/MinGo.StaticPages.Domain           实体
src/MinGo.StaticPages.Application      用例、DTO、Zip 校验
src/MinGo.StaticPages.Infrastructure   EF/SQLite、Local/S3、DI
src/MinGo.StaticPages.Web              Minimal API + 托管
src/MinGo.StaticPages.Web.Client       Blazor Auto 控制台
tests/MinGo.StaticPages.Tests
deploy/                                Docker + YARP 契约
openspec/                              规格与变更
```
