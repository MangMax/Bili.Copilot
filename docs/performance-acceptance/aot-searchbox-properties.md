# SearchBoxViewModel：两个 ObservableProperty 字段迁移

2026-10-31，工作区 `t3-d071584c`，父起点 `aaddb36f965979af5c53da0e8504eec47486debf`。仅迁移 SearchBoxViewModel 的两个声明；完整 AOT **HOLD**、#49 **FAIL** 保留。无子代理、UI、部署、注册、登录、安装、UAC、push、PR、issue 或父 checkout 写入。

已读 AGENTS/domain、strict 台账、Navigation 属性报告及 tdd/code-testing/run-tests/csharp-refactoring/WinUI review/dev/verification 技能。测试范围使用用户指定的真实搜索公开属性、命令与服务边界，未复制 VM 算法。没有测试 overlay、领域 glossary/ADR；未新增依赖或分析器。

## 精确清点与兼容性

只有 `SearchBoxViewModel.cs`、`SearchBoxViewModel.Properties.cs` 两份 partial。后者恰好两个 ObservableProperty 字段；前者保留全部方法体。

| 原输入 | 迁移后公开 API | 默认值、hook、消费 |
| --- | --- | --- |
| private string `_keyword` | public partial string `Keyword { get; set; }` | `string.Empty`；`OnKeywordChanged(string)` 取消旧 CTS、清空 Suggestion；AppSearchBox.xaml:86 TwoWay/PropertyChanged，code-behind 热搜/提交选择写入，NavigationViewModel 非搜索页清空，VM 捕获查询快照 |
| private bool `_isHotSearchLoading` | public partial bool `IsHotSearchLoading { get; set; }` | false；无自定义 hook；LoadHotSearchAsync 在请求前置 true、完成/异常后 false，AppSearchBox.xaml:155 ProgressRing OneWay |

两字段没有任何直接 backing-field 读写；全 partial 和消费者检索记录在 ignored `consumers.txt`。没有转发属性注解、DependentCanExecute/NotifyPropertyChangedFor/验证属性，也没有这里需要保留的私有 setter。ObservableProperty 留在同一声明位置。Suggestion/HotSearchItems 保持只读集合及 identity；服务、时钟、CTS、区域字段与命令声明均不变。DI singleton 与 AppSearchBoxBase 使用不变。VideoDescriptorControl 的既有 SearchCommand 调用也未改动。

实际 SDK RC3 `11.0.100-rc.3.26522.148`、NuGet MVVM `8.4.2`；生成器 GeneratedCode 标记的 `8.4.0.0` 不是包版本。正式配置 `MvvmToolkitEnableINotifyPropertyChangingSupport=false`。新旧 setter 在 backing storage 归一化后执行体相同：相等早退 → changing hooks → 赋值 → changed hooks → PropertyChanged。Keyword 的取消和集合清空因此发生于 PropertyChanged 之前。旧 MemberNotNull 指向私有字段，新代码使用编译器 field 不再需要它；未实现的双参数 Keyword hook 的 oldValue nullable 注解变化，但无实现且编译擦除，不声称生成文本完全相同。公开类型/名称/get/set 保留，元数据属性枚举顺序变化不作为契约。

真实 XAML 全部不改。AppSearchBox 的 600ms 选择抑制、interaction version、焦点回调 late 检查、空输入关闭和卸载取消/清空/停止计时器均原样保留；VM 250ms 去抖、CTS identity 代次、旧 finally 不释放新请求、空输入推荐缓存和区域搜索均不变。未重写 ToolkitDebounce，未扩 AppSearchBox UI。

## 严格红绿及构建

