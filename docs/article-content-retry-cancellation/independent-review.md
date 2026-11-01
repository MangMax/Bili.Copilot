# 固定候选独立 review

审查应用 `f44c22aa..eabb2a48`、kernel `62b376e..cb659e9`，对照前审 `c13f971d`。结论：文章共享判定、取消接入和页面窄修可采用；**kernel 整包暂阻断，需先修复下面两项 P2**。未修改生产代码、父历史或子模块现存工作状态，未 merge、push、PR、issue，也未启动子代理。

## 阻断

1. **P2：公开二参方法签名被删除。** `BiliHttpClient.cs:112` 将 `GetStringAsync(HttpRequestMessage, CancellationToken)` 替换为三参方法。可选参数只保证普通源码调用可重新编译，不能保留旧程序集的 MemberRef、反射或二参委托绑定兼容性。`OriginalTwoParameterSignatureIsRetained` 实测失败。保留原二参入口转发到显式三参重载；避免两个重载的可选参数造成歧义。报告“其余调用语义不变”应限定为重新编译后的默认请求行为，不能涵盖 API 兼容性。
2. **P2：HTTP 错误路径未释放 response/content。** `BiliHttpClient.cs:115-121` 中 response 无 using/finally；403 的 EnsureSuccessStatusCode 抛出后遗失响应所有权，成功路径也未释放。`ForbiddenResponseAndContentAreDisposedBeforeClientDisposal` 用可观测 Dispose 的内容实测失败；异常类型为 HttpRequestException。应让成功/异常路径均释放 response（连带 content/流），不要仅在成功返回后清理。此反例验证的是内容资源未释放，不宣称已观测连接池耗尽；SendAsync 默认缓冲也不等于响应已 Dispose。

新增源码为同目录 `IndependentHttpBoundaryTests.cs`，只补上述两个缺口及默认语义对照，不扩业务矩阵。第三项 `DefaultStillReturnsNonSuccessBody` 通过：默认调用依旧返回 403 正文。独立三项结果为 **2 failed / 1 passed**，不能与候选 27 项合并称为通过。

## 前审复核与可采用范围

- **正文一致性已修复。** HasUsableContent 与 ToArticleDetail 均调用 GetRenderableContent，统一 opus 优先和 FormatHtmlContent；未知段落独占、空 p、旧 content 非空但 opus 为空三个 case 会重取。`RenderableMediaAndMixedContentAreFetchedOnlyOnce` 三例分别断言图片、分隔线、未知段落混合有效文本保留且正文/元数据各一次。未发现第二套判定或合法媒体误拒。
- **403 重取边界已修复，但释放仍阻断。** `HttpForbiddenWithEmptyStateMustNotRetry` 证明正文一次、元数据零次。ensureSuccess=true 仅用于文章正文。普通网络/解析异常未被重取循环 catch；不把它们描述为重试对象。
- **VM 取消已接入。** BeginLoad 先换代、锁外取消旧 CTS；正文服务收到 token，kernel 将它传入正文 SendAsync、200ms delay 和 metadata SendAsync。`NewArticleCancelsInFlightHttpReadBeforeMetadata` 使用真实 VM、真实 kernel 服务与伪 HTTP，证明 A 读取取消且元数据零次，B 正常完成。`SecondOpenCancelsTheFirstRequest`、`LeavingReaderCancelsItsCurrentLoad`、`LateUnloadOfPreviousPageDoesNotCancelNewArticle` 覆盖新文章、离开和旧页面 generation 取消；迟到成功由 `SecondOpenRunsAndStaleResponseCannotOverwriteIt`、`ResponseArrivingAfterLeavingCannotLand` 拦截。取消异常不记当前错误；旧 finally 的 generation 判断保留，不清除 B 的 loading。
- **CTS 边界按源码核对。** Cancel 在锁外，finally 自行 Dispose；已完成源再次 Cancel 的 ObjectDisposedException 被吞掉，随后 Dispose 重复调用。同步完成后页面仍能记录代次并在离开时失效。7 项测试不等于任意线程同时调用命令/Cancel/Dispose 的压力验证；本轮不据此宣称跨线程安全。VM 属性与 generation 的落地检查也不是跨线程原子事务，采用范围是原 UI 调用上下文。
- **页面窄修可采用。** SetParameter 在执行命令前先退订再订阅，卸载先取消/退订后 Clear；CoreWebView2 空保护正确。`FailedSecondOpenClearsPreviousAuthorAndStats` 覆盖新身份清空。async WebView 注入完成时序没有原生证据，不能称真实渲染或完整生命周期验收通过。
- **措辞纠正可接受。** 固定 README 使用“已观测到的可行失败机制”，没有把它外推为全部用户故障原因，也保留 WebView/AOT 限制。

## 复放与来源

所有归档、恢复库、日志和构建输出仅在 ignored `artifacts/article-independent/`。应用为准确 eabb2a48 的 git archive；kernel 从现有对象补齐 bundle 前置，依次恢复 dependency-upgrade、article-final、候选 bundle，最终 HEAD 精确为 `cb659e9d3c70c76ae4b2ce73906d4d04596e4b95`。候选 bundle SHA256 为 `714a45bec3eea5f11f72cefbb9964d669d756a7c5b2cfbfe371ec9909960dde6`。恢复时初始缺少前置；没有 fallback f02，也没有用源码拼接 probe 冒充准确 kernel。

执行入口（以下路径相对 artifacts/article-independent；SDK 为仓库固定 11.0.100-rc.3.26522.148，xUnit/VSTest）：

- `dotnet test kernel/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj --nologo -v minimal`：**27 passed / 0 failed**。
- 红复放：准确 `b56f0d8` 归档，仅替换为 cb659e9 的 ArticleDiscoveryClientTests.cs（新增反例）；`dotnet test red/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj --nologo -v minimal` 得到 **4 failed / 23 passed**。失败恰为 UnrenderableBodyIsRefetched 三例及 HttpForbiddenWithEmptyStateMustNotRetry（实际正文 3 次）。这是带新测试的红对照，不是原封不动的 b56 测试集。
- 原 16 项（ArticleAdapterTests 12 + ReviewCounterexamples 4）相对 62b376e 测试源码无 diff；候选原客户端 3 项仍包含在 27 项中。
- `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~ArticleReaderLatestWinsTests --nologo -v minimal`：**7 passed / 0 failed**。
- 同一固定应用归档 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-build --no-restore --nologo -v minimal`：**596 passed / 0 failed**。这是候选历史的组合，不能混同父当前 605 或独立 animation 4；未将旧历史合入父。
- 将本目录 IndependentHttpBoundaryTests.cs 复制进 kernel 测试目录后，`dotnet test kernel/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj --no-restore --filter FullyQualifiedName~IndependentHttpBoundaryTests --nologo -v minimal`：**2 failed / 1 passed**。

未运行窗口、用户包、安装或读取登录数据。桌面独享仍归 Win2D `fa1a5e1f`；未申请/占用槽。未运行 WinUI analyzer、Native AOT 或真实 WebView2 渲染。本轮不重复候选 Debug 生产构建，也不将 headless VM 测试当作原生验收。

Standards：未发现文章应用窄改明确违反仓库书面标准；资源所有权问题见阻断 2。Spec：前审三项实现问题与原因措辞得到针对性纠正，但公开 API 兼容性和响应释放仍有两项实测阻断。固定应用 diff 的 git diff --check 通过。

