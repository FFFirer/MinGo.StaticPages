# Tasks: add-client-theme-switcher

## 1. 防闪与存储契约

- [x] 1.1 在 `App.razor` `<head>` 增加 classic 同步内联脚本：读取 `mingo-theme`，按 light/dark/auto 解析，写入 `html[data-bs-theme]`（非法/缺失按 auto；localStorage 异常不抛错）
- [x] 1.2 约定并注释存储键与三态取值（`mingo-theme` = `light|dark|auto`），保证与 theme.js resolve 语义一致

## 2. ESM 主题模块

- [x] 2.1 新增 `src/MinGo.StaticPages.Web/wwwroot/js/theme.js`：实现 `getMode` / `resolve` / `apply` / `setMode`，并在 `App.razor` 以 `type="module"` 引入
- [x] 2.2 实现 `bindToggles`：绑定 `[data-theme-toggle]`，同步选中态（class / `aria-checked`），点击后 `setMode`
- [x] 2.3 监听 `storage`（键 `mingo-theme`）与 `matchMedia('(prefers-color-scheme: dark)')` 变化（仅 `auto` 时重解析）

## 3. 切换器标记

- [x] 3.1 在 `Client/Layout/NavMenu.razor` 放置 Bootstrap dropdown 静态标记（浅色/深色/跟随系统 + `data-theme-*`），无 `@onclick` / `IJSRuntime`
- [x] 3.2 在 `EmptyLayout.razor`（登录页）放置同一套切换器标记，保证登录前可切换
- [x] 3.3 如需选中态样式，在 `app.css` 增加最小 `.is-active` / `aria-checked` 样式

## 4. 验收

- [x] 4.1 验证三态切换即时生效且刷新后保持；`auto` 跟随系统深浅色变化
- [x] 4.2 验证深色偏好下冷启动无闪屏；登录页与主布局切换行为一致
- [x] 4.3 验证多标签页同步；禁用 localStorage 时页面不报错并回退 auto
- [x] 4.4 验证 Circuit 断开或 Blazor 未启动时切换器仍可用（零互操作）
