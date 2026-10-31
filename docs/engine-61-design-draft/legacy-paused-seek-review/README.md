# d954aed5 独立审查

**结论：允许进入当前应用的隔离接线验收，有明确条件；不签通用包/任意回调无死锁。** 固定候选 `d954aed59f85c6befda789fc96a0cc72663504ed`，父 `7ec17b2269d122c496730ff25028f78b86565edc`。本区仅 cherry-pick 为 `2c48cea4`，未修改候选两文件。父 `feat/performance-45` 仍为该提交且已是祖先。

## Standards / WinUI

**R1 — Error（有重入 logger 时），新增且真实可达的锁反转。** [DanmakuFrostMaster.cs](../legacy-paused-seek/source/DanmakuFrostMaster.cs) 的 Stop 212–220 行持有 `_playbackGate` 调用 `_render.Stop()`；renderer 576 行同步调用外部 ILogger。worker 在 371 行已持有 `_danmakuList`，随后需要 playback。如果 Stop 的 logger 同线程调用公开 `Seek`，它在 243 行等待 list，形成永久环：worker list → playback；Stop/logger playback → list。无需跨线程 UI dispatch，也无需假定 Present 永远不返回。Close 的 Stop 日志也有同类入口，且 Seek 在检查 closing **之前**取 list。

[Boundary.cs](Boundary.cs) 的 `StopLoggerCallsSeek` 用实际程序集重现：基线完成（exit 0），候选不完成（exit 2，两线程等待）。为安全复放，反射构造并完整初始化 managed 同步状态，canvas/device/layers 为空、列表为空；真实 `Updater_DoWork/Stop/Seek/Logger` 方法执行，无替代算法。日志屏障固定 worker 已持 list 的时刻。这证明 managed 锁环，**不是正常构造、native 清理或 GPU 生命周期测试**。候选两个后台线程随该无 native 子进程退出，不将此退出记为 native retainedexit PASS。

最小接线条件：保持当前 `VideoDanmakuPanel.xaml.cs:65` 的 `new DanmakuFrostMaster(RootGrid)`（logger=null），且本进程不由其他 Legacy 实例安装重入 logger；诊断日志如需启用只做记录，不同步回调 engine/等待 UI。当前 app 未传 logger，故 R1 不阻断这个限定验收。若要接受任意公开 logger，则 R1 是必须先修的阻断：消除 gate 内外部回调导致的反向取锁，在保持 Close 拒绝迟到提交的同时保留本反例，不能仅把锁移走后丢失原修复，也无需做全引擎 owner 重构。

没有发现本片新增的 XAML/MVVM/API 使用问题；未运行 WinUIAnalyzer/AOT，全矩阵不计通过。暂停首帧允许 Update，rolling/reverse 的 elapsedMs=0，TOP/BOTTOM 初次布局后暂停 tick 累加 FirstRenderTime，恢复后按剩余年龄过期；普通播放 paused=false 保留原步长。没有目标前活跃项重建，rolling 初始边界不据此保证暂停时非空。

## Spec

本片暂停最新意图、Close 拒绝迟到 worker 提交、队列尾项补信号、TOP 暂停首帧/剩余寿命的实现与证据相符。没有发现当前无回调 app 路径的新阻断。直接内部锁顺序是 list→playback→queue；dequeue 释放 queue 后才取 list。Canvas Update/Draw 不取 master list/playback，Clear 的 renderList 锁原版已有；没有从普通路径推导出确定的托管锁环。不能将未知 native/UI 内部等待证明为无环。

**W1 — 原有 Stop 限制，不列新增阻断。** `DequeuedThenStop` 在 worker 出队后 Stop、再放行：两版都把 `_isStopped` 从 true 改回 false，time=20000。候选保留暂停意图，Stop 仍非取消已出队工作/永久退休屏障；Close 才有 closing guard。app 当前不通过这个 Stop 入口终止会话。若后续以 Stop 当 teardown，必须先扩展契约并补验证，不能声称此片已解决。

**W2 — 等待范围须准确。** 原 Close/Stop 已经可能在 Clear 等待 renderList/native disposal；原 Pause 也调用 canvas.Invalidate，但没有等待整个 worker Render 的新 gate。候选使 Pause/Resume/Close 等待已经取得 gate 的 RenderDanmakuItem；这是新增延迟范围，不能全部归为历史问题。静态路径未见默认 logger 下 Render 同步派发 UI；有限延迟/无 GPU hang 不在本次证明中。仅有 lock 或假设 Present 无限挂起，不足以把当前接线机械判失败。

