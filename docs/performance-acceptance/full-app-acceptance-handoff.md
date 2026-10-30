# 完整生产应用独立验收交接

2026-10-30。**owned app retained for interactive login; not yet cleaned/accepted**。

完整 Debug x64 应用构建、独立注册、启动页检查成功；当前停在登录页，首页、播放器及 #48 交互尚未验收。不是替身宿主，不是 Native AOT 验收。父级检查交接后再统一协调用户在应用内登录，本子任务没有发出登录请求或执行登录。

## 源码与边界

- 工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-caa6a7be`；分支 `t3/full-app-acceptance-handoff`。
- 实际构建源码：`c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0`（指定 feat/performance-45 基线）。交接提交仅添加本文及安全小日志。
- kernel：`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，起始和构建后均干净，未改指针/源码。
- 已读 AGENTS.md、docs/agents、issue-48.md 实际独立部署命令、release-aot.md、status.md，以及 winui-dev-workflow、winui-packaging、winui-ui-testing、winui-code-review、verification-before-completion。
- 没有生产逻辑、依赖、工具安装、账户数据迁移、PR/issue/push 操作；未操作 PID 9608 或 Dev/生产包。按授权使用 local 独占桌面槽，无子代理。
- 未重跑基线 403 测试；未尝试 AOT。现有 AOT 缺 MSVC 的阻塞保持。专用 WinUIAnalyzer 未引用、未运行，未增加依赖。

## 构建与部署

先查询新身份 `Get-AppxPackage -Name 'Mang.BiliCopilot.PerfAcceptance.Caa6a7be'`，结果为空。仅临时替换 `Package.Dev.appxmanifest` 的 Identity Name 和 AppName 显示资源引用，显示名为“哔哩助理 性能验收 Caa6a7be”。未修改版本或 Publisher。finally 恢复原始 bytes；前后 SHA-256 均为 `5EDADABCC576061C73B8FDDAB3FBDF8CD61C6B8E89253301405AEC5835CBDEA3`，`git diff --exit-code` 返回 0。

只从授权主 checkout 的 Assets 复制本工作区缺失且 `git check-ignore` 确认为 ignored 的两个文件：x64 libmpv、ffmpeg；BBDown 已存在。哈希见 [runtime-assets.json](evidence-full-app-handoff/runtime-assets.json)。未读取/复制账户数据。

实际调用（临时身份生效期间）：

```powershell
$root = 'C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-caa6a7be'
winapp run "$root/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj" `
  -c Debug --arch x64 --detach --json `
  --output-appx-directory "$root/artifacts/full-app-acceptance/layout" `
  -p Platform=x64 -p "SolutionDir=$root/src/" `
  -p "BuildOutDir=$root/artifacts/full-app-acceptance/build/"
```

.NET SDK 10.0.403，WinApp CLI 0.7.1。命令退出 0；实际 dotnet build 成功，1 个既有 protobuf unused import 警告，0 错误，用时 00:02:27.11。输出是 Debug JIT 完整生产应用；未发布 MSIX/AOT。kernel 引用项目使用本工作区子模块自己的 bin/obj，主应用使用上述独立 BuildOutDir。

原始构建/启动日志：`artifacts/full-app-acceptance/build-launch.log`；脚本 `prepare.ps1`、输出和 layout 都位于同一 ignored 目录。安全摘要与日志哈希见 [证据目录](evidence-full-app-handoff/startup-summary.txt)。没有保存应用认证日志、二维码截图或原始 UI 树。

实际 layout `BiliCopilot.UI.dll`：3,964,928 bytes，SHA-256 `8CFB9B382CFED51F9A7CCFE28A40627807FC40D4B849910D59AD8D1D7A80DD2B`，与独立 build 输出 DLL 相同。EXE、Models、kernel Media/Core 和实际 manifest 哈希见 [artifact-hashes.json](evidence-full-app-handoff/artifact-hashes.json)。EXE 是 JIT apphost，不能据此称 Native AOT。

## 保留的 owned 应用

- Identity：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be`
- AUMID：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App`
- Package full name：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_2.2610.0.1_x64__y1mf40g4r9srg`
- InstallLocation：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-caa6a7be/artifacts/full-app-acceptance/layout`
- PID：`20320`；EXE：上述 InstallLocation 下 `BiliCopilot.UI.exe`；进程启动时间 2026-10-30 12:33:20 +08:00。
- HWND：`659978`；窗口 720×460，标题“哔哩助理”，WinUIDesktopWin32WindowClass。窗口标题由生产资源设定，仍是原名称，勿仅凭标题绑定；包的显示名是“性能验收”。
- 首次窗口枚举为空；12:33:57 已出现窗口，12:34:05 只读 UIA 检查退出 0，安全布尔摘要确认登录/网页入口/QR 标记存在、游客标记不存在。没有把首次未就绪当成通过。

源码 `AppViewModel.LaunchAsync` 在没有 token 时进入 StartupWindow；StartupPage 提供二维码和网页登录，未发现无认证首页入口。生产页会自动初始化认证挑战，但本代理没有点击登录/刷新、扫码、输入账号密码或调用收费服务。UIA 原始结果仅在内存中过滤成安全布尔状态，未提交或保存认证挑战树/图片。此证据只确认启动页，不证明截图视觉、首页、播放、弹幕或实际认证成功。

只读检查后使用 workflow `bili-full-app-caa6a7be` 调用 `winapp ui yield --on local --json`，退出 0，实际返回 `released:false`，见 [final-yield.json](evidence-full-app-handoff/final-yield.json)。本轮未做桌面输入，yield 时没有该 workflow 待释放的 idle turn；不能把该返回改写为 `released:true`。不再主动输入，桌面独占槽交回父级。窗口和注册包按明确要求保留，未关闭/卸载，layout 必须保留。

## 父级重新绑定与后续步骤

1. 先核对上面独立包的注册路径，再核对 PID 20320 的 EXE 路径。读取 `winapp ui list-windows --on local -a 20320 --json`，确认 HWND 659978 仍属该进程；不要借用同名其他窗口。
2. 父级检查交接后统一请用户在这个独立应用内完成登录；本子任务不索取凭据，不采集登录挑战。认证页面的截图/树不作为可提交证据。
3. `StartupPageViewModel.ExitAsync(true)` 会调用 Restart，因此成功登录后旧 PID/HWND 可能失效。按上述 InstallLocation 下 EXE 路径重新筛选进程，再执行 scoped list-windows 发现新 HWND；不要复用旧句柄或重新构建标准 Dev 包。身份不变，注册 layout 不可移动/清理。
4. 获得后续交互授权后再执行完整首页/播放器与 #48 视频/番剧换 P、重载、连续切换、加载中关闭。登录页不计任何播放器验收通过；AOT、真实性能与其他 status.md 缺口保持。
5. 后续验收结束由父级协调清理该独立 full package name 和仅确认属于它的进程；本轮明确 retained，尚未 cleaned/accepted。

本轮代码审查范围为临时清单及启动/登录入口核对：没有生产代码 diff，没有新增绑定、线程或依赖变更；不声明整应用 WinUI 代码审查通过。最终清洁性核验应包括主仓库和 kernel，提交范围仅本报告和 evidence-full-app-handoff。
