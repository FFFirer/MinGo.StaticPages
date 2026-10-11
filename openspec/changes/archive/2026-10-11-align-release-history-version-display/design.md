## Context

项目详情页同时展示「版本」表与「发布历史」表：

- 版本表「版本号」列：`<a href="/versions/{id}">{Number}</a>`，蓝色链接。
- 发布历史「目标版本」列：`<code>{ToVersionId 前 8 位}</code>`，粉色等宽短哈希。

`ReleaseDto` 目前只暴露 `ToVersionId` / `FromVersionId`，没有版本号；前端在 `ProjectDetail.razor` 已加载 `versions` 列表，理论上可本地 join，但发布历史接口自包含返回可读版本号更稳妥（版本被删或跨页复用时不依赖调用方是否持有版本表）。

相关规格：`release-management`（历史记录内容）、`management-console`（项目详情 UI）。

## Goals / Non-Goals

**Goals:**

- 发布历史「目标版本」与版本表「版本号」列视觉与交互一致：显示版本号，并链接到版本详情。
- 发布历史 API 返回人类可读版本号，调用方无需二次查表。
- 版本号无法解析时优雅降级，不丢信息。

**Non-Goals:**

- 不改发布/回滚业务语义、通道指针或存储前缀。
- 不改版本表自身展现。
- 不做发布历史分页/筛选/导出等增强。
- 不引入前端路由或组件库重构。

## Decisions

### D1：在 API 层填充版本号，而不是前端 join

**选择**：`ReleaseService.HistoryAsync`（以及 publish/rollback 返回）批量查询版本表，为 `ReleaseDto` 填充 `ToVersionNumber` / `FromVersionNumber`。

**理由**：

- 发布历史 DTO 自描述，Blazor 与未来 API 消费方行为一致。
- 不依赖页面是否已加载完整版本列表（删除过滤、分页场景更安全）。
- 版本软删后 `Number` 仍在库中，可继续展示。

**备选（否决）**：仅在 `ProjectDetail.razor` 用 `versions` 字典映射——改动更小，但 DTO 语义不完整，且版本列表若被过滤会丢号。

### D2：UI 复用版本表的链接形态

**选择**：发布历史目标版本单元格渲染为 `<a href="/versions/{ToVersionId}">{ToVersionNumber}</a>`；`ToVersionNumber` 为空时显示 `{ToVersionId.ToString()[..8]}`（纯文本，不再用 `<code>` 粉色哈希样式）。

**理由**：与「版本号」列同构，满足「展现形式一样」；降级时仍可辨识目标。

### D3：批量解析，避免 N+1

**选择**：`HistoryAsync` 先取 releases（最多 100 条），再收集 `FromVersionId`/`ToVersionId` 去重后一次 `Versions` 查询，内存映射。

**理由**：历史最多 100 条，一次额外查询足够；避免每行单独查库。

## Risks / Trade-offs

- [历史中的版本已被硬删或不属于本项目] → `ToVersionNumber` 为空，UI 回退短 ID 文本。
- [DTO 增加字段] → 纯增量字段，现有 JSON 消费方兼容；需同步更新前端类型/绑定。
- [publish/rollback 单条返回也带版本号] → 与 History 同一映射逻辑，实现上抽私有方法避免重复。

## Migration Plan

无数据迁移。部署顺序：先后端（DTO + 服务）再前端（表格渲染）。回滚即还原 UI 字段绑定与 DTO 属性，无状态残留。

## Open Questions

无。版本号列仅链接目标版本（不展示 From）；若后续需要 From，可另开变更。
