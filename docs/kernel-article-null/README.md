# 文章打开失败：kernel 解析空值修复（实现与证据）

本区父提交 `af46c3ea`，kernel 基线 `0ae5e4e000a41a07f6696746706df11c06e8835b`。
本区只修改 kernel 文章适配器并新增 kernel 本地测试；**未 push、未开 PR/issue、未修改 gitlink**。
没有读取登录存储、token、cookie、媒体 URL 或用户内容，也没有启动、更新或操作用户的 AotDev 实例。

| 项目 | 值 |
| --- | --- |
| kernel 本地分支 | `t3/article-null-guard` |
| kernel 新提交 | `41891eeb28652b357c7ba0a34733347d36733722`（父 `0ae5e4e`） |
| 可恢复 bundle | [kernel-article-null.bundle](kernel-article-null.bundle) |
| 精确补丁 | [kernel-article-null.patch](kernel-article-null.patch) |
| 红/绿 TRX | [red](evidence/article-adapter-red.trx)（9 失败/3 通过）、[green](evidence/article-adapter-green.trx)（12 通过） |
| 父仓 gitlink | **仍为 `0ae5e4e`，本区未改；请父级审查后再决定** |

---

## 1. kernel 初始状态异常（先记录，再恢复）

开工时本工作树的 kernel 子模块不是报告约定的 `0ae5e4e`：

| 观测 | 值 |
| --- | --- |
| `git -C externals/bili-kernel rev-parse HEAD` | `f02c33f639addd5d421700c43fa66382beafe323` |
| 索引状态 | 474 个文件全部 `D`（删除），工作区只剩 `.git` |
| `0ae5e4e` 对象 | 不存在（`fatal: Not a valid object name`） |
| 子模块 reflog | 只有 `clone: from https://github.com/MangMax/bili-kernel.git` |

这与 `1b7b479f:docs/article-refresh-identity/evidence/initial-state.md` 在另一工作树
（`t3-8d1afd60`）记录的异常完全一致，属于工作树初始状态问题，不是本任务产生的改动。

**本区恢复过程（只动本工作树的子模块，不触父仓/其它区）：**

1. 在子模块内验证已提交的恢复包
   `docs/performance-acceptance/dependency-upgrade/kernel.bundle`：
   提供 `refs/heads/integration/dependency-upgrade-d48` = `0ae5e4e`，
   前置 `6501985` 在子模块历史中存在。
2. `git reset --hard HEAD` 还原被索引删除的工作区。
3. 从该 bundle `fetch` 后 `checkout --detach 0ae5e4e`，得到
   `HEAD = 0ae5e4e000a41a07f6696746706df11c06e8835b`、474 个跟踪文件、`git status` 干净。
4. 仅在子模块本地设置 `user.name/user.email`（与仓库既有作者一致）用于本地提交，未改全局配置。

父仓在恢复后 `git status` 只显示预期的子模块差异；父仓历史、其它工作树和用户数据未被改动。

---

## 2. 真实 AOT 日志证据（只读、脱敏）

来源：`%LOCALAPPDATA%\Packages\Mang.BiliCopilot.AotDev_z90r5zjzw296g\LocalState\Logger\log-2026-11-01.txt`。
本区只读统计了 15:41–15:46 窗口，**没有输出**日志正文、文章 id、标题、用户内容或媒体 URL：

| 统计 | 结果 |
| --- | --- |
| `NullReferenceException` | 7 |
| `ArticleAdapter.ToArticleDetail` / `BuildHtmlContent` 栈帧 | 8（其中 1 帧为 `BuildHtmlContent`，其余为 `ToArticleDetail`） |

脱敏摘录（仅版本、时间、异常类型、stack frame）见
`1b7b479f:docs/article-refresh-identity/evidence/aot-log-sanitized.txt`。
其中 15:41:08 / 15:41:15 / 15:44:26 / 15:45:21 / 15:45:24 / 15:45:30 / 15:45:36 共 7 次失败；
15:41:15 深入 `BuildHtmlContent(Opus)`。

