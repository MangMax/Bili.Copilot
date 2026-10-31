# AOT payload 与 MpvKernel 兼容候选

2026-10-31，独立工作区 `t3-0ead9a3c`。本报告接续 [工具链重试](aot-toolchain-retry.md)，旧红记录保持原样。**#49 FAIL / perf HOLD 保持；没有部署、注册或启动应用。**

## 输入与范围

初始根提交 `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`。根与 kernel 状态、patch 保存在 ignored `artifacts/aot-payload-verification/initial-*`。kernel 初始只有 `.git`、索引零条目，保存后从前交付工作区本地 fetch 并恢复精确 `0ae5e4e000a41a07f6696746706df11c06e8835b`。根 fast-forward 合入 `df9508bd9f861aacc8df4acf7cc3a485279f404c`，保留安装取消及重试文档祖先。

使用已安装 RC3 SDK `11.0.100-rc.3.26522.148` 和现有 MSVC 14.51.36231；没有安装、UAC、重启、证书操作。原 libmpv/ffmpeg x64 资产只复制至本工作区。父 checkout、Dev、PID9608、caa6a7be、layout-v3 与凭据均未操作。

## 项目级最小修复

仅 `BiliCopilot.UI.csproj` 增加 AOT 发布属性组：

- `UseOptimizedPublish=false`：RC3 优化 publish 在 ResolveReferences 前进入 PrepareResources，WinUI 的 MarkupCompilePass1 因空引用失败。采用 SDK 明文提供的完整 Build 路径，不改变 `Optimize`、AOT、分析器和 warnings-as-errors。
- `IncludePublishItemsOutputGroup=true`，仅在 `PublishAppxPackage=true`：当前 MSIX targets 的 AOT 默认值为 false，只识别 `GenerateAppxPackageOnBuild` 和旧 `_GenerateAppxPackage` 入口来重新启用。普通 F5 构建仍不提前执行 AOT。
- 同条件下不收集 build 的 debug symbol group；native PDB 仍由 publish 提供，避免同名托管 PDB 的 APPX1101。AOT 打包不生成 .NET JIT 的 runtimeconfig/deps。

[实际 MSIX targets](evidence-aot-payload/msix-targets.txt) 的 `GetPackagingOutputs` 调用 `__GetPublishItems`，后者依赖 `ComputeFilesToPublish`。[Native 发布 targets](evidence-aot-payload/native-publish-targets.txt) 的 `NativeCompile` 依赖 `LinkNative`，随后替换 `ResolvedFileToPublish` 中的托管程序集。首次启用 output group 已验证 [native/link 先于 payload](evidence-aot-payload/output-group-order.txt)，但在 PDB 同名检查失败。

继续追踪发现，MSIX 的 `WinAppSdkRemoveLinkedFilesFromPayload` 仍读取 `_AssembliesToSkipPublish`，而 RC3 SDK/ILC 已不再定义此项。最终两个小 target 明确在项目作用域先完成 `ComputeFilesToPublish`，再以 SDK 的完整 `ResolvedFileToPublish`（含 RelativePath）作为二进制闭包，额外保留 MSIX 的内容、PRI 等资源输出。排除来自各项目的 BuiltProject、CopyLocal、DebugSymbols、SatelliteDlls 和旧 Publish 输出组，避免把 JIT runtime pack、引用项目 DLL 与 native publish 结果混合。集合为空则拒绝打包。没有按固定 DLL 列表删文件，也没有手工加入 EXE、改变 native 编译输入或复制旧 map。

中间方案只按 `PostprocessAssembly` 标记删除已编译输入，MakeAppx 返回 0，但包扫描发现 **182 个 managed PE + coreclr/clrjit/hostfxr/hostpolicy**（`final-verified/` 与 `candidate-contaminated-inspection/`）。原因包含 RC3 替换 runtime pack 时的路径元数据差异以及卫星程序集。因此该包明确判定闭包 FAIL，不作为最终交付；最终方案从正向的完整 publish 输出集合构造二进制 payload，资源仍由 MSIX targets 提供。

前交付原始失败证据仍在 [目标顺序](evidence-aot-retry/native-and-package-targets.txt) 和 [错误 map](evidence-aot-retry/package-map-excerpt.txt)。新调用与产物验证见下文。

## 第三方二进制不兼容

