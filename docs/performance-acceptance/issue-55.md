# #55 下载进程异步等待与释放

## 范围与基线

- 工单：<https://github.com/MangMax/Bili.Copilot/issues/55>，父规格 #45；已执行 `gh issue edit 55 --repo MangMax/Bili.Copilot --add-assignee '@me'`。
- 独立工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-6f7c09f0`，分支 `t3/async-download-process-wait`。
- 起点：`feat/performance-45` / `8cf40300d533ef15acd1c034f6a2630bbb4c88d0`。结束前合并 `3c526f5af07db90c0dbce6ee36d2093304f3236f`（Markdown 诊断证据）并复验；集成再次推进后，最终快进合入 `47f72537fb0cf8fb160042ea07ef395639f8cdd1`（#52），再做完整复验。均只操作本工作区分支，无冲突。
- 已读 AGENTS.md、docs/agents 规则、#55/#45；检查全部相关 PR 列表及 #60 的文件列表，未发现本下载路径的重复修复。
- kernel 始终为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，子模块有效、无源码修改，不需要修复 .git。
- 应用 implement-spec、tdd、code-testing、run-tests、winui-code-review、winui-ui-testing、winui-dev-workflow、verification-before-completion。本票是 focused 下载操作测试；未启动子代理。

## 实现与兼容性

下载 ViewModel 保留原来的命令构造、BBDown 可执行文件选择、ProcessStartInfo 默认值、后台启动和独立下载生命周期。复制命令分支仍在启动进程前返回；不改命令、认证、剪贴板或主窗口关闭逻辑。

`DownloadProcessRunner.RunAsync` 接收并拥有一个未启动的 Process，启动后使用 `WaitForExitAsync`，读取退出码并在 `using` 作用域内释放。退出码为 0 且允许自动开目录时才调用目录打开动作。不开目录时也异步等候并释放，不占用一条线程池线程持续同步等待。目录打开的 Process 包装器也释放，不终止 explorer。

启动/等待/打开目录异常由后台任务边界捕获，只记录异常类型；非零退出只记录退出码，不记录异常消息、堆栈、命令或认证参数，不发成功提示。没有取消令牌、Kill、作业对象或窗口生命周期绑定。释放 Process 包装器不终止子进程。

## 行为测试与红绿

测试接缝为下载操作 → 本地进程退出 → 开目录动作：直接执行应用实际使用的 DownloadProcessRunner，目录动作由可记录的 Action 代替，不打开 Explorer。不是 WinUI 点击测试，也没有替代/重写下载逻辑。

最终替身是 `cmd.exe /d /q /c "set /p gate= & exit /b N"`，`UseShellExecute=false`、`CreateNoWindow=true`、重定向 stdin。stdin 的 `finish` 行控制退出，关闭 stdin 也允许退出；无凭据、网络、下载、固定睡眠或杀进程。15 秒仅为测试失败超时。文件不存在路径使用临时目录下的随机名称。

| 要求 | 确切用例与证据 |
| --- | --- |
| 未完成不能开目录；成功完成后开一次且释放 | `SuccessfulDownloadOpensFolderAfterExitAndReleasesHandle`：完成前 Task 未完成、opened=0；输入后 opened=1、捕获 SafeHandle.IsClosed=true |
| 设置禁止或失败退出不能开目录 | `DisabledFolderOrFailedDownloadDoesNotOpenFolder`：三组 `(0,false)`、`(7,true)`、`(7,false)`；完成前进程仍活着，完成后返回真实退出码、opened=0、句柄关闭 |
| 启动失败不报成功且释放 | `StartFailureDoesNotOpenFolderAndDisposesProcess`：Win32Exception、opened=0；已释放的 Process 再 Start 抛 ObjectDisposedException |
| 开目录失败不漏下载句柄 | `FolderOpenFailureStillReleasesDownloadHandle`：受控 IOException 传播，下载句柄已关闭 |
| 多次完成、等待资源 | `RepeatedDownloadsReleaseHandlesAndRecordWaitingResources`：预热 1 次，3 批各 10 个并发；每批全部未完成时 opened=0，全部完成后 opened=10 且每个捕获句柄关闭 |
| 外部/复制命令/窗口关闭兼容 | ViewModel diff 静态复查：原分支与参数未变；未添加 UI 关闭取消或杀进程。真实交互待验 |

红阶段先提取与旧路径相同的 `Task.Run + WaitForExit` 实现。首次运行因 cmd 的 `echo ready ` 尾随空格暴露测试夹具问题，不算行为红；修正夹具后 `issue55-red-disposal.trx` 为 0 通过/1 失败，失败在完成后的 `SafeHandle.IsClosed`（实际 false）。加入异步等待及 using 后 `issue55-green-disposal.trx` 为 1 通过/0 失败。

后续扩展用例中曾尝试以 Process.Disposed 事件判断释放，该类型未按此假设触发事件；改为公开 Start 在释放后的 ObjectDisposedException 验证。最初 stdout 就绪握手也会把调用方负责释放的同步读取流带入资源统计；最终替身去掉不必要的 stdout 重定向，使用 stdin 出口门控。旧夹具采样不用于结论。

## 命令与报告

工具链：Windows，已安装 .NET SDK `10.0.403`；测试目标 net9.0，项目 RollForward=LatestMajor；xUnit 2.9.3 / VSTest，未更改包。

以下命令均在上述独立工作区执行。定向运行 exit 0：Passed 7, Failed 0, Skipped 0；8cf4030/3c526f5 上完整 Release suite exit 0：Passed 349, Failed 0, Skipped 0；最终 47f7253 上完整 suite exit 0：Passed 369, Failed 0, Skipped 0（包含 #52 新测试）。

```powershell
# 行为红阶段（历史执行，原同步实现，exit 1）
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DownloadProcessRunnerTests --logger 'trx;LogFileName=issue55-red-disposal.trx' --results-directory artifacts/issue55
# 第一轮绿（历史执行，exit 0）
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DownloadProcessRunnerTests --logger 'trx;LogFileName=issue55-green-disposal.trx' --results-directory artifacts/issue55
# 最终夹具与资源采样（exit 0）
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DownloadProcessRunnerTests --logger 'trx;LogFileName=issue55-sample-final.trx' --results-directory artifacts/issue55
# 首次合并返回 Already up to date；结束前再次执行，快进至 3c526f5
git merge --no-edit feat/performance-45
# 完整 Release suite（exit 0）
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=issue55-release-final.trx' --results-directory artifacts/issue55
# Debug x64，构建而不部署或启动
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-6f7c09f0/src/ -p:GenerateAppxPackageOnBuild=false
# 3c526f5 合并后复验
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=issue55-postmerge.trx' --results-directory artifacts/issue55
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug --no-restore -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-6f7c09f0/src/ -p:GenerateAppxPackageOnBuild=false
# 最终 47f7253 合并后复验（构建命令同上一行）
git merge --no-edit feat/performance-45
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=issue55-integrated.trx' --results-directory artifacts/issue55
```

TRX 使用 PowerShell `[xml]` 实际解析，不仅检查控制台绿色。`issue55-release-final.trx` 和 `issue55-postmerge.trx` 的 total/executed/passed 均为 349；最终 `issue55-integrated.trx` 均为 369 且有 369 个结果元素，failed/error/timeout/aborted/notExecuted 均为 0；定向报告为 7/7，全部用例名称与预期匹配。示例复核命令：

```powershell
[xml]$trx = Get-Content artifacts/issue55/issue55-integrated.trx -Raw
$trx.TestRun.ResultSummary.Counters
[xml]$focused = Get-Content artifacts/issue55/issue55-sample-final.trx -Raw
$focused.TestRun.Results.UnitTestResult | Select-Object testName,outcome
($focused.TestRun.Results.UnitTestResult | Where-Object testName -Like '*RepeatedDownloads*').Output.StdOut
```

本地 ignored 证据：`artifacts/issue55/*.trx`、`release-final.log`、`debug-build.log`、`postmerge-tests.log`、`postmerge-build.log`、`integrated-tests.log`、`integrated-build.log`。首次 restore 使用既有 SDK/NuGet 配置，无安装环境。仅从 `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets` 读取复制 `libmpv/x64/libmpv-2.dll`（121108480 bytes）及 `ffmpeg/x64/ffmpeg.exe`（107860992 bytes）到本工作区相同 ignored 路径，不提交二进制。

## 资源样本与限制

最终定向报告 `issue55-sample-final.trx` 中同一测试宿主的一次运行如下，未强制 GC：

| 阶段 | 宿主 handles | OS threads | OS waiting | pool busy | pool threads |
| --- | ---: | ---: | ---: | ---: | ---: |
| 预热完成 | 466 | 22 | 21 | 2 | 5 |
| 第 1 批 10 个等待 | 496 | 22 | 21 | 2 | 5 |
| 完成 10 次 | 486 | 21 | 19 | 3 | 5 |
| 第 2 批 10 个等待 | 516 | 21 | 19 | 4 | 5 |
| 完成 20 次 | 483 | 22 | 21 | 3 | 6 |
| 第 3 批 10 个等待 | 513 | 22 | 21 | 3 | 6 |
| 完成 30 次 | 503 | 22 | 20 | 3 | 6 |

所有 30 个捕获的下载进程 SafeHandle 在操作完成时均关闭，是确定性资源生命周期断言。宿主总句柄并未回到预热值，不能据此宣称总句柄零增长或长期无泄漏；运行时注册等待、流、线程池与测试运行器都在同一宿主内。OS waiting 是全宿主 ThreadState.Wait 计数，不等于下载等待线程；pool busy 是 GetMaxThreads 减 GetAvailableThreads 的瞬时差，含测试执行和回调，不是堆栈归因。各列也不是原子快照。没有 ETW/等待堆栈、长时间稳态样本或同构建真实 BBDown 性能基线，不宣称百分比收益、UI 更流畅或进程总内存下降。

## WinUI 审核与待验

静态检查关注：下载命令签名未变、后台异步任务异常被观察、未捕获 UI 同步上下文、没有跨线程访问 XAML、Process 所有权覆盖启动失败与完成、目录回调仅在成功和设置允许时执行、日志不含认证参数、未新增窗口关闭取消/杀进程策略。未改 XAML/绑定/主题/可访问性或集合行为。

Debug x64 首次构建 exit 0，0 errors、1 warning（既有 kernel gRPC `google/protobuf/empty.proto` unused import），耗时 3:22.32；3c526f5 合并后构建 exit 0，0 warnings、0 errors，耗时 2:06.24；最终 47f7253 合并后构建 exit 0，0 warnings、0 errors，耗时 1:19.77。静态审核无本票阻断项（Error/Warning）；Note 为下述专用分析器及真实 UI 验证未运行。

项目未引用 Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer，本次未安装，所以该专用分析器未运行；保留项目已有分析器和构建配置。测试项目直接链接生产 Pure 源码，是仓库已有惯例，没有新共享测试基建。

UI **未验证**：桌面由父集成 Markdown 诊断独占，未部署、启动、输入或操作桌面应用；未访问 PID 9608、既有包或凭据。真实外部 BBDown PATH 查找、复制命令剪贴板提示、真实 Explorer 开目录、主窗口关闭后的下载、长时/多下载真实资源表现，以及 Release/AOT 发布形态都待独立实机验收。本地子进程测试和静态审查不能替代这些项目。

未 push、修改 PR、关闭 issue 或修改父工作区。
