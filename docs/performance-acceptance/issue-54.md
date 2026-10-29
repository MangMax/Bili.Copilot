# Issue #54 — 播放器模式切换非阻塞收尾

## 范围与基线

- 工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-abcdfe17`；分支 `t3/fix-window-mode-switching`。
- 起始 HEAD 与本地 `feat/performance-45` 均为 `57dceffb1f20dc9db281755acb593c98265534f8`，包含 `d4592d3` Markdown reattach fix。
- 完成前 `git merge --no-edit feat/performance-45` 快进至 `8cf40300d533ef15acd1c034f6a2630bbb4c88d0`（Markdown 验收文档与 harness），其后复验。仅读取本工作区与已提交 Git 对象，不访问根集成工作区未提交文件。
- kernel HEAD 与 gitlink 均为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，源码及指针正常，未修复、未修改 kernel。
- 已读取 AGENTS.md、docs/agents 规则、#54、#45 和 PR #43；`gh issue edit 54 --repo MangMax/Bili.Copilot --add-assignee '@me'` 已执行。未 push、修改 PR、关闭 issue 或创建子代理。

## 实现与行为边界

四条模式路径保留原有 presenter 条件、进入模式标志及置顶清除逻辑，将 `Task.Run` 中的 `Task.Delay(500).Wait()` 替换为窗口专属 `PresenterChangeSession`。原问题是线程池工作线程同步等待，不是 UI 线程阻塞。

会话同步执行 presenter 操作，异步等待 500ms；每次操作拥有代次，只有当前代次能够清除 `IsChanging`。UI 调用上下文由 await 保留；不使用 ConfigureAwait(false)，不在工作线程写窗口状态。原生 presenter 异常仍同步传播，并清理对应代次的切换状态。会话不是通用窗口框架，无共享测试基建或新依赖。

Dispose/Destroying 永久结束会话；四个入口先检查关闭/释放状态。延迟收尾仅引用会话及代次，不访问窗口，也不持有原生 presenter 回调。保留有界的 500ms timer，不额外建立取消源。模式变化触发的标题栏延迟回调在入队执行及每次 await 后检查生命周期；排队焦点和输入激活回调也检查释放状态。Dispose 避免重复 Destroy。

## 行为红绿与测试边界

按 #45/#54 已批准的纯状态收尾会话接缝测试，链接生产源文件到现有 xUnit v2/VSTest 工程。使用可控 TaskCompletionSource 驱动完成顺序，无固定睡眠、私有字段断言、源码文本断言或窗口替身基础设施。测试中的模式字符串只是 presenter 回调的可观察输出，不能证明实际 AppWindow 模式、尺寸或置顶。

| Requirement | Evidence |
| --- | --- |
| 返回未完成任务且立即应用模式；旧等待不能清新状态 | `OldCompletionCannotSettleNewPresenter` |
| 最新先结束，随后第三次切换不被第一轮迟到收尾影响 | `LatestCompletionThenOlderCompletionCannotSettleThirdChange` |
| 关闭期间及关闭后不再执行窗口回调，重复关闭安全 | `CloseRejectsFurtherWindowAccessAndLateCompletion` |
| presenter 同步回调中关闭也不复活状态 | `PresenterCallbackCanCloseSessionBeforeDelayCompletes` |
| 就绪等待立即结束，应用时状态已标记 | `ImmediateCompletionAppliesPresenterWhileChangingAndSettles` |
| 同步原生失败传播且不残留切换状态 | `PresenterFailurePropagatesSynchronouslyAndDoesNotLeaveChangingState` |

红绿记录（`artifacts/issue54` 内 TRX）：

1. `issue54-red1.trx`：1 failed，旧等待完成后 IsChanging 错误为 false；`issue54-green1.trx`：1 passed。红测使用初始抽取的无代次会话复现旧的无条件清理语义，并非运行原始 WinUI 窗口。
2. `issue54-red2.trx`：1 failed / 1 passed，关闭后执行 presenter callback 抛出 Destroyed window accessed；`issue54-green2.trx`：2 passed。
3. `issue54-red3.trx`：1 failed / 2 passed，async 包装吞入 Task 导致同步异常未传播；`issue54-green3.trx`：3 passed。
4. 扩充乱序、同步关闭和立即完成覆盖后完整 Release suite 首次 348 passed；合并后最终结果见下。

精确命令（cwd 为上述工作区，SDK `10.0.403`）：

```powershell
# 各轮替换报告名为 issue54-red1/green1/red2/green2/red3/green3.trx。
# 首轮不传 --no-restore；后续均使用 --no-restore。
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~PresenterChangeSessionTests --logger 'trx;LogFileName=issue54-green3.trx' --results-directory artifacts/issue54 -v minimal

