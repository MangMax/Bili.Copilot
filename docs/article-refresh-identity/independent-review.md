# 固定提交独立审查

对象：`1b7b479f9ab2a99767a776f73e93feb0a1041c7c`；实现 `138af2b9`；对照已集成父 `af46c3ea`。2026-11-01，离线，无子代理、桌面输入、用户实例操作。本报告不合入实现，不改依赖/gitlink。

## Spec：一项未覆盖边界（P2，父版本已有）

`ArticlePartitionDetailViewModel.cs:87–111`：Refresh 在命令入口清游标，在低优先级回调才清集合。两者之间允许一次新的分页完成并写游标；回调第二次 Supersede 只作废请求、不再次清游标，随后刷新可能从后续页开始。

新反例 `RefreshQueuedCallbackMustRestartAtZeroAfterInterleavedLoad`：初始页完成 → Refresh 入队 → 回调前执行 LoadArticles 并完成，返回游标 42 → Drain。期望请求序列 `[0,0,0]`，实际 `[0,0,42]`。用户可见后果是清空旧列表后从后续页重新显示。建议把刷新游标/缓存重置与清集合、启动第一页放到同一个回调内，或阻止这段窗口的分页。

同一反例仅替换分区 VM 主文件为 `af46c3ea` 后仍失败，故**不是本次引入的回归**，也不能把它描述成已被代次机制修复。属于刷新完整性尚存的边界；是否纳入本片验收由父级确定。控件实际通过 CanExecute 调命令；本反例在初始请求已完成时插入分页，不依赖绕过正在执行命令的禁用状态。

## 已核对行为与限制

- 四种列表服务路径在 await 后先核代次，再追加/写游标；热门的 preventLoadMore 写入也有检查。旧成功与旧异常返回均不能关闭新代次 loading 或发出列表更新事件。原刷新接管旧在途第一页用例 fresh 通过。
- 阅读器 AllowConcurrentExecutions 保留可重入命令，Content/作者/统计/AI 注入/ArticleInitialized 在代次检查之后；旧失败/取消不记录当前错误或清 loading。fresh 用例覆盖旧响应先返回、当前内容先完成再收到旧成功、旧失败和旧取消。
- 这些命令不接受 CancellationToken，也不取消旧服务请求；测试取消是模拟服务返回 canceled task。catch(Exception) 包含取消，正常服务异常后会走受代次保护的收尾。没有 finally；评论 Initialize 位于 try 外，日志器或属性/事件处理器抛异常时不保证收尾。这些结构父版本已存在，未将其列为新增缺陷。没有验证真实评论/AI 异步内部状态。
- 缓存分支可直接恢复集合而不加载，Supersede 会释放 loading，旧响应仍被拒绝。另有父版本已有缓存归属问题：ChangeChildPartition 先更新 IsRecommend 再缓存旧集合，跨推荐/子分区时可能放错缓存；本次未修，不应宣称缓存身份全面正确。此项为静态边界记录，未补动态测试。
- 阅读器开始新加载时仅清 Content，不清旧作者/头像/统计；新请求失败后旧辅助字段可残留。这也是父版本已有行为，与“旧响应再次覆盖”不同。

## Standards / WinUI

未发现本差异新增的明确规范违例。检查了 AGENTS.md、code-review、winui-code-review、run-tests、code-testing、verification-before-completion 及 src/.editorconfig。按用户要求内联完成两轴审查，不使用技能中的子代理流程。

头像 OneWay 已在父 af46c3ea，父到固定提交差异无该 XAML 变化，不重复计贡献。本次未运行 WinUI analyzer、原生/AOT 构建或 UI 绑定/回收验证；不把 headless 结果当桌面证据。kernel ArticleAdapter NRE 不在本审查修复范围。

## 本次实际执行

生产源码导出固定提交到临时目录；kernel 从本机父区 Git 对象库导出精确 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`，未使用当前子模块状态。SDK `11.0.100-rc.3.26522.148`，xUnit/VSTest，链接真实 VM/接口，保留原有卡片、评论、AI 与 Dispatcher 替身。

```powershell
dotnet test "$r/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj" -c Debug --filter "FullyQualifiedName~ArticleRefreshGenerationTests|FullyQualifiedName~ArticleReaderLatestWinsTests" --logger "trx;LogFileName=review.trx" --results-directory "$r/results"
```

`$r` 为固定版本临时导出目录，应用本目录 evidence 下两个 review 测试补丁后执行。结果 **6 total / 5 passed / 1 failed / 0 skipped，exit 1**。其中原有 2 例通过，新补 4 例中 3 通过、上述刷新边界失败。见 [本次 TRX](evidence/independent-review.trx)。父分区 VM 对照仅筛选该边界，**1 failed，exit 1**，见 [父对照 TRX](evidence/independent-parent-boundary.trx)。首次临时导出缺 kernel .gitignore 导致 RepoRoot 导入失败，补齐同版本文件后才得到以上有效执行；未修改项目配置或包版本。

原 README 的 **579 passed** 和 Debug x64 0 warning/error 均为实现者历史证据，本次未重跑全量或生产 WinUI 构建。本次构建有原有 nullable 等警告，不能复述为零警告。

结论：Standards 新增明确问题 0；Spec 新增回归未发现，但刷新完整性仍有 1 项 fresh 失败边界（父版本已有）。不据此宣布整片无条件通过。
