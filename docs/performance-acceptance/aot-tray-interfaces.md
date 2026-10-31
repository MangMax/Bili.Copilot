# 托盘两个 IDisposable 类型的严格接口生成

2026-10-31，工作区 `t3-c05d2140`，输入父提交 `bfa3d0476353f2c8abe28c1589a6d939b114ffba`。生产差异仅 `App.TrayIconSession` 与 `TrayLifecycle` 各一个 `partial`；包含类型 App 本来已是 partial，无需修改外层。没有改配置、依赖、gitlink、303 个属性字段、46 个命令或其他域。完整 AOT 仍 **HOLD**，#49/OX 独立审查。

## 消费、注册与 ABI

依据 [payload 独立审查](aot-payload-and-compatibility.md) 的“严格诊断：编译阻塞与可达性分开”和 [strict 台账](evidence-aot-review/level2-by-file.json)，本片处理严格生成要求，不把诊断解释为已经发生的运行故障。

- App.InitializeTrayLifecycle 唯一构造 lifecycle，保存于托管 internal 属性；MainWindow、PlayerViewModel 和设置入口通过 App 访问它。工厂 CreateTrayIcon 创建 session，以 ITrayIconSession 返回给 lifecycle 的 `_tray`。两者没有 DI/WinRT 服务注册，也没有写入 XAML DataContext、Tag、Content、ItemsSource 或作为 WinRT 方法返回值的路径。
- TrayResources.xaml 中传入 WinUI 的是 XamlUICommand、TaskbarIcon 和 MenuFlyout；ExecuteRequested 的目标是 **App** 的处理器，App 再调用 lifecycle.Restore 或 window.RequestExitAsync。AppInstance.Activated、通知回调和 DispatcherQueue 同样通过 App/闭包触发调用，没有将 lifecycle/session 本体注册为 WinRT 接口对象。
- session 在真实 H.NotifyIcon 2.3.2 的 `MessageWindow.TaskbarCreated` 注册 `OnTaskbarCreated`，其 delegate target 因而由消息窗口托管持有。依赖 PE 元数据验证该事件为 System.EventHandler 且 add/remove 有 managed IL。session 再用普通 EventHandler 的 sender=this 发出 HealthChanged；唯一生产消费者是 lifecycle.OnTrayHealthChanged。这是托管事件持有关系，不是 session 本体被当作 IClosable 传入 WinRT 的证据。
- `ITrayIconSession : IDisposable`、`TrayLifecycle : IDisposable` 均命中 CsWinRT 的 IDisposable → Windows.Foundation.IClosable 映射。持有 TaskbarIcon 或捕获 WinUI 命令不足以推定实例本身跨 ABI；partial 是为严格编译生成兼容接口表。本次未观察或复现真实托盘运行故障。

## 生命周期保持与静态审查

去掉新增的两个 partial、归一化 CRLF/LF 后，两份生产源码与父提交逐字一致。RestoreMainWindow 的 Show → 最小化 Restore → Activate → SetForegroundWindow 顺序不变；没有改变前台策略，也不声称纯逻辑测试验证 Windows 前台权限。

新鲜资源字典/命令/图标仍由每次工厂调用拥有；同步 Icon 创建、ForceCreate、IsCreated 检查、失败 Dispose 后原异常重抛不变。session.Dispose 仍先置 disposed，再退订 TaskbarCreated、清空 HealthChanged、退订两个 ExecuteRequested，最后释放 TaskbarIcon 与 image；没有改变其中异常的传播/中断顺序。lifecycle.ReleaseTray 仍先清空 `_tray`，退订 HealthChanged 后 Dispose。禁用、Shell 失败及旧 session 迟到回调的 fencing 不变。

MainWindow.OnClosed 仍在 await 前 `e.Handled=true`，托盘驻留取消原生关闭；退出路径仍共享 `_exitTask`，等待保存及播放器清理后释放托盘。PlayerViewModel 的 CloseAsync、取消、计时器/订阅清理未动。ExitAsync 中保存/清理失败的报告和最终 exit 异常传播保持原状。