**证据边界：** NRE 不能证明“所有网络请求都成功”，也不能证明是 API 故障。可以确定的是：
两个请求都返回了内容并通过了 `BiliHttpClient.ParseAsync` 的 code/Data 检查，失败发生在
`ArticleDiscoveryClient.GetArticleContentAsync` 之后的**映射阶段**。发布版 stack 没有行号，
除 `BuildHtmlContent` 那一帧外，无法从日志判断其余 6 次具体是哪个字段为空，也无法反推是哪篇文章。

---

## 3. 空值的确切可达条件

以下均按 `0ae5e4e` 的 `ArticleAdapter.cs` 行号；`System.Text.Json` 不会因为模型属性标注为非空就拒绝
JSON 中缺失键或显式 `null`。

| 字段/路径 | 基线 NRE 点 | 可达条件 |
| --- | --- | --- |
| `response.readInfo` | `ToArticleDetail` 第 40 行 `response.readInfo.author` | 页面中的 `window.__INITIAL_STATE__` 被正则匹配并反序列化成功，但 `readInfo` 缺失或为 `null` |
| `readInfo.author` | 第 40 行 | `readInfo` 存在，但 `author` 缺失/为 `null` |
| `view.stats` | 第 41–45 行 `stats.view` … | 元数据接口 `code == 0` 且 `data` 非空（否则客户端已抛 `KernelException`），但嵌套 `stats` 缺失/为 `null` |
| `opus.content` / `content.paragraphs` | 第 61 行 `opus.content.paragraphs` | `opus` 非空，但 `content` 或 `paragraphs` 缺失/为 `null` |
| `item.text.nodes` | 第 66 行 | 段落 `para_type == 1`，但 `text` 或 `nodes` 缺失/为 `null`（空文本段落） |
| `node.word` | 第 72 行 `node.word.words` | 节点 `node_type == 1`，但 `word` 缺失/为 `null`（被裁剪/不完整的 opus 数据） |
| `readInfo.content` | 第 56 行 `FormatHtmlContent(content)` | `opus` 为空且 `readInfo.content` 缺失/为 `null` |
| `item.pic.pics` / `img.url` | 第 94 行 | `para_type == 2`，但 `pic`/`pics` 缺失，或图片 `url` 为 `null` |
| `item.line.pic` | 第 110–112 行 | `para_type == 3`，但 `line`/`pic` 缺失，或 `url` 为 `null` |
| 未知 `para_type` | 无 NRE | 不进入任何分支，正文变为空字符串——**静默的空成功**，阅读器显示空白文章 |

已确认原本就安全、无需改动的点：`author.vip?.type`、`readInfo.tags?`、`WebUtility.HtmlEncode(null)`、
`node.word.words == "\\n"`（`null == "\\n"` 为 `false`，不会 NRE）。

另：若页面完全没有 `__INITIAL_STATE__`，`GetArticleInitialJsonAsync` 返回 `null`，
`JsonSerializer.Deserialize(null, …)` 会在进入适配器前抛 `ArgumentNullException`；那是另一条路径，
不属于本次 7 次 NRE 的映射阶段，本区没有改网络/解析流程。

---

## 4. 修复（最小改动，保留正文语义）

文件：`externals/bili-kernel/src/Services/Services.Article/Core/Adapters/ArticleAdapter.cs`
（新提交 `41891ee`）。

| 行为 | 修复后 |
| --- | --- |
| `readInfo` 缺失 | 抛 `KernelException("文章内容数据为空，无法解析该文章.")` |
| `author` 缺失 | 抛 `KernelException("文章作者信息缺失，无法解析该文章.")` |
| `view`/`stats`/点赞/收藏/关注缺失 | 视为可选：计数传 `null`（`ArticleCommunityInformation` 本就接受 `int?`），标志取默认值，不中断正文 |
| `opus.content`/`paragraphs` 为空 | 不抛 NRE；交由正文判定 |
| `text`/`nodes` 为空、`node.word` 为空 | 跳过该段落/节点，保留其余内容 |
| `pic`/`pics`/`url`、`line`/`pic`/`url` 为空 | 跳过该图片/分割线，保留其余内容 |
| 未知 `para_type` | 在存在受支持内容时忽略；只有未知类型时正文为空 |
| 正文为空或只有不受支持内容 | 抛 `KernelException("文章正文为空或格式不受支持，无法显示该文章.")`，**不再静默返回空文章** |

