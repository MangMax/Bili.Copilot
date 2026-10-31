# MainFrame / OverlayFrame 首次导航构造契约

2026-10-31；单一 ≤M 片。起点为父 `feat/performance-45` 的 `8994fed85c6f55c5c93dab30e2069a99d70ad608`。仅本工作区写入，无子代理、外部 push/PR/issue、部署、UI、注册、登录、安装或 UAC；未操作受保护 checkout、layout 或 PID。

## 原因与选择

原 [严格审查](aot-payload-and-compatibility.md) 的两条应用 IL2067 在本轮局部真实 ILC 和完整应用 ILC 均重现，见 [局部 red](evidence-navigation-aot/probe-red.txt)、[完整 red](evidence-navigation-aot/full-red.txt)。缓存命中绕过构造，不覆盖首次访问。

`INavServiceViewModel` / `AppNavigationItemViewModel` 实际来自固定的 Richasy.WinUIKernel.Share `0.0.12-preview4`。直接读取包 DLL 元数据/IL（不加载 Windows 类型），确认接口参数、条目构造参数、`<>c__DisplayClass19_0.pageType` 都没有 DynamicallyAccessedMembers；闭包调用 `navService.NavigateTo(pageType, parameter)`。[元数据和 IL 摘录](evidence-navigation-aot/external-contract.txt) 及 [读取代码](evidence-navigation-aot/inspect-external-contract.cs) 可复核。仅标注应用实现既不能修正外部闭包存储，也会与未标注接口契约不匹配；更改依赖包超出本片。

因此采用用户允许的**限定静态 factory**：`NavigationPageFactory.Create(Type)` 只把 Type 作为比较键，17 个分支直接 `new` 具体页面，编译器可见公开无参构造。两个 frame 仅替换缓存未命中的实例化入口，并删除返回值已保证 UIElement 后的恒真检查（CA1508）。没有新增反射、DAM All、程序集 root、NoWarn 或依赖切换。已有 rd.xml 原字节保留；局部 AOT 探针没有这个 root，因此可独立证明构造不依赖它。

未注册 Type 现在抛出 `ArgumentException`，不再尝试任意反射构造。这是显式封闭页面目录的边界；现有全部调用者均在目录内，未来添加导航页面必须同步添加 factory 分支。异常发生在 frame 内容变更前，测试覆盖当前页/叠层栈不变；不宣称 NavigationViewModel 在异常前没有更新设置或窗口状态。

## 完整 Type 与缓存路径

- 主导航：`GetMenuItems/GetFooterItems → GetItem<TPage> → typeof(TPage) → 外部 AppNavigationItemViewModel 构造闭包字段 → NavigateCommand → INavServiceViewModel.NavigateTo → NavigationViewModel.NavigateTo → MainFrame`。9 个页面为 Popular、Moment、VideoPartition、LivePartition、Anime、Cinema、ArticlePartition、Message、Settings。注释中的 Download 不属于启用目录。
- 叠层：RootLayout、Pinner、Account、Article/Video/Comment/User/Live/Moment item、VideoConnector、ChatMessageSectionDetail、ArticleReader、Anime/Cinema 的直接 `typeof` 调用与 `NavigationViewModel.Search`，合计8类：ArticleReader、Fans、Favorites、Follows、History、Search、UserSpace、ViewLater。具体每个调用位置记录于 [调用清单](evidence-navigation-aot/callers.json)。
- `PageKey` / `LastSelectedFeaturePage` 是 `FullName` 字符串，用于选择、比较、持久化，不反向 `Type.GetType` 或 Assembly.GetType。全应用检索未发现字符串解析到这两个 frame 的入口。已初始化并正在显示同一主页时 service 的早退保持不变；`Search` 已处于搜索页时直接执行搜索命令的路径保持不变。
- MainFrame 缓存仍按实例 `GetType()` 精确匹配；首次 SetParameter 后添加，重访 SetParameter + Initialize；隐藏页仍 CancelLoading。OverlayFrame 缓存命中仍 Move 到栈顶、不额外 Initialize；GoBack 移除顶项并显示前项，ClearBackStack 后再导航重新构造。null Type 的 frame 早退保留。factory 只在未命中时调用，不持有页面实例。

