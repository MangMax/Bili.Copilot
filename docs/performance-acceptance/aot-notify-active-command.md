# NotifyMessageItemViewModel.ActiveCommand：明确 provider 成员

2026-10-31，实施工作区 `t3-325e59c7`，起点及交付前本地父 `feat/performance-45` 均为 `c3ebfb9a37df61f8ffe014d3b42720495dca931b`。**仅本命令的严格诊断消除；全面 AOT / 性能采用仍 HOLD，#49 CoreQX 独立进行。**

## 范围与输入

已读 AGENTS/domain、[AOT 独立审查与切片要求](aot-payload-and-compatibility.md)，应用 tdd、code-testing、run-tests、WinUI review/dev-workflow、verification 技能。没有仓库 test overlay；不启动子代理。测试边界采用任务明确指定的真实 VM 公开命令和 OS 适配边界。

根初始只有 kernel 异常：HEAD `f02c33f639addd5d421700c43fa66382beafe323`，大量索引删除，目标对象缺失。先保存 `artifacts/notify-active-aot/initial-*` 状态及完整 binary patch，再从 `t3-7a7ce646/externals/bili-kernel` 只读 fetch 指定对象，在本工作区恢复并保持 `0ae5e4e000a41a07f6696746706df11c06e8835b` clean。未改 gitlink。媒体资产从该既有工作区复制，未下载、安装或改变正式依赖。SDK 为固定 `11.0.100-rc.3.26522.148`。

生产 diff 只有类的 `GeneratedBindableCustomProperty` 参数及两行说明。保留七个展示属性及继承的 `Data`；索引器集合为空。`[RelayCommand]`、`ActiveAsync` 方法体、公开命令名/类型、XAML 均未改；没有移除 attribute、添加包装命令、更改 NoWarn 或扩大到其他 VM。

## 消费闭包及真实生成器 API

[全部 tracked 消费搜索](evidence-notify-active/all-tracked-consumers.txt) 与 [运行时发现搜索](evidence-notify-active/runtime-discovery-search.txt) 分开核对：

- `NotifyMessageSectionDetailViewModel.LoadItemsAsync` 从服务结果构造 VM，加入 typed `ObservableCollection<NotifyMessageItemViewModel>`。
- `NotifyMessageBody.xaml:25–28` 将 Items 交给 AppRepeater，在 typed DataTemplate 中用 `x:Bind` 传给卡片 ViewModel。代码后置只处理列表事件/滚动。
- `NotifyMessageCardControlBase` 的基类是 `LayoutUserControlBase<NotifyMessageItemViewModel>`；卡片 `RootCard.Command` 在 XAML 第17行使用 `x:Bind ViewModel.ActiveCommand`，未设置 CommandParameter。七项显示数据也都是 x:Bind。
- 仓库其他 `ActiveCommand` 属于 Source/Subtitle/PlayerNotification/UpSection 等类型，未作为此 VM 的消费者。没有发现此 VM 命令的 runtime `{Binding}`、反射查找或字符串命令路由。通用 ItemsSource 过 ABI 不等于此命令需要 runtime property lookup；保留原八成员 provider。
- [项目引用范围 API 查询](evidence-notify-active/winapp-card.json) 确认真实 `Richasy.WinUIKernel.Share.Base.CardControl` 继承 Button，Command/CommandParameter 来自 ButtonBase。工具提示本机 Windows SDK 高于 target；不据此推断 OS API 可用性，最终证明采用实际项目引用的生成代码及编译结果。

真实还原包为 CommunityToolkit.Mvvm **8.4.2**、Microsoft.Windows.CsWinRT **2.3.1**、WindowsAppSDK.WinUI **2.6.4**、Richasy.WinUIKernel.Share **0.0.12-preview4**；包、分析器及 DLL 哈希见 [packages.json](evidence-notify-active/packages.json)。生成代码的版本字符串 `8.4.0.0` 不代替 NuGet 版本。