语义保持：

* 合法文本节点的输出与原来逐字一致（`<p style="font-size:'…'">…</p>`、`<strong>`、换行跳过）；
* 图片段落与分割线的原始 HTML 模板逐字保留（仅把 `line.pic.url` 改为已判空的 `line.url`）；
* 旧版 `readInfo.content` 仍走原 `FormatHtmlContent`：`data-src → src`、`"// → "http://`、
  删除 `<p></p>`/`<p><br></p>`/`<p><br/></p>`，段落/链接/图片标签不被改写；
* 异常类型沿用 kernel 既有约定 `RichasyKernel.KernelException` 与中文消息。

没有改动 `ArticleDiscoveryClient`、响应模型、SourceGenerationContext、HTTP 层，
也没有重复修改上游 `ArticleReaderPageViewModel`（其 `catch` 仍只记录日志；kernel 现在给出可辨识的
`KernelException` 消息，便于阅读器后续呈现失败）。

---

## 5. 测试：真实适配器 + 脱敏结构化 fixture

测试工程（kernel 本地，随 bundle 一起恢复）：
`externals/bili-kernel/src/Tests/Services.Article.Tests/`。

* 直接调用生产代码 `ArticleAdapter.ToArticleDetail` / `BuildHtmlContent`，不是替身；
* fixture 经真实 `SourceGenerationContext` + `JsonSerializer` 反序列化成 `ArticleContentResponse` /
  `ArticleViewResponse`，因此也覆盖真实 JSON 映射；
* 通过 `Services.Article.csproj` 的 `InternalsVisibleTo` 暴露内部适配器，未把适配器改为 public；
* fixture 全部使用合成 id、合成文本和 `example.invalid` 主机，不含真实媒体 URL 或用户内容。

| fixture | 场景 | 断言 |
| --- | --- | --- |
| `legacy-content.json` + `view-stats.json` | 合法旧版 HTML 正文、完整元数据 | 正文逐字保留段落/链接/图片并完成 `data-src`/`//` 转换；作者、统计、标签、时间、点赞/收藏/关注正确 |
| `opus-content.json` | 合法 opus 文本/图片/分割线 | 文本字号与加粗、图片 `<figure>`、分割线模板逐字保留；`\n` 节点跳过；顺序为文本→图片→分割线 |
| `view-no-stats.json` | 缺可选统计 | 不抛异常；6 个计数为 `null`；点赞/收藏/关注默认值 |
| （`null` 元数据） | 元数据对象缺失 | 仍渲染正文；计数 `null`、关系 `Unfollow` |
| `partial-empty-text.json` | `text` 为空、`nodes` 为空、`word` 为空 | 跳过空段落/空节点，保留有效文本 |
| `partial-payloadless-media.json` | `pic` 为空、图片 `url` 为空、`line` 为空 | 跳过无效媒体，保留有效文本 |
| `unsupported-mixed.json` | 未知 `para_type` 与有效段落混排 | 忽略未知类型，保留有效正文 |
| `invalid-missing-readinfo.json` | `readInfo` 为 `null` | `KernelException`，消息含“文章内容数据为空” |
| `invalid-missing-author.json` | `author` 为 `null` | `KernelException`，消息含“文章作者信息缺失” |
| `invalid-empty-body.json` | `content`/`opus` 均为 `null` | `KernelException`，消息含“文章正文为空或格式不受支持” |
| `invalid-opus-without-paragraphs.json` | `opus.content` 为 `null` | 同上 |
| `invalid-unsupported-only.json` | 只有未知 `para_type` | 同上（红态下旧代码会静默返回空正文） |

红/绿（同一工程、同一筛选）：

```powershell
dotnet test externals/bili-kernel/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj `
  -c Debug -f net8.0 --filter "FullyQualifiedName~ArticleAdapterTests"