按 WinUI review/dev 技能进行静态审查：本片没有 UI/XAML、绑定、可访问性、主题、线程或公开成员变化；没有新的 async void、反射业务调用或窗口所有权变更。没有新增专用 WinUIAnalyzer 包，该分析器未运行；未声称全仓符合其规则。

## 编译与证据

严格审计沿用 export-source.ps1 / audit-build.ps1：两个初始无 bin/obj 的独立源码 export，移除副本已有 IL/MVVMTK0045/0046 NoWarn，保留 warnings-as-errors 和分析器，CsWinRTAotWarningLevel=2。`-AllowUnsafeForAudit` 仅沿用历史审计前提以越过 kernel CsWinRT1030；正式 unsafe 配置未动，1030 未被宣称修复。green export 明确复制两份未提交目标源码，防止 HEAD 导出遗漏变更。

原红 **353 = 303 MVVMTK0045 + 46 MVVMTK0046 + 4 CsWinRT1028**，修后 **351 = 303 + 46 + 2**，两次均 exit 1。按文件、行列、编号和完整消息去重比较，仅删除 App.xaml.cs `(236,26)` 的 TrayIconSession 和 TrayLifecycle.cs `(8,23)` 的 TrayLifecycle 两条1028；零新增，其余逐项相同。剩余 CsWinRT 为 DanmakuLoadSession 和 CustomHttpClientHandler。没有 ILC/publish 重跑。

最终数值、逐源码散列和 TRX 逐结果检查由 [verify.py](evidence-tray-aot/verify.py) 复算；见 [verification.json](evidence-tray-aot/verification.json)、压缩 red/green 完整诊断与 [大产物哈希](evidence-tray-aot/large-artifacts.json)。两个 export 的 tracked src/kernel 文件逐一比较，只有目标两份源码允许变化，审计 csproj 两边相同。

应用 Debug/Release 新构建均 exit0，各仅一条既有 protobuf unused-import warning、零错误；最终 DLL/PDB/source 核验结果记录于 verification.json。Metadata 只读 PE，不加载应用：两个类型均具有 WinRTExposedType attribute，指向存在且含 GetExposedInterfaces IL 的详情类型（Debug 52字节、Release 47字节）；CodeView/portable PDB identity 匹配，两配置各2593份文档的磁盘源码 hash 全部匹配，缺失同样失败。生成的 attribute/vtable 摘录随证据提交，不把增量旧 DLL 当作新产物。完整证据复算 exit0，strict 两副本1543份 tracked src/kernel 文件比对通过。

## 隔离 probe 与行为测试

Models 直接链接完整真实 TrayLifecycle/ITrayIconSession；extract-session.py 原样提取 App 中的整个嵌套 session 声明，保留成员和真实 H.NotifyIcon/WinUI/System.Drawing 依赖，仅将外层 App 简化为不继承 Application 的 partial 壳，以免运行部署 initializer。**该 probe 不等同于完整 App 类型上下文**，完整应用类型由上述最终 PE/PDB 另验。未构造 App、session、TaskbarIcon 或 HWND；没有镜像实现业务逻辑。

原源码模型编译发出两条1028，probe 因两个 WinRTExposedType attribute 缺失失败；原 DLL/PDB/log 单独保存。修改后 Debug/Release 模型编译均零警告、零错误，同一 probe 检查 IClosable IID `30d5a829-7fa4-4026-83bb-d75bae4ea99e` 与非零 vtable 通过。两个类型复用同一 IDisposable 详情表，这是生成器去重。Models 两配置 PE/PDB 与各9份文档均核验通过，五项关键依赖与本工作区新应用输出逐字节 hash 相同。没有 COM QueryInterface、Native AOT、实机托盘或 UI 绑定测试。

fresh 基线全套535、修改前后托盘各25、修改后全套535；均 **Passed: N, Failed: 0, Skipped: 0**，TRX逐结果解析并比较名称。没有为 partial 新增行为测试。