| Requirement | Evidence |
|---|---|
| 集合 alias、同步事件及异常保真 | 原 C1a `597dff0d` fixture 原字节恢复；源码和原候选包各 `Passed: 23, Failed: 0, Skipped: 0`，见 evidence/source-run.log、package-run.log。包含 `Legacy_SetGetAliasPreservesOrderAndEntryEdits`、`Core_ThrowingStopSubscriberLeavesClearedOldList`；未把 Core 集合对照算作 Core native。 |
| 队列与最新 Pause/Resume 意图 | 新增 `QueuedTailAndLatestIntent`：受控出队期间排入 22000/21000，Pause→Resume→Pause；最终 time=21000、queue=0、pauseRequested=true。 |
| Close 迟到提交及退出 | 新增 `CloseRejectsLateCommit`：queue=0、time 保持21000、stopped=true，worker 已退出；另外保留 native `DequeuedSeekThenClose` 与 `CloseThenResume_RemainsClosed`。 |
| Stop 与重入反例 | `DequeuedThenStop` 两版同样重新启动；`StopLoggerCallsSeek` 仅候选死锁，见 boundaries.json 和各原始日志。 |
| 暂停首帧零步/寿命 | 检查 OnCanvasUpdate 674–687；原 native `PausedBackward_NonemptyStableFrame`、`PausedRepeated_NonemptyStableFrame` 与三个 Resume 案例。独立查看四张 BACKWARD/REPEATED 图，候选 TOP 20/21 可见、原版无文本。没有把静态/managed seam 冒充新 native 实测。 |
| 同宿主红绿证据 | 重跑原 verify.py；两轮 Program.cs 与 Probe.cs 完全相同，12 项名称/断言齐全，原源 4 PASS/8 FAIL，候选12 PASS/0 FAIL。原有 official-red、candidate1 失败及 candidate2 未完成均保留。 |
| 包来源/API/PDB | `audit.py` 验证归档45文件、精确两文件 patch、原 nupkg/cache/历史layout清单/运行 Loaded hash+MVID；当前 final layout 实体 hash 也相同。DLL 来自 `.nupkg`，PDB 来自对应 **`.snupkg`**。实际执行 PE/PDB identity 和 Legacy7/Share9 文档 hash 检查；公开 API 211 行原字节一致。 |
| retainedexit/WER | 原 final retained handle、exit0、未强杀、cleanup/yield 记录有效；独立重算退出至 query=32.026301秒，source-red=32.013977秒。Application 可读且匹配为空。此为既存记录核验，本审查未重开桌面/native，不能证明历史 AV 已修复。 |

## 最小下一步

在独立应用身份中仅覆盖本候选 Legacy/Share 包，保留当前 panel/VM/真实播放器和无重入 logger 的接线：暂停后后退、短前进、连续 seek 到最终目标；TOP 静止超过3.5秒仍可见，Resume 后剩余寿命正常；再播放→暂停/恢复、卸载/重挂、关闭，观察 UI 响应及 worker 退出。对目标前活跃弹幕不作本片承诺。固定实际加载 DLL hash，真实 retainedexit + ≥31秒 WER 查询、清理/yield；超时仍记 UNKNOWN_EXIT_NOT_PASS。现阶段不要求再跑完整独立 native harness。

只准入上述隔离验收，不修改正式依赖/gitlink、不发布、不关闭 #49。历史 FAIL/UNKNOWN、未证明的 AV、撤销结论维持原状，未将其他历史 HOLD 累加为本片新阻断。

## 复放与工作区

先执行 `python -B docs/engine-61-design-draft/legacy-paused-seek/prepare.py --name reviewafdc`（新目录一次）；然后顺序执行本目录 `replay.py`、`boundaries.py`、`audit.py`。所有构建在 ignored artifacts，本次最终构建0警告0错误。首次复放取 PDB 时误以为在 nupkg，发现实际位于 snupkg 后修正；首次 audit 比较混合换行失败，已在生成 diff 前标准化换行，源字节 hash 始终独立核验。没有引擎修补或削弱断言。

应用与来源区只读。kernel 初始即异常；当前 HEAD `f02c33f6`、474 条状态、gitlink `0ae5e4e0`，均未操作，见 evidence/provenance.json；**整个工作区不 clean**。无子代理、无 push/PR/issue、无凭据或保护窗口操作、无 dump/内存调查。

两轴汇总：Standards 1 项条件性 Error（重入 logger 锁环）；Spec 当前限定接线新增阻断0项，保留2项明确限制。准入隔离验收不等于通用并发安全或发布通过。
