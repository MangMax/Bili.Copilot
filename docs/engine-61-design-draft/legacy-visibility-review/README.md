# 暂停重挂缺字：原 PNG 已有 TOP20，修正验收判读

2026-10-31，基于父 `979eeae28ff6c6009d3627dc5bccd25052e2b8d6`（已含独立评审 `14e7e11d`）。本次没有生产修复：所指定的两轮“缺字”红结论与原始截图像素矛盾。问题定位到 **PNG 保存之后的图片呈现/判读环节**，不是这些截图时刻的引擎绘制、应用合成或屏幕采集缺失。未确定图片展示工具内部的具体算法，不据此声称修好了工具本身。

新隔离完整应用实机三轮复验通过，`Passed: 22, Failed: 0, Skipped: 0`。这只解决本次暂停重挂视觉阻断的证据纠错，**不代表 #49/#61 或完整生命周期整体签绿**。

## 可直接复算的因果证据

先读了 [原独立评审](../legacy-early-close/independent-review/README.md)、audit.py、evidence.zip 与原 prepare/UI/static 脚本，复算其204文件通过。原证据完全未改。

1. 原 `screen-remount1-stable.png` 被判通过，`screen-remount2-stable.png` 被判失败，但两张整图逐字节相同，SHA-256 都是 `19acb1667fa18685f3caf60fc2b30f7bc0df78d2f9583f794edecd7b5b02687e`。同一个 PNG 不可能同时证明“可见”和“不可见”。
2. 对原1680×1110截图的 `(770,18,140,40)` 矩形解码 RGB，该区域就是完整 TOP20 字样。原三轮首次/稳定、baseline、backward 以及 static0/1/2 共11张该区域完全相同：`497428bf56e905ceab6926eadd25e517fb40c26f3eddd69f53fd2a9c31ca6306`。原 forward 图是 TOP21，正确得到另一份像素。
3. System.Drawing 与独立 Python PNG 解码（zlib、PNG逐行滤波，不依赖图片查看工具）对全部旧12张和新18张屏幕图给出相同 RGB 哈希。空白像素负例与 TOP21 都不能通过 TOP20 等值检查。
4. 本轮查看原图也遇到重复大图呈现中整组文字/按钮看似消失；单独查看和原像素局部拼图能看到字。此现象只能支持“查看/判读链不能作为唯一依据”，不能反向覆盖文件字节。原报告所写 static0/1/2 无/无/有，也被三张完全相同的字样像素否定。

因此无需靠 rows、Draw次数或偶然新截图推断旧失败的原因。字样已存在于原 PNG，说明旧指定样本的绘制、合成及采集结果包含字样；截图时序和其他窗口覆盖不能解释“同一份 PNG 被给出相反结论”。不增加引擎观测字段，不套 Invalidate 补丁。OnCanvasUpdate 在逻辑暂停时继续 ticking 的既有行为没有修改。

## 最小夹具修正与新运行

