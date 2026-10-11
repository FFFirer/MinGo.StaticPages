## 1. API：发布历史返回版本号

- [x] 1.1 扩展 `ReleaseDto`：增加 `ToVersionNumber`、`FromVersionNumber`（可空）
- [x] 1.2 `ReleaseService` 批量解析版本号（History / Publish / Rollback 共用映射，避免 N+1）

## 2. UI：发布历史目标版本列与版本号列对齐

- [x] 2.1 `ProjectDetail.razor` 发布历史「目标版本」渲染为版本号链接 `/versions/{id}`，与版本表一致
- [x] 2.2 版本号缺失时回退短 ID 文本（去掉 `<code>` 哈希样式）

## 3. 验证

- [x] 3.1 构建解决方案，确认无编译错误
- [x] 3.2 走通项目详情：发布历史目标版本显示为版本号链接，可点进版本详情；无号场景显示短 ID
