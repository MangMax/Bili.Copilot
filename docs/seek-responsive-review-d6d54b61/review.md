# d6d54b61 独立聚焦 review

**结论：本次源码 review 无新增阻断，可以将面板补丁与 Core/Legacy/Share `0.0.1-seekd1fc.20261101.2` 整体交父任务接入。** 原 c00a4c2b 的两个反例在旧源码复现，在新源码消失。这个结论不代表完整 AOT 或真实设备故障注入已通过；正式接入后的实测仍由父任务完成。

固定候选 `d6d54b6104a3dae961d06bf2aa112c6baa002af2`，父 `0d3a22f61575550e23f935028ec5f6376cac22ce`。Legacy/Share 为 `15434adfee3c80032ca12ab4840bc36f54858f65`，Core 为未改变的 `e094d330a69dbedeba9b0260b6bb499391d643c4`。读取来源 README，随后将固定提交归档、bundle 和证据展开到本审查区 artifacts；不运行来源区中的写入脚本。

## Standards

0 项阻断。已读 AGENTS、code-review、winui-code-review、code-testing、run-tests、verification-before-completion 及仓库 domain 指引和 editorconfig。按用户要求独立完成，没有采用技能建议的子代理并行。没有新跑 WinUI analyzer 或完整 AOT；不把未做完整 AOT 作为这次源码 review 的阻断。未新增架构、双缓冲、依赖或生产测试改动。

## Spec

0 项阻断；原两项已闭合。

- **列表/版本快照**：master 458–473 行在同一短队列锁中获取列表引用、目标、版本、seek 标记；等待 list monitor 之后再检查版本。Set 在 playback→queue 顺序中退休并发布新列表，旧请求不能用新版本证明旧列表有效。Stop/Close 的 queue 临界区结束后才取 playback，不形成 queue→playback 的嵌套反向锁。worker 为 list→playback→queue；内部无持 playback 再取 list 的路径。Set/Clear 不等待整个列表扫描；仍可能等待单条原生操作。
- **故障终态**：RenderSafely 在释放 playback 前保存首个异常、退休版本并清空队列。已经排队的新 seek 同样退休。后续 Seek/UpdateTime/Resume/列表及资源入口明确拒绝，并保留原始 InnerException。Start 成功才写 ready 时钟。Close 可继续最终清理，不自动恢复 worker 或重用失败资源。这里验证的是 master 的资源入口隔离，不把已有 canvas 内部异常处理改称全设备资源恢复保证。
- **原语义**：Set 返回同一外部 list 别名；Clear 发布空列表，不清空外部旧列表。AddList 保留原实现“仅留下 incoming 中 ID 不在旧列表的项”的过滤替换语义，没有擅自改成 union；现在经过同一退休协议，会清旧场景，再由下一时钟/seek 重建，符合用户允许清旧再显新。没有加入外部事件通知；ResourcesCreated 仍仅唤醒 worker。日志不在 playback 内调用；独立日志重入 Set+Seek 也不会提交旧场景。

### 应用调用边界说明（非阻断）

终态拒绝是可观察的异常，不是自动恢复。`VideoDanmakuPanel.xaml.cs` 的 Seek/UpdateTime、样式、隐藏 Stop 和 redraw 回调没有局部 catch；异常可沿事件回到调用方。`PlayerViewModel.Playback.cs:87` 的 seek 失败会使 ChangePositionAsync fault，其 finally 仍释放队列；不再静默等候死 worker。`App.xaml.cs:182–185` 有 XAML UnhandledException 日志和 `e.Handled = true`，因此不能仅凭面板没有 catch 就断言必然未处理崩溃。

本轮没有发现能够证明必然进程终止的新调用链，也没有运行真实 UI 故障注入，不能将 XAML handler 推广为所有后台线程、ABI 或 native fault 的保证。父接入实测应保留终态后进度/seek/隐藏/卸载的观察；若希望故障时只禁用弹幕并一次提示，可在面板边界做有限处理，不应盲目重试或复用失败实例。当前可能反复记录拒绝异常、需要关闭实例后替换，属于所选显式终态策略的限制。

## 独立验证

本区执行：

