# 首页区块模型 CsWinRT 接口生成

2026-10-31，独立工作区 `t3-ee707477`，输入父提交 `a3453b2fd5a6dba10dfb1c5d16a2004060e69318`。仅两个生产声明增加 `partial`：`PopularRankPartitionViewModel`、`PopularSectionItemViewModel`。两者都是顶层类，无外层声明需要修改。成员、构造、集合、通知、相等性和排行请求代码均原字节保留（仅规范化换行后比较）。**本片编译/生成证据不放行全面 AOT；真实绑定实机待验，#49 独立推进。**

## ABI 消费路径与版本

- `PopularPageViewModel.Properties.cs` 的 `Sections` 是只读属性持有的 `ObservableCollection<IPopularSectionItemViewModel>`。初始化添加推荐/热门/排行模型，分区目录返回后添加真实 `PopularRankPartitionViewModel`；本片不更换集合或实例。
- `PopularSideBody.xaml:32` 的 `ItemsView.ItemsSource` 绑定该集合；`OnLoaded` 重设同一集合，卸载时解绑。模板把每项写入原生 `ContentControl.Content`，再由 `PopularSectionTemplateSelector.SelectTemplateCore(object, DependencyObject)` 按两个实际类型选择模板。不是仅因持有 WinUI 对象而推测过界。
- `PopularMainHeader.xaml:35` 以 OneWay 把 `SelectedSection` 写入 `ContentControl.Content`。两个模板的 `x:DataType` 分别指向真实模型；子控件使用编译绑定读取 `Title/Icon/Logo`，展示属性均为 init-only。
- 选择不是 `SelectedItem` 的 TwoWay XAML 绑定：`PopularSideBody.OnSectionSelectionChanged` 从原生 `ItemsView.SelectedItem` 取回实例，再执行 `SelectSectionCommand`；反向恢复以 `Sections.IndexOf(SelectedSection)` 后 `SectionView.Select(index)` 完成。缓存键、目录恢复、排行终止与撤销逻辑均未改。
- `IPopularSectionItemViewModel` 继承 `INotifyPropertyChanged`，基类来自真实 `Richasy.WinUIKernel.Share`。最终还原为 CsWinRT **2.3.1**、MVVM **8.4.2**、WinUI **2.6.4**、Share **0.0.12-preview4**，见 [版本和包哈希](evidence-popular-aot/packages.json)。

