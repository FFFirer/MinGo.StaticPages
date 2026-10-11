# YARP 对接参考（非本仓库实现）

外部 YARP 通过 Resolve API 取元数据后**直读 S3/MinIO**，不经过控制面。

## 1. 解析 API

```
GET /api/resolve/{projectName}                  # 已发布（默认 production）
GET /api/resolve/{projectName}?channel=preview
GET /api/resolve/{projectName}?version=1.0.0    # 指定不可变版本
```

响应示例：

```json
{
  "project": "docs",
  "projectId": "…",
  "versionId": "…",
  "versionNumber": "1.4.2",
  "access": "published",
  "channel": "production",
  "storage": {
    "provider": "s3",
    "bucket": "static-pages",
    "prefix": "projects/{pid}/versions/{vid}/files",
    "endpoint": "http://127.0.0.1:9000",
    "forcePathStyle": true
  },
  "spa": { "enabled": true, "fallback": "index.html" },
  "cache": { "seconds": 60, "immutable": false }
}
```

## 2. 推荐路由约定（YARP 侧配置）

```
/p/{project}/{**path}                 → resolve(project) → prefix + path
/v/{project}/{version}/{**path}       → resolve(project, version) → prefix + path
```

YARP 伪代码：

```
onRequest(req):
  match (project, version?, path) from route
  meta = cachedResolve(project, version)   # published: TTL 60s; version: long
  objectKey = meta.storage.prefix + "/" + path
  # Direct S3:
  #   GET {endpoint}/{bucket}/{objectKey}  (path-style)
  # SPA:
  #   if 404 and meta.spa.enabled and not path has extension:
  #     objectKey = meta.storage.prefix + "/" + meta.spa.fallback
  response.headers["Cache-Control"] =
      meta.cache.immutable
        ? "public, max-age=" + meta.cache.seconds + ", immutable"
        : "public, max-age=" + meta.cache.seconds
```

## 3. yarp.json 片段示意

```json
{
  "ReverseProxy": {
    "Routes": {
      "published": {
        "Match": { "Path": "/p/{project}/{**path}" },
        "ClusterId": "s3"
      },
      "byVersion": {
        "Match": { "Path": "/v/{project}/{version}/{**path}" },
        "ClusterId": "s3"
      }
    },
    "Clusters": {
      "s3": {
        "Destinations": {
          "minio": { "Address": "http://127.0.0.1:9000/" }
        }
      }
    }
  }
}
```

实际生产需要自定义中间件：先调 resolve，再改写 `path` 为 `{bucket}/{prefix}/{path}`，并处理 SPA 回退与缓存头。

## 4. 鉴权

- 管理 API：`Cookie`（控制台）或 `X-API-Key` / `Authorization: Bearer <key>`
- Resolve：默认可匿名；将系统设置 `ResolveRequireAuth` 打开后需 API Key

## 5. Local provider

`storage.provider=local` 时 YARP 无法直读磁盘。开发环境可：
- 使用 MinIO（推荐，与生产路径一致）
- 或自行将 `localRoot` 挂载给数据面
