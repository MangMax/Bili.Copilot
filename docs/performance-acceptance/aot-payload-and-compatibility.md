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

## 独立审查（2026-10-31，固定 b91d634d）

**结论：发布编排可按明确入口单独合入；正式 AOT 采用、完整分发和实机验收仍 HOLD。** 本节是独立审查补充，上述实施记录及原始 red 不覆盖。审查分支 `t3/review-aot-payload-compatibility` 从 `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428` fast-forward 纳入 `b91d634d5d91d44ee1982b30ed1449103001c055`；比较范围 `git diff 0ab128c8...b91d634d`，生产差异只有这 34 行 csproj。未改父 checkout、正式依赖、kernel、生产源文件或测试；未 restore/build/test、部署、启动 UI、登录、UAC、推送或发布包。当前无桌面授权。

已读 AGENTS、三个 docs/agents 规则及 code-review、winui-code-review、winui-packaging、msbuild、verification-before-completion 技能；按用户指令自己完成 standards/spec 两轴，不启子代理。需求以本轮审查委托为准；只读查询 #49 确认其仍是弹幕重绘/生命周期验收，不因 AOT 包成功而关闭。WinUIAnalyzer 未执行。kernel 在进入审查前已显示索引删除异常，原始 status/head/root diff 已保存到 [初始状态](evidence-aot-review/kernel-initial-status.txt)，无需 restore，所以未修复或覆盖它。

### Standards：两项交付准确性问题

1. **P2 / Error：Mpv 默认恢复脚本在当前标准 checkout 上失败。** `evidence-aot-payload/rebuild-mpv.ps1:13` 使用普通 `git apply`。从冻结 bundle clone、checkout 精确 c1ef0f4 后，两个文件末尾 hunk 均不能应用，exit 1，见 [原样失败](evidence-aot-review/patch-default-check.txt)。本机与原 Mpv 源库的 `core.autocrlf=true`。仅在审查临时副本用 `git apply --ignore-space-change` 后成功；逐文件归一化换行比较与原候选源码相等，见 [恢复核验](evidence-aot-review/recovery.json)。这证明源与 patch 可恢复，**不证明当前复放脚本开箱即用**。下一步应使补丁/checkout 换行契约一致，或采用受控忽略空白并校验完整 postimage 哈希；保留失败证据。无需重跑 487 测试证明补丁失败。
2. **P2 / Warning：实施文字把 DebugSymbols 的“收集”与“最终剔除”混淆。** 上文“同条件下不收集 build 的 debug symbol group”不符合最终 34 行代码：没有设置 `IncludeDebugSymbolsProjectOutputGroup=false`，实际 MSIX target 默认收集为 true，然后 `UseAotPublishPayload` 按 OutputGroup 移除。最终没有同名 PDB 冲突是已证事实，但不能据此声称从未收集。这里纠正说明，不重写历史。

未发现 34 行新增 NoWarn、关闭优化/分析器或修改正式依赖；不把原有全局 NoWarn 当成本次新回归。没有为满足风格启发式提出生产重构。

### Spec：两项采用阻塞，一项入口边界

