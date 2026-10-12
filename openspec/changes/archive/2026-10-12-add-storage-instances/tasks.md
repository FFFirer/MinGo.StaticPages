# Tasks: add-storage-instances

## 1. Domain and data

- [x] 1.1 Add `StorageInstance` entity (Name, Kind, LocalRoot/Prefix, S3 connection fields, timestamps) and wire `IAppDbContext` / `AppDbContext`
- [x] 1.2 Add `Project.StorageInstanceId` (required FK) and EF model/migration; adjust `CreateProjectRequest` / `ProjectDto`
- [x] 1.3 Split global `StorageSettings`: keep limits + ResolveRequireAuth + cache seconds as global policy; stop treating a single row as the active backend
- [x] 1.4 Migration/seed: first boot with zero instances → create default Local instance (`local` / `storage-data` / empty Prefix); backfill existing projects to the migrated instance

## 2. Storage routing and Prefix parity

- [x] 2.1 Implement `IStorageFactory` (instanceId/project → `IObjectStorage`) with per-instance config fingerprint cache; replace global `ObjectStorageRouter` injection in version/delete paths
- [x] 2.2 Extend `LocalObjectStorage` with `Prefix` (FullKey semantics isomorphic to `S3ObjectStorage`); keep logical keys relative for list
- [x] 2.3 Route upload/download/list/delete/prefix-delete through the project's bound instance; keep logical key `projects/{pid}/versions/{vid}/files/**`
- [x] 2.4 Tests: two Local instances (different Root and/or Prefix) write to disjoint locations; S3 mock/instance prefix still applied

## 3. Instance API and guards

- [x] 3.1 CRUD + connectivity test endpoints for storage instances (admin role)
- [x] 3.2 Validation by kind (Local requires LocalRoot; S3 requires ServiceUrl + Bucket); secrets write-only on read DTOs
- [x] 3.3 Delete guard: reject when any project references the instance
- [x] 3.4 `POST /api/projects` requires valid `storageInstanceId`; reject updates that change binding; expose instance id/name on project DTOs

## 4. Resolve payload (L2)

- [x] 4.1 `ResolveService` builds `storage` from project's instance (provider, endpoint, bucket/prefix identity, forcePathStyle); include instance name
- [x] 4.2 Local `endpoint` = control-plane file-provider base URL (no LocalRoot path leak); S3 `endpoint` = ServiceUrl
- [x] 4.3 Tests: per-project resolve endpoints differ by instance; secrets absent from payload

## 5. Local FileProvider static access

- [x] 5.1 Custom `IFileProvider` / static-file pipeline for Local-bound projects: project/version resolution → PhysicalFileProvider under instance LocalRoot+Prefix+logical files prefix
- [x] 5.2 Routes for published (`channel`) and by-version paths; SPA fallback to `spa.fallback`; cache headers from global policy + access mode
- [x] 5.3 Path traversal rejection on request paths; S3-bound projects return 404/410 on this route (no control-plane S3 proxy)
- [x] 5.4 Tests: GET published file, SPA fallback, missing version 404, S3 project not served

## 6. Management console

- [x] 6.1 Storage instance list/create/edit/test/delete UI (replace single storage settings form)
- [x] 6.2 Create project form requires storage instance select; project detail shows bound instance read-only
- [x] 6.3 Update README / `deploy/yarp` notes: multi-instance, Local FileProvider endpoint, Prefix semantics

## 7. Verification

- [x] 7.1 Unit/integration: instance seed, binding immutability, per-instance IO, Local Prefix layout
- [x] 7.2 End-to-end: create instances → bind projects → upload/publish → resolve → GET Local file provider URL; S3 path still resolve-only
- [x] 7.3 Migration smoke: existing DB with old StorageSettings + projects upgrades with backfill
