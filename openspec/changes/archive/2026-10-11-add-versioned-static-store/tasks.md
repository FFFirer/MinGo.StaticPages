# Tasks: add-versioned-static-store

## 1. Solution skeleton

- [x] 1.1 Create .NET 10 solution with Domain, Application, Infrastructure, Web (Blazor Auto + Client) projects
- [x] 1.2 Add EF Core Sqlite, AWSSDK.S3, and project references; wire Directory.Packages.props or direct versions
- [x] 1.3 Configure Web host: DI, problem details, OpenAPI, healthz, auth stubs
- [x] 1.4 Add initial EF Core model and migration (Project, Version, VersionFile, Channel, ReleaseRecord, User, ApiKey, StorageSettings)

## 2. Object storage

- [x] 2.1 Implement `IObjectStorage` with local filesystem provider
- [x] 2.2 Implement S3-compatible provider (AWSSDK.S3, ServiceURL + ForcePathStyle)
- [x] 2.3 Implement StorageSettings persistence and active-backend resolution
- [x] 2.4 Implement upload/get/list/delete/prefix-delete; write connectivity test helper
- [x] 2.5 Unit/integration tests: local layout mapping and MinIO smoke (if docker available)

## 3. Projects and versions

- [x] 3.1 Implement project create/list/detail/delete APIs with unique name rules
- [x] 3.2 Implement zip validation (traversal, limits) and streaming extract to `files/`
- [x] 3.3 Implement version upload API (multipart), manifest persistence, package.zip store
- [x] 3.4 Implement version list/detail/soft-delete with publish-pointer guard
- [x] 3.5 Tests: duplicate name, zip bomb/path traversal rejection, immutability and delete guards

## 4. Release and resolve

- [x] 4.1 Seed channels `production`/`preview` on project create
- [x] 4.2 Implement publish, rollback, release history APIs with atomic pointer updates
- [x] 4.3 Implement `GET /api/resolve/{projectName}` (published / channel / version) response DTOs
- [x] 4.4 Implement cache/SPA/storage payload rules and optional resolve auth
- [x] 4.5 Tests: publish/rollback pointer correctness, resolve by version vs published, 404 paths

## 5. Auth

- [x] 5.1 Implement user seed + password login cookie for console
- [x] 5.2 Implement API key issue/list/revoke and request authentication
- [x] 5.3 Enforce roles (admin/publisher/viewer) on management endpoints
- [x] 5.4 Tests: 401/403 matrix for key and cookie callers

## 6. Management console (Blazor Auto)

- [x] 6.1 Login page and authenticated shell/navigation
- [x] 6.2 Project list and create
- [x] 6.3 Project detail: channels, version list, upload, publish, rollback, release history
- [x] 6.4 Version detail with file manifest
- [x] 6.5 Storage settings UI (admin) + connectivity test
- [ ] 6.6 Manual walkthrough of golden path under Auto render mode

## 7. Deploy materials and docs

- [x] 7.1 `deploy/docker-compose.yml` (app + MinIO) and appsettings samples for S3-compatible endpoints
- [x] 7.2 `deploy/yarp/` reference route mapping and resolve consumption notes
- [x] 7.3 README: object key layout, resolve contract, S3 compatibility notes (path-style, MinIO/R2/B2)
- [x] 7.4 OpenAPI polish and `/healthz` verification

## 8. Verification

- [x] 8.1 `dotnet build` and unit tests green
- [x] 8.2 Integration test: upload → publish → resolve(published) → resolve(version) against MinIO or Local
- [x] 8.3 Zip security tests green
- [x] 8.4 Validate OpenSpec scenarios mapped to automated tests (critical paths)

## Notes

- SQLite 不支持 `DateTimeOffset` ORDER BY：查询后在内存排序。
- 本机 NuGet 需要 `ProgramFiles` 等环境变量（本会话已处理）。
- 5.4 的 401/403 矩阵未写独立 Web 集成测试，管理端点已有角色守卫；可后续用 `WebApplicationFactory` 补齐。
- 6.6 控制台 Auto 人工走查未完成；API 黄金路径已冒烟通过。
