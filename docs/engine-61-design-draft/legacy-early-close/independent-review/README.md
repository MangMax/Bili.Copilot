# 21d76cff 独立实机验收：早关闭通过，应用稳定可见未通过

2026-10-31，固定候选 `21d76cff30c09f60255d385af5eec3b7b85ea0dc`。无子代理。仅本工作区创建测试副本、报告和证据；生产代码、正式依赖、gitlink、父和来源工作区均未修改。未发布、push、PR、issue。

**早 Close 小修可采用；完整生命周期不能整体签绿。** 无屏障宿主的早关闭、随后 UI pump、无晚 Loaded、正常 Loaded 关闭、sibling 和重复 Close 均通过。随后实际消费新隔离包的完整应用执行了三轮暂停重挂，并补做屏幕捕获复核。新实例状态正确、rows 非空，但稳定截图仍有 TOP20 缺失；这是交回的实际验收阻断，不是仅返回下一片准入。

## 发现

**P1：完整应用暂停重挂后的稳定可见性未通过。** `full/runtime/screen-remount2-stable.png` 和 `screen-remount3-stable.png` 未显示 TOP20；对应 JSON 均是 Paused、position=20、time=20000、paused=true、stopped=false，rows 含 drawable=true 的 TOP20。第1轮稳定图可见。三轮 master 身份不同；没有使用 Redraw、Resume、第二次 Seek 来补救重挂。第一次采用普通窗口截图也有空图，因此另用 `--capture-screen` 前台实际屏幕复核，仍出现空图。后续同一暂停实例、无媒体操作的 `static-0/1/2.png` 为不可见、不可见、可见。

这证明“rows 非空”不足以支持稳定画面通过；不把截图的空帧擅自归因为本次 Close 的7行变更，也没有原生指令级根因结论。交回范围应是上述暂停帧显示问题的最小定位，保留失败截图，不能以重试至截图可见洗绿。未扩大架构、未修改生产代码追绿。`DanmakuRender.OnCanvasDraw` 与应用可见性/合成路径尚需进一步区分；本轮没有增加它们的观测补丁。

## 原宿主生命周期

沿用前次宿主项目、WinAppSDK 2.6.4 / Win2D 1.4.0、真实 Grid 与 Canvas，调用 new → SetEnabled → SetList(20/21/22) → Pause → Seek(20000) → UpdateTime(20000) → Close。首次 Close 前不 await，不含 ProbeGate/ProbeAttempt；仅保留前次 CreateResources、Paused、RemoveFromVisualTree 日志观测。UI事件只观测公开 Loaded/Unloaded/IsLoaded，不手工触发资源回调。Close 后 await 2秒，让真实 dispatcher 继续运行；然后新建正常实例，等待 Loaded，关闭两次，再 pump 2秒。

| Requirement | Evidence |
|---|---|
| 早关闭精确移除且 sibling 保留 | `ImmediateDetach`，children=1，Canvas 不再在 scene 中 |
| 重复早 Close | `RepeatedEarlyClose` |
| Close 返回后继续 pump、无晚 Loaded | `EarlyCloseAfterPump`，lateLoaded=0、IsLoaded=false；整个日志无 Canvas.Loaded，存在 Canvas.Unloaded |
| 正常首次 Loaded/CreateResources 后关闭 | `NormalLoaded`；日志含 Normal.Loaded、CreateResources enter/return |
| 正常关闭再 pump、重复 Close、sibling 保留 | `NormalCloseAfterPump`，仅 sibling 留存、Normal.Unloaded |
| 精确退出/延迟事件日志/清理 | PID35872，retained exit0，32.015秒后 Application 日志可读且候选匹配为空，packagesRemaining=0、processRemaining=false、yield exit0 |

执行 `artifacts/early-review-90c5/immediate/run.ps1 -RunName lifecycle2`：**Passed: 5, Failed: 0, Skipped: 0，executionFailed=false，exit0**。不能外推为任意线程 Close、device-reset 或回调内部所有竞态通过。

第一次夹具把 sibling 加在构造前，既有 FrostMaster 构造函数 `rootGrid.Children.Clear()` 清掉它，导致4项失败。只把 sibling 移至构造后，正常实例亦如此；保留 `lifecycle` 的原 Program.cs、日志、结果及截图（1 pass/4 fail，PID34192 exit0，32.026秒WER空）。不是生产修补。两次截图逐张查看；第二次 sibling 可见。

## 完整应用实测

