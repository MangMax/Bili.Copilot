# 固定候选 a2c36eef 独立复审

审查 `a2c36eef095c22ae3beedcc0dae2b8c00135145a` 相对父 `f3f5fc22068ac0a9f6702dccb29cb009e471a879`。本区仅 fast-forward 候选；收尾核对最新本地父仍为 f3f5fc22。遵从用户不派子代理、不改生产代码、不重开已通过 native 矩阵的要求；使用 code-review（独立执行 Standards/Spec 两轴）、run-tests、code-testing、winui-code-review、verification-before-completion 及指定 implement-spec，用户明确范围优先于技能的委派/PR流程。code-testing-extensions 未提供，沿用仓库 xUnit2 模式。

**结论：本次队列修复没有确认的新生产阻断。原 21→19→20 最终19已由独立受控红/绿闭合，结合封存真实宿主两引擎最终20证据，可推进应用候选与冻结三包的整体实际接入。不能把应用单独合入仍为 preview6 的正式依赖组合后运行。** 有一项报告文字修正，父可直接处理，不需要新一轮代理或整矩阵验证。

## Standards / WinUI

- 没有发现本次变更引入的同步 UI 阻塞、错误绑定/资源生命周期、额外框架或投机抽象。两处新增 VSTHRD003 pragma 有具体理由：await 的是实例自己创建、仅由 finally 成功完成的排空任务；它们不携带 native 调用异常，不依赖 UI 线程同步等待。调用者等待仍捕获 UI 上下文，RunContinuationsAsynchronously 防止完成尾节点时同步递归执行整个队列；不是用 pragma 遮盖 `.Wait()`/`.Result`。`await previous` 位于 try 外安全的前提是该私有链的节点只通过 SetResult 完成，没有取消/失败入口；当前源码满足。
- 原生 SetCurrentPositionAsync 是 Task.Run 包装同步 mpv_set_property(time-pos)，并非等待 UI Position 通知。ReplayAsync 直接 await PlayAsync/ResumeAsync，不 await 应用 async-void PropertyChanged 处理器。故 UI await 队列不形成“本请求等自己的 UI 回执”的闭环；不能把此判断外推为 native 解码帧已经显示。
- 调用线程逐项核对：ChapterSegmentBar/Overlay、前后跳/章节点击命令、评论时间戳是 UI 命令或事件；SponsorBlock 由 UI 命令及 Player Position 事件驱动；恢复位置来自 Player 属性事件。封存 MpvPlayer 构造强制捕获 SynchronizationContext，OnDataNotify/OnProgressTimerElapsedAsync 用 `_uiContext.Post` 更新 Position/状态。SMTC 先 queue.TryEnqueue；connector 的事件由 UI 选择/切换命令发出，没有发现 Task.Run/ConfigureAwait(false) 后直接进入新队列的调用。无需为任意第三方后台调用加锁或新调度层。
- 新旧 Player 的两组 PropertyChanged 订阅/退订配对；sender/closed 防护前置。SeekRequested 在 panel 的 VM替换处成对订阅，原 unload/redraw invalidation 保留；隐藏不 Resume，暂停意图在 Seek 前设置。没有新 XAML，因此无新绑定/可访问性布局改动。父 Speed partial 与 Overlay 卸载/重挂逻辑未覆盖。专用 WinUIAnalyzer 未运行；封存 build3 0警告/0错误是实现侧构建证据，不冒充本轮构建。

## Spec / 队列与生命周期

