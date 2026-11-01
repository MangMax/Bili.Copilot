# 文章页刷新后的头像/内容错配、旧请求污染与打开失败

基线父 `6133ae590daf474d606e7b0afbdbf21e857b8ed1`，kernel `0ae5e4e000a41a07f6696746706df11c06e8835b`。
本区只修改文章相关的两个视图模型和一处文章卡片绑定；kernel 只读，gitlink、正式依赖与
`App.xaml` 全局缓存策略未改。没有 push、PR、issue 操作，没有读取登录存储或输出媒体 URL、
token、cookie、用户内容。

## 用户现象与真实 AOT 证据

用户报告的三个现象都发生在实际 AotDev 试用中。只读核验链：

| 项目 | 观测 |
| --- | --- |
| 已注册包 | `Mang.BiliCopilot.AotDev_2.2611.1.2_x64__z90r5zjzw296g`（Status Ok） |
| 安装 EXE | `C:\Program Files\WindowsApps\Mang.BiliCopilot.AotDev_2.2611.1.2_x64__z90r5zjzw296g\BiliCopilot.UI.exe`，SHA256 `64978DED3A0DC723AF3CDF337F29EBA17DD82D53A44946ABBB1AB191EAD93D3E` |
| 运行实例 | PID 47980，启动 2026-11-01 13:31:51，路径与上述安装一致；同名 Visor PID 46644 13:31:52 |
| 日志 | `%LOCALAPPDATA%\Packages\Mang.BiliCopilot.AotDev_z90r5zjzw296g\LocalState\Logger\log-2026-11-01.txt`，13:31:52 记录 `App version: 2.2611.1.2` |
| 文章失败 | 15:41:08 / 15:41:15 / 15:44:26 / 15:45:21 / 15:45:24 / 15:45:30 / 15:45:36 共 7 次 `加载文章内容时失败`，均为 `System.NullReferenceException`，栈指向 kernel `ArticleAdapter.ToArticleDetail`（其中 15:41:15 深入 `BuildHtmlContent`），再回到 `ArticleReaderPageViewModel.InitializeAsync` |

脱敏摘录见 [evidence/aot-log-sanitized.txt](evidence/aot-log-sanitized.txt)：只保留版本、时间、异常类型与
stack frame，移除了图片/媒体 URL。该日志不记录滚动、动画或恢复后的像素，**没有记录不代表没有 bug**。

PID 47980 / 2.2611.1.2 / 13:31:51 与本分支外的既有只读快照一致，本任务未启动、更新、关闭或
输入该实例。

## 三个缺陷分开处理

### 1. 回收卡片头像陈旧（已由 `4ca81f88` 交付，本区原样纳入）

`PartitionArticlePresenter.xaml` 的头像 `Source` 原为 `{x:Bind ViewModel.Avatar}`（默认 OneTime），
而卡片其余字段、封面和全应用其它卡片头像都是 OneWay。回收后卡片 ViewModel 变化时，标题/封面
跟随当前文章，头像仍属上一个占用该容器的条目，形成“头像与内容不对应”。

本区保留并纳入 `4ca81f88a455616f4a8142e16c2af602e029dbb6`（`t3-cca140f6`）的同一行修复：
`Source="{x:Bind ViewModel.Avatar, Mode=OneWay}"`，避免两处重复补同一行。该提交的原生 WinUI
50/500 唯一头像身份/像素证据见
[scroll-continuity-results.md](../list-resize/scroll-continuity-results.md)；
**本分支没有重新运行那份原生证据**，只静态确认了最终绑定模式，并把原生复核留给父级独立审查。

### 2. 刷新不换内容、旧分页污染新列表（本区修复 + 受控测试）

`ArticlePartitionDetailViewModel` 的刷新/切换排序/切换子分区都会清空 `Articles` 再加载，但
`LoadArticlesAsync` 用 `IsArticleLoading` 做重入保护：

* 刷新执行时若仍有一次分页加载在路上，刷新排入的低优先级回调里 `LoadArticlesAsync` 会**直接返回**，
  刷新根本不会发出新的第一页请求；
* 随后旧请求完成，`TryAddArticles` 把旧页追加到刚清空的集合，并覆盖 `_recommendOffset` /
  `_childPartitionOffsetCache`。

结果就是“刷新后内容没变”（旧页被追加回来），并且下一次分页会从被旧请求改写的游标继续。
修复为列表代次：刷新/切换排序/切换子分区同时作废在途加载，返回值只在代次仍匹配时才能追加、
写回游标或关闭新请求的加载态。保留了原有低优先级清空、缓存与去重策略，未引入增量刷新/undo
改造（该语义仍属于 video/popular 页面，未被触碰）。

