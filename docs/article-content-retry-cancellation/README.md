# 文章内容重取纠正：可显示性判定、HTTP 边界与阅读器取消

基线父 `f44c22aad17780cfb01fff56503fd70e69ca62c4`（新历史）。输入只读：固定候选 `b639a59d`
（实现 `0cda2112`、报告 `24efd55d`/`b639a59d`）与独立审查 `c13f971d`，均以 `git show` 读取；
未恢复已清理的旧 `docs/article-content-retry/` 证据。kernel 候选 `b56f0d8` 通过提交 bundle 恢复到
本区，本区在其上新增 kernel 提交 `cb659e9`，并以新 bundle 交付。**父 gitlink 保持 `62b376e` 不变**。

本区没有启动子代理；未 push、PR、issue；未启动/输入/关闭 AotDev 或其它用户实例；未访问登录存储、
token、cookie、文章正文或用户内容；未操作其它工作树。

## 1. 纠正内容

### kernel（`62b376e..b56f0d8..cb659e9`）

- **重取判定与最终映射共用同一正文路径**：`ArticleAdapter.GetRenderableContent` 先按 `ToArticleDetail`
  的 opus 优先规则生成正文，再走 `FormatHtmlContent`；`HasUsableContent` 与 `ToArticleDetail` 都调用它。
  因此未知段落独占、`<p></p>`、旧 `content` 非空但 `opus` 为空都会在 HTTP 成功且正文确实不可显示时
  进入有界重取；合法图片、分割线与有效混合文本仍判定可用，不额外重取。未新增第二套格式规则。
- **HTTP 状态先行**：阅读页正文请求通过 `BiliHttpClient.GetStringAsync(..., ensureSuccess: true)`
  在读取正文前拒绝非成功状态。403/认证等永久错误只失败一次，不进入重取循环。该开关为显式 opt-in，
  `BiliHttpClient` 其余调用语义不变。

### 应用

- `ArticleReaderPageViewModel.InitializeAsync`：每次加载创建独立 CTS，并取消上一请求；token 传入
  `IArticleDiscoveryService.GetArticleContentAsync`，由 kernel 继续传到正文读取、200ms 重取间隔与
  元数据请求。取消时先 `Cancel()` 后 `Dispose()`；每个加载在 `finally` 自行释放，重复释放安全。
- `CancelLoad(generation)`：页面在 `SetParameter` 后记录本次代次，`OnPageUnloaded` 只取消匹配代次。
  取消会递增代次并结束加载态，使取消后在途的旧成功/异常也无法落地；旧页面延迟卸载不会误取消新文章。
  既有 generation 结果检查保留。
- 保留候选的窄修：页面先解除再挂载 `Reader.Initialized` / `ArticleInitialized` 且前移到初始化命令之前；
  `ArticleReader.ClearContent` 容忍未初始化的 `CoreWebView2`；新文章开始加载时清空上一篇作者、头像与统计。

## 2. 红/绿与回归

| 项目 | 命令/范围 | 结果 |
| --- | --- | --- |
| kernel 红 | `b56f0d8` 源码 + 本区新反例 | 4 failed / 23 passed（3 种不可渲染正文 + 403，27 总计） |
| kernel 绿 | `cb659e9`，`Services.Article.Tests` | **27 passed / 0 failed** |
| 原有用例保留 | 适配器 12 + 原反例 4 + 候选客户端 3 = 19 | 全部包含在 27 中，未丢失 |
| 应用定向 | `--filter FullyQualifiedName~ArticleReaderLatestWinsTests` | **7 passed / 0 failed** |
| 应用完整 | `tests/BiliCopilot.UI.Tests` | **596 passed / 0 failed**（基线 590 + 新增 6） |
| 父 gitlink 复现 | kernel 检出 `62b376e` 后跑定向 7 项 | **7 passed / 0 failed**，证明父提交不依赖新 kernel |
| 生产 Debug x64 | `dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 -p:SolutionDir=<repo>/src/ -p:AppxPackageSigningEnabled=false` | **0 warning / 0 error**（先复制 ignored 的 libmpv/ffmpeg/BBDown 运行期资源，不提交） |

