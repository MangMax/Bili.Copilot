# NativeAOT x64 开发测试版安装

2026-10-31，独立工作区 `t3-0f9fd712`。用户明确授权先打包、安装并试用当前 AOT 开发版，不等待完整 AOT 审核清零。本次真实完成当前源码 publish、现有证书签名、独立身份安装与首次登录页显示；没有使用 JIT 替代 AOT。

## 安装结果与入口

- 开始菜单：**哔哩助理 AOT 开发测试版**。
- Identity：`Mang.BiliCopilot.AotDev_2.2610.31.1_x64__z90r5zjzw296g`。
- AUMID：`Mang.BiliCopilot.AotDev_z90r5zjzw296g!App`。
- 安装目录：`C:/Program Files/WindowsApps/Mang.BiliCopilot.AotDev_2.2610.31.1_x64__z90r5zjzw296g`。
- 本工作区 MSIX：`artifacts/aot-dev-install/published/packages/BiliCopilot.UI_2.2610.31.1_x64_Test/BiliCopilot.UI_2.2610.31.1_x64.msix`。
- 真实 MSIX 解包 layout：`artifacts/aot-dev-install/layout`，只作保留产物，安装运行使用 WindowsApps 中的签名包。

`Add-AppxPackage` 成功，Status=0，IsDevelopmentMode=false；这是现有受信任证书签名的本机侧载安装，没有走解包开发注册。没有卸载此包。原 `Mang.BiliCopilot` 2.2610.3.0 和 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be` 2.2610.0.3 保持原版本。没有读取或复制登录存储、导出私钥、增加证书信任、改变安全策略、安装组件。

## 当前源码与发布

源 `de41a77eb46ca5d6e01cbbe78d9aa0cac11429bf`，kernel `0ae5e4e000a41a07f6696746706df11c06e8835b`。本 child 的 kernel 目录实际为空且没有 `.git`；初始备份中的 `git -C` 输出解析到了根仓库，不能把它当作 kernel HEAD。`prepare.ps1` 从冻结 bundle 在 ignored artifact 内导出精确 kernel，不初始化或写父 checkout。

`prepare.ps1` 导出当前根 HEAD；与根源码归一化比较，唯一差异为 artifact 的 `Package.appxmanifest`：独立 Name、Version、DisplayName、PhoneProductId 和 toast COM CLSID。应用 C#/XAML、项目文件、中央依赖、gitlink 和 NoWarn 未修改。窗口内部标题仍使用原 AppName“哔哩助理”，开始菜单/包显示名为开发测试版。

使用既有 RC3 SDK `11.0.100-rc.3.26522.148`、MSVC/SDK，通过 `evidence-aot-payload/publish-x64.ps1` 的完整 `dotnet publish`、Release/win-x64、`PublishAppxPackage=true` 入口构建。23:27:41 至 23:40:58（+08），publish exit **0**。实际执行 ILC/native link；ILC 保留 `-O`、`--warnaserror` 和既有 NoWarn。原有 protobuf warning、mspdbcmf 缺失导致不生成 symbols package 的 warning 保留；没有新增 NoWarn、全局 trim root 或修改正式依赖。

仅显式 `CustomAfterMicrosoftCommonTargets=local-candidate.targets` 采用可恢复 Mpv WinUI `0.0.2-preview2-aot-local.1`，nupkg SHA256 `916E00A1D49DDDDE35FFBB4E1EE321C64C0FC6422F6E0AC98A17250E67DC62ED`。Player/Core/Interop 仍为既有正式版本。Danmaku Core/Legacy/Share 均为 `0.0.1-preview6`，没有采用尚待组合验收的新引擎候选。

## 产物与 native 闭包

`sign-and-inspect.ps1` 先核对 MSIX Publisher 为 CN=Mang，然后使用 CurrentUser/My 既有匹配证书 thumbprint 正常签名；未读取或输出私钥。signtool `/pa /v` 成功，0 warning/0 error。脚本最初因为目录附带框架 MSIX 而拒绝多个包，修正为精确匹配应用包后执行；该拒绝发生在签名前。

- 签名 MSIX：167944324 bytes，SHA256 `4FD060010B1EF5960A1E060EBB77606BF4B2A56A8AB5AD508807BAB1CC13B552`。
- 主 EXE：111518208 bytes，SHA256 `148CDAF3999A20C0079656ECF50A044BA608078A636C0A6214648565791ADFCF`。
- publish、包内和实际安装 EXE hash 一致，PE machine 8664、CLR directory=0。
- 包内13个 PE 全部 CLR directory=0；无 managed PE、coreclr/clrjit/hostfxr/hostpolicy 或应用 deps/runtimeconfig。
- 194 个 publish 运行文件逐路径/hash 一致；仅 SDK 排除的 PDB 与经 schema 校验的程序集 XML 文档不要求随包分发。签名包286条目，resources.pri 存在。
- Framework dependency 为 Microsoft.WindowsAppRuntime.2 >=2.6.4.0，本机已有匹配运行时。签名与闭包结果见 [证据](evidence-aot-dev/artifact-results.json)。

## UI 与保留状态

组合任务明确交接桌面后才启动。首次 AUMID 激活得到 PID2928，路径/hash 与安装包吻合。UIA 确认登录页、二维码 Image、启用的刷新按钮、网页登录入口及版本 2.2610.31.1；开始菜单独立名称查询成功。未扫描二维码、输入登录信息或读取登录文件。

随后尝试刷新时旧 PID 已不存在，动作失败 `No process found with PID 2928`，没有执行刷新或关闭。只读核对发现同一安装包的新 PID50412（23:42:02 启动），窗口仍存在；查询近期 Application 1000/1001/1026 未发现匹配事件，但这不单独证明退出原因。立即暂停 UI 输入并向父询问是否用户正在试用，避免关闭用户窗口。**受控正常退出/重启检查尚未完成，不能把首次登录页显示扩大为完整稳定性或登录后功能通过。**

两个进程的实际模块清单已保留。新进程加载安装包 EXE、libmpv-2.dll、Microsoft.UI.Xaml.dll、Microsoft.WindowsAppRuntime.dll 和 System32/vulkan-1.dll，未加载 coreclr/clrjit/hostfxr/hostpolicy。这只证明本机实际加载，**不代表干净机器 Vulkan 分发已经解决**。

当前保留安装包、artifact layout、构建源码/export、发布日志/binlog/native PDB、本地候选 feed。WinApp UI workflow `aot-dev-0f9fd712` 已 yield。运行窗口保留，未强制终止。后续 UI 所有权与退出检查以父协调/用户试用状态为准。

## 限制与复放

这是实验包：完整严格 AOT/trim/动态绑定门禁未清零，旧审计红项不被这次基础启动覆盖；未验证全部导航、AI、播放、弹幕或性能，#49/perf 状态不因本次安装改变。WinUIAnalyzer 未新增或运行；未改应用行为，因此未重复单元测试。本轮自行按 WinUI review/packaging/UI-testing/verification 技能检查身份、源码差异、native/资源闭包及实际窗口，没有启动子代理。

复放：先运行 [prepare.ps1](evidence-aot-dev/prepare.ps1) 建立全新 artifact 源副本，再运行已有 `evidence-aot-payload/publish-x64.ps1 -LocalCandidate` 指定该 SourceRoot 和新 Output。`sign-and-inspect.ps1` 只使用已存在且被授权的证书；`install.ps1` 验证签名及独立身份，并拒绝覆盖已经安装的 AotDev。`launch.ps1` 仅激活已安装的独立 AUMID，按实际安装路径过滤进程。没有自动卸载或清理脚本；用户试用期间保留所有注册和产物。
