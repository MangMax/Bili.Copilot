# latest 媒体目标：有界应用修复及固定组合补验

> **更正（整体集成时依据独立复审）：** 下文“首次激活缺少内嵌时间／等待0.8秒才恢复”的叙述为历史误判，不再作为结论。runtime1 Legacy、runtime3 first-activation/stable 与导出图实际同 SHA256 `120e87529b9e580a016482bef778b708cbdc61b8d2d721c0eaeda7389f98950a`，原像素均为20秒。参见 [独立复审](independent-review/README.md)。下文旧叙述及原证据保留供审计，runtime2不额外授予资格；本次未为纠错重跑UI。

起点为父 `f3f5fc22068ac0a9f6702dccb29cb009e471a879`（含 `3cb008ea` 的真实失败）。本片按差异纳入 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede` 的 11 个应用文件和配套测试，再修复媒体命令乱序；没有整目录覆盖父。父 `PlayerViewModel.Speed.cs`、Overlay 卸载/重挂及所有新文档保留，正式 `src/Directory.Packages.props`、gitlink 未改。

**本轮有界结果：受控红成立，37 个针对性测试通过；固定组合两引擎各 10 次并发 latest 状态检查通过，原像素确认最终媒体 20 秒，关闭边界通过。可交父安排一次独立 review；本实现子任务没有启动子代理，也不把自审称为独立 review。** 这不是正式 preview6 准入或历史 AV/device-reset/AOT/性能全绿。

## 原因与最小修改

必读的原 [组合失败报告](../engine-61-design-draft/combined-desktop-39df/README.md)、两份 seek trace、latest-counterexamples 原样保留。候选旧测试只筛弹幕回执；真实媒体写入已开始，晚完成的旧请求仍能改变媒体，随后 Position 回调又更新弹幕。

已查实际引用 `Richasy.MpvKernel.Core 0.0.2-preview2` nuspec 所标源提交 `c1ef0f403097129c3b6db4c466cc4147054471d2`：[`SetCurrentPositionAsync`](https://github.com/Richasy/mpv-kernel/blob/c1ef0f403097129c3b6db4c466cc4147054471d2/src/MpvKernel.Core/MpvClient.Player.cs#L281) await 的是在线程池执行同步 `mpv_set_property(time-pos)`，不是等待 UI Position 或视频帧。多个调用可按不同线程池执行顺序提交。应用先加版本检查只保护回执发布，不能撤销已经发出的媒体写入。

同时核对 mpv 的[同步属性请求](https://github.com/mpv-player/mpv/blob/9e2ed65eb0c02f99a352dfa1ad3c85ecceefe005/player/client.c#L1323)、[`time-pos` 设置](https://github.com/mpv-player/mpv/blob/9e2ed65eb0c02f99a352dfa1ad3c85ecceefe005/player/command.c#L809)及[绝对 seek 合并](https://github.com/mpv-player/mpv/blob/9e2ed65eb0c02f99a352dfa1ad3c85ecceefe005/player/playloop.c#L424)：同步属性返回意味着属性处理已完成，后续解码/显示仍可异步；后来的绝对目标替换待执行目标。此上游源码是语义参考，**没有声称该提交就是现有 native DLL 的构建提交**；实际冻结 native 文件另以加载路径/SHA256及真实宿主验证。未做 dump、逆向或 native 根因推定。

实现只有实例内任务链：在 UI 线程同步接纳、捕获 player/client/scene 和请求版本；异步等前一个请求释放后再次验证，失效或过期的排队请求直接返回。有效请求的 native 提交串行，成功且仍有效才发布显式弹幕 Seek；`finally` 总是释放后继，原 IO 异常仍从原调用任务传出。任务链只排序提交，不假称 Task 完成时画面已到位，也没有位置轮询、纠正定时器或重试。

Dispose、Initialize 和 connector 换媒体入口在旧媒体失效后异步排空已接纳请求，避免 native 销毁或新媒体加载穿过尚未结束的写入。父 speed drain 仍在原处。旧 sender/closed 回调防护及成对订阅保留；同一个 MpvPlayer 的任意跨媒体迟到 native 通知没有 scene token，未声称解决其全部归属问题。

## 受控红与针对性测试

受控替身在 setter 任务返回前真正更新 `Player.Position` 并发出属性通知，gate 控制旧写入晚到。原应用上 `ConcurrentSeeksCommitLatestMediaTarget` 实际媒体 21、预期20；`LateSeekReceiptCannotReplaceLatestTarget` 实际22、预期25。`red.trx` 为 **Passed:24, Failed:2, Skipped:0**。旧 LateSeekReceipt 保留，改为先接纳两请求、释放旧 gate、再一起等待，避免串行行为下死等。

最终命令见 [tests-command.json](tests-command.json)，SDK11/xUnit2/VSTest，针对 `PlayerSeekTests|DanmakuRedrawSessionTests`，**Passed:37, Failed:0, Skipped:0，exit0**。测试在本区独立完整副本执行，使用逐字相同的当前生产/测试源码；kernel 来自冻结宿主副本，不动本区异常子模块。当前技能目录未提供 code-testing-extensions 参考，沿用仓库 xUnit 示例。

| 要求 | 直接证据 |
|---|---|
| 最后目标写入真实媒体模型，旧回执不可替换 | `ConcurrentSeeksCommitLatestMediaTarget`、`LateSeekReceiptCannotReplaceLatestTarget` 同时断言 Player 和 Danmaku |
| scene/player/client/close 在等待期间失效不下发排队写入 | `InvalidatedQueuedSeekNeverWritesMedia` 四例；旧 `InvalidatedReceiptCannotPublish` 五例保留 |
| 失败与异常释放队列，异常不吞，过期19不下发 | `FailedWriteReleasesLatestQueuedSeek` 两例，要求 Requests 为21、20；异常要求原任务抛出 |
| 关闭等待活动写入，拒绝排队及后续请求 | `CloseDrainsActiveWriteAndRejectsQueuedTarget` |
| 短距、同目标、暂停/播放重建及旧回调防护 | 既有 `ShortSeekReachesMediaClient`、`SameTargetExplicitSeekRequestsReconstruction`、`PausedPositionChangeReconstructsButDuplicateTickDoesNot`、`OldOrClosedMediaCallbacksCannotAdvanceTarget` 等保留 |

所有尝试保留在 ZIP：初次副本遗漏两份 adapter 导致编译失败，复制齐后才得到有效红；`green1.trx` 名称虽含 green，实际仍是修改脚本编码错误后的24过/2失败，不作绿证据。桌面 build1 的 CA2213 促使改成无需 IDisposable 的任务链；build2 的 VSTHRD003 用两处有说明的窄 pragma 处理。最终 build3 **0警告/0错误/exit0**。没有关闭分析器；未运行专用 WinUIAnalyzer、AOT 或性能测试。

## 真实固定组合

仅使用此前冻结组合：Core `0.0.1-pxdpi.9dc8c6c9.clean`（引擎698d546e）、Legacy/Share `0.0.1-reconstruct0b4f.final`（Legacy引擎d7984643）。来源为只读 `t3-39df52c6/artifacts/combined-60cd/full`；完整恢复资料继续引用 [combined-acceptance](../engine-61-design-draft/combined-acceptance/restore.py)。本区身份唯一为 `Bili.LatestA1de`，layout1/2/3 全在本区 artifacts 中。

- `runtime1`：Core、Legacy 各10次 `Task.WhenAll(21,19,20)`，每次2秒后要求媒体 Paused/20、Danmaku/20、renderer/20000；快速顺序28→24→20、真实 Overlay 前后1秒、同目标20及排队后立即 CloseAsync、重开。46项断言全过，不能解释成46个独立场景。
- Core 有效原图为 [core-latest20.png](core-latest20.png)：媒体内嵌 `00:00:20.000`，ROLL18/REV18/PRIOR19/BOTTOM19 可见。
- Legacy 首轮 latest、runtime2 补拍以及 runtime3 首次激活截图缺少视频内嵌时间，**全部保留且不授予媒体视觉通过资格**。runtime3 在同一 owned HWND 激活后等待0.8秒再次截图；两帧间无 invoke/seek，原像素 [legacy-latest20.png](legacy-latest20.png) 明确显示 `00:00:20.000` 及四文本。这只验证观察到的呈现结果，不猜测首次激活帧的 native 原因。
- runtime2/3 仅补 Legacy 媒体时间图，各4项状态断言通过，没有重复所有完整矩阵。四张首轮图和三张补拍图均已逐张人工查看；短距图中的时间预览遮挡不能当作完整视觉矩阵。
- 夹具观察 timer 只读状态/视觉，不写位置。原媒体含时间烧录，故截图时间不是仅应用 VM 文本。

实际加载证明包括进程内 `loaded-modules` 的路径/MVID/SHA256、配对 PDB 文件哈希和 runtime1 `process-modules.json` 的应用/Mpv/native 模块路径。Core DLL `b15225ec…84f97fd`、Legacy `2cc03eba…3adfabf`、Share `0541962a…be8329` 与封存包逐字匹配；完整值见 [package-closure.json](package-closure.json)。源文档/PDB/CodeView 关系复用已封存的同哈希冻结包，不声称 CLR 加载了 PDB。[app-source-closure.json](app-source-closure.json) 核验903个当前应用 C#/XAML；实验宿主仅 App 启动、GlobalDependencies 离线服务两处注入及额外 OfflineAcceptance。测试副本也逐文件核验。

| owned PID | retained exit | 退出后精确 PID/path WER | 清理 |
|---|---|---|---|
| 11648 | 0，未强杀 | 31.0165331秒，无匹配 | 本身份卸载、remaining0、yield exit0 |
| 49472 | 0，未强杀 | 31.0143972秒，无匹配 | 同上 |
| 13468 | 0，未强杀 | 31.016496秒，无匹配 | 同上 |

AotDev、正式、Caa6a7be、Dev 相关已安装包版本/路径前后相同，进程快照保留；未操作这些实例、凭据或 layout。kernel 初始 HEAD/status/索引patch/工作树patch 前后逐字相同，索引 gitlink 仍 `0ae5e4e000a41a07f6696746706df11c06e8835b`。

## 复核与交接

`python docs/seek-latest-media/verify.py` 只读验证 ZIP逐文件哈希、红/绿TRX、每轮退出/WER/清理、loaded三包及当前应用源码。人工像素判断另如上说明。`prepare.py` 为恢复助手，拒绝覆盖现有宿主；本轮实际准备/构建/运行命令与原始失败都封存在证据中，未声称又执行过一次完整恢复演练。

WinUI 自审：UI线程异步接纳与继续、无同步阻塞；旧请求身份在等待后及回执后都检查；故障与排空分离；通知成对订阅；父 speed/Overlay 生命周期未覆盖。请父的一次独立 review 重点复查任务链释放/换媒体关闭次序、native提交语义与落点证据、候选11文件集成边界。**未独立复审通过前不把此实现自审冒充批准。**

正式依赖仍preview6，本片只是应用候选加固定组合补验，不允许直接据此发布旧依赖组合。历史 AV、c000027b、device-reset、AOT 和性能结论保持原状。推荐页累积条目后 resize 卡顿及类似页面问题按用户顺序留到弹幕接入后，本片未扩到列表优化。未 push/PR/issue，未写来源或父工作区。
