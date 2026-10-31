# Native AOT 工具链与发布验证

2026-10-31。**阻断：提权调用被系统报告为用户取消，安装器未启动；未执行安装后 Native AOT publish，没有新 EXE/MSIX。** 不是安装成功或 3010，也不是新发现的源码编译失败。#49 FAIL / perf HOLD 不变。

## 基线与工作区

- 主源码：`0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`。
- 工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-42c80974`，分支 `t3/native-aot-toolchain-verification`。
- gitlink：`0ae5e4e000a41a07f6696746706df11c06e8835b`。
- 初始 kernel 只有 `.git`、零索引条目、零工作树文件；HEAD 为 `f02c33f639addd5d421700c43fa66382beafe323`，旧 HEAD 文件显示 staged deletion。没有实际用户源码文件或 unstaged 修改。
- 先保存根/子模块状态和原 HEAD 到 ignored `artifacts/aot-toolchain-20261031/`；原 index 文件不存在，无可复制的 index。随后仅在本工作区从父 `t3-1d19cc80/externals/bili-kernel` 本地 fetch 精确 gitlink 对象，再 detached checkout；子模块现干净且与 gitlink 一致。未改父 checkout 或其他 worktree。
- 正式主/kernel 源码、NoWarn、项目依赖、身份、证书、系统默认 SDK 均未更改。未运行 WinApp、应用、注册/部署包、UI 自动化、读取认证、终止用户进程或重启；未触碰 caa6a7be layout-v3 / Dev / PID 9608。
- 已读 AGENTS、docs/agents、WinUI setup/packaging/dev-workflow、verification 技能；msbuild 指南预读，无本轮 MSBuild 失败或修复。

## 授权、检测与组件选择

授权来自 [user-decisions.md](../engine-61-design-draft/user-decisions.md)：允许现有安装器补缺 MSVC x64/ARM64 与 SDK、UAC/大下载，禁止自动重启/重装 IDE。没有重复请求常规许可。

[安装前探测](evidence-aot-toolchain/detection-before.json)：

- VS Community 2026 实例 `bf64169a`，`C:/Program Files/Microsoft Visual Studio/18/Community`，版本 `18.11.12324.101`。
- Installer `C:/Program Files (x86)/Microsoft Visual Studio/Installer/setup.exe`，版本 `4.11.24.2075`。
- 部分 MSVC `14.51.36231` cl/link/lib 已有，但 vcvarsall.bat 和桌面 lib/x64 缺失。部分可执行文件存在不能证明工具链可用。
- 已选 `Microsoft.VisualStudio.Component.Windows11SDK.28000` 版本 `18.11.12301.404`。磁盘 SDK `10.0.28000.0` 有 UM/UCRT x64、ARM64 库与 include；项目中央 BuildTools 为 `10.0.28000.2957`。TFM API 基线 22621 不表示必须额外安装旧版 native SDK；本次不重复安装 SDK。
- Developer Mode 已启用。发现 WinApp 命令，但遵守本轮禁止 WinApp 的约束，没有执行或升级它。
- RC3 SDK：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe`。绝对路径执行确认 `11.0.100-rc.3.26522.148`，见 [dotnet-info.txt](evidence-aot-toolchain/dotnet-info.txt)。仅在子进程设置 DOTNET_ROOT/PATH，未安装或重置 dotnet。