## 红绿与验证

[自动断言和哈希结果](evidence-navigation-aot/validation.json) 由 [validate.py](evidence-navigation-aot/validate.py) 生成。大文件、全部 TRX/binlog、冻结 IL 输入和原失败日志位于 ignored `artifacts/navigation-aot`；小诊断原文入库。

- 局部真实 Native AOT：直接链接生产两个 frame、factory 和三个生产页面接口；仅 XAML/动画/UIElement/页面表面使用无桌面 adapter。原实现 ILC exit 1、两条 IL2067；最终 `dotnet publish` exit 0，无 IL 诊断，native console probe exit 0，17 类型的两种 frame 首次构造、复用、参数与返回通过。它不运行真实 WinUI 页面构造函数或 XAML，不能代替独立身份实机验收。
- 完整应用：复用原正式依赖 publish rsp，移除 nowarn 和合并诊断，保留 `-O`、warnings-as-errors、全部 feature/runtime switches、原 roots。每相位348个输入复制到独立目录，obj/sourcelink/def 全部独立；两个输入清单仅主应用 DLL 哈希不同；绿审计后221份实际可用 PDB 与冻结副本哈希仍相等。严格诊断从8条降到6条，恰好删除 MainFrame/OverlayFrame 的 IL2067，无新 IL2092/IL2067 等下游洞。SharpGen 两条、Azure 四条和正式 Mpv 两条 will-always-throw 逐条相等；两轮完整 ILC 都 exit 1，**不是全面 AOT 通过**。[完整 green](evidence-navigation-aot/full-green.txt)。
- fresh Release xUnit/VSTest：Passed **509**, Failed **0**, Skipped **0**；TRX XML 校验509个不同 executionId，其中原487项及本片22项全部通过。
- x64 Debug / Release 普通构建完成（Debug：1条既有 protobuf unused-import warning、0 error；Release：0 warning、0 error），采用原项目分析器与 warning 配置，未禁用分析器。严格364编译错误及 kernel CsWinRT1030 仍是既有独立证据，本轮没有把其重跑或通过作为结论；没有迁移309属性或47命令。

| Requirement | Evidence |
| --- | --- |
| 17个现有页面首次构造及参数传递 | `NavigationFrameTests.FirstNavigationConstructsRequestedPageAndPassesParameter`（17例）；无root局部native probe |
| 主页重访保留缓存、Initialize与隐藏页取消 | `NavigationFrameTests.MainRevisitReusesPageUpdatesParameterInitializesAndCancelsHiddenPage` |
| 叠层缓存置顶、返回显露前页及空栈返回 | `NavigationFrameTests.OverlayRevisitMovesCachedPageToTopAndBackRevealsPreviousPage` |
| 清空后新实例与null早退 | `NavigationFrameTests.ClearedOverlayConstructsFreshPageAndNullNavigationLeavesContentUnchanged` |
| 未注册类型拒绝且frame状态不变 | `NavigationFrameTests.UnregisteredTypeIsRejectedWithoutChangingCurrentPages`（2例） |

