# Proposal: add-storage-instances

## Why

存储配置目前是全局单行 `StorageSettings`，所有项目共用同一后端/同一 LocalRoot/同一 Bucket，Resolve 也只能吐一个全局 endpoint。无法按项目隔离到不同 MinIO 桶、不同本地目录；Local 下 `Prefix` 未真正落盘，且 YARP 无法直读磁盘导致 Local 发布后「打不开网页」。需要 **存储类型（Kind）→ 多实例（Instance）→ 项目终身绑定**，并让 Resolve 始终解析出该项目实例的 endpoint。

## What Changes

- 新增 **存储实例** 模型：每个 Kind（`Local` / `S3`）可有多个实例；Local 实例配置 `LocalRoot` + `Prefix`，S3 实例配置 endpoint/region/凭证/bucket/`Prefix`/forcePathStyle；**Prefix 语义两 Kind 同构**（对象键 = `Prefix` + `projects/{pid}/versions/{vid}/files/{rel}`）。
- **项目创建必须指定存储实例**，绑定后 **不可修改**；所有版本对象读写、Resolve、删除均按该项目实例路由。
- **Resolve** 的 `storage` 块改为吐项目绑定实例的配置；Local 实例的 `endpoint` 为 **控制面 Local FileProvider 可读基址（L2）**，S3 为 `ServiceUrl`。
- 为 Local Kind 提供 **Local FileProvider** 静态读出口（仅 Local 实例）：按项目/版本路由从该实例的 `LocalRoot`+`Prefix` 提供文件，含 SPA 回退与缓存头（与 resolve hints 对齐）；S3 仍由外部数据面直读。
- **限额**（MaxPackageBytes / MaxFileCount / MaxSingleFileBytes）与 Resolve 鉴权、默认缓存秒仍为 **全局策略**（不进实例）。
- **种子数据**：首次启动若无任何实例，创建默认 Local 实例（如 name=`local`，`LocalRoot=storage-data`，`Prefix` 空）；可增删改（有项目引用则拒删），**不是**系统唯一内置特权实例。
- **BREAKING**：创建项目 API 必带 `storageInstanceId`；原全局「存储设置」单行语义升级为实例列表；存量项目迁移时回填到种子/迁移出的实例。
- **不做**：实例级限额、项目改绑、S3 控制面代理吐字节、YARP 数据面本体、预签名下载进 Resolve、多租户 ACL。

## Capabilities

### New Capabilities

- `storage-instances`: 存储实例注册（Local/S3 多实例、连接配置、连通性测试、删除守卫）、默认 Local 种子、项目创建必绑且终身不变、按实例路由 `IObjectStorage`、Local Kind 的 FileProvider 静态读出口（含 SPA/缓存头）。

### Modified Capabilities

- `object-storage`: 后端配置从「全局单设置」改为「按存储实例」；Local 与 S3 同构应用 `Prefix`；对象键布局语义不变但仍相对实例根。
- `resolve-api`: `storage` 载荷来自项目绑定实例（含 instance 标识）；Local 的 endpoint 为控制面 FileProvider 基址而非 null；S3 仍返回 ServiceUrl/bucket/prefix/forcePathStyle。
- `project-management`: 创建项目必须指定 `storageInstanceId`，创建后不可变更；列表/详情暴露绑定实例信息。
- `management-console`: 「存储设置」改为「存储实例」管理（列表/创建/编辑/测试/删除）；创建项目强制选择实例；项目详情只读展示绑定实例。

## Impact

- **数据**：新增 `StorageInstance`；`Project` 增加 `StorageInstanceId`（必填、不可变）；原 `StorageSettings` 单行拆为「实例连接字段」+「全局策略」（限额/Resolve 鉴权/缓存秒）；迁移脚本回填存量项目。
- **API**：新增 `/api/storage-instances*`（CRUD + test）；`POST /api/projects` 增加 `storageInstanceId`；`GET /api/resolve/*` 的 `storage` 形态调整；旧 `/api/settings/storage` 需兼容或替换为实例 API。
- **代码**：`IStorageFactory`（按实例取 `IObjectStorage`）替换全局 `ObjectStorageRouter` 注入用法；`LocalObjectStorage` 支持 `Prefix`；新增 Local FileProvider 中间件/路由；`ResolveService`、`VersionService`、`ProjectService` 改走项目实例。
- **存储布局**：逻辑键仍为 `projects/{pid}/versions/{vid}/files/**`；物理位置 = 实例 `Prefix` + 逻辑键（Local 落在 `LocalRoot` 下）。
- **外部**：YARP/数据面按 resolve 的 endpoint 读；Local 时期可直接 GET 控制面 FileProvider 基址。
