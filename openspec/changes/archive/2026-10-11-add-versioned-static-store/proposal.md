# Proposal: add-versioned-static-store

## Why

自托管静态站点平台的完整产品面（站点/域名/SSL/多环境）过重，MVP 无法快速验证核心差异化：**不可变版本包 + 指针发布 + 可被外部 YARP 直接消费的解析契约**。本变更先落地最小闭环——上传 zip、版本管理、按版本/已发布对外访问——流量由外部 YARP 直读对象存储，控制面不代理静态内容。

## What Changes

- 新建 .NET 10 解决方案：Blazor Web Auto 控制台 + Minimal API + EF Core(SQLite) + 可插拔对象存储（Local / S3 兼容）。
- 新增 **Project**（扁平唯一名的资源包命名空间）与 **Version**（不可变资源包）。
- 新增 zip 上传流水线：大小/数量/路径穿越校验 → 解包写入存储 → 写入文件清单。
- 新增 **Channel** 指针（`production` / `preview`）及发布、回滚、发布历史。
- 新增只读 **Resolve API**：按项目（已发布）或指定版本返回存储前缀、SPA 回退、缓存建议，供外部 YARP 直读 S3。
- 新增管理鉴权：API Key（CI）+ Cookie（控制台）。
- 新增精简 Blazor 控制台：登录、项目列表/详情、版本上传/列表/详情、发布/回滚、存储设置。
- **不做**：站点、域名、SSL/ACME、访问控制、多租户隔离、通知统计、YARP 数据面本体（仅契约与参考配置）。

## Capabilities

### New Capabilities

- `object-storage`: 可插拔对象存储（Local FS 与 S3 兼容端），统一 Put/Get/List/Delete/前缀删除与预签名。
- `project-management`: 项目的创建、列表、详情、删除（扁平唯一 `name`）。
- `version-management`: zip 上传校验与解包、不可变版本、文件清单、列表/详情、软删除。
- `release-management`: Channel 指针（production/preview）、发布、回滚、发布历史。
- `resolve-api`: 面向 YARP 的只读解析契约（按已发布 / 按指定版本）。
- `management-auth`: API Key 与控制台 Cookie 鉴权，保护管理 API。
- `management-console`: Blazor Auto 精简控制台（登录、项目、版本、发布、存储设置）。

### Modified Capabilities

（无——仓库内尚无既有 spec。）

## Impact

- **代码**：全新解决方案（`src/` 四层工程 + `tests/` + `deploy/`）。
- **API**：新增 `/api/projects*`、`/api/versions*`、`/api/resolve/*`、`/healthz`；OpenAPI 自动暴露。
- **数据**：SQLite（Project / Version / VersionFile / Channel / ReleaseRecord / ApiKey / User / StorageSettings）。
- **存储**：S3 兼容桶或本地目录；对象布局 `projects/{pid}/versions/{vid}/files/**`。
- **依赖**：.NET 10、EF Core Sqlite、AWSSDK.S3、Blazor Web Auto 模板。
- **外部系统**：外部 YARP 仅依赖 Resolve 契约与 S3 读权限；不引入新运行时依赖到数据面。
