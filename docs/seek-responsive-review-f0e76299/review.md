# f0e76299 独立审查：暂不接入 .5 三包

审查固定 `f0e7629923e1aaa6ed5d71a9b9b8fdbd09690ba5`，父为 `0d3a22f61575550e23f935028ec5f6376cac22ce`。审查结束前只读核对父工作区仍在该父提交，源工作区仍在固定提交。未修改两者。遵从用户要求，不委派、不启动用户应用、不改正式包或 gitlink；只在本审查区提交报告和小证据。

**结论：不能批准当前补丁 + Core/Legacy/Share `.20261101.5` 整体接入。** 面板隐藏门禁方向正确，Legacy 去同步重建解决主要 UI 耦合的证据可信，但以下两个边界需要小范围修复后以新候选版本复核。不能只合面板门禁就宣布可见密集 seek 已完整修复。完整 AOT 发布验证仍未完成。

## Standards

无独立的格式、WinUI API、MVVM 或 owner/borrow 架构阻断项。不建议为形式重写架构。按 code-review、winui-code-review、code-testing、run-tests、verification-before-completion 技能审查；用户禁止子代理优先于技能并行建议。未运行新的 WinUI analyzer 或完整 AOT 构建，不声称这些已通过。

## Spec：两项需修复

### P1：列表引用和请求版本不是同一个快照，新 seek 可以提交旧列表

位置：候选 Legacy bundle 的 `src/Danmaku.Legacy/DanmakuFrostMaster.cs:413–430`，以及 `SetDanmakuList`（约 378 行）。关键新增行是 `var list = _danmakuList; lock (list)`；随后才在 `_updateTimeQueue` 锁中读取 `_seekVersion` 和新目标。

可达时序：worker 捕获旧 list 后被抢占；UI `SetDanmakuList(newList)` 通过 `Clear` 增加版本并替换列表，再 `Seek(newTarget)`；worker 恢复后从队列取出**新版本、新目标**，却将它们和先前捕获的**旧列表**传入 `Reconstruct`。逐项版本比较全部通过，最终 `_lastTimeMs` 也提交新目标，旧内容因此能够冒充最新 ready。现有覆盖在重建已经进行后替换，未覆盖捕获列表到出队之间的间隙。

独立复现 `ListReplacementLatestSeek` 直接链接 bundle 未修改的 master，持有旧 list monitor 将该调度间隙固定下来，再替换并 seek；输出 `expected NEW; actual OLD`。旧 list 来自公开列表接口可被锁定，这个锁仅用于确定性调度，不改变生产代码；普通线程抢占也能产生同一交错。

建议：在短临界区内将列表/场景身份与队列目标、版本一起捕获，并让列表发布与版本退休使用同一协议。不要恢复持有整个列表/播放锁重建，也不要只在循环里继续比较同一个错误配对的版本。补一个该间隙的确定性测试。

### P2：异步重建异常终止唯一 worker，后续 seek 静默挂起

位置：同文件新增 `Reconstruct`（约 266–309 行）及调用处约 440 行；外围 `catch/finally` 约 508–516 行。

`Reconstruct` 现在在原更新线程内调用 `Pause`、`Stop(false)` 和资源准备。任何逃逸异常落入包围整个 `while` 的 catch，仅写日志后退出线程；`Seek` 仍继续将请求入队，没有故障状态/完成通知，也不会再有线程处理。原同步显式 seek 的故障并不会通过此新调用路径杀掉 worker。真实 `Pause` 调用 canvas Invalidate，`Stop` 清理并 Dispose 原生资源，均不是可假定永不失败的纯赋值。

`FailureThenLatestSeek` 在原生表面替身的第一次 Stop 注入异常，确认线程已退出，再提交第二个 seek：`workerAlive=False; starts=0; queuedSeekAccepted=true`。这是确定性的错误处理反例，不是已观察到真实设备必然触发同类故障的声明，也不是 GPU/AOT 验证。它证明用户要求的“失败不卡死/错误传播”尚不成立。

建议：将单次请求故障与 worker 生命周期分开；清理失败请求、按版本处理后续请求，或显式发布终态并拒绝后续请求，不能静默接受而不处理。保留原生 owner/资源失败保活规则，不在失败后盲目复用不安全资源；无需换新线程架构。

## 其余审查范围

