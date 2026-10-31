# Native AOT 工具链重试与 RC3 发布

2026-10-31，独立工作区 `t3-041ea9a9`。**安装成功，x64/ARM64 native 编译链接通过；RC3 x64 已生成 Native AOT EXE，但 MSIX 打包失败，完整 publish 退出 1。** 旧 [UAC 取消记录](aot-toolchain-and-publish.md) 原样保留，代表第一次尝试；本次仅接续父已启动的 helper，没有再次启动安装器或 UAC。

## 安装与 native 工具验证

父 helper PID 7996 于 08:39:22 +08 启动，安装开始/结束及退出码见 [证据目录](evidence-aot-retry/)。本次 `installer-exit.txt=0`，结束时间 `08:41:14.9477025+08:00`；对应 `dd_installer_elevated_20261031083929.log` 于 08:40:55 记录 Exit Code 0。VS 实例 complete=true、rebootRequired=false；不是 3010，没有重启。

命令仅显式添加 VC.Tools.x86.x64 与 VC.Tools.ARM64，quiet/norestart。实际组件差异另含依赖 ARM64EC，见 [差异](evidence-aot-retry/component-delta.json)。既有 SDK 28000 保留，无新增 SDK 请求。未重装 IDE。

[复放脚本](evidence-aot-retry/verify-native.ps1) 检查 vcvarsall、MSVC 14.51.36231 Hostx64 的 x64/ARM64 cl/link/lib、桌面 CRT/C++ 库及 SDK 10.0.28000.0 UM/UCRT 库，然后分别编译并链接 stdio 最小 C 程序。两者退出 0，二进制哈希见 [结果](evidence-aot-retry/native-results.json)。未运行任何生成的程序；ARM64 只验证交叉编译/链接，不声称运行通过。

## 输入冻结与隔离

源码基线 `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`，并 fast-forward 原交付 `0508a73b3029f377acef4c4eace55d6bfebf0b59`。kernel 初始只有 .git、索引零条目；根/子模块初始状态、HEAD、staged/unstaged patch 已保存到 ignored `artifacts/aot-retry-verification/`。随后从 `t3-42c80974` 本地 fetch 并 detached checkout 精确 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`；没有父工作区写入。

仅把父已验证的 libmpv/ffmpeg x64 复制到本工作区 ignored Assets，同原交付哈希。使用 RC3 绝对路径 `t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe`，SDK `11.0.100-rc.3.26522.148`。无源码、NoWarn、依赖版本、包身份、证书变更。

## 默认发布失败与最小调用调整

[原调用复放](evidence-aot-retry/publish-x64.ps1) 保留原参数：Release/win-x64、正确 SolutionDir、PublishAppxPackage=true、不签名。退出 **1**，WinUI 2.6.4 `MarkupCompilePass1` 报 WMC9999（type 为 null），尚未 ILC/link。实际 XAML input.json 的 ReferenceAssemblies/ReferenceAssemblyPaths 都为空；[调用栈](evidence-aot-retry/xaml-failure-stack.txt) 指向 markup extension 类型解析。

binlog replay 确认执行 `_PublishOptimizedBuildAlternative`，没有先执行 ResolveReferences。RC3 [SDK 源摘录](evidence-aot-retry/rc3-publish-targets-excerpt.txt) 显示 AOT 默认 UseOptimizedPublish=true，其顺序是 PrepareForBuild → PrepareResources → Compile；WinUI 在 PrepareResources 中先执行 MarkupCompilePass1，引用尚未解析。这是发布目标顺序的兼容问题，不是证明某个应用 XAML 类型写错。

已先报告原因与最小建议，再使用 [完整 Build 调用](evidence-aot-retry/publish-x64-full-build.ps1) 单独加 `UseOptimizedPublish=false`（SDK 明文支持），输出位于 `artifacts/aot-retry-verification/full-build/`。该参数恢复发布前完整 Build，不关闭 PublishAot、C# Optimize、ILC 优化、analyzer 或 warnings-as-errors；不修改生产项目。

## 最终发布结果与剩余阻塞

完整 Build 调用实际为 **exit 1**，见 [完整日志](evidence-aot-retry/full-build-publish.txt)。C#/XAML 编译完成；第二次 XAML 输入含 334 个引用。binlog 证明 ILC 于 08:46:24–08:50:00 执行、LinkNative 于 08:50:00–08:50:06 执行完成，见 [目标证据](evidence-aot-retry/native-and-package-targets.txt)。发布 EXE 为 PE x64（8664）、CLR directory RVA=0，结合 ILC/link 执行证明是真实 native 产物，非普通 apphost。

- 文件：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-041ea9a9/artifacts/aot-retry-verification/full-build/publish-x64/BiliCopilot.UI.exe`
- 大小：111497216 bytes。
- SHA256：`3064A440B29BAF7709A897B61959AA9D8FD5EF1202BE4F9063DE2F87FF343754`，见 [PE 结果](evidence-aot-retry/native-exe.json)。原生 imports 完整 dump 留 ignored artifacts。
- **没有 MSIX/appx**；[检查脚本](evidence-aot-retry/inspect-artifacts.ps1) 在完成 PE 检查后明确失败 `No MSIX/appx package found`。包内 EXE 同一性、包依赖闭包均未通过，不能把发布目录当成可部署包。

