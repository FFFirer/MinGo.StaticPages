# Design: add-versioned-static-store

## Context

产品完整愿景是「自托管 Netlify」（站点、域名、SSL、多环境）。经探索收敛后的 MVP 只验证核心：**版本化静态包仓库 + 发布指针 + 外部 YARP 可消费的解析契约**。

约束：

- 技术栈固定：Blazor Web Auto + .NET 10 + EF Core + SQLite + S3 兼容存储。
- 静态流量**不经过**控制面；YARP 解析后直读对象存储。
- 无站点/域名/SSL；命名空间为扁平 `Project.name`。
- 本机已有 .NET SDK 10.0.x。

## Goals / Non-Goals

**Goals:**

- 上传 zip → 校验解包 → 不可变 Version → 指针发布/回滚。
- 外部可按 **指定版本** 与 **已发布通道** 两种方式访问资源（由 YARP 基于 resolve 结果实现）。
- 对象存储可切换：本地目录（演示）与任意 S3 兼容端（MinIO/AWS/R2/B2/Wasabi/Ceph 等）。
- 管理 API + 精简 Blazor 控制台走通闭环。
- Resolve 契约稳定、可缓存、可测试。

**Non-Goals:**

- 站点、域名绑定、DNS 验证、SSL/ACME。
- YARP 数据面本体实现（仅契约、参考配置、docker-compose 示例）。
- 多租户隔离、项目级 ACL 矩阵、SSO。
- 灰度/审批/定时发布、通知、统计分析。
- 对象存储跨端同步、CDN 刷新。

## Decisions

### D1. 解决方案分层：Domain / Application / Infrastructure / Web

| 选择 | 四层：`Domain`、`Application`、`Infrastructure`、`Web`（含 Blazor Client） |
|---|---|
| 理由 | 存储与 EF 可替换、用例可单测、Web 同时承载控制台与 API（产品双入口同宿主） |
| 备选 | 单项目更快，但 S3 抽象与测试边界会糊在 UI 旁；多宿主拆 API 与 Blazor 会重复鉴权与配置 |

### D2. 对象存储：`IObjectStorage` + AWSSDK.S3 + LocalFileSystem

| 选择 | 自定义 `IObjectStorage`，S3 实现用 `AWSSDK.S3` 4.x（`ServiceURL` + `ForcePathStyle`） |
|---|---|
| 理由 | 一套代码覆盖 AWS 与几乎所有 S3 兼容端；Local 便于无外网演示；MVP 不绑 MinIO 专用 SDK |
| 备选 | MinIO SDK 对 MinIO 最好但跨端弱；直写 HTTP 签名成本高且易错 |

关键配置：`Provider(Local|S3)`、`ServiceURL`、`Region`、`AccessKey`、`SecretKey`、`Bucket`、`Prefix`、`ForcePathStyle`。

### D3. 数据与不可变版本

- SQLite + EF Core；WAL；开发期 `Migrate`，版本化迁移随包。
- `Version` 创建后 `files/` 前缀只读；删除为软删（`Status`），对象清理可后置。
- 对象布局：

```
projects/{projectId}/versions/{versionId}/package.zip
projects/{projectId}/versions/{versionId}/files/**{relativePath}
```

### D4. 发布 = Channel 指针切换（非拷贝）

- `Channel(name, ProjectId, CurrentVersionId)`，内置 `production`、`preview`。
- `ReleaseRecord` 追加发布/回滚历史。
- 发布事务内更新指针 + 写历史；回滚指向历史版本或上一 Release。
- 指针对解析可见后即生效，无文件搬迁。

### D5. Resolve 契约（YARP 对接面）

```
GET /api/resolve/{projectName}                 → 通道已发布版本（默认 production）
GET /api/resolve/{projectName}?channel=preview → 指定通道
GET /api/resolve/{projectName}?version={id|number} → 指定不可变版本
```

响应字段（稳定契约）：`project`、`version_id`、`version_number`、`access`、`storage{provider,bucket,prefix,endpoint,force_path_style}`、`spa{enabled,fallback}`、`cache{seconds,immutable}`。

- 按版本：`immutable=true`、长缓存。
- 按通道：短 TTL（默认 60s）、`immutable=false`。
- 管理写操作不强制推送失效（YARP 缓存 TTL 即上界）；可选 Webhook 留到后续。

### D6. 鉴权

- 管理 API：API Key（`Authorization: Bearer <key>` 或 `X-API-Key`）或 Cookie 会话（控制台）。
- Resolve：可配置 `RequireAuth`；默认不要求（内网 YARP）。
- 首次启动种子一个管理员用户与可选演示 API Key。

### D7. Blazor Web Auto 控制台

- 模板：`dotnet new blazor -int Auto`；交互组件 `InteractiveAuto`。
- 数据一律走同宿主 `/api/*`（Server 与 WASM 共用 HttpClient），上传用 `InputFile` → HTTP multipart，不走 SignalR 电路。
- 页面：登录、项目列表、项目详情（版本+发布）、版本详情、存储设置。

### D8. Zip 安全

- 拒绝绝对路径、`..` 段、符号链接条目。
- 限制：最大包体积、单文件上限、文件数上限（系统设置可配）。
- 解包后按相对路径写入 `files/` 前缀；清单记录 path、size、contentType。

### D9. 端口与宿主

- 控制面单进程：Blazor + API + Resolve + `/healthz`。
- `deploy/docker-compose.yml`：app + MinIO；`deploy/yarp/` 放参考路由约定与 yarp.json 片段（非完整服务）。

## Risks / Trade-offs

- [S3 兼容端行为差异（List 分页、ETag、删除）] → 只用保守 API 子集；集成测试覆盖 MinIO + 配置说明标注 path-style；兼容矩阵写入 README。
- [YARP 对 Local provider 无法直读文件系统] → Resolve 返回 `provider=local` 时约定：开发用控制面可选静态透传或改用 MinIO；文档标明生产路径是 S3 兼容。
- [SQLite 并发写] → WAL + 短事务；控制面写 QPS 低；后续可换 PostgreSQL 而不改用例。
- [大文件上传占用内存] → 流式解压/写入，禁止整包进内存；上限可配。
- [指针发布后 YARP 缓存未命中窗口] → cache.seconds 为明确上界；文档说明；可选后续失效通知。
- [Blazor Auto 双端差异] → 业务 API 与组件分离，上传只走 HTTP。
- [契约若含凭证则泄露风险] → Resolve 默认不返回永久密钥；可选预签名 URL 仅下载场景，默认 YARP 自持 S3 读权限。

## Migration Plan

1. 新仓库无历史数据；`dotnet ef migrations add InitialCreate` 一次性建表。
2. 系统设置表存储后端配置；启动时探测连通性，失败则 UI 可改配置。
3. 回滚：代码回退即可；对象存储中版本对象保留，不自动删。
4. 对外契约 `/api/resolve` 版本化策略：字段只增不改语义；破坏性变更需新路径 `/api/v2/resolve`。

## Open Questions

- Resolve 是否提供「列出版本文件清单」给 YARP 做校验？（默认不做，YARP 按 path 取对象。）
- 预签名下载 URL 是否进入 MVP？（默认不做，YARP 直读 S3。）
- 上传是否需要服务端异步队列？（默认同步处理；超大包限制最大体积。）