1. **P1 / Error：正式依赖的 Mpv 缺失成员仍在。** 成功产物显式用了本地 `0.0.2-preview2-aot-local.1`；正式 `0.0.2-preview2` provider 的两个 `UseSegoeMetrics` MemberReference 仍指向当前 FluentIcons 不存在的 API。这是确定的二进制不兼容，相关 accessor 被执行会抛异常；不等于已观察到启动或所有播放器操作必崩。采用必须先获得可恢复、兼容且经独立验证的依赖输入，不能把本地候选成功标成正式 AOT 成功，也不能擅自发布第三方包。
2. **P1 / Error：媒体原生依赖仍缺少可分发的 Vulkan 前提。** 独立 PE import directory 解析确认 libmpv-2.dll、ffmpeg.exe 都直接导入 `vulkan-1.dll`，不是 delay import；应用及附带框架都不含它。缺 loader 时加载 libmpv/启动 ffmpeg 这一具体路径失败，不能仅以选非 Vulkan 渲染器规避静态导入。完整干净机分发 HOLD，详见下节。
3. **P2 / Warning：修复仅覆盖 `PublishAppxPackage=true`。** `BiliCopilot.UI.csproj:1553,1555` 的条件不会覆盖仅 `GenerateAppxPackageOnBuild=true` 或 VS `_GenerateAppxPackage` 入口。独立求值显示 legacy 模式 IncludePublishItemsOutputGroup=true、PublishAppxPackage=false；新 payload target 不执行，旧 `_AssembliesToSkipPublish` 适配问题未被修复。本轮未重建旧入口，故不声称实际复现其失败。若支持这些入口，必须另做同等集合验证；当前单独合入应明确唯一受验入口为完整 `dotnet publish ... -p:PublishAppxPackage=true`。

### 34 行 targets 与 RC3 的独立核验

读取了本机实际 RC3 SDK、ILCompiler `11.0.0-rc.3.26522.148` 和 MSIX `1.7.251221100` targets；使用 RC3 MSBuild 内建 replay 读取原始 `closed-publish/publish.binlog`，没有重新构建。完整 replay 保留在审查工作区 `artifacts/aot-review-local/publish-replay.log`，小摘录见 [顺序](evidence-aot-review/target-order.txt)。replay exit 0 仅说明读取成功；原记录另有 `closed-publish/exit.txt=0` 和 0 个错误。

- `UseOptimizedPublish=false` 选择完整 Build 路径，未改变 `Optimize=true`；应随 RC3 版本固定评审，不能视为所有未来 SDK 的永久契约。
- `PrepareAotPublishPayload` 在 GetPackagingOutputs 前以 DependsOnTargets 建立同一项目的 ComputeFilesToPublish 结果。该 SDK 的 NativeCompile 依赖 LinkNative，随后从 ResolvedFileToPublish 移除托管输入和 IlcSatelliteAssembly，再加入 native binary/PDB。这避免只依赖 CallTarget 输出而拿不到父 target scope 中完整集合的问题。
- replay 确认 ILC 09:25:38–09:29:20、LinkNative 结束 09:29:27.345、Prepare 09:29:27.346、Use 09:29:27.463、_ComputeAppxPackagePayload 09:29:27.470。不是空集合兜底、手拼 EXE 或用旧 map 证明顺序。
- Use 排除五个已知 build/publish 输出组，再保留其他 MSIX resource/content 项；不是所有类型文件的纯白名单。重新加入的 SDK 项显式映射 FullPath→TargetPath=RelativePath，保留子目录布局。过滤是按 OutputGroup，未来若新组携带托管 DLL，空集合 Error 并不能防止污染；应保留产物检查作为门禁，而不是再硬编码 DLL 黑名单。
- satellite DLL 不应从旧 Build 输出组补回：RC3 将其作为 IlcSatelliteAssembly 输入并在 NativeCompile 后移除，ILC 负责资源编译。当前包无独立 `.resources.dll`，不能误判“无 satellite 文件=丢了所有语言资源”。已有 194 路径与 22 PRI 完整性可证；PRI 内部资源解析、语言 fallback 和真实绑定仍要实机验证。
- Debug/JIT/AOT/legacy 只读重新求值见 `evidence-aot-review/properties-*.json`。普通 Debug：PublishAot=True 是既有配置，但 PublishAppxPackage/IncludePublishItemsOutputGroup=false，deps/runtimeconfig=true，Optimize=false；不会由新增 target 提前 ILC。显式 JIT `PublishAot=false`：两个 target 均不执行，deps/runtimeconfig=true，Optimize=true。AOT 打包：两组条件满足，deps/runtimeconfig=false、Optimize/分析器/WAE=true。本轮只有求值与原 AOT binlog证据，不宣称重新验证过 Debug/F5/JIT 构建或 UI。