```

| 状态 | 结果 |
| --- | --- |
| 红（仅把 `ArticleAdapter.cs` 还原为 `0ae5e4e`，保留测试接缝） | **9 failed / 3 passed / 12 total** |
| 绿（`41891ee`） | **12 passed / 0 failed / 12 total** |

红态下仍通过的 3 个是合法内容测试（旧版正文、opus 正文、未知类型混排），说明修复没有改变合法输出；
失败覆盖 `readInfo`、`author`、`stats`、`text.nodes`、`node.word`、`pic/line`、空正文与未知正文。
TRX 见 [red](evidence/article-adapter-red.trx) / [green](evidence/article-adapter-green.trx)。

**测试边界：** 只覆盖映射与本地反序列化，不覆盖 HTTP、真实文章样本或 WinUI 呈现；未用日志失败次数
推断具体文章。上游阅读器 VM 的失败呈现仍是既有行为，本区未改。

---

## 6. 验证与恢复

| 项目 | 结果 |
| --- | --- |
| `Services.Article` 多目标构建（netstandard2.0 + net8.0） | 0 error；仅 Grpc proto 的既有 import 警告（与本改动无关） |
| 红/绿测试 | 红 9/3；绿 12/12 |
| bundle 校验 | `git bundle verify` okay；tip `41891ee`，前置 `0ae5e4e` |
| 精确恢复链（离线验证） | 从 `main` 单分支克隆（= `f02c33f`，`0ae5e4e` 不存在）→ 旧 `kernel.bundle` → `0ae5e4e` → 新 bundle → `41891ee`；`HEAD^{tree}` 与源工作树完全一致 |
| 补丁 | 在 `0ae5e4e` 上 `git apply --check` 通过 |

文件校验：

| 文件 | SHA256 |
| --- | --- |
| `kernel-article-null.bundle` | `82960118afbea1bd179e2ce2332f3e422d7ab70d3d6ef20dbdff1ec37e8bc0c9` |
| `kernel-article-null.patch` | `9904de0b7b897ce67c41f38481e0b545bdc0f935f409921cbc94f86fa8b435a1` |
| `evidence/article-adapter-red.trx` | `a24da9a1525ebb3f8ae2aac26baebff18b970f7ef4999a734c096300b3bc0990` |
| `evidence/article-adapter-green.trx` | `754d13bf1f022129ab15926b89e55d5bbd6f4c78da3139524cd7647ce3315474` |

复现顺序：

```powershell
# 1) 从当前工作树的 kernel 仓库导出新提交（本区已完成，产物见上）
# bundle 需要具名 ref，否则会被判定为空 bundle
git -C externals/bili-kernel bundle create `
  docs/kernel-article-null/kernel-article-null.bundle 0ae5e4e..t3/article-null-guard
git -C externals/bili-kernel format-patch 0ae5e4e..t3/article-null-guard --stdout `
  > docs/kernel-article-null/kernel-article-null.patch

# 2) 在干净 clone 上按链路恢复
git fetch <old-kernel.bundle> refs/heads/integration/dependency-upgrade-d48
git checkout --detach 0ae5e4e
git fetch docs/kernel-article-null/kernel-article-null.bundle refs/heads/t3/article-null-guard
git checkout --detach 41891ee
```

---

## 7. 父级需要明确的事项

1. **gitlink 本区故意未改**，仍指向 `0ae5e4e`；因此父仓会显示 `M externals/bili-kernel`
   （子模块工作区在新提交上）。请父级独立审查后再决定是否把 gitlink 指向 `41891ee`。
   在决定前不要把子模块路径加入父仓提交。
2. 空正文策略：本区选择抛 `KernelException` 而不是返回空文章。请确认阅读器端能接受该可辨识失败。
3. 可选统计策略：缺 `stats` 时计数为 `null`（沿用 `ArticleCommunityInformation` 的 `int?`），
   而不是填 `0`。请确认与 UI 展示预期一致。
4. 测试工程是 kernel 本地新增工程，未加入 `src/BiliKernel.sln`，也未接入 CI；是否纳入由父级决定。
5. 本区未做 HTTP 级、真实样本级或 WinUI 呈现级验证；日志无法定位具体失败文章。
6. 未 push、未开 PR/issue；以上均为本地提交与本地证据。
