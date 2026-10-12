# Design: add-storage-instances

## Context

当前全局单行 `StorageSettings`（`Id=1`）同时充当「连接配置」与「策略」：`ObjectStorageRouter` 按该行构造唯一 `IObjectStorage`，`VersionService`/`ResolveService` 都读它。`S3ObjectStorage` 会把 `Prefix` 拼进对象键，`LocalObjectStorage` 只认 `LocalRoot`、忽略 `Prefix`。Resolve 的 `storage.endpoint` 对 Local 恒为 `null`，外部 YARP 无法读盘。

目标形态：**Kind（Local|S3）→ 多 Instance → 项目创建时绑定且终身不变**；Resolve 吐该项目实例的 endpoint（Local 为控制面 FileProvider 基址）；Local 可 N 个实例，各自 `LocalRoot`+`Prefix`。

## Goals / Non-Goals

**Goals:**

- 存储实例 CRUD + 连通性测试；Local/S3 对称多实例。
- Local 与 S3 同构应用 `Prefix`（逻辑键不变）。
- 项目必绑实例、绑后不可改；对象 IO 与 Resolve 一律按实例路由。
- Local Kind 提供 FileProvider 静态读出口；Resolve Local endpoint = 该读出口基址（L2）。
- 默认 Local 实例种子（可删，无特权）。
- 限额 / Resolve 鉴权 / 缓存秒保持全局策略。

**Non-Goals:**

- 实例级限额、项目改绑、控制面代理 S3 吐字节。
- YARP 数据面本体、域名/SSL、预签名 URL 进 Resolve。
- 迁移对象物理搬迁（换 Root/Prefix 视为新库，数据自理）。

## Decisions

### D1. 实例实体与字段拆分

| 保留全局（策略） | 进 StorageInstance（连接） |
|---|---|
| MaxPackageBytes / MaxFileCount / MaxSingleFileBytes | Kind, Name |
| ResolveRequireAuth | Local: LocalRoot, Prefix |
| PublishedCacheSeconds / VersionCacheSeconds | S3: ServiceUrl, Region, AccessKey, SecretKey, Bucket, Prefix, ForcePathStyle |

**理由**：限额/缓存/鉴权与「连到哪」无关；实例只描述可替换的后端连接。备选「限额也进实例」——延后，字段可后续加。

### D2. 项目绑定不可变

`Project.StorageInstanceId` 创建必填，API 不提供修改路径。  
**理由**：版本对象已在该实例 `Prefix` 下；改绑等于历史失踪。备选「改绑+搬迁」——超出 MVP。

### D3. 对象键：Prefix 同构

```text
逻辑键 = projects/{pid}/versions/{vid}/files/{rel}   # Version.StoragePrefix 仍存逻辑前缀
物理键 = Join(Prefix, 逻辑键)                        # S3: bucket 内；Local: LocalRoot 下
```

`LocalObjectStorage` 增加与 `S3ObjectStorage.FullKey` 相同的 Prefix 语义（List 返回相对逻辑键）。  
**理由**：消除 UI 可填 Prefix 但 Local 不生效的不一致；多 Local 实例可用 Root、Prefix 或两者组合隔离。

### D4. IStorageFactory 取代全局 Router 注入

`IStorageFactory.For(instanceId | project)` → 缓存的 `IObjectStorage`；配置指纹失效粒度为实例。`VersionService` 等改为按项目实例解析，不再注入全局 `IObjectStorage`。  
**理由**：全局单例在多实例下会写错库。备选「每次 new」——热路径浪费；「静态缓存」即可。

### D5. Local endpoint = L2（FileProvider 基址）

Resolve `storage.endpoint`：

- S3：`ServiceUrl`
- Local：控制面可读基址（如相对路径 `/s/{projectName}` 或绝对 URL），配合 FileProvider 实际吐文件

Payload 可附 `instance` 名便于排障。  
**理由**：数据面规则统一「有 endpoint 就去读」；Local 不暴露磁盘绝对路径。备选 L1（endpoint=null + 旁路约定）——契约分裂，已否决。

### D6. Local FileProvider 路由与范围

- 仅 **Kind=Local** 的实例提供；S3 项目 GET 静态路由 → 404/410（不静默）。
- URL 语义对齐 YARP 契约：`/p/{project}/{**path}`（已发布）与 `/v/{project}/{version}/{**path}`（指定版本）；若与控制台路由冲突则用独立前缀 `/s/...` 并在 resolve.endpoint 使用该前缀。
- 实现：`UseStaticFiles`/`StaticFileOptions` + 自定义 `IFileProvider`（项目/版本 → 该实例 Root+Prefix 下的 `PhysicalFileProvider`）；SPA 用 fallback 到 `spa.fallback`；缓存头读全局策略与 access 模式。
- 路径安全：沿用 `IsSafeRelativePath`/PhysicalFileProvider 根限制，禁止 `..`。

### D7. 默认 Local 种子

首次启动且 **零实例** 时插入 name=`local`、`LocalRoot=storage-data`、`Prefix=""`。可删（无项目引用时）；创建项目 UI 可预选列表中任一实例，无系统唯一内置实例。  
**理由**：开箱可建项目；同时满足「Local 可多实例、无特权」。

### D8. 迁移

1. 原 `StorageSettings` 行 → 一个实例（按原 Provider/LocalRoot/Prefix/ServiceUrl…）。  
2. 存量 `Project` 全部回填该实例 id。  
3. 全局策略字段迁到设置表或 appsettings。  
4. 回滚：保留旧表一版，或反向合成单行（丢多实例数据）。

## Risks / Trade-offs

- [改实例 LocalRoot/Prefix/桶 = 换库] → API 允许但文档/二次确认标明「旧对象不迁移」；仅密钥/显示名变更视为安全操作。
- [LocalRoot 热更新导致 FileProvider 指错目录] → 与 `ObjectStorageRouter` 指纹相同机制按实例失效缓存；或设置保存后重建 provider。
- [`/p` `/v` 与未来 YARP 双挂载] → resolve.endpoint 使用约定前缀；文档写清 Local 由控制面代供，S3 由数据面直读。
- [Resolve 泄露] → 永不返回 SecretKey；Local endpoint 只给相对/控制面基址，不给 `LocalRoot` 绝对路径。
- [双语义 Prefix 与 StoragePrefix] → 对外 resolve 的 `prefix` 继续表示「版本文件逻辑前缀」（`projects/{pid}/versions/{vid}/files`）；实例 `Prefix` 为键根，文档区分「instance key prefix」vs「version storage prefix」。

## Migration Plan

1. 建 `StorageInstance` 表与 `Project.StorageInstanceId`。  
2. 数据迁移：settings → 实例 + 项目回填。  
3. 替换注入/路由（factory、resolve、version 服务）。  
4. 上线 FileProvider（仅 Local）与实例管理 API/UI。  
5. 验证：多实例上传隔离、resolve endpoint 正确、Local 文件可 GET、S3 仍走 YARP 契约。

## Open Questions

- Local 静态路由前缀最终用契约 `/p` `/v` 还是独立 `/s`（实现时按冲突情况定，resolve.endpoint 必须与之一致）。
- 旧 `/api/settings/storage` 是 410 移除还是代理到默认实例（倾向移除 + 迁移说明）。
