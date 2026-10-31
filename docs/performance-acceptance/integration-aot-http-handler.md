# 图片 HTTP handler 独立集成审查

2026-10-31。父 checkout `t3-1d19cc80` 独占，初始分支 `feat/performance-45`、HEAD `7aa55712bd1ffef840df9ace9b1c0a1300c1af5c`，root/kernel clean。候选 checkout `t3-39aaeabf` 固定 `08314a112d6996dba5673b938703eacc081fd600`，root/kernel clean。读取 AGENTS/domain、code-review、WinUI review、verification、run-tests、editorconfig 与构建约束，自行完成两轴审查，无子代理。

无阻断，以 `git merge --no-ff 08314a112d6996dba5673b938703eacc081fd600` 合入 **`83af5919e9f229dd9cbe0b2e60c71ebb499a5b74`**。双亲为上述父 HEAD 和固定 tip；无冲突，合并树与候选一致。后续提交仅集成报告、status 与复核证据。

## Standards

0 阻断。生产仅 `InternalHttpExtensions` 与嵌套 `CustomHttpClientHandler` 各增加 partial，去掉两个关键词并归一化换行后整份源码与父提交一致。SendAsync 的 HTTP 2.0、RequestVersionOrLower、原 cancellationToken 转发及 ConfigureAwait(false) 不变；没有改 timeout、重试、异常、Dispose、静态客户端所有权。未扩其他类型、NoWarn、正式依赖或 kernel gitlink。专用 WinUIAnalyzer 未运行；无 UI/XAML 改动。

## Spec

0 阻断。handler 唯一构造点传给托管 HttpClient，BasicCoverImage/BlurCoverImage 返回的是 HttpClient，RecommendationCoverImage 调用其 GetByteArrayAsync。未发现 handler 本体的 WinRT 注册或传参路径。IDisposable 映射 IClosable、外内层 partial 满足严格生成要求；图片控件使用客户端不证明 handler 实例跨 ABI，不声称修复了已复现运行故障。第三方控件内部所有路径、线上 HTTP、真实 Dispose 与 Native AOT 运行仍未验。

## 历史证据独立复核

[review.py](evidence-handler-aot-integration/review.py) 只读候选原产物，输出在父 ignored `artifacts/handler-aot-integration`；未运行会重写候选证据的原 verify.py。

- 73 份原产物长度及 SHA256 全匹配，包括最终应用 DLL/PDB、原构建日志/binlog、TRX。1543 份 strict 输入逐项散列匹配，非目标文件红绿逐字一致。
- 原 build.log 与提交压缩完整诊断集合相同：351→350，仅删除 handler `(53,26)` 的 CsWinRT1028，零新增；303 MVVMTK0045 + 46 MVVMTK0046 + DanmakuLoadSession 的 1 条1028逐项保留，两次仍 exit1。
- 审计副本局部 WinUICookies AllowUnsafeBlocks 红绿一致；参数没有全局 unsafe。audit-build 移除副本原 IL/MVVMTK 抑制，level2、分析器和 warnings-as-errors 保留。正式配置与 kernel 均未改，此证据不代表 kernel 1030 已修复。
- 原 baseline/all 各535、http-before/after各32，逐结果 Passed、executionId 唯一、前后名称集合相同，原 TRX 与提交 gzip 逐字一致。32项覆盖真实取消/timeout/重试及 MarkdownImageLoader，不直接覆盖私有 handler 的实际网络/释放。
- 候选应用 Debug/Release 构建 exit0 是历史结果，本轮未重跑应用 build、strict、ILC 或 publish。原 red 完整保留。

## 本轮 fresh 验证

- 新读取候选最终应用 DLL/PDB：生成 attribute 指向存在的详情类型，GetExposedInterfaces 有 Debug 52 / Release 47 字节 IL；PE/PDB identity 匹配，两配置各2594份文档实际散列匹配。候选 Models 两配置各7份文档亦匹配。生成源码明确映射 IDisposableMethods IID/vtable，生成摘录按换行归一化一致。
- 父执行 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=all.trx" --results-directory artifacts/handler-aot-integration/tests`，exit0，**Passed: 535, Failed: 0, Skipped: 0**，逐项名称与候选相同。
- 父完整 InternalHttpExtensions.cs 直接链接 Models，Debug/Release 各新构建0警告0错误。命令为 `dotnet build docs/performance-acceptance/evidence-handler-aot/Models/Models.csproj -c <Debug|Release> -p:HandlerSource=<父路径>/src/Desktop/BiliCopilot.UI/Extensions/InternalHttpExtensions.cs -p:CompilerGeneratedFilesOutputPath=<父路径>/artifacts/handler-aot-integration/generated-<配置>`。
- 使用候选 Metadata/Probe 工具读取父 Models，IClosable IID `30d5a829-7fa4-4026-83bb-d75bae4ea99e` 和非零 vtable 通过，各7份PDB源码匹配；父 Models WinRT.Runtime 与候选完整应用输出逐字相同。probe 不实例化 handler/静态客户端，不联网，不加载应用 initializer，不等于 COM QueryInterface 或完整 AOT 实机。

[finalize.py](evidence-handler-aot-integration/finalize.py) 复算并归档 fresh TRX、日志、PDB清单与[结果](evidence-handler-aot-integration/verification.json)。审查工具初次比较生成摘录未归一化 CRLF/LF，断言失败，修正后完整通过；一次读取猜测的旧 verifier 路径不存在，仅为准备查询错误。原始候选证据未改写。

## 保留状态

kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` clean，正式依赖及 gitlink 不变。完整 AOT/性能及真实网络运行 **HOLD**，OX-R1独立进行，#49 FAIL 保留。CoreRX 独占桌面；无 UI、部署、登录、安装、UAC、push、PR、issue 操作。
