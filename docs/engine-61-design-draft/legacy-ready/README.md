# Legacy 未就绪暂停目标保留候选（验收未完成）

本区基于父 `e8143389f8765e25456003213d3eb2f4b3dde061`。源码起点是父 `legacy-logger-fix/source` 两文件，恢复原45文件时按 `legacy-paused-seek/origin.json` 校验。未改父、来源、正式依赖或 gitlink；未 push/PR/issue，未启用子代理。

**已确认未ready消费游标的根因，并形成最小两文件候选；尚不能交付为完整验收通过。** 正常构造矩阵在 ready 前 Close 阶段发生 Win2D 原生 AV，按用户“出现异常保留并停止扩展”的边界停止。完整应用已构建，但三轮 x:Load 重挂、短 seek 及完整应用退出均未运行。没有把未完成项换成“准入下一片”，也没有声称整片完成。

## 因果红绿与实现

实验使用真实 `new DanmakuFrostMaster(scene)`、真实 CanvasAnimatedControl 与 Win2D。仅实验包增加 CreateResources 屏障和 worker/提交观测信号；没有替换构造、伪造 device 或通过反射写游标。屏障在 worker 尝试处理目标后释放，反射仅读取 device/index/rows。100ms与600ms用于实验观测，不在应用或干净候选中。

| 同一个20000暂停目标 | ready前 | ready后 |
|---|---|---|
| 原 logger 修复源码（red） | device=false、index=1、paused=true、rows=[] | device=true、index=1、paused=true、rows=[] |
| 本候选（green） | device=false、index=0、paused=true、rows=[] | device=true、index=1、paused=true、TOP20 drawable=true |

红灯证据证明至少这个真实构造路径中，`RenderDanmakuItem` 因 `_device==null` 没有提交，worker仍推进 `_lastIndex`；随后 CreateResources 没有补处理。同目标无需再次 Seek/UpdateTime/redraw 即由绿灯自动出现。它不证明历史所有空帧只有这一种原因。

生产改动只有 [DanmakuFrostMaster.cs](source/DanmakuFrostMaster.cs) 和 [DanmakuRender.cs](source/DanmakuRender.cs)：

- 内部 volatile ready 位在 device/maxsize 初始化后发布，内部事件只唤醒已有 `_updateEvent`。
- 未ready时 UpdateTime 队列只保留最新目标；worker未ready不出队、不消耗游标。ready后由原worker处理。
- 出队放入既有 list 锁，使新 Seek 清除队列/改游标后，旧已取目标不能再迟到覆盖。未增加 owner、通用调度框架或应用延时重试。
- CreateResources 的已有外部 logger 回调后检查 canvas 是否已关闭；事件本身不恢复播放。worker原 `_isClosing` 检查拒绝发布。
- RenderDanmakuItem 的返回语义、density/unknown/unsupported过滤与正常消耗保持原样；没有把过滤当作可重试项。Pause/Close/logger修复、公共API与集合别名保留。目标前全集重建不在本片。

## 实测与失败

`prepare.py red/green` 为单一因果实验，`prepare.py matrix` 为真实构造矩阵。对应执行命令为 `& artifacts/legacy-ready/<phase>/run.ps1 -RunName <run>`。实际执行源码和脚本随每次证据封存，避免后续脚本变化改写历史。

| Requirement | Evidence |
|---|---|
| 未ready暂停目标→ready后同目标可见 | `NotReadyPausedTargetVisibleAfterReady`：red Passed:0, Failed:1, Skipped:0；green Passed:1, Failed:0, Skipped:0。真实图已逐张查看，red为空、green为TOP20。 |
| ready前多次目标只最新 | matrix/native2 的 `BeforeReadyOnlyLatestTarget`：20→21→22，ready前index2，ready后index3且仅TOP22。 |
| 暂停继续保持 | `PauseKeepsTargetFrame`：3.3秒后rows完全一致、保持暂停。 |
| 已ready普通播放/Resume | `ReadyPlaybackAndResume`：Resume+21000出现TOP21；`ResumedItemsExpire`：恢复后正常到期。 |
| ready前Close不发布 | **未通过验收**：`BeforeReadyCloseNeverPublishes` 未到断言，正常构造后、ready前Close阶段发生AV，不能拿托管state测试替代。 |
| 旧23项集合/API兼容 | clean/compat-results：23 passed；public-api.txt与原基线逐字节一致。涵盖Core原包与Legacy候选的既定集合行为，不称23项均为Legacy。 |
| logger重入/异常及队列/Close | `pack-verify.py` 七个原边界命令 exit0；沿用原Boundary，仅为原“已ready”托管空列表场景设 `_resourcesReady=true`。该测试使用未初始化对象，不能替代真实构造/native关闭验收。 |
| 完整应用三轮暂停重挂、实际短seek | **未运行**，AV后停止扩展。不能把隔离TOP20图当作应用重挂图。 |

