# Win2D 1.3.2 Legacy 卸载能力 spike

2026-10-30。结论：**S1 的强非阻塞退役方案不成立，不能据此进入 L2/L3。** 已启动控件的 `GameLoopStopped` 是本实验可达的动画线程清理点；它解决了普通排队清理被取消的问题，却没有消除框架卸载期间的 UI 停顿。`Close`/`Children.Clear`/`RemoveFromVisualTree` 返回快，并不意味着随后 UI dispatcher 仍能推进。

这是一份独立实验报告，不修改 [review](review.md)、[spec](spec.md)、[acceptance](acceptance.md) 或 [ADR](adr-0001-render-ownership.md)。四份文件及 source-verification 原样复制自 `54c00e05a3751ed930e5bdc8fd7e93da21f1d74f`。当前用户授权足以执行本次有限 spike；审查中的后续技术准入建议没有被解释为额外用户审批要求。

## 范围与来源

父基线 `4861038a87bf10e0c92722a0944377c779730384`，分支 `t3/win2d-capability-spike`。自行执行，无子代理。读 AGENTS/docs/agents、scope-gate、WinUI 开发/UI 测试/代码审查、code-testing/run-tests、verification-before-completion；启动调查时补读 systematic-debugging。定级为高难度能力 spike，交付只含 docs 实验，不修改正式 src/tests/externals、包依赖或引擎。