- 接纳在首次 await 前发布新尾节点和版本；请求有效性闭包固定 player/client/scene。旧活动请求可完成，但过期排队请求在下发前跳过；成功回执在发布 Seek 前再次检查版本和身份。现有失败/抛异常两例要求 Requests=[21,20]，证明过期19不下发。异常由原任务传播，finally成功释放队列，不会把一个失败传染给后继或 teardown。
- InitializeCore 在递增媒体代次后先等 speed 再等 seek，再 Pause/Stop/加载；connector 在递增代次后先排空 seek，再 Pause/Stop/Initialize（后者保留 speed drain）。Dispose 先关闭输入，再等 initialization、speed、seek，最后解除订阅/销毁 Player/Client。speed 和 position 的释放不相互 await；position 不依赖 initialization 任务，未发现新增环路。关闭后新 seek 直接拒绝，已排队项身份检查失败仍经 finally释放。
- 上述排空针对已接纳的队列，不宣称建立了所有历史异步媒体切换的事务隔离；同一 Player 跨媒体迟到通知没有 scene token、重叠 connector/initialize 的旧生命周期边界仍保留。此次没有新增它们的修复承诺。
- End 分支保留 Replay 而不是直接设置已结束媒体；原 Replay API 自己捕获加载异常、没有成功结果，因此“await Replay 完成必定媒体成功”仍不可承诺。本次没有改变这一既有约定。同步完成、同步 IO回调再次请求 seek、SeekRequested订阅者重入并抛异常，均完成专项检验，队列没有锁死。
- 全局搜索仍见历史直连入口：Commands.cs:389/406 的 PageUp/PageDown 章节跳转、PlayerInteractivePanel.xaml.cs:354 手势完成及独立 Replay/选源路径未纳入新队列。不能把本补丁描述成“所有媒体写入全局串行”。这些未变入口与其他请求混用的原有竞争不在本片承诺内；不以此重开 gesture/pointer、源切换框架或阻断此次指定 `ChangePositionAsync` 反例的修复。

## Fresh 测试及受控反例

本区 `artifacts/seek-queue-review/full` 是独立测试副本：候选 src/tests 与本区1125个跟踪文件逐字相等；kernel从已冻结39df宿主副本复制，未修复或使用本区异常工作树。测试 runner为固定SDK11、xUnit2/VSTest。完整候选测试先执行，随后加入本目录三个 review-only 测试；没有修改生产文件或主测试目录。

```powershell
dotnet test artifacts/seek-queue-review/full/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=full.trx' --results-directory artifacts/seek-queue-review/tests
# 把本目录 SeekQueueReviewTests.cs 复制到上述副本 tests/BiliCopilot.UI.Tests 后：
dotnet test artifacts/seek-queue-review/full/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore --filter 'FullyQualifiedName~SeekQueueReviewTests' --logger 'trx;LogFileName=review.trx' --results-directory artifacts/seek-queue-review/tests
```

- Fresh完整应用逻辑测试：**Passed:572, Failed:0, Skipped:0，exit0**。这是仓库完整 BiliCopilot.UI.Tests，链接真实应用逻辑并适配WinUI服务，不能叫完整原生UI自动化。kernel proto有原有unused import warning，不伪称本轮构建零警告。
- 独立反例 `MiddleWriteFinishingLastCannotLeaveMediaAt19`：21先完成，19的真实模型写入卡在gate，20已接纳，再释放19。仅在副本把 Playback partial 换成44ce原版本（另加仅供adapter编译的已完成drain空桩），**Passed:0, Failed:1, Skipped:0，exit1，Expected20/Actual19**。恢复候选原字节后相同用例通过。不借只看弹幕回执来掩盖媒体19。
- `SynchronousNativeCallbackCanEnqueueLatestWithoutDeadlock`：IO同步回调接纳20，21同步完成，最终请求顺序21/20、媒体和弹幕20、drain完成。
- `ThrowingSynchronousSeekSubscriberStillReleasesQueue`：发布21时订阅者排队20并抛错；原调用保留异常，20继续完成且drain可结束。
- 最终三个专项 **Passed:3, Failed:0, Skipped:0，exit0**。原候选37项中的失效scene/player/client/close、失败释放、End、同目标和短距均已包含在fresh572中，未泛泛重做矩阵。初次专项因继承测试类多运行了已有用例，已改为独立IDisposable测试类后重跑；最终TRX仅3项。