初始 root 仅 kernel 异常：kernel HEAD `f02c33f...`、大量 staged 删除。保存 initial-root/initial-kernel/二进制 patch 后，只读父工作区 fetch 精确对象，在本 worktree checkout 恢复 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`。没有修改 gitlink 或 kernel 源码。媒体文件仅从父既有资产复制到本 worktree，无下载。未碰保护包、Dev、PID9608 或其他桌面资源。

沿用 export-source/audit-build，隔离无 .git 副本移除 IL/MVVMTK0045/46 NoWarn，level2、优化、分析器与 warnings-as-errors 保留。两轮均显式 `-AllowUnsafeForAudit`（全局仅审计命令），与 Navigation 报告口径相同；最新 Danmaku 报告用的是副本局部 WinUICookies unsafe，**本轮没有把全局 audit 绕过当作正式 kernel1030 修复**。完整 ILC 未运行。

| 唯一诊断 | red | green |
| --- | ---: | ---: |
| MVVMTK0045 | 303 | 301 |
| MVVMTK0046 | 46 | 46 |
| 合计 | 349 | 347 |

两轮 strict 均 exit1。red 与父最新 Danmaku green 诊断逐路径/位置/全文相同；集合差只有 SearchBoxViewModel.Properties.cs:26/29 两条 MVVMTK0045，无新增，其余347条逐项不变。cold red 有一条 protobuf unused-import warning，warm green 无此 warning；不把增量跳过 protobuf 当成 warning 修复。隔离源码1539份对 archive 逐文件核对，除目标两行及审计 csproj NoWarn 外不变。

普通 Debug 基线 exit0（1条同样 protobuf warning）；迁移后 Debug exit0、0 warnings/errors。Release exit0、1条既有 protobuf unused-import warning、0 errors。最终 DLL/PDB 见 [verification.json](evidence-searchbox-properties/verification.json)；普通构建仍有正式 NoWarn，不能替代 strict 结果。

检查实际应用生成的两属性和五个命令：setter 执行体等价，命令生成文本逐字不变。SearchBoxViewModel 没有 GeneratedBindableCustomProperty；前后 WinRTCustomBindableProperties 都没有其 provider，不能声称新增了动态属性 provider。WinRTVtable 保留 INotifyPropertyChanged，WinRTExposedType 指向生成器共享的 ChapterItemViewModelWinRTTypeDetails；此共享命名不是错绑。元数据读取验证最终 DLL 包含公开属性、该 attribute 指向存在的详情类型及 GetExposedInterfaces 方法，PE/PDB identity 一致。Debug 基线/迁移后2595份源文档全匹配。Release 也为2595份全部存在匹配，详情方法 IL 为47字节（Debug52字节），记录在 verification。测试 PDB 139文档中112份物理源码存在且全部匹配；27份未输出到磁盘的生成文档明确不计作物理匹配。目标两个 partial 文档及目标属性文件 checksum 均核实，未依赖旧增量测试 DLL。

## 行为证据

基线18项搜索测试通过。新增4例在旧源和新源均通过：相关 **Passed: 22, Failed: 0, Skipped: 0**，全套 **Passed: 539, Failed: 0, Skipped: 0**（原535+4）。各 TRX 逐结果/唯一 executionId 校验，前后用例名称集合相同。正式配置 false；临时 override=true 时三个通知用例全部失败，恢复 false 后显式 Rebuild，再跑相关和全套。这个 red 是配置敏感性证据，不冒充旧实现存在功能 bug；strict red 才是本片原始目标缺陷。

| Requirement | Evidence |
| --- | --- |
| 默认空串/idle、同值不通知、集合 identity、无意外请求 | `DefaultsAndEqualAssignmentsPreserveEmptyIdleState` |
| 相同文本保留活动请求；不同文本先取消/清空再通知；迟到结果不发布 | `KeywordNotifiesAfterCancellationAndClearButEqualTextKeepsRequestAlive` |
| 加载 true/false 通知观察到已更新状态、同值去重、成功缓存、失败复位/日志 | `HotSearchLoadingNotifiesAroundCompletionAndResetsAfterFailure` ×2 |
| 250ms 边界与连输最后词 | `BurstRequestsOnlyFinalKeywordAfter250MillisecondsOfQuiet` |
| 去抖期取消/清空不请求 | `CancellationOrClearDuringDebouncePreventsAnyRequest` ×2 |
| 代次/late/旧 finally 与新 CTS | `NewInputRejectsLateResultsBeforeAnotherRequestStarts`、`NewestRequestWinsEvenWhenKeywordIsUnchanged`、`OldCompletionCannotReleaseTheNewRequestsCancellation` |
| 空输入推荐缓存、关闭设置、取消推荐不污染缓存 | `EmptyFocusUsesRecommendationCacheAndHonorsDisabledSetting`、`CancelledRecommendationsCannotReopenSuggestionsOrPopulateCache` ×2 |
| 区域切换、可见旧建议清除、取消/真实失败区分 | `RegionSwitchRejectsGlobalResultsAndRestoresBothChoicesAfterSubmission`、`NewInputImmediatelyClearsAlreadyVisibleSuggestions`、`NormalCancellationIsQuietIncludingTransportWrappedCancellation`、`RealFailuresRemainDiagnosableEvenAfterCancellation`、`UnrequestedCancellationRemainsDiagnosable` |
| 600ms、真实 IME、焦点及卸载 UI | AppSearchBox 原码保持与静态审核；**未运行真实 UI**，headless 取消用例不代替这些验收 |

WinUI 静态审核无本片新增绑定/线程/生命周期/通知问题；未装/运行专用 WinUIAnalyzer。正式依赖、命令 provider46项、SharpGen/Azure/媒体分发、完整 Native AOT 运行与 #49 门槛均保留。

## 复放与证据

大日志/binlog/DLL/PDB/source export/generated 输出在 ignored `artifacts/searchbox-properties/`，[large-artifacts.json](evidence-searchbox-properties/large-artifacts.json) 记录 hash；小 TRX gzip、完整诊断 gzip、生成属性摘录及 [verify.py](evidence-searchbox-properties/verify.py) 已提交。Metadata.cs 可放入 ignored net11.0 console 项目执行，参数为 DLL、PDB、输出 JSON。必须在修改旧源码前捕获基线 PDB 文档匹配，后续可用保存的原属性文件复核旧 checksum。

```powershell
$root = (Get-Location).Path
# red 导出必须来自 aaddb36f；迁移后不能把当前 HEAD 导出冒充 red。
./docs/performance-acceptance/evidence-aot-payload/export-source.ps1 -Destination "$root/artifacts/searchbox-properties/strict-source"
./docs/performance-acceptance/evidence-aot-payload/audit-build.ps1 -Dotnet (Get-Command dotnet).Source -SourceRoot "$root/artifacts/searchbox-properties/strict-source" -Output "$root/artifacts/searchbox-properties/red" -WarningLevel 2 -AllowUnsafeForAudit
# 替换副本中唯一目标文件后，用全新 green 输出重跑同一 audit。
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --filter FullyQualifiedName~SearchSuggestionTests --logger 'trx;LogFileName=after.trx' --results-directory artifacts/searchbox-properties/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=all-after.trx' --results-directory artifacts/searchbox-properties/tests
# cfg=Debug / Release；generated 分别 generated-after / generated-release
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c $cfg -r win-x64 -p:Platform=x64 "-p:SolutionDir=$root/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$root/artifacts/searchbox-properties/$generated"
python docs/performance-acceptance/evidence-searchbox-properties/verify.py
```

准备阶段一次 audit 早于 export marker 完成，直接拒绝 `Missing export-source marker`，未计作 red；一次生成文件读取早于编译完成、一次 verifier 早于 Release 完成，均未计成功。Metadata 初版把测试未落盘生成文档也要求存在，按实际证据边界改为所有存在文档必须匹配、目标两份源码必须存在匹配；应用仍要求全部2595份存在匹配。未通过弱化产品诊断或测试断言洗绿。

结束只读核对父 tip 仍为 aaddb36f；执行 git merge --no-edit feat/performance-45 返回 Already up to date。verification 脚本通过、git diff --check 通过；kernel clean 且 gitlink不变。验证器首次读取超长生成文件路径失败，改用 Windows extended path 后完成复算，不涉及生产改动。