kernel 红在 ignored `artifacts/article-content-retry-cancellation/red-kernel` 中，以 `b56f0d8`
归档加本区测试文件运行；未据此声称复放了旧审查树里的原始文件。真实 VM + 伪 HTTP 传输验证 A→B：
旧文章正文只请求 1 次、读取中被取消、元数据 0 次；新文章正文 1 次、元数据 1 次并正常落地。
另有“忽略取消的迟到成功”用例确认离开后结果不能写入；旧页面迟到卸载的旧代次不会取消新文章。

## 3. kernel bundle

| 文件 | 内容 |
| --- | --- |
| `kernel-content-retry-cancellation.bundle` | tip `cb659e9d3c70c76ae4b2ce73906d4d04596e4b95`，ref `refs/heads/t3/article-content-retry-cancellation`，前置 `62b376e` |
| bundle SHA256 | `714a45bec3eea5f11f72cefbb9964d669d756a7c5b2cfbfe371ec9909960dde6` |

恢复方式（只在待审查区；bundle 含 `b56f0d8` 与 `cb659e9`）：

```powershell
git -C externals/bili-kernel fetch <repo>/docs/performance-acceptance/dependency-upgrade/kernel.bundle refs/heads/integration/dependency-upgrade-d48:refs/heads/t3/restore-base
git -C externals/bili-kernel fetch <repo>/docs/kernel-article-null/kernel-article-final.bundle refs/heads/integration/article-null-final:refs/heads/t3/restore-final
git -C externals/bili-kernel fetch <repo>/docs/article-content-retry-cancellation/kernel-content-retry-cancellation.bundle refs/heads/t3/article-content-retry-cancellation:refs/heads/t3/article-content-retry-cancellation
git -C externals/bili-kernel checkout t3/article-content-retry-cancellation
```

前两条仅在 kernel 初态缺少 `62b376e` 时需要；本区 kernel 子模块对象库为空，因此按序恢复。
若父级不接受 kernel 指针前移，父 gitlink 已保持 `62b376e`，本 bundle 独立交付。

## 4. 边界与未覆盖

- 本轮只声明**已观测到的可行失败机制**：阅读页间歇返回结构完整但 `readInfo` 不可用的状态。不主张这
  已证实全部用户故障的根因，也不宣称展示/导航生命周期问题已被排除。
- 未取得桌面原生槽：没有 WinUI/WebView2 输入、视口、像素或动画证据；未复现真实文章渲染。
- 未做 Native AOT 编译或运行验收；上表的“生产 Debug x64”只是普通构建，不等于 AOT，也不是实际运行。
- 无网络真实文章采集、凭据或 URL/正文输出；原始日志、TRX 与临时构建仅在 ignored `artifacts/`。
- 维持有界 3 次 / 200ms 重取，不做浏览器兜底、无上限重试或静默空成功。

## Parent HTTP boundary correction and integration

Independent review reproduced two failures and one default-behavior pass. The parent reran those same cases before changing code: 2 failed / 1 passed. Restored the public `GetStringAsync(HttpRequestMessage, CancellationToken)` entry; it forwards to a nonoptional three-argument overload, avoiding optional-overload ambiguity. A using scope now releases the response/content on both successful reads and EnsureSuccessStatusCode failures. Added two successful-disposal cases, one per ensureSuccess setting.

Final kernel: `496728dd54f7d1c80c764c2dd8a4f9b68e12876b`. `kernel-content-final.bundle` includes b56f0d8, cb659e9 and the HTTP correction; prerequisite is `62b376e` from the existing article-final bundle. Ref `refs/heads/integration/article-content-final`; SHA256 `A7C1805168BC819F042305D6B72D144D85AAEE1FC4CF63AE6CB4E4C00366AA22`. Bundle verification passed; no remote publication.

Fresh exact-kernel suite: **32 passed / 0 failed** (original 27 + five HTTP boundary cases). Fresh combined application UI suite: **611 passed / 0 failed**. Earlier independent animator boundary suite remains separate (4 passed); do not sum these as one suite. Raw TRX/logs remain ignored in artifacts/article-http-integration.

The parent adopts this kernel gitlink and the reviewed application cancellation/page changes. No new production GUI build, Native AOT or WebView run was performed in this integration: avoid heavy background compilation during the active Win2D frame experiment. These validations remain pending. Installed AotDev 2.2611.1.4 is unchanged and does not contain this candidate.
