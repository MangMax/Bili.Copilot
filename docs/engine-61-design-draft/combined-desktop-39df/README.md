# 双引擎桌面验收：矩阵已执行，连续 latest 竞态阻止组合准入

本区起点/最新本地父 `2b8d1cb2c508ddadb3276a664a3706a63ed0fad3`；应用验收源 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`。用户已恢复桌面工作，本轮实际执行，不再沿用上一 checkpoint 的 PENDING。**结论为 FAIL_CONTINUOUS_LATEST，不是全绿。**

发现同一实质失败在两引擎出现：暂停位置 20，依次发起并发 `ChangePositionAsync(21), (19), (20)` 并等待全部完成，真实媒体与弹幕可能最终停在 **19**。Legacy 在 runtime2 捕获，Core 在 runtime3 捕获；后者等待 2 秒后仍失败，截图媒体时间为 `00:00:19.000`。六次额外短复测通过不推翻竞态证据。其余要求已实际覆盖；失败后仍完成独立矩阵，并非只交下一轮计划。

## 要求与证据

原始路径均在 `runtime-evidence.zip` 内；ZIP SHA256 与每个文件校验值见 `runtime-evidence-manifest.json`。`assertion-summary.json` 保留每轮完整断言计数与失败名，不能将重复断言数解释为独立用例数。

| 要求 | 本轮结果与具体证据 |
|---|---|
| 暂停 Core→Legacy→Core；隐藏后播放切换恢复 | PASS。runtime1 `CoreTargetFourModes` / `LegacyTargetFourModes` / `CorePausedStable`；`hidden-playing-restored.json/png`。runtime3/continuation 再次执行隐藏→播放→Legacy→Core→暂停→显示，真实媒体 Paused/20 与四文本恢复 |
| 四模式当前配置位置/年龄 | PASS。`CoreConfiguredPositions` / `LegacyConfiguredPositions`；20 秒时滚动/逆向年龄 2000 ms、上/下 1000 ms。画布 1646×1087、scale 1.5、speed 1、extra 1。按各引擎实际宽度与配置算 x，误差阈值 1.5 px；独立四模式槽 y=0，原像素确认上/下实际绘制区域。未要求跨引擎同像素 |
| 暂停时间不消费寿命、恢复位移 | PASS。两引擎 `FourModesPausedPositionAgeStable`，暂停 3.4 秒；最终严格复核 `tight-displacement.json`：间隔约 0.751 秒，滚动 −133.70/−133.75 px、逆向 +130.55/+130.60 px，与速度公式误差 <0.35 px；固定上下位移 0。固定项容差 2 px，滚动容差 75 ms 采样/帧差对应距离+2 px，并要求位移大于容差，零位移不可能通过 |
| 四模式剩余寿命 | PASS。runtime3 Core、runtime3/continuation Legacy `ObservedRemainingLifetimes`。固定项剩余 2 秒，最后存在约 1.756 秒、首次消失约 2.007 秒；滚动/逆向按各引擎移除边界计算约 7.936–8.077 秒，最后存在约 7.770、首次消失约 8.021 秒。以实际 Playing 观察为时钟，250 ms 采样，端点容差 550 ms；未将 Legacy 静态重建年龄冒充播放中的实时年龄 |
| 短距前后、同目标 | PASS。真实 Overlay `ForwardButton` / `BackwardButton`，媒体 20→21→20；`ShortAndSameTarget` 与再次四模式/年龄/位置断言。原轮 `ShortSameLatest` 不可覆盖后续 latest 失败 |
| 连续 latest | **FAIL**。`latest-counterexamples.json`；runtime2 `LegacyTargetFourModes` 失败细节（位置19），runtime3 `CoreContinuousLatestTarget`；`core-latest-media-time.png` 及 ZIP 内完整 `runtime3/core-latest.png/json` |
| reload / x:Load 三轮 | PASS。runtime1 与 runtime3/continuation `XLoadNewInstance1..3`、`OverlayReload1..3`；卸载时 renderer 不存在，重建实例变化且恢复四模式；Overlay 实际 Children remove/insert 触发卸载/加载，不伪造生命周期方法调用 |
| seek 后 CloseAsync、重开 | PASS。`CloseAfterSeek`、新 `closed` 事件、disposed=true；`reopened.json/png`。最终轮使用事件计数排除旧 closed 记录 |
| 加载屏障中 CloseAsync、释放后关闭并重开 | PASS。明确为离线 `GetVideoPageDetailAsync` Task gate，不称真实网络竞争。`ControlledLoadingClosePending` 记录 pendingInitialization=true；释放后等待新 closed，`ControlledLoadingCloseDisposed`，`reopened-after-held` |
| 原像素正例、空白/不同文本负例 | PASS（限无遮挡 runtime1）。`pixels/` 为原尺寸局部，人工逐个读出 ROLL18、REV18、PRIOR19、BOTTOM19；blank 同区域无文本；other 的 FUTURE21 可辨且不同于 PRIOR19。`pixels.ps1 -Run runtime1` exit0，8 个正例中性亮像素≥100、对应 blank <40。裁剪不缩放，不以 rows-only 签绿。runtime3 部分正例被控件遮挡、变暗，像素门失败，**明确弃用其视觉通过资格** |
| 实际 loaded DLL/PDB/source 闭包 | PASS。`loaded-closure.json` 为 PID49348 进程内实际程序集路径/MVID/SHA256，三个都位于本区 layout39df-3；配对 PDB 原字节与冻结包相同。PDB/CodeView/source document checksum 沿用且核对封存 metadata；不声称 CLR 在运行时加载了 PDB |
| retained exit、退出≥31秒精确PID/path WER | PASS，三轮 PID49612 / 8636 / 49348 均 retained exitCode=0、未强杀，31.0127 / 31.0133 / 31.0181 秒后 Application 1000/1001 精确 PID/path 无匹配。见 `exit-summary.json` 和 ZIP 原日志 |
| 清理、yield、保护其他身份 | PASS。三轮仅卸载 `Bili.Combined39df`，remaining=0，yield exit0。保护身份包版本/安装路径前后相同；AotDev 由用户在期间重启（50412→19868），本任务无操作指向两者。未读写登录数据或其 layout |

## 最小反例及判定边界

前置：应用源44ce、冻结三包、本地60秒媒体，暂停位置20，engine Core 或 Legacy，已完成渲染。夹具按钮 `FastLatest` 的唯一动作：

```csharp
await Task.WhenAll(Seek(21), Seek(19), Seek(20));
// Seek 只 await vm.ChangePositionAsync(target)，然后记录完成日志。
```

C# 实参按左到右求值，最后请求目标20。runtime2 Legacy 和 runtime3 Core 均记录最终媒体 Paused/19、danmakuPosition=19、renderer time=19000；Core 原截图还独立显示媒体19秒。因此这不是仅行数组不同，也不能通过放宽重建年龄断言解决。完整事件、命令、截图和两份精简 seek trace 已封存。

代码定位：44ce 的 `PlayerViewModel.Playback.cs:44`，`ChangePositionAsync` 在 await 原生 `SetCurrentPositionAsync` 后才用 seekVersion 筛选显式弹幕 seek；`OnDanmakuPlayerPropertyChanged` 仍会把实际媒体位置写回弹幕。**推断**：版本检查没有提供媒体命令最终目标的串行/latest 保证；此处仅是定位线索，未做 native 内存调查、dump、逆向，也未把推断写成已证明的 native 根因。按测试子任务边界不修改生产代码，交父任务处理这一最小反例。

## 可合入与不得合入范围

- **现在可合入本分支**：此目录的脚本、证据与上述失败结论。生产应用源码、正式 `Directory.Packages.props` 和 gitlink 没有改动。
- **当前不能批准“应用+候选依赖”整体准入/发布**：latest 是明确要求，已在两引擎复现失败；不得用第一轮52个通过断言或后续六次通过复测冲掉失败。
- 后续修复并复验后，可评估的应用候选范围是44ce的11个生产文件：ChapterSegmentBar两文件、OverlayTransportControls的seek接线、VideoDanmakuPanel、DanmakuRenderViewModel两文件、PlayerViewModel Commands/Methods/Playback/SponsorBlock/主文件，及该提交配套单测。不能用旧应用整目录覆盖父；父 speed partial 与本轮源逐字一致，Overlay 与父仅seek接线不同，卸载生命周期保留。
- 唯一被本轮验证的依赖范围：Core `0.0.1-pxdpi.9dc8c6c9.clean`（引擎698d546e）、Legacy/Share `0.0.1-reconstruct0b4f.final`（Legacy引擎d7984643）。Core DLL `b15225ec…84f97fd`、Legacy `2cc03eba…3adfabf`、Share `0541962a…be8329`，全哈希在 package/loaded closure。不能换成正式 Core preview6。本轮无正式依赖升级或 gitlink 修改。
- 历史 AV / c000027b / UNKNOWN / device-reset / AOT / 性能结论原样保留。三次正常退出只是本轮有限观察；Legacy 最坏 O(N²) 未做性能准入，不外推到大列表、设备重置或 AOT。

## 夹具修复及复验记录

1. 原源码路径写死到来源区，不能直接复用 EXE。只读复制源并逐文件核验到本区，改夹具 Root 和隔离包身份后构建；应用其余生产C#/XAML相对44ce只有原两个离线注入。构建1 exit0/1条已有proto警告；构建2、3 exit0/0警告。
2. runtime1 首次启动 controller 尚未创建，原 runner StopIteration；`first-startup-attempt.json` 保留。等待实际窗口后原矩阵52个断言通过。新 runner 增加启动等待。
3. 原 modules 在初始化前为空；改为实际运行期进程内记录。runtime2 发现 winapp 部署不携带 PDB，读取配对符号失败（原 observe-error 保留）。最终 launch 先核 exact owned path 再把已验证 PDB 复制到同一 owned layout；成功后才置记录完成标志。runtime3 无 observe-error/action-error/unhandled/startup-error。
4. Legacy 的 Canvas 暂停仍tick，源码明确通过移动 FirstRenderTime 排除暂停时间；初版新增断言比较整行导致假失败。只比较 text/x/y/width/height/age 后实际重跑通过，原失败保留。位移断言自审发现初版基于播放起点的容差过宽，最终使用两条播放观察差分；重新计算真实记录并通过严格门，见 `recheck-motion.py`，没有仅修改阈值宣布通过。
5. runtime3 `foreground_not_target` 拒绝截图并中止 runner，原命令保留；continuation 只对同一 owned HWND 作有限重试并完成剩余实际矩阵。不会把未截图或遮挡帧记为视觉PASS。没有关闭、激活或输入用户 AotDev。

使用 implement-spec、WinUI UI testing/code review/dev workflow、run-tests、code-testing、systematic-debugging、verification。遵照用户禁用子代理；未增加/升级测试依赖，未运行 dotnet test、WinUIAnalyzer、AOT、性能或单引擎全矩阵。静态自审范围为夹具/runner：真实VM和Overlay入口、异步gate、owned PID/HWND、失败证据保留、fresh closed事件、严格位移门。源码来源区原文件哈希末次复核不变（`source-unchanged.json`）。本区 kernel 初始 f02c33f/474异常的 HEAD/status/staged/worktree 二进制diff 前后逐字一致；gitlink仍0ae5e4e。

恢复封存证据时先核 manifest 的 ZIP SHA256，再解到本区新的目录。原包恢复仍可使用上一 checkpoint 的 restore.py；不要覆盖现有 layout。最终本区提交后合并最新本地父 `feat/performance-45`，不 push/PR/issue。
