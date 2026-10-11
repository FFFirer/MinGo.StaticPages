## Why

项目详情页的「版本」表用可点击的版本号链接（如 `2.2.0-dev.6`）展示版本，而「发布历史」的「目标版本」列却用截断的 GUID 哈希（`<code>` 样式，如 `63e06614`）。同一页面上两套展现不一致，用户无法直接读懂发布/回滚指向了哪个版本，也无法点进版本详情。

## What Changes

- `ReleaseDto` 增加可读版本号字段（`ToVersionNumber`，以及可选的 `FromVersionNumber`），由发布历史接口在返回时解析并填充。
- 项目详情页「发布历史」的「目标版本」列改为与「版本」表「版本号」列相同的展现：版本号文本 + 链接到 `/versions/{id}`。
- 若版本号无法解析（例如版本已不存在），回退为短 ID 文本，保持信息不丢失。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `release-management`: 发布历史记录除版本标识外，还 SHALL 返回人类可读的目标版本号（及来源版本号），供管理台一致展示。
- `management-console`: 发布历史「目标版本」列 SHALL 与版本列表「版本号」列采用同一展现形式（版本号链接到版本详情）。

## Impact

- 代码：`Application/Dtos/Dtos.cs`（`ReleaseDto`）、`Application/Services/ReleaseService.cs`（`HistoryAsync` / 发布与回滚返回值）、`Web.Client/Pages/ProjectDetail.razor`（发布历史表格）。
- API：`GET /api/projects/{id}/releases` 与 publish/rollback 响应体增加版本号字段；为向后兼容的增量字段，不破坏现有消费方。
- 依赖/系统：无新增依赖；不改存储与解析链路。
