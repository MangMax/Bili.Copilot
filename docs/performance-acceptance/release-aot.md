# Release / Native AOT 独立发布验证

2026-10-30。**结果：发布被既有 C++ 工具链不完整阻塞，未生成 Native AOT EXE 或 MSIX。** Release C#/XAML 编译通过后，在 Native AOT `SetupOSSpecificProps` 阶段失败。没有运行应用，不能替代 runtime / UI 验收。

## 基线与边界

- 验证 checkout：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-64b6ed4f`，分支 `t3/verify-release-native-aot`。
- 起始集成：`a236aaa2266156804651223743444a0418af4822`，包含生产修正 `5151c1b`。
- kernel：`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，源码完整且干净，没有修复指针或修改子模块。
- 读取 AGENTS.md、docs/agents、winui-packaging、winui-dev-workflow、winui-code-review、verification-before-completion；MSBuild 失败后读取 msbuild、systematic-debugging 与 binlog-failure-analysis。
- 不改生产代码、版本、身份或证书；不安装工具、不上传、不注册、不启动应用、不操作桌面。#52 的桌面占用不受本次验证影响。

## 实际项目与工具条件

`BiliCopilot.UI.csproj` 无条件设置 `PublishAot=True`，不是仅 Release 开启。x64 profile 设置 `RuntimeIdentifier=win-x64`、`SelfContained=true`、`PublishSingleFile=False`。Release 实际 `Optimize=true`，仍定义 `LOCAL_DEV;ARCH_X64`。配置名 `Publish|x64` 的额外 Optimize 条件不等于执行 publish。

Release 选择 `Package.appxmanifest`：`Mang.BiliCopilot`、`CN=Mang`、`2.2610.0.0`。默认签名启用；本次仅用命令行 `AppxPackageSigningEnabled=false` 生成未签名证据，不访问签名私钥。`WindowsPackageType=MSIX`，`WindowsAppSDKSelfContained` 未设置，因此 .NET Native AOT 不意味着 Windows App SDK 自包含。

本机 .NET SDK **10.0.403**（CI setup-env 声明 9.0.x，故并非相同 SDK 环境），VS Community **18.11.12324.101**，存在 MSVC **14.51.36231** 的部分文件（并非完整桌面 C++ 工具链），WinApp **0.7.1**。本次还原解析到 ILCompiler **9.0.22**、WindowsAppSDK **1.8.251106002**、SDK.BuildTools **10.0.26100.6901**、MSIX BuildTools **1.7.20250829.1**、CsWinRT **2.3.0-prerelease.251015.2**。后者是实际传递依赖结果，不能仅引用中央版本文件的 2.2.0。

工程已有大量 `NoWarn`（见 [实际属性](evidence-release-aot/properties.json)），包含 IL/AOT/trim 与 MVVMTK 项。本次没有新增抑制、降低 warnings-as-errors 或关闭 analyzer/optimizer。`TreatWarningsAsErrors=true`、`SuppressTrimAnalysisWarnings=false`、`CsWinRTAotWarningLevel=1`、`CsWinRTAotOptimizerEnabled=true`。专用 WinUIAnalyzer 未引用，未运行；不能把编译成功解释为全部 AOT/WinUI 警告清零。

## 运行时资源

从用户授权的 `C:/WorkFile/Project/Bili.Copilot/src/Desktop/BiliCopilot.UI/Assets` 复制缺失 x64 libmpv 与 ffmpeg 到本 checkout 的同相对路径，先用 `git check-ignore` 验证目标被忽略。BBDown 本 checkout 已存在，没有替换。

| 文件 | SHA-256 |
| --- | --- |
| libmpv/x64/libmpv-2.dll | `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A` |
| ffmpeg/x64/ffmpeg.exe | `A1BCBA486CCE341EB033522F0625BB356AE17B1D124A2BC59CFA22540DB81D4B` |
| bbdown/x64/BBDown.exe | `8C94F19AB3992AA1FA2E785B88FF692875D10030BB2190FD6BDF33212A412442` |

项目 `ValidateRuntimeDependencies` 在打包前检查三文件存在和 x64/ARM64 平台，不证明它们能加载或播放。

## 仓库流程与第一次失败

`.github/actions/setup-env/action.yml` 下载运行库、安装 .NET 9、还原；`.github/actions/build-app/action.yml` 用 MSBuild `GenerateAppxPackageOnBuild=true`，分别选择 SideloadOnly + 签名或 StoreUpload + 不签名。仓库流程还包含版本/身份与发布步骤，本次不执行这些有副作用的步骤。

首次 `dotnet publish` 同时传 `GenerateAppxPackageOnBuild=true`、`AppxBundle=Never`、`UapAppxPackageBuildMode=SideloadOnly`。退出 1：全局打包属性传播到三个 kernel WinUI resolver 类库（WindowsAppSDK 1.6.250108002），其 MakeAppx 缺少 AppxManifest.xml，报 `0x80080203`。这属于调用路径/类库打包失败，不是性能分支新增 C# 错误。原始 `publish.binlog` 已经 MSBuild replay 为本地诊断日志，证实全局属性传播；没有改项目或伪造清单绕过。