- 面板隐藏时 Invalidate + Stop，seek、时钟、列表重建和样式入口受门禁；显示后的 redraw 从 VM 读取当前列表、位置、样式及暂停状态。允许清空后显示新弹幕，没有引入保留旧画面架构。隐藏状态的暂停调用仍存在，但不是历史重建。
- Legacy seek 发布只持队列锁；原更新线程逐项版本检查，播放锁覆盖单条原生准备；Stop/Close 的退休先于资源处理，Close 防止后续提交。上述列表快照漏洞例外。单条极长文本准备及清理仍可短暂占锁，不承诺全设备绝对无感。
- Core 直接按目标年龄过滤，固定寿命 `>`，滚动 `distance > CanvasWidth + width * 1.2`，逆向 `>= CanvasWidth + width`，分别与已有更新删除边界对应。Legacy 滚动阈值是 `CanvasWidth + width`，不能与 Core 混用。宽度来自各引擎原生测量，没有武断按历史秒数截断。
- 速度为零/负数时，正年龄不会被这些正距离阈值误判过期；这延续既有非正速度的运动/保留语义，不是新增有效速度约束。任意长文本仍以实际宽度参与寿命公式；原有最大尺寸/密度设置仍会排除条目。这里只做源码边界核对，未新做原生零/负速或超长文本矩阵。
- 固定弹幕按年龄回填 FirstRenderTime；Core 使用 active playback clock，暂停墙钟不进入 total，恢复首步不积累暂停时间；Legacy 现有暂停时间补偿仍在。既有 coverage2 包含暂停坐标及恢复后短寿命消失/长寿命保留的实测。目标存活集不保证历史轨道完全相同，这是已接受范围。
- Core 资源准备仍在原 owner 中，逐项取消、EndRebuild finally、文本布局转移与未提交对象归还仍在；未改 owner/borrow/失败保活底层实现。没有为此扩展审查全部历史资源代码或追旧 dump。

## 证据核验与归因限制

在本区复制证据后运行原 `verify.py`，重算 summary 与源提交 summary 结构完全相同；README 标注 final 的近似数值与该组一致。coverage2 是另一轮数值，不能把其数值与 final 拼成同一次实验。原生断言数 final=46、coverage2=50；托管相关测试为44项，是三种不同计数。

独立 `audit.py` 另外比对了冻结 nupkg 内 DLL 与 baseline-exact 已加载模块哈希、候选 nupkg 与 final/coverage2 已加载模块哈希。visible-red/visible-baseline 确为候选 DLL，不能当红。两个 bundle 的基线差异与交付 patch 完全相同；37 个非生成 PDB 文档与 bundle 源码相符，其中7个仅 checkout 换行差异，其余逐字节相同。原校验覆盖归档、六个包、PDB 源文档及 retained exit/WER/cleanup。没有宣称独立重建包具有可重复二进制哈希。

性能证据支持“冻结 Legacy 同步重建阻塞事件返回，进而推迟进度通知；候选解除了这段 UI 耦合”。无网络密集样本排除网络作为该阻塞的必要条件；不能推出全部用户卡顿均由此造成，也不能给纹理/GPU 或 .NET 11 分配耗时百分比。

媒体 setter/确认、进度 VM、Dispatcher 心跳、原生 ready 是不同终点。内部 ready/活动资源不是光学显示证据。长时间轴是3600秒弹幕数据，实际本地媒体仅60秒，长目标直接进入引擎；延迟服务是 Task gate，不是真 HTTP。Core 短密集样本 ready 变慢的事实保留，不能宣称 Core 各场景均提速。

本轮未再启动 native：两个确定性阻断已足够决定不接入，继续跑整套原生矩阵不能消除它们。已有证据全部 owned retained exit、退出后至少31秒 WER 和 cleanup/yield；这只描述交付证据，不冒充本轮新 native 运行。未触碰用户 AotDev/Caa6a7be/Dev/官方应用、现有实例或凭据。

## 独立复放

将固定提交 src/tests/global.json/nuget.config 用 git archive 展开到本区 `artifacts/seek-review/test-source`。最初使用本区 kernel HEAD 的干净源码归档，因 BiliHttpClient 构造函数与现有测试不匹配而编译失败（CS1729）；未算测试失败或通过。随后只读复制源验收宿主的 kernel 源码到这个私有测试快照，不改变任何正式 kernel。

成功命令：

```powershell
dotnet test artifacts/seek-review/test-source/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter "FullyQualifiedName~PlayerSeekTests|FullyQualifiedName~DanmakuLoadSessionTests|FullyQualifiedName~DanmakuRedrawSessionTests" --logger "trx;LogFileName=replay44.trx" --results-directory artifacts/seek-review/results
dotnet run --project artifacts/seek-review/probe/Probe.csproj
python artifacts/seek-review/audit.py
```

托管结果：**Passed: 44, Failed: 0, Skipped: 0**，退出0；TRX名称集合与交付44项逐项相同。它们验证 VM/会话逻辑，不直接覆盖本次修改的原生引擎或面板全部行为。两个独立探针为“反例确认器”，退出0表示成功复现缺陷，不是候选产品行为通过；详见 evidence.zip 内 Program.cs、Stubs.cs、probe-output.txt。夹具只有 master 一类，未改变产品实现。

本区 kernel 初态与结束均为 HEAD `f02c33f639addd5d421700c43fa66382beafe323`，保留原有474项 staged deletion 和顶层 ` M externals/bili-kernel`，从未执行 reset/update/checkout/write。正式依赖、冻结包及 gitlink 在父到候选差异中未改变。本提交只包含本报告和小证据归档。

Standards：0项独立阻断；Spec：2项（P1 列表/版本快照竞态、P2 worker 异常后静默停止）。新版本修复这两点后可进行聚焦复核，再由父任务整体接入三包和做完整 AOT 验证。
