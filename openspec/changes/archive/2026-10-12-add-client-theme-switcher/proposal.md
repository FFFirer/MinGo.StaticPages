# Proposal: add-client-theme-switcher

## Why

管理控制台目前只有默认浅色外观，无主题偏好持久化，也无切换入口。夜间使用体验差，且若主题依赖 Blazor 交互/服务端状态，在 `prerender: false`、登录页 `EmptyLayout` 或 Circuit 断开时会出现闪屏或开关不可用。需要一套**纯客户端**的主题方案：首帧前套用本地偏好，运行时可切换，零 Blazor 互操作。

## What Changes

- 新增客户端主题偏好存储（localStorage），支持 `light` / `dark` / `auto` 三态；`auto` 跟随 `prefers-color-scheme`。
- 在 `App.razor` `<head>` 增加**同步内联脚本**：文档解析时读取本地最新主题配置并写入 `html[data-bs-theme]`，避免首帧闪白/闪黑。
- 新增 ESM 脚本 `wwwroot/js/theme.js`：解析/应用/持久化主题，绑定 `data-theme-toggle` 标记，监听 `storage` 与系统主题变化；**不使用** `IJSRuntime` / DotNet 互操作。
- 在 `NavMenu`（及登录页 `EmptyLayout`）放置纯 HTML 主题切换器（Bootstrap dropdown），由 ESM 绑定交互，Razor 仅作标记落点。
- 切换后立即生效并写回 localStorage；多标签页通过 `storage` 事件保持一致。
- **不做**：品牌色/`--bs-primary` 定制（产品文档中的 P2「主题色」另案）、服务端用户主题档案、SSR 注入主题 cookie。

## Capabilities

### New Capabilities

- `client-theme`: 浏览器本地主题偏好（light/dark/auto）的存储、首帧套用、纯客户端切换 UI、多标签与系统主题联动。

### Modified Capabilities

（无——既有 spec 不因本变更改写需求。）

## Impact

- **代码**：`src/MinGo.StaticPages.Web/Components/App.razor`（内联防闪脚本）、`src/MinGo.StaticPages.Web/wwwroot/js/theme.js`（新增 ESM）、`Client/Layout/NavMenu.razor` 与 `EmptyLayout.razor`（静态切换器标记）、必要时 `app.css` 少量选中态样式。
- **依赖**：无新增 NuGet/npm；复用已有 Bootstrap 5 `data-bs-theme` 与 `bootstrap.bundle.min.js`。
- **API / 数据**：无服务端 API、无数据库变更；状态仅存 `localStorage`。
- **渲染模式**：不依赖 Interactive Server/WASM；主题在 Blazor 启动前即可用。