### 产物、native 依赖与许可

独立脚本 [recompute.py](evidence-aot-review/recompute.py) 直接读取原 MSIX、publish 目录、框架 MSIX、TRX、gzip 诊断及 source hash 输入，未调用实施方检查器。结果见 [recomputed.json](evidence-aot-review/recomputed.json)：

- 包 167942971 字节、SHA256 `7DE8C7FA5FC8265C00A9EAA24E9DA60E3908B9D13B7DAD46A5C3CC68A31D6320`；284 ZIP 条目，194 个运行时/内容文件按相对路径逐项哈希相等；15 PDB、16 符合 doc/assembly/name 的文档不随包，未忽略任意 XML。无缺失或不匹配。
- 13 PE 的 Machine/CLR/import/delay 表独立解析，见 [应用 PE](evidence-aot-review/pe-imports.json)；全部 native x64。主 EXE `4D1DA8CE...417CDDCED` 与 publish 相同。结合真实 ILC/link 顺序、native 输出替换和包/publish 同一性，支持“这个候选是 Native AOT 产物”；单项无 CLR 头不足以证明 AOT，更不证明运行正确。
- 框架 MSIX hash `2C914AAB...C67A46A` 匹配，另解析其全部 53 PE 的直接及 delay 导入，见 [框架 PE](evidence-aot-review/framework-pe-imports.json)。包清单声明 WindowsAppRuntime.2 >=2.6.4.0；框架提供 WindowsAppRuntime/FrameworkUdk，应用自带 DirectML/onnxruntime。递归到这两个包的文件集合后，外部导入全表仍列于 recomputed.json；Windows/API-set、ICU/DirectX 等需按目标系统契约验证，不能用本机文件存在取代。此审查没有解析所有 export forwarder 或动态 LoadLibrary 字符串，也没有验证实际 package graph/DLL 搜索顺序，故不给完整运行时闭包 PASS。

Vulkan 来源方面，仓库 `.github/actions/setup-env/action.yml:81–120` 从 shinchiro/mpv-winbuild-cmake 的 latest 通配资产只提取 libmpv/ffmpeg，没有对应 loader 来源、版本、签名、许可证/NOTICE 或部署方案。冻结媒体 hash 能保证本次文件身份，不能从 latest 脚本反推其完整可再分发来源链。