阅读精确 MVVM nuspec commit `35dfe7b12da0e64384bfaec7ebc0883196a89f9f` 的 [MVVMTK0046 分析器](https://github.com/CommunityToolkit/dotnet/blob/35dfe7b12da0e64384bfaec7ebc0883196a89f9f/src/CommunityToolkit.Mvvm.SourceGenerators/Diagnostics/Analyzers/WinRTRelayCommandIsNotGeneratedBindableCustomPropertyCompatibleAnalyzer.cs)：只有 attribute 成员集合包含生成命令时才报该错误；显式非空 string 数组中无此名时不报。不是禁用规则。

阅读 CsWinRT 2.3.1 发布标签对应 `ff5227a4ffca06dfa6de8ca0a5c127c04f294291` 的 [Attributes.cs](https://github.com/microsoft/CsWinRT/blob/ff5227a4ffca06dfa6de8ca0a5c127c04f294291/src/WinRT.Runtime/Attributes.cs) 和 [AotOptimizer.cs](https://github.com/microsoft/CsWinRT/blob/ff5227a4ffca06dfa6de8ca0a5c127c04f294291/src/Authoring/WinRT.SourceGenerator/AotOptimizer.cs)：构造器是 `(string[] propertyNames, Type[] indexerPropertyTypes)`；生成器遍历本类及基类的 public 属性并按名选择。源码下载冻结于 ignored `upstream/`，真实应用构建进一步验证所用包接受此 API。

## strict red → 单项差额

原源独立 export，沿用 `evidence-aot-payload/audit-build.ps1`，仅在无 .git 的审计副本移除既有 IL/MVVMTK0045/0046 NoWarn，启用 `CsWinRTAotWarningLevel=2` 和审计专用全局 `AllowUnsafeBlocks=true`。后者仅越过历史 kernel CsWinRT1030 前提，不代表正式修复。warnings-as-errors、分析器和依赖不变；没有执行 ILC/publish。

原 red exit1：**303 MVVMTK0045 + 47 MVVMTK0046 + 8 CsWinRT1028 = 358**。目标原诊断为 `NotifyMessageItemViewModel.cs(49,24): MVVMTK0046 ... ActiveAsync()`。修改后仍 exit1：**303 + 46 + 8 = 357**。逐条按文件、行列、编号、消息比较，精确删除目标一条，无新增，其余357条完全相同。见 [结果](evidence-notify-active/verification.json)、[原诊断](evidence-notify-active/red-diagnostics.json.gz)、[新诊断](evidence-notify-active/green-diagnostics.json.gz)。全局严格编译仍红，不能写成完整 AOT 通过。

真实应用生成代码前后核验：

- [provider](evidence-notify-active/provider.txt) 仍为 PublishRelativeTime、FirstUserAvatar、FirstUserName、IsMultipleUsers、Subtitle、Message、SourceContent、Data，七个 init 属性只读，Data 原 getter/setter 保留。目标 provider 段在原 Debug、新 Debug、新 Release 逐字相同；ActiveCommand 原先就不在 provider 中。
- [命令](evidence-notify-active/command.txt) 三份字节完全相同，SHA256 `DB776A90A48F2D84C875A2CAB416168DECD192D5CC52CCDE22EA99EA379BAA97`：私有 nullable AsyncRelayCommand 字段，public IAsyncRelayCommand getter 使用 `??= new AsyncRelayCommand(Func<Task>(ActiveAsync))`。因此 lazy 实例、默认 options 和公开类型均保持，不新建第二个命令实例。
- [x:Bind 路径](evidence-notify-active/xbind-path.txt) 为 typed VM 的 `obj.ActiveCommand` → `Update_ViewModel_ActiveCommand(IAsyncRelayCommand)` → `ButtonBase.Command = value`。原/新 Debug 整个生成文件字节相同。Release 移除 Debug 的 Disable/IXamlBindScopeDiagnostics 设施，以上 typed 命令链一致；不声称 Debug/Release 整文件相同。

## 行为、构建及输入封存

测试直接链接真实 VM 两份源文件，使用原测试工程的真实 MVVM 8.4.2 generator/AsyncRelayCommand 和 kernel NotifyMessage/UserProfile/BiliImage。仅适配 Windows Launcher（AsyncLocal handler）、WinUI 基类数据持有、attribute 与相对时间展示；复用已有资源适配器增加两个 enum 值。无真实网络/账号/OS activation，**测试 adapter 不验证 CsWinRT ABI、UI 绑定或 Humanizer 展示**；provider 以真实应用生成输出另行核验。

原源 fresh 全套 **Passed: 524, Failed: 0, Skipped: 0**。新增命令用例在原源上 **Passed: 11, Failed: 0, Skipped: 0**；修后显式 Rebuild 测试项目，再 --no-build 跑相关 **11/0/0**、全套 **535/0/0**。TRX 逐项检查 outcome、计数及唯一 executionId；原524个 testName 集合全部保留，新旧11集合相同。

| Requirement | Evidence |
| --- | --- |
| 稳定实例、每 VM 独立、IAsyncRelayCommand、初始状态 | `ActiveCommand_HasStablePerInstanceIdentityAndAsyncContract`；真实生成 getter 的字节等价补充验证 lazy 字段 |
| null/空/非http/大小写原行为，不调用 launcher | `ActiveCommand_NonHttpSourceDoesNotLaunch`（4例） |
| ICommand.Execute 参数被忽略、实际 source URI、launch true/false 都正常完成 | `ActiveCommand_LaunchesSourceAndIgnoresCommandParameterAndLaunchResult`（2例） |
| pending IsRunning/CanExecute、通知、不可取消、直接再次执行语义 | `ActiveCommand_PendingLaunchDisablesCanExecuteButDirectExecutionStillRuns` |
| ExecuteAsync 异常与 fault task 保持 | `ActiveCommand_ExecuteAsyncPreservesLaunchException` |
| Execute 默认异常重新投递调用上下文 | `ActiveCommand_ExecuteRethrowsOnCallingSynchronizationContext` |
| 无效 http URI 在 launch 前失败 | `ActiveCommand_InvalidHttpUriFaultsBeforeLaunch` |

Debug/Release x64 均 exit0。新 Debug 0 warning/0 error；Release 1 warning/0 error（kernel proto 既有 unused import，首次原 Debug/strict red 同样出现），不报成全零警告。没有安装 WinUIAnalyzer 包，未声称执行该包检查；按 WinUI skill 自审命令、绑定及 AOT 边界，未引入其他 UI 改动。

原/新 Debug、新 Release PDB 各2587 document，全部文件校验通过；原/新测试 PDB 各139 document，112个落盘文件均核验，27个未落盘生成 document不作全文件证明。复用 PdbInputs 工具记录磁盘 SHA256；其中 ComputeSharp 使用 SHA1，verifier 先核被冻结文件 SHA256 再按 PDB 算法核 SHA1。目标 VM 的旧/新 PDB checksum 分别匹配冻结原源和候选源。新应用/测试 DLL、PDB、原 PDB、生成输出、TRX、binlog、源码归档保留 ignored artifacts；[大产物哈希](evidence-notify-active/large-artifacts.json) 和 verification 中源/全 artifact manifest hash 可交接，未把大文件提交。

准备阶段 audit 在 export 完成前启动，报 Missing export-source marker；保留 `preparation-red.txt` 后重跑获得真实358红。证据脚本早期 Windows 长路径枚举、错误假定 Debug/Release XAML 全字节一致、错误把 SHA1 当 SHA256 的三次失败分别保留；修正验证器后全部断言通过。它们不是产品缺陷 red。本片没有发生 stale 增量编译验收：修后测试显式 Rebuild，PDB 核验通过；原 strict red 和此前历史报告均不覆盖。

## 复放与交付边界

在仓库根执行（生成目录使用绝对路径；red/green 要求新输出目录）：

```powershell
./docs/performance-acceptance/evidence-aot-payload/export-source.ps1 -Destination "$pwd/artifacts/notify-active-aot/strict-source"
./docs/performance-acceptance/evidence-aot-payload/audit-build.ps1 -Dotnet (Get-Command dotnet).Source -SourceRoot "$pwd/artifacts/notify-active-aot/strict-source" -Output "$pwd/artifacts/notify-active-aot/red" -WarningLevel 2 -AllowUnsafeForAudit
# 原 red 必须从上述父基线导出；只替换该 VM 文件为候选后，同命令使用新 green 输出目录。
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --filter FullyQualifiedName~NotifyMessageCommandTests --logger "trx;LogFileName=behavior-after.trx" --results-directory artifacts/notify-active-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger "trx;LogFileName=all-after.trx" --results-directory artifacts/notify-active-aot/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/notify-active-aot/generated-after-debug" -bl:artifacts/notify-active-aot/debug-after.binlog
# Release 使用 -c Release、generated-after-release、release-after.binlog。
python docs/performance-acceptance/evidence-notify-active/verify.py
```

以上是已有依赖完成构建后的命令序列；全新工作区先正常运行 dotnet test 建立引用项目输出。verifier 要求本次对应 PDB/exit/原源证据均已按上文封存，不伪造缺失项。交付前再次只读核对父 tip 已在当前祖先中，不改父 checkout。仅本分支提交，无 push/PR/issue。

无 desktop/UI、部署、登录、安装、UAC；未操作 caa6a7be/layout-v3/Dev/PID9608。本片没有发布或运行 Native AOT，编译绿与 headless 行为测试不构成真实 AOT 绑定 UI 通过。其余303属性、46命令、8 CsWinRT、kernel unsafe 前提、正式第三方兼容与真实绑定/运行验收继续 HOLD，#49 不关闭。
