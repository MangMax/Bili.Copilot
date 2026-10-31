# DanmakuLoadSession 独立集成审查

2026-10-31。独占父 checkout `t3-1d19cc80`，初始分支 `feat/performance-45`、HEAD `acca8df07108777c8547a46d6b6b909ff9650cdd`，root/kernel clean；kernel HEAD 与正式 gitlink 均为 `0ae5e4e000a41a07f6696746706df11c06e8835b`。候选 `t3-e90045ae` 固定 `ee740a628cf3a181129c80fdff6f32743904055e`，root/kernel clean。读取 AGENTS/domain、code-review、WinUI review、verification、run-tests、editorconfig 与构建约束；按任务要求自行审查，无子代理。

无阻断，以 `git merge --no-ff ee740a628cf3a181129c80fdff6f32743904055e` 合入 **`a618484f5616c401d16a98e42e0f6782b4b3a30a`**，双亲为上述父 HEAD 与固定 tip，无冲突、合并树与候选一致。后续提交仅 status、集成报告及验证工具/证据。

## Standards

0 阻断。唯一生产变化为 DanmakuLoadSession 顶层声明增加一个 partial；移除该关键词并归一化换行后，源码与输入父提交一致。锁、失效先于取消、SemaphoreSlim(6,6)、未取得许可的取消等待不释放、迟到结果抑制、非取消失败部分结果及 CTS finally 释放不变。消费端换 P、InitializeCoreAsync、reload 取消，以及 CloseAsync/DisposeAsync 入口销毁顺序不变。未扩大其他迁移，未修改 NoWarn、正式依赖或 kernel gitlink；无 UI/XAML 修改。专用 WinUIAnalyzer 未运行。

## Spec

0 阻断。唯一生产构造点在 PlayerViewModel.Danmaku.cs，实例存私有字段，经直接方法调用使用。DispatcherQueue 接收 Action 包装回调；未发现 session 本体进入 DataContext/Tag/Content/ItemsSource、WinRT 参数或注册路径。闭包捕获及持有 UI 回调不能证明本体跨 ABI。IDisposable 映射 IClosable，本改动满足 level 2 严格接口生成要求，不等同于修复已复现运行故障。私有 Session 未实现 IDisposable，不扩改。

## 历史证据复核

[review.py](evidence-danmaku-aot-integration/review.py) 只读候选原产物，输出到父 ignored artifacts；没有执行会改写候选证据的原 verify.py。

- 43 份产物长度/SHA256 全匹配，包括最终应用 DLL/PDB、原 build/binlog、原 TRX；1543 份 strict 输入逐项散列匹配，非目标输入红绿逐字相同。
- 原日志与提交的完整诊断集合相同：strict **350→349**，仅删除最后一条目标 CsWinRT1028，零新增；303 MVVMTK0045 属性与46 MVVMTK0046 命令诊断按路径、位置及全文逐项保留。两次均 exit1。
- strict 使用隔离 export，audit-build 去除副本原 IL/MVVMTK 抑制，保留 level2/分析器/warnings-as-errors；WinUICookies 仅在审计副本局部 AllowUnsafeBlocks，参数无全局 unsafe。这不表示正式 kernel 的1030已修复，更不表示整个 CsWinRT/AOT 没问题；第三方 IL 与完整未抑制 ILC 仍待验。
- 原 baseline/all 各535、session-before/after 各7，逐结果 Passed，executionId 唯一，前后名称集合相同；原始 TRX 与提交 gzip 逐字相同。7项覆盖换 P、取消、连续 reload、close、迟到成功/失败、排队发布、六并发与部分失败；headless 测试不能替代真实窗口或 AOT 运行。
- 候选应用 Debug/Release build exit0 为历史证据；本轮没有重跑应用 build、strict、ILC 或 publish，原 red 保留。

## 本轮 fresh 验证

重新读取候选最终 Debug/Release 应用 DLL/PDB：attribute 指向存在的详情类型，GetExposedInterfaces IL 分别52/47字节；PE/PDB identity 匹配，各 **2595** 份文档重新读取散列匹配。唯一 session 源文档及生成声明匹配，生成详情摘录与实际源码一致，包含 IDisposableMethods IID/vtable。

父运行：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=all.trx' --results-directory artifacts/danmaku-aot-integration/tests
# cfg 分别为 Debug、Release；完整 session 源码与真实 kernel Abstractions 项目引用。
dotnet build docs/performance-acceptance/evidence-danmaku-aot-integration/Models/Models.csproj -c $cfg "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/danmaku-aot-integration/generated-$cfg"
```

fresh 测试 exit0，**Passed: 535, Failed: 0, Skipped: 0**；逐项名称与候选535相同，含7项 session。两配置隔离 Models 均零警告零错误，CsWinRT 2.3.1 level2 且 warnings-as-errors。只读 Metadata 工具核验新 DLL 的生成 attribute/详情方法、52/47字节 IL、PE/PDB identity，各8份源文档全匹配；生成 vtable 含 IDisposableMethods IID/AbiToProjectionVftablePtr。这里不执行 COM QueryInterface，不实例化应用，也不等于完整 Native AOT 验收。

[finalize.py](evidence-danmaku-aot-integration/finalize.py) 复算 fresh TRX/父子源码一致性/生成接口/PDB源码并归档，[verification.json](evidence-danmaku-aot-integration/verification.json) 区分历史与 fresh。首次 Models 相对路径多一级导致 MSB9008/CS2001，修正后两配置通过，原工具配置失败日志保留；一次猜测 kernel 目录读取失败属准备错误，非产品失败。

## 保留状态

root/kernel 最终 clean，kernel0ae5e4e 和正式依赖/gitlink不动。完整 AOT **HOLD**、真实绑定/原生 session 待验，CoreRX1 独立候选进行，#49 FAIL/未关闭保留。没有 UI、部署、登录、安装、UAC、push、PR、issue 操作。