已查 [Khronos loader 官方接口说明](https://github.com/KhronosGroup/Vulkan-Loader/blob/main/docs/LoaderApplicationInterface.md)：Windows loader 通常随驱动/runtime 安装，官方不鼓励应用私带固定 loader。可考虑明确的受支持 runtime 前提/安装方案，或有完整来源且消除此强导入的媒体资产；选择由后续单独切片做出。本轮未选定或下载 loader。[官方 LICENSE](https://github.com/KhronosGroup/Vulkan-Loader/blob/main/LICENSE.txt) 主体 Apache-2.0，部分文件有更宽松例外；若选择再分发，需针对精确源码/二进制附许可证、适用 NOTICE、保留声明及修改说明，并审核所用 installer/附属组件各自条款。该源码许可证不是对任意 System32 文件的分发许可证明，也不是对 GPU 驱动的许可。禁止复制 System32 来补闭包；本机存在 loader 不能放行干净机分发。

### Mpv 可恢复性与修改范围

五项 source-hashes 全部复算吻合，Mpv/kernel bundle 均通过 `git bundle verify`，完整历史含指定提交。仅在审查临时 source clone 重放 Mpv patch；kernel 保持异常现场。默认 patch 应用失败与受控恢复已分别保存，原 red 不修改。

候选 nupkg SHA256 `916E00A1D49DDDDE35FFBB4E1EE321C64C0FC6422F6E0AC98A17250E67DC62ED`，包内 DLL hash `CE960E27...93C7EA61` 与复查 DLL 一致；PRI 存在于正确 TFM 目录。nuspec 的 Player 仍为 0.0.2-preview2，新增 FluentIcons 2.2.344 直引、AppSDK/BuildTools 对齐；正式应用中央引用未切。bundle+patch+冻结 nupkg 在本机可恢复；Git 提交中不包含大 bundle/nupkg，跨机器须另交付这些具有哈希的文件，且未证明重新 pack 可字节完全复现。

[重新读取元数据](evidence-aot-review/metadata-recheck.txt) 确认上游 DLL 含两个缺失 MemberReference，候选 provider 不含它们，程序集版本仍 1.0.0.0；FluentIcons 的 SymbolIcon/GenericIcon 均没有该 API。patch 只改两份项目/包配置，没有业务 C#/XAML diff，因此“通过重新生成兼容 provider 去掉坏 accessor”成立。**不能进一步声称二进制只改变 provider**：整份 WinUI 程序集被不同依赖重新编译，PRI/生成代码也可能变化，本轮未做全方法 IL 等价证明。Player/Core/Interop 源码不纳入重编项目；未授权向 NuGet/GitHub 外部发布候选。

### 严格诊断：编译阻塞与可达性分开

gzip 解压独立计数：309 MVVMTK0045 +47 MVVMTK0046=356；level2 加8 CsWinRT1028=364。另有先前 kernel Dictionary 的 CsWinRT1030，使未加 unsafe 的第一轮更早退出；364 是审计副本全局 AllowUnsafeBlocks=true 后的计数，不能把 1030 当成已经正式修复。按文件台账见 [level2-by-file.json](evidence-aot-review/level2-by-file.json)。ILC 8 条为 IL2067×3、IL2072×1、IL2026×2、IL3050×2；此外还有 Mpv 两条 will-always-throw，不把它们吞进“8 IL”数字。

这些是严格无抑制策略下的真实构建阻塞，但并非 364+8 个已观测运行故障。现有正式 NoWarn 仍抑制它们；本地候选成功使用现有配置，并没有完成候选的全套无抑制编译+ILC 审计。

| 类别 | 独立源码/证据判断 | 采用门槛 |
| --- | --- | --- |
| 309 field ObservableProperty | 跨 generator 看不到生成属性，不能靠给 class 加 partial 修复；例如 NavigationViewModel 仍有 field 与 partial property 混用。 | 按单一 VM/视图迁移 public partial property，追踪旧 backing field 的直接引用、默认值、通知 hook、Dependent/CanExecute 属性及序列化；保留 setter 通知语义，禁止全库替换。 |
| 47 RelayCommand + GeneratedBindableCustomProperty | 必须逐项审计消费端。NotifyMessageItemViewModel.ActiveAsync 的消费是 NotifyMessageCardControl.xaml:17 的 x:Bind；不能直接说此命令运行时 Binding 必失效。 | 静态 x:Bind-only 的命令可从显式 provider 成员集合排除；确需运行时发现的命令采用手写可见 command property/typed wrapper，并保留 async、参数、CanExecute、取消与并发语义。不要只移除类 attribute，也不要用同样不可见的另一个生成属性包装敷衍。 |
| 8 CsWinRT1028 | PopularRankPartition/PopularSection/SponsorBlockCategorySetting 作为 ItemsSource/DataTemplate 模型，ABI 暴露有实际依据。TrayIconSession、DanmakuLoadSession、CustomHttpClientHandler、SponsorBlockService、TrayLifecycle 涉及 IDisposable 等映射接口；当前看到的是托管会话/handler/回调，持有 WinUI 对象不自动意味着实例本身跨 ABI。 | 对真实过界类型及其外层声明 partial；其余追踪传参/注册/容器逃逸。可为严格编译生成接口支持，但不能把编译警告直接标注为已发生运行事故，不能 blanket suppress 1028。 |
| MainFrame/OverlayFrame IL2067×2 | 首次 NavigateTo 的 Activator.CreateInstance 可达；NavigationViewModel:84/120 转发任意 Type，涉及主页与叠层搜索等正常功能。缓存命中不等于首次构造安全。 | 两个 frame 加准确 PublicParameterlessConstructor 契约并沿 service/interface/Type 存储链传播，或限定静态 factory 注册；扫描所有 typeof、泛型/字符串路径并验证构造保留，不以 root 整个程序集掩盖缺口。 |
| SharpGen IL2067/2072 | 闭包中 vtable reflection 被 ILC 标记；AppToolkit:226 的 DXGI factory 是已存在 interop 入口，但不足以证明每条 callback/vtable 分支都执行。 | 固定实际 SharpGen/Vortice 版本，追踪 GetTargetVtbl/RegisterFromReflection 到具体可达回调；修正准确字段/属性契约或采用可审核兼容上游版本，单独验证目标 GPU/interop 功能。 |
| AzureInference IL2026/3050×4 | GlobalDependencies:74–75 注册 Azure AI/OpenAI，不能当成未使用依赖。OpenTelemetryScope 构造/End 中 object JSON 反射不安全；具体 listener/content-recording 条件尚无证据。 | 获取精确源码及开关默认值，修成 typed DTO+JsonTypeInfo 或经产品确认关闭这一具体 telemetry 路径，并证明 linker 可消除/代码不再含该危险调用。仅默认不开 listener 不足以消除采用阻塞。无需登录或发送真实 AI 请求来做此分析。 |

### ≤M 的后续切片与最小下一步

M 按仓库 issue 标签含义“多文件但边界清晰”，每片只解决一个明确机制；以下是任务规格，不是本轮授权改动。任何新测试执行前必须读 run-tests 技能。

1. **下一片最小任务（S）：修复 Mpv 恢复入口的换行契约。** 仅 `rebuild-mpv.ps1`/patch 与恢复证据：从冻结 bundle 新目录 checkout，普通或受控 apply 必须成功；校验两个完整 postimage、包内 DLL/PRI 身份及 Player 依赖不变。无需应用 UI、无需批改 AOT 警告、无需外发 nupkg。先让候选能可靠交接，再做依赖采用决策。
2. **S：明确打包入口契约。** 明文只支持当前 publish 命令，或单独适配 legacy 两种入口；用目标条件矩阵及各自包集合验收，不能以一次成功代替。与候选依赖采用分开提交。
3. **M：导航首次构造。** 只 MainFrame/OverlayFrame 与导航 service 的 Type 传递闭包；新增/调整精确构造契约或静态 factory，严格 ILC 两条应用 IL2067 消失且无下游新洞；独立 AOT 包后续验证首次导航、重复导航与叠层返回。不要把 309 属性一起改。
4. **S/M：kernel cookie Dictionary ABI。** 仅 resolver 项目的 unsafe 生成配置及直接 ABI 路径，验证 CsWinRT1030 消失；不全局给所有项目开 unsafe，不擅改 kernel gitlink。
5. **S/M 首片 + 重复 M：MVVM 属性。** 首片仅 NavigationViewModel 的6个 field；其余按 `level2-by-file.json` 的单个 VM/视图分片。SettingsPage 的49项至少按设置子区域拆片，每片保留初始化、通知和 binding 消费清单，以该片诊断差额验收，不要求其余存量一次清零。
6. **S 首片 + 重复 M：命令 provider。** 首片仅 NotifyMessageItemViewModel.ActiveCommand 的 x:Bind/provider 成员决策与一个消费控件；其余47项按单一 item VM 分片，复杂 Video/Moment VM 再按命令功能分片。验收包括生成 provider 可见成员和 command 行为。
7. **S/M：CsWinRT 类型。** UI ItemsSource 三个模型按各自视图拆片；托盘两个生命周期类型同片；HTTP、弹幕会话、SponsorBlock 各一片。先证明 ABI 过界或严格生成需要，再改 partial/外层声明；不混入功能优化。
8. **M：SharpGen vtable 路径** 与 **M：AzureInference telemetry** 分别做精确依赖源码/路径修复；每片需局部无抑制 AOT 诊断和可控行为证据，禁止全局 trim root/NoWarn。候选全闭包严格审计须在这两片及 MVVM 完成后重跑，原红计数仍保留。
9. **M：媒体资产分发前提。** 固定下载 URL/tag、媒体与 loader 的源/构建/许可证链，选择 runtime 前提或无强 Vulkan 导入资产，交付哈希、许可/NOTICE 和安装边界。任何安装/UI/驱动验证由父级另行授权，本轮不做。
10. **M：正式依赖采用与独立身份实机验收。** 在上述编译及分发门槛通过后才考虑切正式依赖；验证播放器、字体/PRI/语言、绑定、导航、interop 和 AI 可达路径。#49 的双引擎暂停/卸载契约须独立验收，不能由 AOT 工作抵销。

### 可合入与 HOLD 的边界

- **可单独合入：** 34 行发布编排，在版本固定、完整 dotnet publish/PublishAppxPackage=true、保持当前 Release 优化及警告配置、继续产物扫描的明确范围下；这只认可 packaging 修复。文档、原 red 和独立审查证据也可单独合入。
- **需先修再称可复放：** 默认 Mpv 恢复脚本。不得宣称候选只改了 provider 的二进制字节或已获第三方发布授权。
- **HOLD：** 正式 Mpv 采用、未抑制完整 AOT 门禁、legacy/ARM64 未验证入口、Vulkan 干净机分发、独立身份实机与性能/#49 验收。本轮没有任何新运行/性能 PASS。

TRX 独立 XML 读取总计487、executed487、Passed487、failed0/notExecuted0，并核对487个不同 executionId，SHA256 `7AE42249...426E71`；这是原 RC3 Release 单元回归的证据复核，非本轮重跑。未发现需要重新运行该套单元测试的新行为变更：补丁恢复失败、条件入口差异与依赖缺口可用静态/文件检查证实。故不运行 test、不触碰 kernel，不用单元测试冒充桌面验收。

standards 两项（最严重：默认恢复失败）；spec 三项（最严重：正式二进制不兼容及 Vulkan 分发缺口）。结束时只读再次核对本地父 `feat/performance-45` 仍为0ab128c8且已在本分支祖先中；仅提交本节及 evidence-aot-review，无合父、推送或发布。

审查交付验证：`validation.json` 的断言通过（194/13/284、364/8、487、source hashes、父 tip、kernel 初始状态及生产 src 不变）。报告与复算脚本 whitespace 检查通过；全证据 `diff --check` 仅报 `patch-whitespace-check.txt:2` 中原 git 输出引用的上游空格+tab，按原始证据保留，不把全证据检查写成零警告。

### 父级修正恢复脚本（2026-10-31）

保留默认恢复失败的原 red。`rebuild-mpv.ps1` 现在允许上下文换行/空白差异，并在任何 build 前验证两份完整 postimage 的 UTF-8/LF SHA256；不修改冻结 patch，不改变包版本或生成源内容。哈希与上述 recovery.json 一致，内容偏离则抛错停止。

父级在新的 ignored `artifacts/mpv-recovery-parent-fix/source` 从冻结 bundle 执行脚本的 clone/checkout/apply/hash 前缀，恢复及两个哈希均通过；完整脚本 PowerShell 语法检查通过。git apply 的两条既有空白 warning 原样保留。这是恢复入口验证，未重新 pack、运行487或启动应用，不宣称新的包字节复现或 AOT实机通过。受验发布入口仍仅完整 dotnet publish + PublishAppxPackage=true，legacy入口未覆盖。