复放入口（在本工作区根目录，RC3 SDK，输出目录必须新建）：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=all.trx' --results-directory artifacts/navigation-aot/tests-final
dotnet publish docs/performance-acceptance/evidence-navigation-aot/Probe/Probe.csproj -c Release -r win-x64 -o artifacts/navigation-aot/probe-verified
./artifacts/navigation-aot/probe-verified/Probe.exe
# Debug/Release 均使用下列参数；SolutionDir 必须为本工作区的 src/ 绝对路径。
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -r win-x64 -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-f02c3f07/src/ -p:AppxPackageSigningEnabled=false
python docs/performance-acceptance/evidence-navigation-aot/freeze-ilc.py green ../t3-0ead9a3c/artifacts/aot-payload-verification/audit-ilc-isolated/original.rsp ../t3-0ead9a3c
& C:/Users/hp/.nuget/packages/runtime.win-x64.microsoft.dotnet.ilcompiler/11.0.0-rc.3.26522.148/tools/ilc.exe '@artifacts/navigation-aot/full-green/audit.rsp'
python docs/performance-acceptance/evidence-navigation-aot/validate.py
```

必须等对应配置的 ILC 完全退出后才能重编。复制 DLL 和旁边 PDB **不保证** ILC 不打开 CodeView 记录中的原绝对 PDB 路径。本轮一次同时重编被 CS2012 拒绝，完整原日志保留；随后完整红审计退出，再串行完成最终 Release 与完整绿审计，未争用主 DLL 或 sourcelink。首轮普通 Release 另因新工作区缺 ignored 媒体资产在编译后失败；仅从既有实施工作区复制 libmpv/ffmpeg，源与目标 SHA256 相等，无下载/安装/依赖采用。测试初次编译还发现与现有 PopularPage adapter 重复，移除旧空壳定义并共用本片 adapter；这些前置失败不计为缺陷红，真正红是两轮 ILC。

## WinUI 静态审查与交接

只审本片：页面构造仍在原导航调用线程；无静态页面单例、后台UI访问、额外事件订阅或缓存寿命改变。参数、动画、可见性、CancelLoading、重访 Initialize、Overlay Move/GoBack/Clear 均与原代码对应。未改 XAML、绑定、资源、命令或服务生命周期。公开页面无参构造由实际应用编译验证；未注册边界有明确测试。没有发现本片新增静态审查错误。WinUIAnalyzer 包未安装/运行，本轮不新增依赖；原代码的 MVVM/AOT 已知门禁未清理。

kernel 初始 HEAD `f02c33f6` 且索引为未展开删除状态，先保存完整 status/index patch/hash；从父 kernel 仓库只读获取指定对象，仅本工作区 checkout `0ae5e4e000a41a07f6696746706df11c06e8835b`，最终 clean，gitlink 未改。结束前执行 `git merge feat/performance-45`，父 tip 仍为 `8994fed85c6f55c5c93dab30e2069a99d70ad608`，结果 Already up to date；无父 checkout 写入。

**真实首次导航 UI、重复导航、叠层返回仍待独立 AOT 身份实机验收。** 正式 Mpv 不兼容、严格364编译错误、其余6条IL及分发/实机门禁仍 HOLD，本片产物不部署。

## 父级补正：注册页面构造异常契约

独立审查发现静态构造改变了原 Activator 的异常包装。保留审查 red；本次仅将 factory 中注册页面的直接构造放在 try/catch 内，抛 TargetInvocationException 并保留原 InnerException。未注册类型的 ArgumentException 仍在 catch 外；SetParameter/Initialize/缓存操作均在 factory 外，不被此包装捕获。

新增 `ConstructorFailurePreservesWrapperAndBothFramesCurrentContent`：以测试页面构造故障验证 Main/Overlay 包装及原异常身份、现有内容/可见性/栈不变，清除故障后可继续正常导航。测试适配器 AsyncLocal 故障仅作用于当前测试上下文，无生产测试钩子。

命令：RC3 dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~NavigationFrameTests --logger "trx;LogFileName=green.trx" --results-directory artifacts/navigation-wrapper-fix。

红阶段定向 1 failed；绿阶段导航 23 passed / 0 failed / 0 skipped，TRX 已解析。最初测试代码将 object 当 UIElement 的编译错误已修正，它不是行为 red。新增代码已静态核对 WinUI 线程/缓存/生命周期无改变，未运行专用 WinUIAnalyzer。此次没有重跑全套、完整应用构建或 ILC，因此原 8→6 证据仍只对应原候选源码；最终集成需补验证。真实 AOT 首次导航仍未验。