红/绿：`ArticleRefreshGenerationTests.RefreshDiscardsPendingPageAndKeepsFreshCursor`

* 红（修复前）：刷新后请求页序列为 `[0]`，刷新未发出新请求；
* 绿（修复后）：`[0, 0]`；迟到的旧页不追加、不写回游标；新结果生效；下一次分页使用刷新返回的游标 5。

### 3. 点击进入后文章加载失败（日志已定位，本区不修）

7 次真实 AOT 失败发生在 **kernel 反序列化/映射**，不是网络整体不可用：

* 调用 `GetArticleContentAsync` 先取正文 HTML、再取元数据；两者都成功后才进入
  `ToArticleDetail`，因此失败不是“文章 Id 为空/解析不到页面”（那会是
  `KernelException`，不是这里的 NRE）；
* `ToArticleDetail` 直接 NRE 的可能点：`response.readInfo` 为 null、`view.stats` 为 null
  （`stats.view`），或 `response.readInfo.content` 为 null 时进入 `FormatHtmlContent`；
* 另有 1 次深入 `BuildHtmlContent(Opus)`：`opus.content.paragraphs`、`item.text.nodes`
  或 `node.word` 在 `node_type == 1` 时未做空值保护。

这是对合法响应的空值处理缺陷，属于**文章解析路径**，不是“先假定 API 问题”。修复位置在
`externals/bili-kernel` 子模块；本任务约定不改正式依赖/gitlink，且 kernel 在本区是只读来源，
因此只保留脱敏证据交父级处理，没有在本分支声称修复。

### 4. 单例阅读器的旧请求污染（本区修复 + 受控测试）

`ArticleReaderPageViewModel` 在 DI 中是单例，`InitializeCommand` 会被连续导航直接 `Execute`。
修复前第二次打开与第一次并行时，先返回的旧响应会覆盖 `Content`，`InitializeUser/InitializeStats`
把标题、作者、头像、统计改回旧文章，并提前结束加载态。这不等于第 3 条的 NRE，但同样属于
“旧请求污染新文章”。

修复为内容代次 + `AllowConcurrentExecutions = true`：旧代次响应不再写入 Content/作者/头像/统计/
加载态，也不触发 `ArticleInitialized`。

红/绿：`ArticleReaderLatestWinsTests.SecondOpenRunsAndStaleResponseCannotOverwriteIt`

* 红（修复前）：打开 B 后，迟到的 A 响应使 `Content` 变成 A（`Assert.Null` 失败）；
* 绿（修复后）：B 的标题/标识立即生效，A 的迟到响应被丢弃，只有 B 注入 AI 并触发一次初始化。

## 验证范围

| 项目 | 结果 |
| --- | --- |
| 生产 WinUI 工程 Debug x64 单工程构建（带 `SolutionDir`） | 0 warning / 0 error |
| `BiliCopilot.UI.Tests` 全量 | 579 passed / 0 failed / 0 skipped |
| 新增两例红/绿（同一测试工程、同一筛选） | 红 0 passed / 2 failed；绿 2 passed / 0 failed |
| 真实 AOT 日志/版本/二进制 | 已核验（见上表与脱敏摘录） |

TRX：[article-red.trx](evidence/article-red.trx)（2 failed）、
[article-green.trx](evidence/article-green.trx)（2 passed）。

### 明确未覆盖

* 未取得桌面槽，**没有**原生 UI 输入、没有重新跑 50/500 回收滚动或像素断言；头像行的原生
  50/500 结论来自 `4ca81f88`，本区只纳入该行。
* 两个测试链接真实生产视图模型与真实 `IArticleDiscoveryService` 接口，但卡片条目
  `ArticleItemViewModel`、评论模块和 AI 模块使用最小替身；它们不是本次修复对象，也不单独
  证明 UI 绑定。真实模板侧的回收断言仍需父级原生复核。
* 第 3 条打开失败未修复；第 2 条的“刷新”语义只覆盖文章分区页现有的清空重载，不包含视频/
  popular 的增量刷新与 undo。
* 日志中没有滚动/动画/交互事件，无法从日志反推具体点击了哪篇文章；未用失败次数推断某篇文章。

## 复放

```bash
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Debug
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-build -c Debug \
  --filter "FullyQualifiedName~ArticleRefreshGenerationTests|FullyQualifiedName~ArticleReaderLatestWinsTests"

dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -p:Platform=x64 -c Debug \
  -p:SolutionDir="<repo>/src/"
```

红态复放：把两个视图模型目录临时还原为 `HEAD` 版本后重跑同一筛选；不要同时改动测试文件。