固定应用 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`（参考证据 `d84618bf`，已读独立review）。`prepare-app.py` 直接只读归档来源 kernel 的0ae5e4e到 artifacts 副本；没有恢复当前异常 submodule。901个生产 C#/XAML文件与固定应用归一化比较一致，仅 App.xaml.cs/GlobalDependencies.cs 为已有离线启动注入；完整已有 OfflineAcceptance 作为额外测试源保留，包含对象级只读反射观测。没有外部进程内存读取、dump、反汇编或逆向工具。

从上游45文件校验后覆盖 legacy-ready，再覆盖此次 DanmakuRender。干净包 **0.0.1-preview6.review90c5.clean** 无任何引擎测试钩子/日志注入；Legacy/Share通过真实 PackageReference 消费，nupkg DLL与build及layout逐字节一致。Core仍正式preview6。隔离身份 `Bili.Review90c5`，PID27464，路径均属本 artifacts。

Release x64 JIT build exit0，0错误、1条既有 protobuf unused import warning。未运行专用WinUIAnalyzer/AOT/perf。没有重复旧23兼容或完整.NET测试套件；本轮是自定义UI/native夹具。

| Requirement | Evidence |
|---|---|
| 三轮新实例、暂停目标不丢失 | `PausedRemountStateRound1/2/3` PASS，screen-remount*-stable.json |
| 三轮稳定可见 | `PausedRemountVisibleRound1` PASS；`PausedRemountVisibleRound2/3` **FAIL**，逐张实际屏幕复核 |
| 真实播放器短跳按钮 | `ActualButtonforward1`、`ActualButtonbackward1` PASS，20→21→20，Paused；图分别TOP21/TOP20。未把控制器Seek按钮称为真实短跳按钮；本轮未重跑bar拖动 |
| 关闭并继续UI pump | `CloseFullDisposes`、`NoPublicationAfterClose` PASS；disposed=true、players=0，关闭后约2秒无seek/redraw发布，再ExitOwned |
| 退出和事件日志 | `NormalExit`、`WERQuietAfter31Seconds` PASS；retained exit0、非forced、31.0235481秒后WER无候选匹配 |
| 测试异常 | `NoHarnessExceptions` PASS；退出后自有包0、自有进程无，yield成功 |

完整应用结果 **Passed: 11, Failed: 2, Skipped: 0，acceptancePassed=false**，其中视觉结果是人工逐张审查写入，非像素自动识别。`ui-app-screen.py` 命令exit0仅表示状态断言及CLI完成，不代表视觉通过。22张完整应用截图均已查看，加宿主2张共24张。

夹具问题亦保留：首次UIA调用时控件已自动隐藏，PlayPauseButton未找到；先ShowControls后重试。一次窗口选择误选短暂tooltip HWND，capture报告not accessible；改为精确播放器标题后执行完整屏幕矩阵。没有生产修改。第一次探索批次的commands-matrix被后一次失败批次覆盖，原截图/状态/连续events仍保存；权威屏幕复核批次 `commands-screen-matrix.json` 完整保留。没有把这些工具失败算成应用生产异常。

## 历史与边界

已读并解包774b的evidence.zip，53项manifest哈希全部匹配；旧logger PID41012与ready PID4668均Close.return→Canvas.Loaded→Win2D c0000005，精确PID/path，延迟>=31秒。原无ready红、closed矩阵Win2D AV、完整应用原c000027b、原FAIL/UNKNOWN均保留；本次正常退出不反写旧结果。

候选仅从已知Panel.Children移除owned Canvas，随后既有内部清理；没有清 siblings、公共API或XAML变化。master先设置closing并清queue，ready通知仅唤醒原worker。首次ready不代表当前device永远有效：device-reset失效窗口、并发换列表、目标前全集、Core完整矩阵及完整#49/AOT/perf继续未通过/未验证。不能把本轮早关闭成功外推到这些范围。

使用 systematic-debugging、winui-code-review、winui-ui-testing、run-tests、code-testing、verification-before-completion。run-tests overlay不存在；无新增生产接口问题需SDK API查询。用户禁止代理及扩大架构的约束优先。

## 复放与封存

`evidence.zip` 是相对 `artifacts/early-review-90c5` 的小证据包，包含实际源、隔离nupkg、脚本、命令、连续事件、截图、retained退出/WER和前次只读证据。`audit.py` 可只读复算，不启动桌面；它验证证据一致性，**不表示完整验收通过**。

新运行请在新的输出目录/身份下使用prepare脚本，勿覆盖封存目录。原执行顺序：prepare.py immediate → run.ps1 lifecycle/lifecycle2；prepare-clean.py clean → prepare-app.py → launch-retain.ps1（持续持有进程句柄）→ ui-app.py matrix → ui-app-screen.py matrix → static-capture.py → ui-app-screen.py close →延迟WER→精确身份卸载/yield。来源依赖仍按脚本中的固定归档路径提供，不封存巨大media/libmpv/整应用输出。UIA脚本会从当前启动identity.json获取PID；static-capture.py内HWND是本次原始记录，复放须替换为新窗口。

kernel初始HEAD f02c33f及474项差异已记录binary staged/worktree patch，并保持字节一致。索引gitlink仍0ae5e4e。保护登录Caa6a7be、Dev、其他实例/凭据/layout均未触碰。父本地tip核对仍21d76cff，交回前在本分支merge最新父。
