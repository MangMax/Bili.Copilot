# a97d4024 独立复核及完整应用隔离验收

**Logger 局部修复可采用；完整应用验收 FAIL，不准正式依赖升级/发布。** 2026-10-31，父 `feat/performance-45` = `a97d4024d5edc55d6fcf0aad0cb4d04b42ff43fe`，本分支直接基于该提交。无子代理、生产改动、push/PR/issue、父区写入、认证操作、dump 或进程内存调查。

## Standards / Spec review

本片新增实质阻断 **0**。master Stop/Close 在 playback gate 内完成状态/队列/renderer 操作，外部日志移到 gate 外；renderer public Stop()/Close() 保留，internal bool 重载不扩大公开 API。原 list→playback 顺序下，Stop/Close 的直接日志回调重新 Seek 不再组成原锁环。Close 先设置 closing、清队列并通知 worker，再关闭 renderer；回调 Seek 仍会取得 list，但随后被 closing guard 拒绝。没有移走必要的提交互斥。

重新编译 [Boundary.cs](Boundary.cs)，没有使用父旧 Boundary binary。原反射 managed-state seam 保留：真实程序集与方法、空列表/null canvas，不把它叫 native 或正常构造测试。新包上 `StopLoggerCallsSeek`、`CloseLoggerCallsSeek` 均 completed=true；后者还断言 time=21000、stopped=true、queue=0、worker退出。`throwstop` / `throwclose` 验证 **同一异常对象**传播，已提交 stopped/queue/closing 状态保留。原 `QueuedTailAndLatestIntent`、`CloseRejectsLateCommit` 通过。`DequeuedThenStop` 仍 stopped=false/time=20000，明确保留原 Stop 不取消已出队 seek 的限制。

本结论不保证所有 ILogger 调用均已移到锁外：Seek 和 worker Render 等日志路径未被本片重构；任意跨线程回调、UI 同步等待和全部 native 生命周期不在此证明内。未新增 owner 架构。专用 WinUIAnalyzer/AOT 未运行；完整应用正常构建分析器已执行。

## 新包与来源闭合

由 legacy-paused-seek/origin.json 固定归档恢复 **45 个文件并逐字节校验**，依次覆盖 legacy-paused-seek/source 两文件、legacy-logger-fix/source 两文件。最终仅两个文件不同于原归档，且与父 logger-fix 覆盖源字节一致。指定的 source/replay.py 实际不存在，读取的是 logger-fix/replay.py；未执行它的来源区写入/旧 binary 复用逻辑。

实际消费 `Danmaku.Legacy/Share 0.0.1-preview6.loggerreview4e91.1`，仅本地 feed。第一次版本 `0.0.1-loggerreview4e91.1` 被 Core preview6 的 Share 最小版本阻止（NU1605），原包保留，使用新名称重新打包，未抑制降级检查。正式 Core 仍 preview6，未复制实验 Core/7ac 链。

固定 `597dff0d` C1a 原 fixture 字节，新编译并装入实际 nupkg DLL、对应 snupkg PDB：**Passed: 23, Failed: 0, Skipped: 0**；211 行公开 API 原字节一致；Legacy 7 / Share 9 PDB 文档验证执行。Core 是集合对照，不是 Core native 测试。Boundary 全部从本区重新编译，最终日志 `*-delivery.log`。

Legacy DLL SHA256 `7d6e49171fe084e9787cf40d31166638ee3fa7e2e362bfafa4d64b2e13fd45c1`，MVID `64aa13b4-48a3-45a7-85a1-db6bb84a2b91`。nupkg 条目、NuGet cache、完整应用 build、layout 与实际 PID34652 加载路径/hash 相等，见 [validation.json](validation.json)。共享 NuGet cache，未声称干净机器恢复。

## 完整应用实测

当前父全部 src 的离线副本，指定 kernel `0ae5e4e0` 从只读来源仓库归档到本区；仅实验启动/DI/合成离线服务/身份/包版本不同。真实 PlayerViewModel、VideoDanmakuPanel、ChapterSegmentBar、Mpv 播放器均保留。900 个生产 C#/XAML 归一换行字节一致；关键 panel/VM/Core 控件源 hash 还核对本次 consumer PDB，PE/PDB及实际 loaded应用DLL匹配。合成每秒 TOP、本地600秒媒体，无真实账号/网络服务。

相关完整应用 Release/x64/JIT 构建 `full-build-2.log`：**exit 0，0 warnings，0 errors**。未重跑539，也未将此称正式包/AOT构建。独立身份 `Bili.LoggerReview4e91`，独占授权 local，PID34652，所有路径在本区 artifacts/legacy-logger-review4e91/full。