精确引擎源 `4d0f8691f08f1e143294402477e72306a1b38b6f`：按 [source-verification](source-verification.json) 重新计算 45 个文件，零不匹配；ZIP SHA256 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`。[核验结果](win2d-spike/evidence/source-recheck.json)。历史 [engine-49-trace](../performance-acceptance/engine-49-trace/README.md) 仅用于溯源及界定调用关系，没有加载它的引擎 DLL 或带 `_test` 的候选。

本宿主只使用真实 `CanvasAnimatedControl` 和一个真实 `CanvasRenderTarget`。没有 Danmaku/Core/Legacy 包或生产 panel 的编译引用，也没有实现正式 owner adapter、通用调度器或 DirectX attach。这里的实验 `Close()` 只是封闭后续绘制并选择清理机制，不冒充原引擎 `DanmakuRender.Close()` 的完整行为，尤其没有复刻其先 Paused、解绑和同步清列表。它用于独立验证拟议清理机制及 WinUI 框架卸载，不能签署应用修复。

## 版本/API 事实

- 原源与应用 `src/Directory.Packages.props` 均指定 `Microsoft.Graphics.Win2D 1.3.2`；本宿主从已有本机 cache 离线 restore 同版本，唯一 source 为本机 `.nuget/packages`，未升级、联网下载或安装工具。
- `Microsoft.WindowsAppSDK 1.8.251106002`，WinUI 包 `1.8.251105000`；实际进程加载 Windows App Runtime `1.8_8000.994.2142.0` 的 `Microsoft.UI.Xaml.dll`。SDK `10.0.403`、WinApp CLI `0.7.1`，net9 x64 Release JIT，非 AOT。
- `winapp find-api` 针对 restored harness 返回 `RunOnGameLoopThreadAsync(DispatcherQueueHandler): IAsyncAction`、`GameLoopStarting`、`GameLoopStopped`、`HasGameLoopThreadAccess` 及 `RemoveFromVisualTree(): void`。[GameLoop 元数据](win2d-spike/evidence/api-gameloop.json)、[Remove 元数据](win2d-spike/evidence/api-remove.json)。首次用 `|` 合并 filter 未命中，因为它不是正则过滤；随后分别查询。
- CLI 明示机器 SDK 元数据回退到 10.0.28000.0，排除了不匹配的已安装 Runtime 2.6；因此不泛化机器 SDK API。上述 Win2D API 另外由当前包实际编译和调用确认。包自带 Win2D 源标记 `feefabe2d8ef80025030c1dd7c9d3d71e2462cd3`，不是 Danmaku 源 commit。
- native `Microsoft.Graphics.Canvas.dll` SHA256：`813c714a54df82227fb02fa295dea8379061c3c408b57bff18532f7953118dbf`；interop：`1f54381b9c138601ac60612e2d8f401b680725dc284561c37427427bf687fbf0`。cache、包内容、layout 和实际加载模块核验分开记录。[元数据/源及截图 hashes](win2d-spike/evidence/metadata.json)、[包内字节核验](win2d-spike/evidence/package-check.json)。没有借检查这些 DLL 读取受保护完整应用的 layout。

## 方法与观察边界

[Program.cs](win2d-spike/Program.cs) 是明确带实验诊断的独立 WinUI 宿主。`queue` 用同步 delegate 排队释放，观察真实 IAsyncAction Completed/Canceled；`stopped` 在加入视觉树前订阅停止事件，在事件内同步释放，不投递或等待 UI。两者都预订阅仅记录的 Loaded/Unloaded/Starting/Stopped。

首次 Draw 在动画线程创建 target；Draw 记录每次最后使用，清理记录线程、次数、方式。每个已绘制 case 创建一个 target；普通清理丢失的 target 在 Summary 后、确认 Stopped 后由 **HARNESS-RESCUE-UI-NOT-CANDIDATE** 单独释放，防止把失败实验残留当作成功。此救援不是生产设计，也不证明任意资源都可移交 UI。未创建的 target 计数为零，不能算“在 pending CreateResources 下释放成功”。

屏障设置在真实 `RunOnGameLoopThreadAsync` 回调，独立后台线程 1500 ms 后释放；WaitOne 有 2500 ms 硬上限，应用有 90 s 进程看门狗。屏障暴露 `Local\Win2DSpike702c-<PID>` 命名事件，可由外部进程 Set；本次实际释放者为独立后台线程，**没有 UI 依赖**。UI 在屏障内先观察 250 ms 心跳，然后发 Close 并立即 Clear，或 Remove 后立即 Clear；另有完全不先调用 Close 的纯框架 Clear 对照。50 ms dispatcher timer 是心跳，实际通常约 62 ms。所有阻塞回调均由后台释放，未触发 2500 ms 或 90 s 超时。

计时是 Stopwatch 的 CPU 回调观察。资源操作没有 GPU fence；不证明 GPU completion、完整 native 无泄漏、所有 Canvas 内部资源归零。异步 delegate 的 await 之后清理没有测试；这里所有传入 delegate 均同步结束，不把 async-void 返回当 fence。

## 运行观察

最终运行 PID **39980**，HWND **3149150**，自有包 `BiliCopilot.Win2DCapability702c_1.0.0.0_x64__y1mf40g4r9srg`，宿主 DLL SHA256 `4efcb47f802acfb1c9ab7c782eae12e3ef06891711079f4d4e72247feaa42bfc`。共 14 个 case、977 条连续事件，[原始 JSONL](win2d-spike/evidence/events.jsonl)、[汇总](win2d-spike/evidence/summary.json)、[UI 命令和返回](win2d-spike/evidence/commands.json)。没有将“14 个采集完成”称为 L1/L3 通过。

| 场景（每项 queue/stopped 各一次） | 普通排队观察 | 预订阅停止事件观察 |
|---|---|---|
| normal | 先异步等清理 action 完成再 detach，target 1→释放1 | Remove/Clear 后停止事件释放1 |
| parent-first | Clear、Unloaded 后 Close，action Canceled；target 留存到实验救援 | 停止事件先释放1，随后 UI Unloaded、Close |
| never-loaded | 入队立即 Canceled；没有动画 owner、Stopped、Unloaded；资源0 | 不发生 Stopped；不能只等这个事件完成关闭 |
| pending-resources | TrackAsyncAction 未完成时 Close/Clear；清理 action Canceled，未 Draw、资源0 | 已有 Starting/Stopped，但资源0；Unloaded 后才完成资源任务，没有后续 Draw |
| blocked-close-clear | Close 返回，紧接 Clear 返回；释放后清理 action Canceled，需救援 | 停止回调在 barrier 返回后同动画线程释放1 |
| blocked-remove | Close→Remove→Clear 都返回；普通清理 Canceled，需救援 | barrier 返回后停止事件释放1 |
| blocked-framework-clear | **在任何 Close 之前** Clear；普通清理晚到被取消 | 同样发生 UI 停顿，停止事件仍释放1 |

所有已 Loaded 的 case 中，CreateResources 在 UI native TID **6368**；Starting、Draw/target 创建/最后使用、Stopped 在该控件自己的动画 TID；Unloaded 在 UI 且晚于 StoppedReturn。owner 是每个实例实际观测值，不假设跨控件相同。最后 Remove 后再排的探针全部 Canceled，强引用列表保留控件并未保活动画线程。

normal/queue 的异步等 action 是有利对照，不符合应用 F10 同步 Close 后立即清父容器的完整时序。后面三个阻塞 case 才覆盖“立即清除”；它们没有等待 cleanup action。

### UI 心跳与方法耗时必须分开

单位 ms；右列是该 case 最大相邻心跳间隔，**不是**同步 Clear 的耗时。阻塞前约 250 ms 的观察窗均有 4–5 次 UI 心跳。

| 阻塞场景 | Close | Clear | Remove | barrier 实际保持 | 最大心跳间隔 |
|---|---:|---:|---:|---:|---:|
| queue/blocked-close-clear | 0.108 | 0.109 | 0.074 | 1509.102 | 1248.633 |
| queue/blocked-remove | 0.100 | 0.007 | 0.163 | 1500.777 | 1250.266 |
| queue/blocked-framework-clear | 0.110 | 0.147 | 0.067 | 1502.274 | 1241.180 |
| stopped/blocked-close-clear | <0.001 | 0.134 | 0.076 | 1509.916 | 1305.677 |
| stopped/blocked-remove | <0.001 | 0.008 | 0.135 | 1503.527 | 1256.882 |
| stopped/blocked-framework-clear | 0.010 | 0.280 | 0.121 | 1501.399 | 1292.654 |

最关键的 `stopped/blocked-close-clear` 原始顺序：

1. seq833 / 17885.91 ms：动画 TID61664 进入屏障。
2. seq839 / 18140.31：UI 完成屏障内心跳观察；Close 返回后，seq843 / 18140.45 Clear 返回。
3. 此后直到外部释放没有 UI heartbeat。seq844 / 19395.79：后台 TID38108 Set；seq845 / 19395.83 动画屏障返回。
4. seq847–851 / 19396.51–19396.52：动画线程 Stopped 内释放 target 一次；seq852 / 19396.84 才有 UI Unloaded。
5. seq853 / 19399.80：原本仅 await Task.Delay(150) 后的 UI continuation 此时才执行 Remove。

**观察事实**：方法都已返回但 UI dispatcher 不能在 owner 被保持的后续卸载阶段推进；纯框架 Clear 对照也存在同类停顿，排除了“仅候选 Close 中显式 Wait”这一解释。**推断**：框架的延后卸载处理等待 game loop 退出。没有采集 native stack，故不声称定位到某个 native Wait 函数；即使不采用这一内部归因，UI 非阻塞契约的反例已成立。

此前 PID10652 的 [first-matrix](win2d-spike/evidence/first-matrix/summary.json) 也完成 14 项，观察一致。其初始截图抓在 FirstDraw 前，是空渲染区，不能充当首帧可见证据。因此只修正截图就绪标记后重采最终 run，没有修引擎或调整负面结论。最终初始截图逐张看见蓝色 target 与 `REAL CanvasAnimatedControl 1.3.2`，完成截图显示 DONE、空子容器；均关联上述 PID/HWND/source/DLL hashes。PNG 不对应严格单帧 ID，窗口采集时间见 commands。

### 可控资源失败：已触发，清理没有闭合

早一独立运行 PID18036 的 `queue/resource-failure` 在 CreateResources 注册真实 TrackAsyncAction，然后后台向该任务注入 `InvalidOperationException`。seq487 / 8672.65 ms 记录注入；进程随后以 `0xc000027b` 在 **CoreMessagingXP.dll** 终止。没有 Close/Stopped/资源释放终态，Application.UnhandledException 没有相应日志，不能报“故障清理成功”。Windows Event 1000 与 PID0x4674 相符。[完整失败证据及当次源码](win2d-spike/evidence/resource-failure/)、[Windows 事件](win2d-spike/evidence/resource-failure/windows-event.json)。

这是合成资源创建任务失败，不是 device loss；没有故意破坏设备。保留这一次失败后不重复该 native 失败路径，也没有为让它过关扩建恢复器。stopped 策略的资源失败未执行；失败中已部分创建的真实资源、设备重复 CreateResources、设备丢失/恢复及应用原异常策略仍未验。正常矩阵单独继续完成，不能把其完成覆盖此失败。

启动阶段另有五次无有效矩阵的尝试，包含过早发现窗口、15 s 就绪超时和一次初始 XAML 错误；[返回记录](win2d-spike/evidence/startup-attempts/)。标准 XAML 启动、LocalState 记录和 55 s 有界就绪后完成运行。没有足够证据将所有早期超时归因于同一原因，前述初始化修正只是宿主修正，不是 Win2D 能力修复。

## S1 可采用的最小修订

1. 删除“先脱离可见父容器，再靠引用保活动画 owner 至清理 ack”的成立断言。当前资产下已卸载/未 Loaded 的常规排队会取消，引用保活与线程存活是两回事。
2. 将预订阅 Stopped 作为**已启动实例的候选最终同步清理点**，回调内绝不等待 UI。保留独立 retirement 完成记录与已创建资源账本；从未启动、pending 初始化和失败终态必须另行定义，不能永远等 Stopped，也不能把 action Canceled 当已释放。
3. 明确“Close API 快速返回”“清除方法快速返回”和“随后 UI dispatcher 持续可用”是三个不同条件。当前 CanvasAnimatedControl 在 owner 被保持时，**最后一个条件失败**；仅换成 Stopped 回调不足以关闭 S1。
4. 若继续使用该控件，需先选择可满足的约束：是否可以协调所有父容器卸载，在 owner quiescent 后才 detach；或接受框架卸载等待的有界风险。前者对已发生的 parent-first unload 没有补救证明，后者改变强非阻塞要求。当前不能承诺 native 工作总可取消/短时返回。保留强非阻塞条件则需另选生命周期方案并另行设计，不能在本 spike 偷换成 UI native 执行或通用调度器。
5. L1 仍是能力调查，L2/L3 不准入；原 S2 DirectX attach 独立未验。这是技术结论与待确认设计边界，不是要求用户重新批准本次已完成实验，也不授权正式依赖变更或发布。

## 复放、验证与清理

只在已获准的独占本机桌面复放；identity 固定，先确认本实验身份未在使用。禁用 sandbox，所有 UI 指令使用 local + 自有 PID/HWND。

```powershell
# 在 repo 根目录；输出必须是新的 ignored artifacts 路径。
& docs/engine-61-design-draft/win2d-spike/prepare.ps1 -WorkRoot artifacts/win2d-spike-replay
& docs/engine-61-design-draft/win2d-spike/run.ps1 -WorkRoot artifacts/win2d-spike-replay
& docs/engine-61-design-draft/win2d-spike/metadata.ps1 -WorkRoot artifacts/win2d-spike-replay
python docs/engine-61-design-draft/win2d-spike/analyze.py artifacts/win2d-spike-replay/run
python docs/engine-61-design-draft/win2d-spike/validate.py artifacts/win2d-spike-replay/run
```

默认不重放已终止进程的 resource-failure；原 `.cs.txt` 是该次精确诊断源供审阅。硬超时是失败探测，不是成功 fence。复放脚本会在 finally 结束自己的进程、拷贝自己的证据、移除自己的包并 yield；不使用全局进程名 kill。最终运行 build exit0、0 warnings/errors；`validate.py` 检查 14 项的连续序列、真实线程、取消、每个 target 最后使用后单次释放（含明确分列的救援）和 UI 停顿反例，exit0。这是证据自洽验证，**不是修复通过的测试**。

提交时仅整理了两份 manifest 的尾部空白；原运行输入以 [source-manifests.json](win2d-spike/evidence/source-manifests.json) 保留精确字节。prepare 会恢复这些字节并把 C#/XAML/csproj 规范为运行时的 LF，避免 Git autocrlf 令源 hash 无法复核。脚本另增加已有同名实验身份即拒绝的检查；原运行返回仍由 commands 原样记录，不以脚本后续整理冒充当次执行。证据内容全部为合成离线数据，未含用户业务内容或凭据；保留本机路径以核对加载来源。

未跑 403、原26或完整应用 build；未跑 WinUIAnalyzer/AOT，没有安装分析器。WinUI 自审：无 UI 显式等待 owner；有且仅有标为实验的动画屏障；资源仅在 Draw 创建，DrawSession 不跨回调，停止清理不访问 UI；代码后置日志/计时分配会扰动线程时序，不作性能预算或全面审计承诺。

最终 PID39980 CloseMainWindow=true、graceful=true、remaining=false；自有包移除后 remaining=0；`winapp ui yield --on local --json` exit0，`released:true`。此前 PID10652 也正常退出；PID18036 已自行异常终止；启动尝试的所有自有 PID 均已结束，详情见 cleanup 及命令返回。独立自有包已不存在，桌面槽释放。没有启动、卸载或读取 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be` 的凭据/layout，没有操作 PID9608，没有改系统 DPI。

所有提交路径限定 docs，`externals/bili-kernel` 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d` clean。结束同步最新本地父分支并检查 clean；准确提交 SHA 见交付消息。不 push、PR 或 issue 写入。