包元数据指向 MpvKernel 源提交 `c1ef0f403097129c3b6db4c466cc4147054471d2`。其 WinUI 层经 `Richasy.WinUIKernel.Share/0.0.7-preview6` 使用 FluentIcons 1.1.292；当前应用实际使用 WinUI 2.2.344 / Common 2.1.344。

[FluentIcons 官方提交 94da1186](https://github.com/davidxuang/FluentIcons/commit/94da118635f197ae0816c8a6de53cb50a8b8df7e) 删除已弃用的 `UseSegoeMetrics`。当前包记录源提交 `874f2da40bef0c36a009ca081a5a6bd58ea077c2`。本地源码摘录见 [API 删除](evidence-aot-payload/fluent-api-removal.txt)。[实际 DLL 元数据](evidence-aot-payload/metadata.txt) 确认：

- 上游 MpvKernel DLL 有 get/set 两个 MemberReference；FluentIcons 当前 DLL 及其 GenericIcon 基类没有对应成员。
- 本地重新生成的 XamlTypeInfoProvider 不再包含这两个 accessor；程序集版本保持 `1.0.0.0`。
- 没有通过 trim root 或保留规则处理不存在的 API，也没有回退 FluentIcons。

候选仅重编 WinUI 层，Player/Core/Interop 保持原 `0.0.2-preview2` 包。对齐 FluentIcons、AppSDK 和 BuildTools，使用源包原有分析器；修正上游 pack 的 PRI 路径以包含 x64。见 [精确 patch](evidence-aot-payload/mpv-candidate.patch)、[构建结果](evidence-aot-payload/mpv-rebuild.txt) 和 [复放](evidence-aot-payload/rebuild-mpv.ps1)。源 commit + 本地 bundle + patch 可恢复。

候选版本 `0.0.2-preview2-aot-local.1`，nupkg SHA256 `916E00A1D49DDDDE35FFBB4E1EE321C64C0FC6422F6E0AC98A17250E67DC62ED`。正式中央依赖未改变；仅显式导入 [local-candidate.targets](evidence-aot-payload/local-candidate.targets) 的实验使用它。禁止将该候选的发布成功等同于正式依赖已修复；采用前需独立代码/包复核和父安排的独立身份实机验证。

## 未抑制诊断审计

独立源码 export 编译去掉应用 NoWarn 中 IL 系列、MVVMTK0045/0046，保留其余既有配置及 warnings-as-errors。exit 1，356 个错误：309 个 MVVMTK0045（ObservableProperty 字段对 WinRT AOT 绑定不安全），47 个 MVVMTK0046（GeneratedBindableCustomProperty 无法看到另一 generator 生成的 RelayCommand 属性）。[计数](evidence-aot-payload/compiler-counts.json)；[完整去重诊断压缩文件](evidence-aot-payload/compiler-diagnostics.txt.gz)。编译失败阻止更后阶段，不能由此推断没有其他 CsWinRT/IL 诊断。

进一步显式设置 `CsWinRTAotWarningLevel=2`，首先在 kernel `Resolvers.WinUICookies/WinUIBiliCookiesResolver.cs:30` 报 `CsWinRT1030`：Dictionary 的 WinRT 接口生成代码需要 `AllowUnsafeBlocks=true`，[原始结果](evidence-aot-payload/cswinrt-level2.txt)。仅在审计副本通过全局参数允许 unsafe 代码生成（不禁用任何规则），继续得到 **364 个错误**：上述 356 个 + 8 个 CsWinRT1028，[完整诊断](evidence-aot-payload/level2-diagnostics.txt.gz)、[计数](evidence-aot-payload/level2-counts.json)、[8 项明细](evidence-aot-payload/cswinrt-expanded.txt)。

8 个类型为 TrayIconSession、DanmakuLoadSession、CustomHttpClientHandler、SponsorBlockService、TrayLifecycle、PopularRankPartitionViewModel、PopularSectionItemViewModel、SponsorBlockCategorySettingViewModel。规则要求类型/包含类型 partial 以生成 AOT 所需接口支持，实际影响仍需检查是否跨 WinRT ABI；这里报告严格编译阻塞和需要审核的风险，不声称已观察到 8 个运行故障。正式 kernel gitlink、项目 warning level 和 AllowUnsafeBlocks 均未改变。

ILC 审计从实际发布 rsp 复放，移除 nowarn，展开依赖诊断，保留 `-O`、`--warnaserror`、roots 和 feature switches。所有输出包括 obj、sourcelink、def 都重定向至独立目录。[复放脚本](evidence-aot-payload/audit-ilc.ps1)。第一次试验只重定向 obj，与仍运行的原发布争用 sourcelink，失败证据保留在 `audit-ilc/`；它不是完整审计结果，后续使用 `audit-ilc-isolated/`。

完整隔离 ILC 审计 exit 1，[逐条诊断](evidence-aot-payload/ilc-diagnostics.txt)、[计数](evidence-aot-payload/ilc-counts.json)：

| 归属 | 诊断 | 阻塞与采用条件 |
| --- | --- | --- |
| 应用 MainFrame / OverlayFrame | IL2067 ×2 | NavigateTo 的 Type 参数传给 Activator.CreateInstance，缺少公开无参构造保留契约；需追踪所有调用者并验证导航集合。 |
| SharpGen.Runtime | IL2067 ×1、IL2072 ×1 | vtable 反射访问公开/非公开字段与属性的标注不完整；需结合具体 interop 路径或兼容上游修复验证。 |
| Richasy.AgentKernel.Core.AzureInference | IL2026 ×2、IL3050 ×2 | OpenTelemetryScope 构造与 End 使用反射 JSON 序列化；AOT 动态代码不可用，需上游 source-generation/关闭该具体功能的产品决策，不能 blanket suppress。 |
| 旧 MpvKernel provider | 两条 will-always-throw 消息 | 缺失方法，独立于 IL 编号；本地候选消除对应 MemberReference，仍需实机。 |

ILC 审计输入是正式上游依赖版本，明确不是“候选完整无警告”证明。保留全部 runtime/trim feature switches 和已有 rd.xml，因此上述计数是当前完整发布闭包在这些配置下发出的诊断，不代表所有可能运行路径已获证明。默认 warning level 没有直接 CsWinRT 编号，也不能抵消 level 2 结果和 356 个 WinRT 绑定兼容错误。

一次与严格 ILC 审计同时重编同一主 DLL 的重试因只读映射锁报 CS2012（`upstream-package/`）；等待审计结束后再执行最终发布。一次 `--no-build` 快速复放触发 WinUI 缺少 InitializeComponent 的编译错误（`candidate-payload-fixed/`）；最终复放脚本采用完整 Build，不依赖该路径。

首次 closure 适配在未带 `PostprocessAssembly` 元数据的字体资源上报 MSB4096（`upstream-final/`、`final-publish/`）；修正为限定项类型的 `%(ResolvedFileToPublish.PostprocessAssembly)`，使资源项按空元数据正常跳过。保留失败 binlog，使用 `upstream-verified/` 和 `final-verified/` 复验。

## 最终 x64 产物与闭包边界

`artifacts/aot-payload-verification/closed-publish/` 的完整 publish exit **0**，使用显式本地 Mpv 候选。独立源码 export 起初没有 bin/obj；修复迭代复用了该 export，最终发布使用全新的输出目录，并真实重跑 ILC/link，不宣称最后一次是全冷缓存构建。生产依赖仍有上述缺失成员风险。

[最终目标顺序](evidence-aot-payload/final-target-order.txt)：ILC 09:25:38.405–09:29:20.816，LinkNative 至 09:29:27.345，随后 PrepareAotPublishPayload、UseAotPublishPayload，最后 09:29:27.470 计算 MSIX payload。[发布日志](evidence-aot-payload/final-publish.txt) 保留 mspdbcmf 缺失导致不生成 symbols package 的警告；native PDB 仍保留。最终候选日志没有旧 provider 的 missing-method / will-always-throw 消息。

| 产物（相对 closed-publish） | 字节 | SHA256 |
| --- | ---: | --- |
| `publish/BiliCopilot.UI.exe` | 111508480 | `4D1DA8CE36EDDC0B1601958A3741B53BAA752D825AF9A13FAEAA896417CDDCED` |
| `packages/BiliCopilot.UI_2.2610.0.0_x64_Test/BiliCopilot.UI_2.2610.0.0_x64.msix` | 167942971 | `7DE8C7FA5FC8265C00A9EAA24E9DA60E3908B9D13B7DAD46A5C3CC68A31D6320` |

[包检查](evidence-aot-payload/artifact-results.json) exit **0**：主 EXE 为 x64、CLR directory 为 0，包内与 publish 哈希相同；284 项中全部 13 个 EXE/DLL 均无 CLR directory；没有 managed PE、CoreCLR、clrjit、hostfxr、hostpolicy 或应用 runtimeconfig/deps。194 个发布运行时/内容文件逐项哈希一致。SDK 排除的 15 个 PDB 和 16 个程序集 XML 文档不要求随 MSIX 分发；检查器只在 XML 的 doc/assembly/name 与文件名匹配时认可文档排除，不忽略任意运行时 XML。包未签名，未变更身份，未安装。

[框架依赖证据](evidence-aot-payload/framework-dependency.json) 确认 manifest 要求 Microsoft.WindowsAppRuntime.2 >= 2.6.4.0，输出附带同版本 x64 框架包，包含 Microsoft.WindowsAppRuntime.dll / Microsoft.Internal.FrameworkUdk.dll。DirectML 与 onnxruntime 在应用包内。

**干净机器的 native 依赖闭包仍有条件，不能标记全面 PASS。** [13 个 PE 的依赖表](evidence-aot-payload/native-dependents.json) 及 libmpv/ffmpeg imports 摘录显示，两者直接导入 `vulkan-1.dll`，不是 delay-load；应用包和附带框架都没有该 DLL。本机 System32 存在 loader 只说明构建机环境，不能证明分发环境满足。没有复制主机 DLL、安装运行时或替换既有冻结媒体资产。父实机验证须核实 Vulkan loader 来源与可用性，或另行复核有许可/来源记录的可再分发 loader 或无该依赖的资产。其余 OS/API-set 导入也仍受目标系统运行验证约束。

精确源、包和大产物见 [source-hashes.json](evidence-aot-payload/source-hashes.json)、[packages.json](evidence-aot-payload/packages.json)、[artifact-hashes.json](evidence-aot-payload/artifact-hashes.json)。大文件留在 ignored artifacts，包括原始 binlog、native obj/PDB、MSIX、第三方 source bundle 与 kernel bundle。ARM64 应用未构建；SDK 自动复制的其他架构框架 redist 不代表 ARM64 应用通过。

## 回归与复核边界

[487 回归](evidence-aot-payload/tests.txt)：Passed 487 / Failed 0 / Skipped 0，SDK RC3、Release、VSTest/xUnit。未改测试或应用运行时代码。项目差异仅改变发布编排；本地第三方 patch 不改变 XAML/C# 业务逻辑，重新生成 provider。

本实施代理自行执行 standards/spec/WinUI 静态审核，没有启动子代理，不替代父安排的独立复核。现有项目没有 Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 包，本轮没有该分析器的检查结果；已启用的编译和 CsWinRT/MVVM 工具保持原配置，审计另列真实错误。AOT 实机运行、播放器行为、动态绑定及第三方反射行为仍未验证，不关闭 #49。

交付前再次同步本地父分支 `feat/performance-45`，tip 为 `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`（已在当前祖先中）。仅提交项目级发布修复、此报告、小证据与复放脚本；没有 push、PR、issue 操作或父 checkout 改动。普通 Debug 的 PublishAppxPackage、IncludePublishItemsOutputGroup 仍为 false，runtimeconfig/deps 仍开启；Release Optimize、AOT 与分析器保持开启，没有添加 NoWarn 或关闭规则。

## 复放入口

先用 [export-source.ps1](evidence-aot-payload/export-source.ps1) 建立新的源码 export（不复用 bin/obj），再用 [publish-x64.ps1](evidence-aot-payload/publish-x64.ps1) 指定 SDK、SourceRoot、新 Output；`-LocalCandidate` 显式采用本地 feed。本地 feed 通过 rebuild-mpv.ps1 从精确源与 patch 重建，也可用本地源 bundle 替代远端 Repository。完整依赖输入见 [packages.json](evidence-aot-payload/packages.json)。

使用 [inspect-package.ps1](evidence-aot-payload/inspect-package.ps1) 比较 publish/包内 EXE SHA256、检查 PE 的 CLR directory、扫描所有 EXE/DLL，并拒绝残留 managed 主程序、CoreCLR/hostpolicy/hostfxr/clrjit 或应用 runtimeconfig/deps。打包框架依赖仍应按 [微软部署文档](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-packaged-apps) 随包检查；未安装框架包或应用。

项目 XML 和全部 PowerShell 脚本语法检查通过。代码/脚本差异 whitespace 检查通过；完整证据差异保留上游 diff 与 binlog 摘录原有尾空格（mpv-candidate.patch、fluent-api-removal.txt、coreclr-build-output.txt），不重写取证原文。