```powershell
python -X utf8 artifacts/review-d6d54b/docs/seek-responsive-fix/replay.py artifacts/review-d6d54b/replay
python -X utf8 artifacts/review-d6d54b/docs/seek-responsive-fix/verify.py
dotnet run --project artifacts/review-d6d54b/replay/probe/Probe.csproj
dotnet run --project artifacts/review-d6d54b/docs/seek-responsive-fix/audit-tool/Audit.csproj -- artifacts/review-d6d54b/closure
python -X utf8 artifacts/review-d6d54b/audit.py
```

原探针红：9 项失败，包含 `expected NEW; actual OLD` 及故障后接受请求；预期退出 1。原样绿：13/13，退出 0。增加五个窄边界后共 **Passed: 18, Failed: 0, Skipped: 0**，退出 0。替身 ILogger 与环境导入引用有 CS0436 警告，编译选择本地替身；不是产品构建告警，也未隐藏它。

| Requirement | Evidence |
|---|---|
| 两个旧阻断红绿复放 | `ListReplacementLatestSeek`；`FailureThenLatestSeek_Stop/Pause/Item/Start`，原样 red.txt/green.txt |
| 退休、准备前替换、暂停、逐项取消、参数提前拒绝 | 原探针其余 8 项；完整名称见 green.txt |
| Set/Clear 别名、AddList 过滤 | `SetClearAddListAliasAndFiltering` |
| 替换后普通时钟重建 | `SetThenFirstClockReconstructs` |
| AddList 退休捕获中的旧请求 | `AddListRetiresBlockedSnapshot` |
| 故障后代表性资源入口拒绝、原因稳定 | `TerminalRejectsEveryResourceEntryAndKeepsCause`（名称中的 Every 不代表穷举所有 setter） |
| 外部日志重入 Set+Seek | `LoggerReentryRetiresCapturedScene` |
| 已有相关回归归档 | 44 项 TRX 全通过，名称集合与原 review TRX 逐项相同；本轮未重跑 44 项 |
| 原生归档闭包 | 15 项 assert 全通过；最终 loaded-modules 与新三包 SHA 全相等；本轮未新启动 native |

独立读取三包 DLL 和 snupkg PDB，核对 CodeView/PDB GUID、全部 PDB 文档 SHA。审核器改为读取本区 snapshot 文档，避免依赖 PDB 内原绝对路径；37 个非生成文档再与 bundle HEAD 源码比较（允许 checkout 换行差异）。两个 bundle 的 HEAD、相对基线 patch 逐字节核验通过；Legacy 相对 `221bbb019` 仅 master 改变。生成文档按 PDB hash 核验。未宣称重建二进制可重复哈希。

manifest/六包哈希、同版 Share dependency、最终运行模块哈希均通过。Core 仍是原源码，只换新包版本；不覆盖 `.5`。DLL SHA256：

```text
Core   90671b115567f9ccb5c5a758fec4b479df24f073f5e6dcceb4e138c80d36ac4d
Legacy 27d9ff048134adbe1d5b977596c7700ca511e0c47aea20b098352ff9a56532fa
Share  7586640e484b4582b2369679be08339c3656dc25cb645d96e6c8bd80121a2027
```

## 保留的性能与运行限制

原已审性能证据支持 Legacy 去掉同步历史重建对 UI 的耦合，面板隐藏门禁继续有效。新密集样本的 UI 提交和媒体确认→进度通知很短，但 native ready 仍约 2.35 秒；它们不是同一终点，ready 也不是光学显示。Core 原短密集样本未提速，不宣称全部场景性能收益。长时间轴是弹幕数据，实际媒体仍 60 秒；单项极长原生准备及 Close 清理也不保证所有设备低延迟。

未启动任何 native 或用户应用，不触碰用户 AotDev/Caa6a7be/Dev/官方实例和凭据。归档最终运行 retained exit=0、非 forced，精确 PID/path 的 WER 查询在退出后 31.0239 秒，零匹配，清理剩余包数为 0；仅描述已交付证据，不冒充本轮新运行。

最终只读核对父工作区仍为 `0d3a22f61575550e23f935028ec5f6376cac22ce`，来源仍为固定 `d6d54b6104a3dae961d06bf2aa112c6baa002af2`。本区 kernel 仍为 `f02c33f639addd5d421700c43fa66382beafe323`，保留原 474 项 staged deletion。未写父/来源区、正式依赖、gitlink，也未 push/PR/issue；只提交本报告和小证据。

Standards：0 项阻断；Spec：0 项阻断，原 P1/P2 已闭合。允许父整体接入新三包及面板补丁，继续既定正式实测。