| Requirement | Evidence |
|---|---|
| Playing → Paused | 初始真实媒体 Playing；控制器调用真实 `PlayPauseCommand`，24.458秒变Paused，VM/renderer暂停一致。后续播放/暂停直接 UIA invoke 原 `PlayPauseButton`。 |
| 暂停后退 | `ChangePositionAsync(20)`：24.458→20，renderer time=20000、pause=true，TOP20有首帧。paused-backward.png / paused-stable.png 相隔约22秒仍可见；条目坐标保持。属于命令层。 |
| 用户进度条 | 原 ChapterSegmentBar 没有 UIA RangeValue peer；按自有窗口截图坐标实际 press/release：20→26.375，Paused，engine time=26000。坐标原记录 progress-click25.json，文件名25不是实际目标断言。 |
| 1秒短跳 | **BLOCKED（现有接线）**。Back1、Forward1、ChangePositionAsync(21) 均停20秒；真实条上小位移也停26.375。VM `ChangePositionAsync` 和 ChapterSegmentBar `MinSeekDelta` 都拒绝 <2秒。未绕过生产入口直调引擎伪造通过。 |
| 可接受前跳 | 命令20→25保持Paused，TOP21–25可见，随后前台屏幕仍有这些条目；原算法积累20–25，不声称精确重建目标前活动集。 |
| 连续seek最终目标 | Repeated调用28→24→20，最终播放器20、engine20000、paused=true。第一轮28被26.375的最小差值门槛拒绝；第二轮从20开始三目标均可接受，最终仍20。不是三个请求都获得独立native完成的证明。 |
| 暂停稳定重绘 | **未获一致通过**。连续seek日志持续TOP20/drawable=true；部分截图为空，前台实际屏幕 repeated-screen.png有TOP20，13秒后 repeated-screen-stable.png无文本，行记录仍在。ShowControls附近也出现截图有/无文本变化。保留原图，未把逻辑行存在算视觉PASS；渲染/宿主合成归因未证明。 |
| 恢复剩余寿命 | 原PlayPauseButton恢复后TOP20仍可见（resume-remaining.png）；约3秒后日志移除TOP20，resume-expired.png仅后续TOP23–25。暂停年龄未直接耗尽；这是该TOP路径有界观察，不泛化全部模式。 |
| 卸载重挂 | IsDanmakuControlVisible false→true，实际x:Load卸载/新建，master32065947→65756507。UI继续响应；**暂停25.458重挂后rows=[]且截图为空**，直到之后显式seek恢复内容，不签“暂停原画面重建”通过。 |
| 关闭与退出 | CloseFull实际CloseAsync返回 disposed=true/remaining=0，UI仍可执行ExitOwned。但retained exit **-1073741189 (0xC000027B)**、forced=false。退出后31.0246307秒，精确PID/path WER匹配Application Error1000，CoreMessagingXP.dll 12.6.4.0/c000027b。**FAIL**，不是正常退出；不据此断言本片引入或修复历史AV。 |

所有截图逐张查看，包含原空图与补充真实屏幕图。工具首次启动未ready空树、脚本Path序列化错误保留；没有绕过前台保护。未改生产接线来追绿。未开启引擎独立native宿主冒充完整应用。

## 清理、复放及保留项

owned包0、PID不存在；已调用winapp ui yield，返回released=false（执行间隔后没有释放到活动租约，不伪记true）。WER和retained exit均保留。kernel初始f02c33f6/474条异常先备份index/worktree diff及untracked ZIP，**完全未修改子模块**；audit逐字节复核初始状态。父正式src/依赖/gitlink未变，整工作区仍因初始kernel异常不clean。

本区根执行 `python -B docs/engine-61-design-draft/legacy-logger-review/replay.py` 可在新的空 artifacts 路径恢复/打包/重编测试；`prepare-app.py` 恢复并完整构建离线应用；`launch-retain.ps1` 仅获local授权时运行。ui-batch.py记录本轮分段续跑，HWND必须用新运行的自有窗口替换，不能盲复用。`audit.py`核验本次证据且**期望保留native FAIL**，并非产品通过测试。

本轮工具修正另保留：fixture输出目录未创建；日志自动加前缀导致最初throw断言未触发，改为原反例同样的Contains后才验证；来源mpv路径修正；实验观察器遗留unused owners导致CA1823，删除未用代码后完整构建通过。没有降低测试断言/编译检查。证据zip不包含凭据、native包或dump；本地feed/构建产物保留供复核。

可采用：两文件logger修复、独立managed回归、包来源及限定暂停状态/寿命证据。仍阻断整票：实际异常退出、暂停视觉稳定性未一致证明、暂停重挂空画面与1秒跳转入口限制。目标前活跃项重建继续排除在Legacy小片声明外。原FAIL/UNKNOWN、历史AV、#49、AOT和其他既有HOLD全部保持，不以本片局部green抵销。