微软官方 [VS 2026 组件清单源文件](https://raw.githubusercontent.com/MicrosoftDocs/visualstudio-docs/main/docs/install/includes/vs-2026/workload-component-id-vs-community.md) 与本机 18.11 catalog 交叉确认以下 ID；网页呈现曾混入 VS17 文案，因此采用明确的 includes/vs-2026 官方源文件。

| 计划显式添加 | 本机 catalog 版本 | 理由 |
| --- | --- | --- |
| Microsoft.VisualStudio.Component.VC.Tools.x86.x64 | 18.11.12301.404 | 完整桌面编译器、链接器和 x64 库 |
| Microsoft.VisualStudio.Component.VC.Tools.ARM64 | 18.11.12301.404 | host x64 到 ARM64 的 native 工具和库 |

未选择工作负载、推荐组件全集、旧版工具或新 SDK。必要依赖由 Installer 解析；本轮未进入解析/安装，不能声称实际依赖闭包已安装。

## 实际安装尝试

[完整 helper](evidence-aot-toolchain/install-tools.ps1) 预备执行：

```text
setup.exe modify --installPath "C:\Program Files\Microsoft Visual Studio\18\Community"
  --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64
  --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --quiet --norestart
```

以 Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden 启动 helper，仅为已授权操作提权。helper 自身用 Start-Process setup.exe -Wait -PassThru 记录 ExitCode；没有传 setup.exe 不支持的 --wait。[微软命令行文档](https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2026) 要求 --norestart 与 quiet/passive 配合；未使用 --force。

实际观察到 consent.exe，helper 未写入开始记录。随后外层 Start-Process 返回“操作已被用户取消”，shell exit **1**。见 [错误记录](evidence-aot-toolchain/elevation-result.txt)。这不是 installer exit 1：setup.exe 未由本次尝试启动，**installer exit 不存在**，无本轮 setup 安装日志。不会虚构安装完成或重启要求。

[前组件清单](evidence-aot-toolchain/components-before.json) 与 [后组件清单](evidence-aot-toolchain/components-after.json) 完全一致；[后实例状态](evidence-aot-toolchain/vs-after.json) 仍为原版本、isRebootRequired=false。实际安装变化为零，未重试。后续需用户完成 UAC 点击；若提示关闭 IDE，由用户处理。若以后退出 3010，必须记录“需重启、尚不能称就绪”，不得自动重启。

## 发布准备与警告审计边界

按 [release-aot.md](release-aot.md) 纠正调用，准备了 [x64 发布脚本](evidence-aot-toolchain/publish-x64.ps1)：RC3 绝对路径、正确 SolutionDir、Release、win-x64、Platform=x64、无签名、PublishAppxPackage=true，独立输出到 ignored `artifacts/aot-toolchain-20261031/publish-x64/` 与 `packages-x64/`，保留 binlog、日志、参数和退出码。不用会传播到 kernel 类库的 GenerateAppxPackageOnBuild=true，不漏传 SolutionDir。

该脚本**本轮未执行**。当前全新工作区未复用旧 Models 输出或 managed DLL。安装前置条件未完成，不重复制造已知缺 linker 的发布失败；新源码的 AOT 编译结果仍未知。

已从父现存 Assets 只读复制缺失 x64 libmpv/ffmpeg 到本工作区 ignored 路径，未改源文件；[来源、大小、SHA256](evidence-aot-toolchain/runtime-assets.json) 已冻结。这是输入核验，不是加载或包闭包通过。BBDown 是仓库现有文件。

[还原前求值属性](evidence-aot-toolchain/properties-before-restore.json)：PublishAot=True、SelfContained=true、TreatWarningsAsErrors=true、CsWinRTAotWarningLevel=1、optimizer=true。尚未 restore 的 WindowsPackageType/SuppressTrimAnalysisWarnings 空值不能当最终解析值。原 NoWarn 完整保留，含 IL2104/IL3053 汇总、多项 IL2026/IL3050 等 AOT/trim 诊断和 MVVMTK0045/0046。**本轮无实际编译警告计数；不能写“0 warnings”或“trim/AOT 审计通过”。**

安装就绪后先跑原政策的 fresh publish，另在独立输出中做不隐藏 AOT/trim/CsWinRT 项的诊断审计；保留其他诊断政策，不新增 NoWarn、不降低警告级别洗绿。若现有源码诊断失败，列出文件/诊断/最小修复，不扩展成全仓 MVVM 改造。本轮只完成抑制策略静态审计，动态未抑制审计待执行。

## 接续验证与交接

1. 用户完成 UAC 交互后重新执行精确 modify；保存 command/version、installer exit、对应本次的 dd_* 日志，重新比较组件。勿重复启动 installer。
2. 验证 vcvarsall、hostx64 cl/link/lib、桌面 x64/ARM64 库和 SDK；分别在进程局部环境编译/链接最小 native 文件。ARM64 仅能报告交叉编译，不能在 x64 主机声称运行通过。
3. 执行准备的 x64 fresh publish。失败时读 binlog 定位首个根因；必要小配置改动先形成可评审修复，保持本任务正式主/kernel 源码边界。
4. 成功后验证 ILC/native link 实际发生、EXE machine/CLR directory、native import、MSIX 内容/清单依赖及包内 EXE 同一性；冻结输入源、包闭包、EXE/MSIX 哈希。无 CLR header 单项不足以证明 AOT（普通 apphost 也可能没有）。
5. 本轮无可运行产物、native/package hash 或应用运行入口。成功发布后由父协调桌面和独立包身份实机验收；不能注册正式身份覆盖现有包，不操作 Dev/layout-v3。ARM64 app 发布仅在工具及项目闭包支持时做，不以 JIT/托管 DLL 替代。

原始 VS 状态、备份、大日志仅留 ignored artifacts；入库仅本文和精简安全证据。#49 FAIL / perf HOLD 与真实运行验收均未关闭。
