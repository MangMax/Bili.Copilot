# Notify ActiveCommand 固定提交集成审查

2026-10-31，独占父 checkout `t3-1d19cc80`，无子代理。起点 `c3ebfb9a37df61f8ffe014d3b42720495dca931b`、kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 均 clean。已读 AGENTS/domain、code-review、WinUI review、verification、run-tests；无仓库 test overlay。按用户要求本人执行 Standards/Spec 两轴，不委派。

固定候选 `6f64b929501d0534cf179d22d981ccaf36150c2b` 静态审查和历史证据复算无阻断后，以 `--no-ff` 合入 **`2b54efdbfd28b797f2035e9d523be1b3b6835bab`**。双亲为指定父与候选，合并 tree 与候选完全相同。合后仅新增集成文档与核验材料。

## 审查结论

Standards：无阻断或新增规则违规。Spec：限定整改通过，完整 AOT 不通过。本片唯一生产修改是目标类 GeneratedBindableCustomProperty 的成员集合，七个展示属性及继承 Data 保留，索引器集合为空。旧生成器原本就看不到同轮生成的 ActiveCommand；新声明明确该边界，而非删除一个原来存在的 runtime 命令 provider。原/新八成员 provider 段完全一致，七个 init 属性只读，Data getter/setter 保留；未遗漏其他已有 runtime 可见属性。

独立 tracked 搜索与阅读确认完整消费链：NotifyMessageSectionDetailViewModel 服务结果 → typed ObservableCollection → NotifyMessageBody 的 typed DataTemplate → NotifyMessageCardControlBase 的 typed ViewModel → RootCard.Command 的 x:Bind。生成代码直接读取 obj.ActiveCommand，并以 IAsyncRelayCommand 传给 ButtonBase.Command；没有 CommandParameter。其他同名命令属于不同类型。未发现本 VM 命令的 runtime Binding、反射查找或字符串路由；通用列表经过 ABI 不要求本命令动态发现。此结论限当前源码和包，不替代真实 Native AOT UI 验收。

公开生成类型仍为 IAsyncRelayCommand，私有 AsyncRelayCommand 字段由 ??= 每实例 lazy 初始化，委托仍为 ActiveAsync、默认 options 未变。原/新及父级生成命令字节相同；生产方法体与 XAML 不变。因此参数忽略、async CanExecute/IsRunning、直接重复 ExecuteAsync、异常重新投递语义不变。

11 项测试链接真实 VM 两份源文件及真实 Toolkit 8.4.2 generator/AsyncRelayCommand，不复制 ActiveAsync。适配只位于 OS Launcher、基类数据持有、时间展示/资源/attribute 边界；AsyncLocal launcher 隔离用例。覆盖每实例 identity、空/非 http/大小写、参数和返回值、pending 并发/CanExecute 通知、ExecuteAsync fault、Execute 上下文异常以及非法 URI。它们不证明 Humanizer 文案、CsWinRT ABI 或真实 UI。原524个测试名称保留。

## 历史证据与本轮执行分账

历史：只读核验候选完整 artifact/source manifest 与全部列出的大产物 hash，重新解析原始 strict 日志与 TRX。red 为303 MVVMTK0045 +47 MVVMTK0046 +8 CsWinRT1028；green 为303+46+8。集合差恰好目标 ActiveAsync 一条删除，零新增，其余357条逐项一致。原524及原/新11、新535均通过并验证名称集合。未重跑 strict 构建或完整 ILC；审计副本移除既有 NoWarn、AllowUnsafeForAudit 的前提仍仅属于历史审计，不是正式配置修改。

本轮：父级测试项目显式 Release Rebuild 后 `dotnet test ... -c Release --no-build --logger "trx;LogFileName=all.trx" --results-directory artifacts/notify-active-integration/tests`，**Passed: 535, Failed: 0, Skipped: 0**。原524名称完整包含，差集正好11项通知命令用例。

候选与父级实际 DLL/PDB 通过独立 PE CodeView GUID+stamp+age 与 Portable PDB ID 配对检查，随后按 PDB 声明算法逐文件核验源码（包含 SHA1 ComputeSharp 与 SHA256 文档），不只相信先前 manifest 的 actual 字段。测试各139 document，其中112落盘输入全部匹配，27未落盘生成 document不宣称磁盘闭包；应用每份2587 document全匹配。生成命令/provider/XAML 路径另外与实际应用生成文件核对。源码比较覆盖全部1120个 tracked src/tests/global.json 文件，统一 CRLF/LF 后与固定候选一致；生产目标 VM 的原始字节及 PDB checksum 另外一致。

本轮应用 Debug/Release x64 均 exit0、0 warnings/0 errors。两者使用 `dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c <Debug或Release> -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/notify-active-integration/generated-<debug或release>" -bl:artifacts/notify-active-integration/<debug或release>.binlog`；测试重建为 `dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false`，已有引用产物保留。

本轮命令、日志、binlog、TRX、PDB 文档清单及最终 DLL/PDB hashes 记录于 `artifacts/notify-active-integration`；可复算脚本与摘要见 [integration evidence](evidence-notify-active/integration/validation.json)。历史核验脚本首次碰到 Windows 长路径限制，改用扩展路径后通过；汇总脚本另修正新增测试 CRLF/LF 差异及 XAML obj 查找路径，最终全部断言通过。这些是核验脚本准备失败，不是产品测试 red。独立 PDB 工具编译有一条 nullable-context CS8632，不属于应用构建警告；未抑制任何规则。

## 门禁与范围

有限源码/命令/provider 集成 PASS；全面 AOT/真实绑定/性能采用继续 HOLD。严格剩余303+46+8、正式第三方兼容、Mpv/Vulkan/kernel前提保持。#49 CoreQX 在独立审查，本轮不关闭。专用 WinUIAnalyzer 包未运行。

无其他 VM、正式 Mpv/kernel 引用、NoWarn 或警告策略改动；无子代理、UI、部署、登录、安装、UAC、push、PR、issue 操作。kernel 保持指定0ae5e4e clean。