原始TRX、日志、红绿完整命令、kernel初态和像素局部在 [evidence.zip](evidence.zip)，逐文件哈希见 [evidence-manifest.json](evidence-manifest.json)。`verify.py` 可独立复算。

## 真实包/源码/像素/退出复核

本轮只读运行上级 `verify.py`，检查sealed ZIP所有哈希、37项TRX、46项状态断言、实际加载路径/DLL与配对PDB哈希、903个应用源码对应及三轮退出/WER/清理。另实际打开磁盘nupkg，核对包和内部DLL SHA256，与 loaded 记录吻合，详见 [verification.json](verification.json)。PDB/CodeView/source文档闭包沿用同哈希冻结组合；不声称进程加载了PDB，也不从上游参考mpv源码推断冻结native DLL构建提交。

媒体图使用System.Drawing按原像素裁剪(0,65,240,110)，无缩放；Core/Legacy局部均明确读出烧录 `00:00:20.000` 和帧号480。完整Legacy原图也可读ROLL18/REV18/PRIOR19/BOTTOM19。Core原图SHA73e8d380…，Legacy120e8752…与ZIP对应。

**非阻断文档发现：上级README称runtime1 Legacy、runtime3首次激活“缺少视频内嵌时间”，但封存runtime1/legacy-latest20.png、runtime3/legacy-first-activation.png、runtime3/legacy-latest20-stable.png及导出legacy-latest20.png实际全部同一SHA256 `120e87529b9e580a016482bef778b708cbdc61b8d2d721c0eaeda7389f98950a`，原像素局部均有20秒。** 父应删去“首帧缺少时间/等待0.8秒才恢复”的不受证据支持叙述；稳定帧本身仍可作20秒正证据。没有使用可疑首帧来额外冒领另一轮视觉通过，也不根据历史缩略大图制造native故障。runtime2不额外授予资格。

三轮既有运行PID11648/49472/13468：retained exit0、forced=false；退出后31.0165331/31.0143972/31.016496秒精确PID/path WER无匹配；身份Bili.LatestA1de均清理remaining0、yield exit0。此处是独立复核封存记录，不是本轮再启动三次UI。本轮无生产改动，无需再跑全native矩阵；未启动或操作AotDev/正式/Caa6a7be/Dev、凭据或layout，未做dump/外部内存/逆向。

## 可采用边界与下一步供应

整体采用单元是本候选应用接线/队列，加 Core `0.0.1-pxdpi.9dc8c6c9.clean`（698d546e）、Legacy `0.0.1-reconstruct0b4f.final`（d7984643）及匹配 Share final。正式 src/Directory.Packages.props仍preview6；不要只合应用然后在旧包上运行。父下一步应实际接入这一组合，保留父speed/Overlay生命周期，更新正式依赖供应与锁定来源；不用为了本次批准重新验证历史。

供应需保存三包nupkg、匹配snupkg/PDB、源码文档闭包及各SHA256，可用 `docs/engine-61-design-draft/combined-acceptance/restore.py` 从冻结offline-evidence.zip恢复到新owned目录；须明确feed/可恢复来源，避免仅依赖某代理机NuGet缓存。来源宿主仍在39df；`prepare.py`重建助手须用新owned路径/身份且不得覆盖现有layout，本轮没有声称重新执行过完整UI恢复。集成后核对实际解析/加载组合，不能让preview6静默回退。若暂不能供包，则同时保留应用与依赖候选，不发布半套。

历史AV/c000027b/device-reset/AOT/perf状态原样保留；本轮没有授予它们通过，也不扩展到推荐列表。用户高优先resize卡顿工作应在实际接入后立即继续。

本区kernel初始HEAD实际为f02c33f639addd5d421700c43fa66382beafe323（异常状态以初始原文为准），HEAD/status/index/worktree patch前后逐字一致；gitlink仍0ae5e4e。只提交本目录报告与小证据，不push/PR/issue，不写父/来源。