matrix/native首次因初始状态和视觉停点都叫READY，Continue过早发生，随后wait-for超时。进程exit0、WER空；保留原记录。仅把测试初始状态改为IDLE后重跑native2，没有改生产代码追绿。native那张TOP20截图已查看，但不作为完整矩阵通过证据。

native2已完成 **Passed:5, Failed:0, Skipped:0，executionFailed=true**；缺少第六项及SuiteCompleted，绝非6/6通过。PID42928，retained exit `-1073741819 / 0xC0000005`，forced=false；32.0129891秒后Application Error1000精确PID/path匹配 `Microsoft.Graphics.Canvas.dll 1.4.0.0`、offset `0x52cc8`。只记录观察，不据此归因候选、屏障或Win2D，也未新增退出调查、dump、内存读取或改Unhandled/logging。原 `c000027b` 与历史AV继续未证；本次不是修复或洗绿原失败。

red PID10092与green PID9868均retained exit0，32秒后WER无匹配。四次实验包均已移除（packagesRemaining=0）、进程退出、desktop yield。未触Caa6a7be/Dev、其他窗口、登录或凭据。

## 真实包与完整应用构建

干净候选 `0.0.1-preview6.ready06c7.1`（Legacy+Share）由 `pack-verify.py` 独立打包；不包含 ProbeGate/ProbeAttempt。nupkg/snupkg、45文件来源校验、构建/兼容日志及哈希封存在 evidence.zip。实验red/green/matrix各有独立包版本，不能混称干净候选的native消费。

`prepare-app.py` 固定归档应用 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`，kernel固定0ae5e4e，使用自有包身份 `Bili.Ready06c7` 与本区Root，真实PackageReference消费干净候选；Core仍正式preview6。Release/x64/JIT build exit0、0错误、1条既有protobuf warning。901个生产C#/XAML文件与应用候选去BOM/换行后相同，仅App.xaml.cs/GlobalDependencies.cs为既定离线启动注入；父speed/Overlay随固定候选保留。Legacy/Share的nupkg DLL与应用构建输出逐字节一致，见 validation.json。**该应用从未启动，因此没有进程加载/实际三轮重挂/完整应用退出结论。**

kernel初始索引删除异常已保存status、HEAD、binary staged/worktree patch；只读来源导入对象后仅本区detach恢复0ae5e4e。父最新本地tip仍e8143389，是本分支祖先。正式应用源码和依赖未切换，应用候选也未合父。

## 自审与复核

WinUI自审：没有新XAML/MVVM/API，未添加同步UI等待至生产源；ready处理复用worker，锁次序维持list→playback/queue。资源事件只发信号，不能越过Close发布。集合实现未变。原生Close实测失败使静态推理不足以判绿。未运行WinUI专用Analyzer或AOT验证。

使用指定implement-spec、systematic-debugging、tdd、code-testing、run-tests、WinUI审查/UI-testing及verification-before-completion；按用户约束不使用implement-spec的子代理/PR流程。本轮是编译运行自定义原生/托管fixture，不是dotnet test/TRX；run-tests overlay不存在。

只读核验：`python docs/engine-61-design-draft/legacy-ready/audit.py`。它校验122个封存文件、红绿状态、原AV和不完整矩阵、退出/WER及清理。exit0仅表示证据一致，**不是完整验收通过**。所有四张实际截图已查看。
