# 托盘接口生成独立集成审查

2026-10-31。父 `feat/performance-45` 初始 `bfa3d0476353f2c8abe28c1589a6d939b114ffba`，root/kernel clean。候选固定 `4c31a79b594d6b0f552111b84740c4eb02c7fe7e`，证据工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-c05d2140`，候选 root/kernel 亦 clean。核验通过后以 `--no-ff` 合入 `419afe099a03f4a4504066c57a1dd6ce595ea389`，无冲突。父 checkout 独占操作，无子代理。

## Standards

无阻断。读取 AGENTS/domain、code-review、WinUI review、verification、run-tests，以及源码 editorconfig/构建约束。生产差异仅 App.TrayIconSession 与 TrayLifecycle 各增加 partial；去掉关键词并归一化换行后与父源码逐字相同。公开契约、TaskbarCreated/HealthChanged/ExecuteRequested 订退阅、Dispose 顺序及异常传播不变；驻留关闭、RestoreMainWindow 的 Show/Restore/Activate/SetForegroundWindow 与共享退出任务、保存/播放器清理均未改。没有改其他生产类、NoWarn、依赖、正式 Mpv 或 kernel gitlink。专用 WinUIAnalyzer 未运行。

## Spec

无阻断。ITrayIconSession/TrayLifecycle 的 IDisposable 映射生成 IClosable 表。App 构造/托管持有 lifecycle，session 由 lifecycle 持有；WinUI 命令目标是 App，TaskbarCreated 为带 managed add/remove IL 的 System.EventHandler。持有 TaskbarIcon、事件目标和闭包不是 session/lifecycle 本体跨 WinRT ABI 的证据。该修改满足严格接口生成要求，不宣称修复已复现托盘运行故障。

非阻断文档修正一项：原报告写 H.NotifyIcon 2.3.2，但其 packages.json 与解析依赖为 2.4.1。已纠正报告，原生产历史注释未扩改；实际依赖未变。原 DLL 上事件类型及 managed accessor 的复核仍成立。

## 历史证据复核

- 88份原产物长度及 SHA256 全匹配，包括最终应用 DLL/PDB、构建日志/binlog、probe red 和 TRX；原证据未重写。
- 原始日志与压缩诊断全文相等：strict 353→351，只删除 TrayIconSession `(236,26)` 与 TrayLifecycle `(8,23)` 的两条1028，零新增。余303 MVVMTK0045 + 46 MVVMTK0046 + 2 CsWinRT1028逐项保留（DanmakuLoadSession、CustomHttpClientHandler）；两次历史 exit1。1543份 strict 源文件逐项散列复算，非目标文件均相等，包含审计配置。
- 原 baseline/all各535，tray-before/after各25，逐结果 Passed、名称前后相同，TRX与提交gzip逐字一致。
- 候选应用Debug/Release构建exit0、各1条既有protobuf warning，是历史构建结果。没有把历史构建、strict、ILC当作父fresh；本轮未重跑应用build、strict、ILC或publish。

## 本轮 fresh 验证

- 重新读取候选最终Debug/Release应用DLL/PDB：PE/PDB身份匹配，各2593份源码全部存在且散列匹配；Python另行复算全部文档。两个生成attribute均指向存在的详情类型，GetExposedInterfaces有52/47字节IL。候选Models各9文档及两配置接口probe重新核验通过。
- 父运行 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=all.trx" --results-directory artifacts/tray-aot-integration/tests`，exit0，**Passed: 535, Failed: 0, Skipped: 0**。逐结果及全部名称与候选相同。
- 从父App原样提取完整session声明，Models直接链接父真实TrayLifecycle/ITrayIconSession；两配置新构建均0 warnings/0 errors。命令为 `dotnet build docs/performance-acceptance/evidence-tray-aot/Models/Models.csproj -c <Debug|Release> -p:SourceRoot=<父路径> -p:SessionSource=<父路径>/artifacts/tray-aot-integration/session.cs -p:AppBin=<候选路径>/src/_build/x64/<配置>/BiliCopilot.UI/bin`。
- 使用已验hash的候选Metadata/Probe工具DLL检查父Models：两声明的IClosable IID及非零vtable通过，各9份PDB文档匹配父输入；五项关键真实依赖与已核验候选应用输出逐字节相同。两个生产文件与候选逐字相等。简化外层App的probe不代表完整应用上下文，也未运行COM QueryInterface、UI或Native AOT。

[复算脚本及证据](evidence-tray-aot-integration/verification.json)包含父TRX、构建日志、接口结果、压缩PDB文档清单与散列。大文件保留在父ignored `artifacts/tray-aot-integration` 和候选原工作区。复核脚本初次Python读超长生成文件路径失败；添加Windows长路径前缀后完整复算通过，未改源文件或覆盖候选证据。准备阶段从父读取尚未合入报告失败，仅为检查路径错误。

## 保留状态

真正AOT托盘、真实原生资源释放及前台策略仍待验；完整AOT和性能HOLD。OX-R1在独立候选修复，本片未合入，#49仍FAIL。kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` clean，gitlink不变。没有UI/部署/登录/安装/UAC/push/PR/issue操作。两轴共0阻断；仅上述1处文档版本勘误已处理。
