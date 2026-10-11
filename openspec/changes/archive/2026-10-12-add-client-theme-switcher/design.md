# Design: add-client-theme-switcher

## Context

管理控制台是 Blazor Web App（Server + WebAssembly 已注册；页面当前为 `InteractiveServerRenderMode(prerender: false)`）。Bootstrap 5 已内建 `data-bs-theme=light|dark`，但仓库内无任何主题接线：无 localStorage 偏好、无切换 UI、无防闪脚本。

约束（来自探索结论，已与需求方对齐）：

- **纯客户端**：主题状态只存在于浏览器（DOM + localStorage），不参与 Circuit / API / 数据库。
- **ESM 交互**：切换逻辑用 `wwwroot/js/theme.js`（`type="module"`）。
- **零 Blazor 互操作**：不用 `IJSRuntime`、`DotNetObjectReference`、组件 `@onclick` 驱动主题。
- 登录页使用 `EmptyLayout`（无 NavMenu），且页面 `prerender: false`——主题不能等 Blazor 启动。

## Goals / Non-Goals

**Goals:**

- 首帧前套用用户本地主题偏好，无闪白/闪黑。
- 运行时可切换 `light` / `dark` / `auto`，立即生效并持久化。
- 切换器为纯 HTML 标记 + ESM 绑定，登录页与主壳行为一致。
- 多标签页主题一致；`auto` 跟随系统 `prefers-color-scheme` 变化。

**Non-Goals:**

- 品牌色 / `--bs-primary` 可配置（P2「品牌定制·主题色」另案）。
- 服务端记住主题、用户档案同步、SSR cookie 注入。
- 自定义主题包、多套皮肤、按路由主题。
- 用 Blazor 组件状态驱动主题（与「零互操作」冲突）。

## Decisions

### D1. 两段脚本：classic 内联防闪 + ESM 交互

| 选择 | `<head>` 内联 **classic** `<script>`（同步）负责首帧；`wwwroot/js/theme.js` 以 `type="module"` 负责绑定与持久化 |
|---|---|
| 理由 | `type="module"`（外链或内联）一律 deferred，首帧前跑不完，会闪屏；防闪必须同步阻塞执行。交互层用 ESM 符合需求且利于模块化 |
| 备选 | 纯 ESM 单文件：违反无闪屏；纯 classic 全局函数：与「使用 ESM」要求不符 |

内联脚本**自包含**（约 15 行，重复 `resolve` 三元逻辑），不 `import` theme.js——避免加载顺序耦合，也避免为 3 行逻辑引入非 module 依赖。

### D2. 存储「模式」而非「解析结果」

| 选择 | `localStorage['mingo-theme'] = 'light' \| 'dark' \| 'auto'`（字符串） |
|---|---|
| 理由 | 存解析结果会让 `auto` 在系统切换后失效；字符串足够，无多来源合并场景故不必 JSON |
| 备选 | JSON `{mode, updatedAt, v}`：当前无消费方，属过度设计 |

`html[data-bs-theme]` 只存**生效主题**（`light|dark`）；`html[data-theme-mode]` 可选镜像**用户模式**（含 `auto`）供 UI 高亮，二者分离。

解析规则：

- `light` / `dark` → 直接使用
- `auto`（或键缺失/非法值）→ `matchMedia('(prefers-color-scheme: dark)')` 解析为 `dark|light`

### D3. 切换器 = 静态 HTML 标记，Razor 只负责摆放

| 选择 | `data-theme-root` / `data-theme-toggle` / `data-theme-value` 标记；`theme.js` 顶层 `bindToggles()` 事件委托 |
|---|---|
| 理由 | 零互操作；Blazor 重绘不会与 `@bind` 抢 `data-bs-theme`；Circuit 未就绪时按钮仍在（静态 HTML） |
| 备选 | Blazor 组件 + `IJSRuntime`：与需求冲突，且 `prerender: false` 导致开关出现晚 |

UI 形态：Bootstrap dropdown（浅色 / 深色 / 跟随系统），与现有 navbar 一致。选中态由 ESM 改 class/`aria-checked`，不靠 Blazor re-render。

### D4. 脚本落点：`Web/wwwroot/js/theme.js`

| 选择 | 与 `app.css` 同侧的 `MinGo.StaticPages.Web/wwwroot/js/` |
|---|---|
| 理由 | 主题属于文档壳层（`App.razor`），不随 WASM 程序集走；登录页/静态场景同样能加载 |
| 备选 | `Web.Client/wwwroot/js/`：壳层脚本放 Client 会绑死 WASM 资产管线，且 Server 渲染路径需额外转发 |

引入方式（`App.razor` `</body>` 前，与 bootstrap/blazor 脚本并列）：

```html
<script type="module" src="@Assets["js/theme.js"]"></script>
```

### D5. 属性挂在 `<html>`，由 JS 独占

`data-bs-theme` 写在 `document.documentElement`。不把该属性绑在组件元素上，避免 Blazor 局部刷新覆盖。Bootstrap 已为 `[data-bs-theme=dark]` 设置 `color-scheme` 与全部 CSS 变量，无需自建配色层。

### D6. 多标签与系统主题在 ESM 内监听

- `window.addEventListener('storage')`：键为 `mingo-theme` 时 `apply` 新值。
- `matchMedia('(prefers-color-scheme: dark)').addEventListener('change')`：仅当模式为 `auto` 时重新 `apply`。

两者都在 `theme.js` 模块顶层注册，无 Blazor 生命周期。

## Risks / Trade-offs

- [内联与 ESM 重复 `resolve` 逻辑，可能漂移] → 内联脚本保持极短并在 design/注释中标明「与 theme.js resolve 对齐」；验收用例覆盖三态与非法值回退。
- [`localStorage` 不可用（隐私模式/禁用）] → 读写包 try/catch，失败时退回 `auto` 解析，不抛错、不阻塞页面。
- [切换器选中态与真实主题短暂不一致] → ESM 启动时 `bindToggles()` 内先 `syncToggleUi()`；`setMode`/`storage` 路径统一走 `apply`。
- [Login 页无导航时切换入口不一致] → `EmptyLayout` 同样放置 `data-theme-root` 标记（同一 ESM 已全局绑定）。
- [与未来 P2 品牌主题色交叉] → 本变更只动 `data-bs-theme`，不改 `--bs-primary` 等品牌变量。

## Migration Plan

无数据迁移。回滚 = 移除内联脚本、`theme.js` 引用与切换器标记即可；用户 localStorage 残留键无害。

## Open Questions

（无阻塞项。实现时若 Bootstrap dropdown 在 EmptyLayout 需要额外 CSS 变量，允许在 `app.css` 增加少量 `.theme-toggle-item.is-active` 选中态。）