首次另有三个 `mspdbcmf.exe` 找不到的符号包警告，以及 kernel `pgc/gateway/vega/v1.proto(10,1)` 未使用 `google/protobuf/empty.proto` 的警告；均归属于既有依赖/工具调用环境，不是 UI 行为结论。

原始大日志与二进制只留在 ignored 的 `artifacts/release-aot/`，不提交 binlog 或签名材料。

## 调用修正与复现

第二次独立 publish 省略了 `SolutionDir`，Models 的 `AdditionalFiles Include="$(SolutionDir)Desktop\BiliCopilot.UI\Resources\zh-Hans-CN\Resources.resw"` 未提供实际资源输入，UI 报三个 `CS0246 StringNames` 和下游 XAML `MSB3073`。这是本次验证调用错误。第三次补上 `SolutionDir` 仍失败，binlog replay 证明 Models 的 `CoreCompile` 因增量时间戳被跳过，复用先前不完整的 DLL。仅对本 checkout 的 Models 执行一次 Rebuild（`BuildProjectReferences=false`），退出 0、零警告/错误，随后确认 DLL 含 StringNames。没有清理共享缓存或修改资源生成器。

以下 PowerShell 从本 checkout 根目录执行；输出均在本 worktree。先准备上述运行库，保留项目的 AOT/trim/优化/警告政策：

```powershell
$project = 'src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj'
New-Item -ItemType Directory artifacts/release-aot -Force | Out-Null

# 原始第一次：错误地把构建时打包属性传播给引用类库（退出 1）。
dotnet publish $project -c Release -r win-x64 -p:Platform=x64 `
  -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=true `
  -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly `
  "-p:PublishDir=$PWD/artifacts/release-aot/publish/" `
  "-p:AppxPackageDir=$PWD/artifacts/release-aot/packages/" `
  -bl:artifacts/release-aot/publish.binlog *> artifacts/release-aot/publish.log

# 第二次：仍漏传 SolutionDir（退出 1）。
dotnet publish $project -c Release -r win-x64 -p:Platform=x64 `
  -p:AppxPackageSigningEnabled=false `
  "-p:PublishDir=$PWD/artifacts/release-aot/publish/" `
  -bl:artifacts/release-aot/aot.binlog *> artifacts/release-aot/aot.log

# 第三次：参数正确，但 Models 命中前一次的过期增量输出（退出 1）。
dotnet publish $project -c Release -r win-x64 -p:Platform=x64 `
  "-p:SolutionDir=$PWD/src/" -p:AppxPackageSigningEnabled=false `
  "-p:PublishDir=$PWD/artifacts/release-aot/publish/" `
  -bl:artifacts/release-aot/aot-corrected.binlog *> artifacts/release-aot/aot-corrected.log

# 仅纠正本次不完整调用留下的 Models 输出；新 checkout 从一开始传正确参数时无需此步骤。
dotnet build src/Desktop/BiliCopilot.UI.Models/BiliCopilot.UI.Models.csproj `
  -t:Rebuild -c Release -p:Platform=x64 "-p:SolutionDir=$PWD/src/" `
  -p:BuildProjectReferences=false --no-restore `
  -bl:artifacts/release-aot/models-rebuild.binlog *> artifacts/release-aot/models-rebuild.log

# SDK 发布后打包候选路径；本机在 AOT 工具链阶段失败，尚未完成打包。
dotnet publish $project -c Release -r win-x64 -p:Platform=x64 `
  "-p:SolutionDir=$PWD/src/" -p:AppxPackageSigningEnabled=false `
  -p:PublishAppxPackage=true -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly `
  "-p:PublishDir=$PWD/artifacts/release-aot/publish/" `
  "-p:AppxPackageDir=$PWD/artifacts/release-aot/packages/" `
  -bl:artifacts/release-aot/final-publish.binlog *> artifacts/release-aot/final-publish.log
$LASTEXITCODE

dotnet msbuild $project -t:ValidateRuntimeDependencies -p:Configuration=Release `
  -p:Platform=x64 "-p:SolutionDir=$PWD/src/"
```

运行库门禁单独执行退出 0。命令退出码另存同名 `.exit.txt`。失败日志摘要见 `evidence-release-aot/*-diagnostics.txt`。SDK MSIX targets 定义 `PublishAppxPackage=true` 时 `GenerateMsixAfterTarget=Publish`，与构建时打包不同；没有覆写任何 gate target。

## 最终阻塞、警告归属与产物

最终 `final-publish` 退出 **1**。UI DLL 已编译完成，然后 `Microsoft.NETCore.Native.Windows.targets(138,5)` 报 **Platform linker not found**。本机事实见 [result.json](evidence-release-aot/result.json)：

