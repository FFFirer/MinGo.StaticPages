# 项目上下文

## 容器镜像（deploy/Dockerfile）

2026-10-27 完成一轮体积精简：324 MB → 200 MB（-38%，amd64 未压缩本地测量）。恢复或再调整时保留以下决策背景。

### 关键决策

- 基础镜像：`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`。选 `-extra` 是为了保留 ICU + tzdata（未开 InvariantGlobalization，中文格式化不受影响）；默认非 root 用户 uid 1654（`app`），镜像内 `/data` 属主已设为 1654，compose 的 `app-data` 卷首次初始化可继承。
- 单架构：发布写死 `-r linux-x64 --self-contained false`（原生 SQLite 库从 22 平台 33 MB 收敛为根目录单个 `libe_sqlite3.so`）。将来要 ARM 需把 RID 参数化并按架构分别构建。
- 构建期裁剪（在 build stage 的 RUN 里）：删除 `BlazorDebugProxy`（约 12 MB，运行时不加载，Program.cs 未启用 UseWebAssemblyDebugging）；`wwwroot/lib/bootstrap/dist` 只保留 App.razor 实际引用的 `bootstrap.min.css` 和 `bootstrap.bundle.min.js`（含各自 .gz/.br），其余变体和 .map 删除；发布关闭 pdb（`-p:DebugType=None -p:DebugSymbols=false`）。
- `COPY Directory.Build.props ./` 必须保留：根 props 锁定 SQLitePCLRaw 2.1.12、Microsoft.OpenApi 2.12.2 的安全版本，旧 Dockerfile 漏拷导致容器构建曾静默跳过这些锁定。
- `.dockerignore`（仓库根）排除 bin/obj/.git/tests/docs/openspec/deploy 等，构建上下文约 460 MB → 约 2 MB。

### 本地构建命令（Podman）

容器内会注入失效代理（127.0.0.1:7890 Connection refused），必须清代理构建；GitHub Actions runner 直连正常，无需改动 workflow：

```powershell
podman build -f deploy/Dockerfile -t mingo-candidate `
  --env http_proxy= --env https_proxy= --env HTTP_PROXY= --env HTTPS_PROXY= .
```

### 已知副作用与验证状态

- 被删的 bootstrap 变体 URL 返回 200 空内容而非 404（endpoints 清单仍含路由，页面不引用，无功能影响）。
- 已验证 PASS：容器启动（uid 1654）、SQLite 建库、首页/登录渲染、指纹资源 `bootstrap.min.css` / `bootstrap.bundle.min.js` / `blazor.web.js` 均 200；GHCR amd64 压缩传输 134.7 MB（0.2.1）→ 87.5 MB（0.2.2）。
- 未验证：漏洞扫描、zh-CN 显式格式化测试。
- GHCR 标签由 metadata-action 生成，自动去掉 `v` 前缀（git 标签 v0.2.2 对应镜像标签 0.2.2 和 latest）。

### 升级注意：数据目录属主（0.2.2 有 bug，0.2.3 修复）

镜像自 0.2.2 起非 root（uid 1654），SQLite 写库需要目录写权限，属主不对即崩溃循环 `SQLite Error 8/14`。**0.2.2 的坑**：buildah/Podman 的 `COPY --from` 不显式 `--chown` 会把属主重置为 root，导致镜像内 `/data` 是 root 属主，全新卷初始化后照样 Error 14（已本地复现）。0.2.3 起所有跨 stage COPY 一律显式 `--chown=1654:1654`，并验证了"无卷直跑"和"全新命名卷"两种场景均能建库。另注意 Podman 构建缓存不对 `--chown` 参数变更失效，改这类参数后须 `--no-cache` 验证。

≤0.2.1 的旧部署升级时，数据目录/库文件属主仍是 root，需一次性移交（卷里已有数据时删卷不可取）：

```bash
docker inspect <容器> --format '{{json .Mounts}}'   # 找数据挂载路径
sudo chown -R 1654:1654 <数据目录>
docker compose up -d
```

卷是空的（从未成功建库）则无需 chown：空卷在容器启动时会从新镜像 `/data` 重新初始化属主。
- Chiseled 镜像无 shell/包管理器，`podman exec` 进不去 shell；排障用 `podman cp <container>:/app 目标目录` 取文件，或跑 debug stage：`podman build --target build` 后用 sdk 镜像的 find/du 查看发布树。

### 体积基准数据（复测时对比用）

| 指标 | 精简前 | 精简后 |
|---|---|---|
| 整镜像（amd64 未压缩） | 323,926,836 B | 199,777,106 B |
| 发布产物 | 89 MB | 36 MB |
| 基础镜像 | 234 MB（Debian aspnet:10.0） | 163 MB（noble-chiseled-extra） |

### 尚未处理的瘦身线索

- `_framework` 约 24 MB：Blazor WASM 客户端经 Application 项目引用把整套 EF Core 打包进了浏览器端（项目注释明确 WASM 禁引 SQLite）。属架构调整，与镜像瘦身分开做。