核对固定 CsWinRT 发布源码 [AotOptimizer.cs](https://github.com/microsoft/CsWinRT/blob/ff5227a4ffca06dfa6de8ca0a5c127c04f294291/src/Authoring/WinRT.SourceGenerator/AotOptimizer.cs)：`NeedVtableAttribute` 筛选可实例化 partial 类，随后扫描映射接口；嵌套声明通过 `ContainingType` 处理。这里缺的是本类声明，不是要求修改 NuGet 基类。真实构建证实仅两个关键词足够，没有新增 unsafe、NoWarn、attribute 或依赖修改。

## 严格 red 与差额

使用历史 `export-source.ps1`/`audit-build.ps1` 的无 `.git` 审计副本，移除该副本应用 NoWarn 中 IL/MVVMTK0045/0046，`CsWinRTAotWarningLevel=2`，保留 warnings-as-errors、分析器、优化及正式依赖。沿用历史 `-AllowUnsafeForAudit` 越过 kernel CsWinRT1030；该全局 unsafe **只存在于审计命令**，不是本片正式修复或新增方案。

red：303 MVVMTK0045 + 46 MVVMTK0046 + 8 CsWinRT1028 = **357**，exit1。green：303 + 46 + 6 = **355**，仍 exit1。按完整文件、行列、编号、消息去除构建摘要重复后比较，恰好删除两个模型在 `(11,21)` 的 CsWinRT1028，零新增，其余355条完全相同。参见 [复算结果](evidence-popular-aot/verification.json)、[red](evidence-popular-aot/red-diagnostics.json.gz)、[green](evidence-popular-aot/green-diagnostics.json.gz)。这些是编译阻塞计数，不是已观测运行故障数；未运行 ILC/publish。

剩余 CsWinRT1028：`TrayIconSession`、`DanmakuLoadSession`、`CustomHttpClientHandler`、`SponsorBlockService`、`TrayLifecycle`、`SponsorBlockCategorySettingViewModel`。303属性、46命令、kernel unsafe 前提及其余全面 AOT 阻塞保持原台账；原 [独立 review](aot-payload-and-compatibility.md) 与历史 red 不改写。

## 生成源码、最终 DLL 与有限行为验证

两个类型均生成 `WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")` 和 `WinRTExposedType`。实际应用去重后引用 `ChapterItemViewModelWinRTTypeDetails`，名称复用是生成器去重结果，不是误绑业务模型；该表使用 `INotifyPropertyChangedMethods.IID/AbiToProjectionVftablePtr`。两个配置的生成片段及共享表保存在本证据目录。

普通应用 Debug 构建零警告零错误；Release 构建一条既有 protobuf unused-import warning、零错误，均 exit0。`Metadata` 工具只读 PE/portable PDB：验证最终 DLL 两个 attribute 指向存在且有 IL 方法体的接口详情类，CodeView GUID/stamp/age 匹配，两个配置各2589份 PDB 源文件（包括两个模型及其生成声明）散列均匹配。共享方法 IL 在 Debug 为52字节、Release 为47字节。完整输出、大日志/binlog和 DLL/PDB 以 [大产物哈希](evidence-popular-aot/large-artifacts.json) 交接，均留在 ignored 构建目录。

隔离 `Models` 工程链接本仓库两个真实模型、真实接口及枚举，直接引用本次应用输出的 Share/MVVM/Kernel/FluentIcons/WinUI DLL，使用同版本 CsWinRT；不复制模型实现、不加载应用模块、不添加 AppSDK 自动初始化。`Probe` 验证构造展示值、`Data` 引用身份、区块按 Type 的缓存键相等性/哈希、不同排行区块不相等、读取不产生通知，并实际调用生成 `GetExposedInterfaces` 检查非零 vtable 和投影提供的 IID `90b17601-b065-586e-83d9-9adc3a695284`。模型只有 init-only 展示属性，不伪造 setter 通知测试。

原源码隔离构建有两条1028，probe 因缺少 `WinRTExposedType` 失败；修后 Debug/Release 隔离构建零警告零错误，同一 probe 通过。它验证托管行为和生成接口表，**没有执行 COM QueryInterface、真实 WinUI 控件、Native AOT 或绑定运行**。

已有相关回归前后各30通过，完整535通过、失败0、跳过0。重点包括 `SavedPartitionWaitsForDirectoryThenLoadsThatPartition`、`PopulatedRankingStopsLoadMoreNotificationsButRefreshStillLoads`、`QueuedSwitchesKeepCacheOwnedByDisplayedSection`、`EmptyRefreshPreservesListCursorAndPreviousUndo`、`SwitchingSectionsRejectsLateContentAndLoadsCurrentSection`。现有首页测试链接真实页面逻辑，但两个模型仍是已有 headless adapter；不能用535替代上述真实模型 probe 或实机验收。没有添加重复业务实现测试。

## 异常记录与静态审核边界

初始 kernel HEAD `f02c33f`、索引为空、目录仅 `.git`，指定对象本地缺失；初始根/kernel状态和 binary patch 均先保存于 ignored `artifacts/popular-aot/initial-*`。从父 kernel 只读本地 fetch 精确 `0ae5e4e` 后仅恢复本子工作区，结束 kernel clean，正式 gitlink未改。

准备时 export 尚未完成就启动审计，报 `Missing export-source marker`；等待导出完成才运行有效 red。初版真实 DLL 执行 probe 在模型构造前触发 AppSDK 自动初始化，创建 `DeploymentInitializeOptions` 即报 `REGDB_E_CLASSNOTREG`。立即停止该路径，没有执行部署/注册命令，没有用安装、登录或 UAC 解决；改为上述源码链接隔离 probe。不能把这次初始化异常报成模型缺陷或UI验收。另保留 probe 过宽引用导致 CS0518、运行时 DLL 未复制、手写 IID 的5869误写以及错误投影类型查找的失败日志；修复验证器后才取得最终证据。

按 WinUI review/dev、tdd、code-testing、run-tests 和 verification 技能执行本片自审：仅声明变化，无绑定名/模式、通知实现、构造、线程、生命周期或集合身份修改；未涉及303字段、SponsorBlock或托盘实现。专用 WinUIAnalyzer 包未安装或运行。无 desktop slot，未启动应用UI；未操作保护 checkout、layout-v3、Dev/PID9608、CoreQX 环境。无 push、PR、issue 或父工作区写入。

## 复验入口

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter "FullyQualifiedName~PopularPage" --logger "trx;LogFileName=popular-after.trx" --results-directory artifacts/popular-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger "trx;LogFileName=all.trx" --results-directory artifacts/popular-aot/tests
# 对 Debug / Release 分别执行；不部署、不启动应用。
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/popular-aot/generated-debug" -bl:artifacts/popular-aot/debug.binlog
dotnet build docs/performance-acceptance/evidence-popular-aot/Models/Models.csproj -c Debug "-p:SourceRoot=$pwd" "-p:AppBin=$pwd/src/_build/x64/Debug/BiliCopilot.UI/bin"
dotnet run --project docs/performance-acceptance/evidence-popular-aot/Probe/Probe.csproj -c Release -- "$pwd/docs/performance-acceptance/evidence-popular-aot/Models/bin/Debug/net11.0-windows10.0.22621.0"
python docs/performance-acceptance/evidence-popular-aot/verify.py
```

审计的精确命令参数保存在 red/green `arguments.json`；需要新目录重放导出，green 明确复制两个修改后的模型进副本（原 export 取 HEAD），不可把尚未提交的模型漏出审计。