git merge --no-edit feat/performance-45
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=issue54-release-final.trx' --results-directory artifacts/issue54 -v minimal

dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-abcdfe17/src/ -p:GenerateAppxPackageOnBuild=false -v minimal
```

首次测试 restore/build 有 kernel protobuf unused import warning，未修改子模块或屏蔽诊断。运行库仅从用户指定的 `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets` 复制现有 libmpv/x64 和 ffmpeg/x64 至本工作区对应 ignored 目录；BBDown 已由仓库提供，未覆盖。没有提交运行库。

## WinUI 审查与兼容性

使用 tdd、code-testing、run-tests、winui-dev-workflow、winui-ui-testing、winui-code-review、verification-before-completion 技能。用户禁止桌面操作与安装环境的约束优先；未增加 WinUIAnalyzer 包，其专门诊断未运行。没有修改 XAML、绑定、资源、UI 文案、MVVM 或主题。

静态审查：切换标志在 SetPresenter 前设置；所有会话操作限于调用方 UI 上下文；await 后仅会话状态写入；关闭检查先于四入口读取 Presenter；模式变更引发的延迟标题栏、焦点回调有销毁保护；全屏/小窗进入时取消普通窗口置顶的既有逻辑未变，退出仍切回 Overlapped。未发现本次差异的必须修复项。生命周期 guard 的原生有效性仍需真实窗口验证。

PR #43 只读审查基于 head `5c1b52bb365c8888018969c2380623c9c7409bb5`。其 PlayerWindow 差异是在 DisposeAsync 中 `!IsClosed` 才 Destroy；本次同样保持这项必要的幂等销毁保护。其 CloseAsync/清理流程仍可调用原有 Close/DisposeAsync，不需托盘依赖；本次无托盘实现。仅静态 API 与生命周期兼容判断，未合入 PR #43、未构建组合分支、未验收托盘驻留/退出/恢复；未来合并仍需检查同文件冲突与行为。

## 真实 UI 与性能未验收

状态：代码/自动化验证交付，真实 UI 尚未验收，不能把本票标记为完整 ACCEPTED。

桌面由父级 Markdown 复验独占，本任务没有部署、启动、输入、捕获窗口，也未操作既有 Dev/生产身份、PID 9608 或认证资料。未安装环境。Debug build、纯会话测试和静态审查均不能替代以下检查：

- 真实播放器全屏↔小窗↔普通模式连续快速切换，最终可见 presenter 与最新操作一致。
- 两种模式各自还原、窗口位置/尺寸保存与原有置顶行为。
- 500ms 收尾期间关闭；标题栏两次 800ms 延迟和排队焦点期间关闭；无已销毁窗口异常。
- 独立/集成播放器关闭、托盘 PR #43 组合后的隐藏、恢复及完整退出。
- 同素材同构建 CPU、线程池等待和内存采样；没有耗时/百分比收益或泄漏消除结论。
- Release 原生应用、发布形态和 Native AOT 均未验证。

## 验证结果与原始记录

- 合并 `8cf4030` 后完整 Release suite：**Passed: 348, Failed: 0, Skipped: 0**，进程 exit 0。
- 使用 `[xml]` 解析 `artifacts/issue54/issue54-release-final.trx`，确认 total/executed/passed 均为 348、failed/notExecuted 为 0；348 条 UnitTestResult 全为 Passed，六个 PresenterChangeSessionTests 均已发现并通过。所有红绿 TRX 也已解析核对记录数量，汇总在 `artifacts/issue54/trx-summary.json`。
- 原始构建日志：`artifacts/issue54/debug-build.log`。这些运行产物保持 ignored，不提交二进制或测试基础设施。

TRX 最终校验命令：

```powershell
[xml]$trx = Get-Content artifacts/issue54/issue54-release-final.trx
$counts = $trx.TestRun.ResultSummary.Counters
$rows = @($trx.TestRun.Results.UnitTestResult)
if ($counts.total -ne 348 -or $counts.passed -ne 348 -or $counts.failed -ne 0 -or $counts.notExecuted -ne 0 -or $rows.Count -ne 348 -or @($rows | Where-Object outcome -NE 'Passed').Count -ne 0) { throw 'TRX validation failed' }
git diff --check
git -C externals/bili-kernel status --porcelain
```

Debug x64 构建已结束：exit 0，**0 errors / 1 warning**，耗时 00:06:53.40；唯一警告为未改动 kernel 的 `pgc/gateway/vega/v1.proto` unused `google/protobuf/empty.proto` import。构建在最终合并前启动、合并后完成；合并仅增加 docs/验收 harness，应用与测试生产输入未变。构建后按 WinUI 审查清单复核本次差异，无新增必须修复项。没有执行部署或启动，也没有将 JIT 构建宣称为 Native AOT 验证。