两项阻塞必须保留：

1. ILC 提示 Richasy.MpvKernel.WinUI 的 `XamlTypeInfoProvider.set/get_27_SymbolIcon_UseSegoeMetrics` 将始终抛异常，缺失 `FluentIcons.WinUI.SymbolIcon.set/get_UseSegoeMetrics`。实际解析 `Richasy.MpvKernel.WinUI/0.0.2-preview2`、`FluentIcons.WinUI/2.2.344`、`FluentIcons.Common/2.1.344`。这是第三方预编译 provider 与当前 API 的二进制不兼容证据，不能靠增加保留规则创造缺失成员。最小建议：对齐该 provider 的依赖并重新构建/发布兼容组件，先单独验证，再重新 AOT；本轮未修改依赖或第三方源码。
2. MakeAppx 0x80080204：清单声明的 `BiliCopilot.UI.exe` 不在包内。实际 `IncludePublishItemsOutputGroup=false`；`_ComputeAppxPackagePayload` 于 08:46:21 先运行，native link/PublishItemsOutputGroup 在 08:50 后才完成。最终 [package map 摘录](evidence-aot-retry/package-map-excerpt.txt) 含 managed DLL/runtimeconfig，不含 native EXE。最小建议是独立验证打包阶段使用 publish output group 并在 native 产物完成后计算 payload；不能只把 EXE 塞进当前含 managed runtime 的 map 就宣称闭包正确。本轮不扩大项目打包改造。

还保留 protobuf unused import、找不到 mspdbcmf 导致不能生成符号包的警告。没有新增 NoWarn；既有 IL/AOT/trim 抑制完整保留，ILC rsp 包含 `-O` 和 `--warnaserror`。本轮不是未抑制诊断审计，不能宣称 trim/AOT 警告清零。后续需要独立未抑制审计，以及修复后重新生成和验证 MSIX。ARM64 应用 publish 未执行；只完成 native 工具交叉编译验证。

## 证据与集成

大日志、binlog、PE/PDB、native probe、备份都在 ignored `artifacts/aot-retry-verification/`；[哈希清单](evidence-aot-retry/artifact-hashes.json) 冻结大证据。仅提交报告、复放脚本与小证据。复放发布脚本已补建输出目录并传播 dotnet 退出码，避免 wrapper exit 0 掩盖 publish exit 1；SDK 参数未因此改变。

收尾执行 `git merge --no-edit feat/performance-45` 返回 Already up to date；当时最新本地父 tip 为 `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`。父 checkout 未变更，kernel 干净且与 gitlink 一致。没有 push/PR/issue 操作。

## 验收边界

未运行 .NET tests（本次是 native 编译/发布验证），未启动应用、注册、部署、登录或操作 UI；未碰 Dev/PID9608/caa6a7be/layout-v3，没有占用 B1 桌面。即使发布成功，独立身份实机验证仍由父协调。**#49 FAIL / perf HOLD 不变。**