| 保持要求 | 既有用例 |
| --- | --- |
| 驻留与反复恢复 | EnabledSettingSurvivesRestartAndClosesToTray、ActivationRecoversSameHiddenOrMinimizedWindowRepeatedly |
| 退订和迟到事件 | RepeatedEnableAndRecoveryDoNotAccumulateHealthSubscriptions、DisabledSessionCannotRestoreWindowOrAffectReplacement |
| Shell 失败可达性与重试 | FailedShellRecreationRestoresAccessAndSubsequentCloseRetries、PostCreationLossPreventsCloseIntoMissingTrayAndCanRetry |
| Close/Dispose/退出等待 | DisposedLifecycleCannotHideRestoreOrRecreateTray、ExitWaitsForSaveAndPlayerCleanupAndRejectsReentrantActivation、ExitJoinsPendingPlayerCloseWithoutRestoringOrClosingMainAgain |
| 异常与播放器驻留 | CleanupFailureIsReportedAndStillReleasesTrayAndExits、SaveFailureStillCleansPlayersAndExitsWithoutReactivation、ResidentPlayerCloseKeepsMainHiddenUntilExplicitRestore |

这些测试直接链接 lifecycle 的真实源码，通过既有边界替身验证逻辑；不覆盖真实 session 的原生资源释放、SetForegroundWindow、窗口取消事件或播放器取消实机结果。相关路径不变由源码对照支持，真实 AOT 托盘声明仍待验。

## 环境与复放

初始 kernel HEAD `f02c33f639addd5d421700c43fa66382beafe323`、索引0项，只有 `.git` 的异常保存为 initial-root/initial-kernel 与 working/index binary patch。只读从父 kernel fetch 精确对象，然后仅本工作区恢复至 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`，结束 clean。媒体资产从既有 `t3-4b0da1fe` 副本复制到本工作区，未下载或替换受保护资产；probe 使用该副本的真实依赖，实际拷入 Models 输出的 DLL 全部记 hash。

无子代理、desktop/UI、注册、登录、部署、安装、UAC；未触碰 Caa6a7be/layout-v3/Dev/PID9608，无 push/PR/issue/父工作区写入。准备阶段的缺失文档/通配路径查询报错仅为检查错误，未作为产品失败；原 strict 与 probe red 保持不覆盖。

汇总脚本首次沿用了旧证据目录前缀，修正后在 Release 尚未完成时按预期拒绝缺失的 exit 文件；待两配置完成后完整复算通过，未填造结果或覆盖 red。交付前 `git merge --ff-only feat/performance-45` 返回 Already up to date，父仍为 `bfa3d047`。本片无行为新增，未调用代码审查子代理。

复放时使用新 export/output 路径，勿覆盖原红：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter "FullyQualifiedName~TrayLifecycle" --logger "trx;LogFileName=tray-after.trx" --results-directory artifacts/tray-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger "trx;LogFileName=all.trx" --results-directory artifacts/tray-aot/tests
# 两配置各自执行；生成路径按配置分别指定。
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/tray-aot/generated-debug" -bl:artifacts/tray-aot/debug.binlog
python docs/performance-acceptance/evidence-tray-aot/extract-session.py src/Desktop/BiliCopilot.UI/App.xaml.cs artifacts/tray-aot/session-green.cs
dotnet build docs/performance-acceptance/evidence-tray-aot/Models/Models.csproj -c Debug "-p:SourceRoot=$pwd" "-p:SessionSource=$pwd/artifacts/tray-aot/session-green.cs" "-p:AppBin=$pwd/src/_build/x64/Debug/BiliCopilot.UI/bin"
dotnet run --project docs/performance-acceptance/evidence-tray-aot/Probe/Probe.csproj -c Release -- "$pwd/docs/performance-acceptance/evidence-tray-aot/Models/bin/Debug/net11.0-windows10.0.22621.0"
dotnet run --project docs/performance-acceptance/evidence-tray-aot/Metadata/Metadata.csproj -c Release -- src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.pdb artifacts/tray-aot/debug-pdb.json src/_build/x64/Debug/BiliCopilot.UI/bin/H.NotifyIcon.dll
python docs/performance-acceptance/evidence-tray-aot/verify.py
```