复用 `t3-90c5efdd/artifacts/early-review-90c5` 脚本，在本 worktree 的 `artifacts/remount-6273` 建立全新副本。生产应用固定 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`；kernel只读归档 `0ae5e4e` 到 artifacts。901个生产 C#/XAML 文件保持一致，仅既有 App.xaml.cs / GlobalDependencies.cs 离线启动注入，额外 OfflineAcceptance 源不变。`source-closure.json` 保存全部903文件归一化哈希。

引擎从校验过的上游 archive 恢复，使用 legacy-ready master 与 legacy-early-close render，未再修改。重新打唯一隔离 Legacy/Share 包 `0.0.1-preview6.remount6273.clean`；nupkg DLL、实际应用 build DLL 与启动 layout DLL逐字节相同。Core仍正式preview6，亦核对实际DLL一致。正式应用依赖、gitlink均未改。

夹具仅增加：

- 截图前后状态及精确 owned PID/HWND 窗口枚举；仍用原 `winapp ui screenshot --capture-screen`。每个截图只执行一次，不筛图、不补 Seek/Redraw/Resume。
- 每轮卸载后的空白对照；重挂首次图、延迟3秒稳定图、再间隔1秒的两张静止图。状态同时证明实例不同且一直 Paused / 20秒。
- 原始PNG之外输出未缩放局部与 RGB 哈希；结果脚本从实际像素生成结论，删除旧脚本 `i==1` 这种人工写死的视觉结果方式。固定几何不符即失败，不做猜测性缩放/寻找最佳帧。

首次窗口枚举发生在播放器创建前，StopIteration、空窗口列表及原命令保存在 `startup-*`。只修为等待精确播放器标题出现，随后完整矩阵一次完成。像素脚本最初扫到了尺寸不同的旧普通窗口截图，按设计拒绝；改为仅检查本任务的 screen/static 图，未修改图像内容。

Release x64 JIT build exit0：0错误，1条既有 protobuf unused import warning。未运行专用WinUIAnalyzer、AOT、性能或全量.NET测试；这是定向WinUI/native夹具与离线像素审计。

| Requirement | Evidence |
|---|---|
| 原红结论与原图的矛盾可重复证明 | `OriginalVerdictContradictedByPixels`；原 results.json 11 PASS / 2 FAIL 保留，原两个等字节PNG |
| 独立像素解码和空白负例 | `IndependentDecodersAgree`、`BlankNegativeRejected`；30张PNG，两套解码一致 |
| 三轮暂停重挂状态 | `PausedRemountStateRound1/2/3`、`DistinctMasters`；每轮四个固定样本，master分别46779618、59945944、12716079（以JSON为准） |
| 三轮稳定可见 | `PausedRemountVisibleRound1/2/3`；三轮共12张的TOP20区域均与旧基准逐字节相同 |
| 采集响应真实卸载，非缓存图 | `UnloadNegativeRound1/2/3`；三张目标区域全黑，随后新实例重新出现字样 |
| 正确前台窗口与短跳内容 | `CapturePlayerForeground`、`ActualButtonforward1`、`ActualButtonbackward1`；PID34476 / HWND1380696，TOP21→TOP20 |
| Close及后续pump | `CloseFullDisposes`、`NoPublicationAfterClose`、`NoHarnessExceptions`；关闭后约2秒再ExitOwned |
| 退出、事件日志、自有清理 | `NormalExit`、`WERQuietAfter31Seconds`、`OwnedCleanupAndYield`；retained exit0，非forced，31.0243124秒后WER无候选，包0/进程无/yield exit0 |

18张新原图逐张打开，再检查 `glyph-contact.png` 的18个原像素局部，三轮卸载空白、TOP20重挂和TOP21短跳均一致。局部拼图是对保存PNG的诊断派生物，原PNG没有编辑。固定矩形只针对本夹具窗口与字体，不能当作通用OCR或任意分辨率视觉测试。

## 复放、审计和边界

`evidence.zip` 路径相对 `artifacts/remount-6273`，包括脚本、实际引擎源和包、构建日志、源码闭包哈希、连续事件、全部18张原图、局部、结果、退出/WER/清理。源脚本另放 `source/`。`python audit.py` 从本证据包及原独立评审包重新解码原PNG，检查manifest及状态/结果，不启动桌面。它会保留并核实旧11/2红结果，再证明其指定视觉结论与像素矛盾，而非重写旧结果。

重跑UI应复制source脚本到新 artifacts 目录并更换输出根、workflow和包身份（现脚本保留实际运行值供审计），先prepare-clean.py clean，再prepare-app.py，launch-retain.ps1持续持有句柄，ui-app-screen.py matrix/close；随后31秒WER和精确身份卸载/yield。固定来源archive、media/libmpv来源仍依原脚本，只读使用，未封存庞大应用layout。check-pixels.ps1要求新输出目录；app-results.py需要原证据解包为prior以及两次像素报告。

WinUI代码自审：无生产C#/XAML/API/线程/释放改动；夹具按owned PID/标题选窗，几何不符失败，无截图重试洗绿、无全局窗口或进程清理。使用指定implement-spec、systematic-debugging、code-testing/run-tests、winui-code-review、winui-ui-testing、verification技能；按用户约束无代理、push、PR、issue写入。run-tests overlay不存在。

kernel初始 `f02c33f` / 474项差异的HEAD、status、binary staged/worktree patch已先备份，结束逐字节比较保持一致；gitlink仍`0ae5e4e`。未恢复异常submodule，未写父或来源worktree，未触碰Caa6a7be/Dev/其他实例、凭据或layout。未抓dump、外部内存、逆向。

原AV/c000027b/UNKNOWN继续保留；早Close 5/5不重跑。device-reset、并发换列表、目标前全集、Core完整矩阵、AOT/perf继续不在本次绿范围。
