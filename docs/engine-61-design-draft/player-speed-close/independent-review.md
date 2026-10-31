# 固定候选独立审查

2026-10-31，审查区 `t3-f5ea2a34`。候选 `bb9c45dd37053327f7cd1c1c176757968a3d8296`（生产 `161a47c3873927865a48fb35daf419b6d1b68e8e`），对照 `bd02775bd9a48d7e62c169a6367eec58707dad8e`，包含未合父 `7ac24a44`。一次有界审查，无子代理、生产修改、UI/部署/远端操作。

**结论：调速接纳/排空修复有限通过；推荐抽取 speed 子集，不直接合入整个候选链。** 未发现本次 speed diff 新增的实质阻断。不能据此签署正式 preview6/Legacy seek 全面通过，也不证明历史 runtime5 AV 已修复。

## Standards

未发现新增文档规范违例。八处调用全部走 `ChangeSpeedAsync`，仅保留一处底层 `SetSpeedAsync`；协调遵守既有 UI dispatcher 约定，没有通用调度框架、锁或同步等待。`finally` 在失败/异常时释放计数，异常继续向调用者传播。未执行专用 WinUIAnalyzer、完整构建或 AOT。

## Spec

- `CloseAsync`/直接 `DisposeAsync` 同步关闭接纳；重复关闭/释放共享完成任务。释放顺序为 initialization → speed drain → Player.Dispose（含历史保存）→必要时 client fallback。初始化可以等待 speed，但 speed 不等待初始化或关闭，无新增等待环；底层永久不完成仍会使关闭等待，这不是超时/全部 native 生命周期保证。
- 六个 VM 入口、两个 Overlay 入口均接入。媒体 generation、player/client 身份和 closed 谓词使旧完成返回 false；长按、End 恢复和右键释放的 await 后续据此退出。初始化在旧 client Pause/Stop 前排空已提交写入。
- Overlay tick 在 await 前停止 timer、取走并清空队列，旧完成不再清掉后来的输入；待发请求在换 VM/卸载时撤销。**不等于完整重挂/视图回调有效性覆盖**：谓词只代表 VM 媒体，未代表控件 attachment；`CheckSpeedButtonState` 自身还有既存 300ms delay。
- **既存 P2 边界，非本 diff 新回归**：`OverlayTransportControls.xaml.cs:55-57,74-96`。同一实例构造并 Loaded → Unloaded（两个 Tick 退订）→ Loaded（只 Balance）→ slider 改值 → timer 触发，无 `OnSpeedChangeTick` 订阅，调速不会提交。这是源码可直接执行推导的反例，未冒称真实 WinUI 重挂复现；新建实例可工作。本次 native reopen 是新 VM，不证明同一 Overlay 重挂。父可局部补对称、幂等 Loaded/Unloaded 订阅；不需新框架，不将它升级为本次排空修复阻断。

## 执行和证据

`python docs/engine-61-design-draft/player-speed-close/probe.py independent-review`：exit 0，**Passed: 8, Failed: 0, Skipped: 0**。实际 TRX 已检查；仅替身未用成员警告，控制台中文有编码失真，TRX 不受影响。八项为 CloseWaitsForAcceptedSpeedBeforeDisposal、ClosingAndClosedPlayersRejectSpeed、RepeatedCloseJoinsAndDrainsEverySpeed、SpeedFaultPropagatesAndStillReleasesClose、DirectDisposalAlsoRejectsAndDrainsSpeed、OldCompletionCannotReportSuccessForNewMediaOrClient（两分区）、ReopenedPlayerIsIndependentOfOldClose。它们未编译真实 Overlay 或完整初始化；初始化等待图另作源码审查。

`python docs/engine-61-design-draft/player-speed-close/independent-verify.py`：exit 0。核对 96 个归档文件长度/hash、red 的 1 个预期失败、final2 的 8/0、suite 原始 TRX 的 **563/0**，没有重跑全套。核对 48 个生产文件和归档 source/PDB checksum；Speed.cs 当前 checkout 为 CRLF，归档 hash 与固定候选 git blob 的 LF 字节精确一致，仅换行差异。第一次严格 checkout hash 检查因此停止，未清理或改写源文件。归档不含 DLL/PDB 二进制，本次不冒称重新对实际加载模块取证。

两个 native 归档均有 pending=20、完成/释放、重开 initialized、panel=false、正常 exit 0 和退出后 ≥31 秒准确 PID/path WER 无匹配。runtime2：11:26:14 的普通加速命令 1.25 为 Playing；11:26:51 快捷按钮 1.5 和 11:27:01 slider 1.75 的生效记录均为 **End**。11:27:15 的 1.75/Playing 是重开后沿用设置，不能证明 slider 在 Playing 下操作。用户交接中的“普通按钮/slider 在 Playing 实测”未被这些事件支持，须缩窄该声明。两轮快速20次关闭前最新状态均为 **End**。`panel=false` 对应弹幕 panel，不是 Overlay 同实例重挂证据。native 结果是核验已有记录，并非本审查再次执行。实验 Core 与正式 preview6 不同，正式 Legacy 暂停 seek P1 保留；旧 runtime5 AV 根因仍未知。

## 给父的可执行集成边界

1. 从 `bd02775b` 应用 **`git diff 7ac24a44 161a47c3 -- src`** 的五个文件改动：OverlayTransportControls、PlayerViewModel.Commands、Methods、cs、Speed.cs。临时独立 index 上 `git apply --cached --check` 已成功，不曾修改真实 index/生产树。
2. 再只复制候选 `PlayerViewModel.Playback.cs:12-21` 的 `CapturePlaybackValidity()` 到 Speed partial。父已有 `_danmakuPlaybackVersion`、初始化递增和 `_isDanmakuClosed`；缺的只是此 helper。不要整份复制 Playback.cs：它同时含新的 ChangePositionAsync 和弹幕回调拆分。故 **161a47c3 原样 cherry-pick 并非自足可编译提交**，但 speed 修复可独立抽取。抽取后的编译/针对性 probe 由父执行，本审查未冒称验证了一个尚未生成的集成提交。
3. 此子集不包含 VideoDanmakuPanel Seek/Redraw、Danmaku SeekRequested、SponsorBlock/章节 seek 路由及 seek 测试变更，不触碰正式依赖/gitlink。可保留本目录证据，调整 probe 的 helper 提取位置即可。
4. 原 gesture/pointer 链在源码层面已有单 owner、press-time generation、取消清理和 release-before-await 防护，本次未找到新反例；既有 owner 8 项证据仍是替身，未变成真实输入验证。若父希望同批带入，须单独拆出 PlayerInteractivePanel 的交互改动及 helper，并确认其调用的父 ChangePositionAsync 语义；不能以 speed 验收批准整个 `bd02775b..bb9c45dd`（其中仍有正式 Legacy 暂停 seek 的实质 P1）。推荐本次仅 speed，小修后由父安排集成；历史 G1/AOT 等无关 HOLD 不机械阻断此子集。

本区 kernel 初始 HEAD `f02c33f639addd5d421700c43fa66382beafe323`，与 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b` 不符且已有删除项；全程原状保留。父区/来源区没有写入。阅读 AGENTS、指定五项技能并采用直接审查，verification-before-completion 用于交付核验。

Standards：0 新发现；Spec：0 新增代码阻断，1 个既存 P2 重挂缺口，1 处实机覆盖声明纠正。有限通过仅适用于上述 speed 子集及明确覆盖范围。