- `vswhere -all -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 返回空列表。
- `VC/Tools/MSVC/14.51.36231/bin/Hostx64/x64/link.exe` 存在，但标准桌面 `lib/x64` 不存在；只有 `lib/onecore/x64` 等部分库，未拿它们替代桌面工具链。
- `VC/Auxiliary/Build/vcvars64.bat` 存在，但它调用的 `vcvarsall.bat` 不存在。直接调用失败，原文见 [native-tools.txt](evidence-release-aot/native-tools.txt)。
- ILCompiler 的 `findvcvarsall.bat` 通过上述 vswhere 组件查询定位并调用 vcvarsall；并非仅凭 PATH 上存在 link.exe 即足够。没有设置 `IlcUseEnvironmentalTools`、伪造 LIB 路径或跳过门禁。

父级需要在另一台已完整配置的机器上复现，或另行授权修复 Desktop development with C++ 所需组件。本任务禁止安装，因此在这里停止。未发现需要在本验证分支扩修的性能生产代码错误。

警告/错误分层：

1. 首次 kernel protobuf 未使用 import、三个 mspdbcmf 符号工具警告：既有 kernel / 打包环境；没有改 kernel。
2. 首次 MakeAppx 及后续 StringNames/XAML 错误：本次调用参数与留下的增量输出问题；保留历史日志，不算最终生产缺陷。
3. 最终 Release C#/XAML 编译未输出警告或 CsWinRT 诊断，但沿用了仓库的既有 NoWarn，且 CsWinRT 警告等级仅 1。未添加 blanket NoWarn；不能宣称 level 2 或未抑制审计通过。
4. 最终阻塞发生在 ILCompiler 实际编译/链接之前，**没有完整 trim/Native AOT 警告结果**；没有用 JIT/普通 Release build 充数。后续必须重新收集 IL/trim/CsWinRT 警告并归属，不能以本次零可见编译警告作为免审依据。

实际留下的是 **x64 托管中间 DLL**：`src/_build/x64/Release/BiliCopilot.UI/bin/BiliCopilot.UI.dll`，3,457,024 bytes，PE machine `0x8664`，PE32+，CLR header RVA `8192`（非零）。SHA-256：`639D4067E8229E39E986CB6F2D26EB06CAB881B5C82599CC64B5C3A21FFC1CEA`。见 [intermediate-pe.json](evidence-release-aot/intermediate-pe.json)。它不是可交付的 Native AOT 产物。

计划的 `artifacts/release-aot/publish/` 和 `artifacts/release-aot/packages/` **没有本次成功发布的 EXE/MSIX**；不存在可报告的最终 native/package 哈希。完整构建命令日志与 binlog 保留在 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-64b6ed4f/artifacts/release-aot/`。小型 [最终日志](evidence-release-aot/final-publish.txt)、失败摘要、工具探测及 [日志哈希](evidence-release-aot/log-hashes.json) 提交入库；大型 replay/binlog 不提交。

## 后续独立运行的条件与限制

当前没有可注册产物。完整工具链就绪后，从正确 SolutionDir 的干净输出执行上述最终命令；必须看到 ILC/native link 完成、退出 0、打包 gate 通过。再检查发布 EXE 的 PE x64 架构、CLR directory、ILC/link 日志及 MSIX 内 EXE 的同一性，记录 EXE/MSIX SHA-256。只有无 CLR header 也不足以证明 Native AOT（普通 apphost 也可能如此），须结合 native 编译链与包内文件验证。

后续运行须等 #52 释放桌面并获得运行授权，由验收方拥有独立 identity 与输出目录。在独立的产物副本中准备与 Dev/生产包不同的 Name、身份相关声明和匹配的受控签名；完成包依赖与 Windows App Runtime 检查后才注册、按包身份激活。不要直接注册本报告正式 `Mang.BiliCopilot` 身份的候选包，不复用现有 Dev/生产包数据，不直接运行 EXE 代替打包激活。本次未生成、签名、信任或导入证书，也未修改任何已注册包。

真实验收仍需覆盖启动、资源/绑定/反射序列化、登录、播放/下载，以及 #45 各票 UI/性能场景。publish 成功、文件存在和哈希一致均不能替代这些验收。

## 最终集成检查

完成前在本 checkout 执行 `git merge --no-edit feat/performance-45`，返回 `Already up to date`；本地集成仍为 `a236aaa2266156804651223743444a0418af4822`。本次没有生产变更，kernel 保持固定且干净，只提交本文和小型证据。若之后集成生产代码、项目/包版本、资源或 kernel 发生变化，需在完整工具链上对新 head 重做 publish/打包/哈希验证；仅新增本文不触发生产重验。工具链阻塞消除后仍必须完成本次尚未完成的 AOT 验证。
