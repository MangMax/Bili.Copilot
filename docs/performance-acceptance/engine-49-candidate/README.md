# #49 本地调度候选：部分实现，验收未通过

2026-10-30。**本轮未完成所要求的完整引擎修复。交付的是可复放、可审查的调度状态候选，不可集成/发布的通过版本。** 真实两调度器的受控出队测试通过，但暂停前 seek、双向暂停切换仍无可见目标帧，Win2D 暂停内容保持失败；同步设计仍有必须解决的 UI 等待风险。不得据此关闭 #49。

没有改正式应用源文件、包引用、项目配置或 bili-kernel gitlink；没有 push、NuGet 发布、issue 写操作、安装环境或启动子代理。源码、隔离 NuGet 缓存、包、布局、dump 均在本 worktree ignored artifacts。提交内容仅本目录的补丁、测试、脚本、许可、报告和小型验收证据。

## 基线和来源

- 工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-c9d5219a`，分支 `t3/engine-playback-race-fix`；开始 clean，HEAD 精确为 `51154f0052f86428282550e2ca8910dccc228a03`。结束前 `git merge --no-edit feat/performance-45` 返回 `Already up to date`，父分支仍为同一 SHA。
- 实际发布来源：[Richasy/danmaku-kernel 4d0f8691](https://github.com/Richasy/danmaku-kernel/commit/4d0f8691f08f1e143294402477e72306a1b38b6f)，Git tree `9b9e51a665f3cf4c0ee7efc5854c8269f556405a`。直接下载该 SHA 的 codeload ZIP；SHA256 **`5B6465B63C438CDBFA8C0C2D7697719A677539522D98FEEB7C2B7B87F1691BC0`**。
- 独立下载 Core/Legacy/Share `0.0.1-preview6` 的 nupkg，三个 nuspec 均指向该 commit 和 MIT。原始 nuspec、commit 查询响应保留在 [provenance](evidence/provenance)。源码 `nuget-package.props` 自带版本仍是 preview5；本轮不以此否认 nuspec 的来源，也不声称本地编译 DLL 与已发布 DLL 字节相同。
- 源归档没有 AGENTS/README/CONTRIBUTING；已读 `.editorconfig`、构建/中央包配置和许可。[上游 MIT 许可](UPSTREAM-LICENSE.txt) 原样保留，包括其原有 `[year] [fullname]` 占位符，未擅自补写版权人。
- 应用 AGENTS、docs/agents、指定四份 #49 方案/报告、GitHub #49 已读。应用目录没有相关 glossary/ADR/run-tests overlay。使用指定的 debugging、tdd、code-testing、run-tests、scope-gate、WinUI review/UI testing/dev workflow、verification 技能；复用宿主前也读 winui-design。测试 seam 已由本任务明确为真实调度器及生产 panel/VM，不新增确认流程。code-testing 研究/计划/状态留在本 worktree Git 元数据，未提交；无 code-testing-extensions，沿用已有宿主约定。

## 保留的候选与被撤回的实验

[scheduler-candidate.patch](scheduler-candidate.patch) **只改** Core `DanmakuScheduler.cs` 与 Legacy `DanmakuFrostMaster.cs`：

- 内部 `_wantsPlaying` 保留调用者 Pause/Resume 意图；内部 seek 暂时停止不能把暂停意图覆盖为 Resume。Pause、Resume、seek 收尾和关闭状态用同一个稳定 `_stateGate` 排序，避免锁住会被替换的列表对象。
- 队列项携带 generation；Clear、SetList、显式 Seek、Stop、Close/Dispose 失效旧代次。worker 出队后、改游标/发 render 调用前再次验证。关闭后拒绝新 UpdateTime；事件句柄由 worker finally 释放，不在 UI Dispose 与 WaitOne 同时释放。
- 空队列不再用 `0` 代表；0ms 可提交；取走一个项后如仍有项则重新 signal，避免 AutoResetEvent 合并通知后队列剩余工作无人唤醒。Core 自动 seek 不再 continue 跳过目标时间的调度。
- 明确当前候选契约：Stop 清队列并将意图设为暂停；Restart 从 0 开始并设为播放；Seek 保留最新意图；Clear 清列表/画面、游标归零但保留意图。**Clear 后 render 仍 stopped，后续恢复的完整兼容性未证明。** 显式 Seek 的静止首帧也未解决。

公开签名保持不变；无新增 Task API、生产反射、固定等待或每 tick 重建。既有上游的同步等待、Thread.Sleep 等没有被算成本轮新增实现，也没有因此得到安全认证。

[rejected-render-experiment.patch](rejected-render-experiment.patch) 是**失败实验记录，默认准备脚本不应用**。它尝试暂停单帧、Legacy 冻结逻辑时间，以及移除 DirectX Start/Stop 的 Dispatcher 等待；手动队列路径又暴露持 GameLoopThread 锁执行帧的问题，随后尝试异步启用原 Dispatcher。两次运行只完成第一个 DirectX 用例即退出，PID47948/46904 对应 Application Error `CoreMessagingXP.dll / 0xc000027b`。根因没有定位到可以安全修复的程度，不能宣称是已证明的 GPU/驱动缺陷。证据见 [crashes-final.json](evidence/crashes-final.json)、两个 partial barrier JSON 和 wait timeout。该实验从最终源副本撤回，最终两个 render 文件与归档逐字节一致。

按照 [systematic-debugging 技能](C:/Users/hp/.codex/skills/systematic-debugging/SKILL.md) 的 “**If ≥ 3: STOP and question the architecture**” / “**DON'T attempt Fix #4 without architectural discussion**”，在状态修复仍空白及两次渲染尝试失败后停止扩大渲染实现；本轮没有擅自扩展到 GameLoopThread、DirectXResourceManager 重写。这是留下未完成工作的原因，不是候选通过的理由。

## 实际红绿与边界

SDK `10.0.403`，WinApp `0.7.1`，本地桌面，Release **JIT** x64。Debug 源码可构建，但 D3D DEBUG device 创建后在 CreateSwapChain 空引用，故用未改源码的 Release JIT 建立测试；未安装图形调试组件，具体 D3D HRESULT 未采集，缺少 debug layer 仅是推断。

复用 harness-49 / harness-49-remount，独立身份 `BiliCopilot.Acceptance49C9D5_y1mf40g4r9srg`。实际生产 panel/XAML/VM/parser/session 和 WinUIKernel 基类参与编译；仅 Danmaku 包引用在**独立宿主**替换为归档源码 ProjectReference。XAML 没有更改。内存设置/字体适配器沿用原宿主；没有登录、网络视频或真实账户。此次宿主不需要视频 runtime 资产，因此没有复制 libmpv/ffmpeg，也未读取凭据。

`instrument.py` 仅在测试副本加入出队后和下一次 WaitOne/finally 的 Action 钩子；真实 scheduler/master 和 render 仍执行。测试在 worker 已出队后通过 ManualResetEventSlim 保持它，UI 异步等待出队通知，调用真实公开方法，再释放 worker，等待 idle/finally。没有用睡眠决定竞态顺序。10s/5s 是失败超时，不能当完成信号。测试反射只用于该测试专用钩子和内部只读观测；最终包无这些字段。

最终同一版 [EngineCases.cs](EngineCases.cs)：

- 原始来源 PID55292：[barrier-red-final.json](evidence/barrier-red-final.json)，**Passed: 9, Failed: 17, Skipped: 0**；完整 26 个唯一用例，evaluate exit **1**。
- 状态候选 PID2828：[barrier-final.json](evidence/barrier-final.json)，**Passed: 26, Failed: 0, Skipped: 0**；evaluate exit **0**。两边用例集合完全相同，详见 [test-summary.json](evidence/test-summary.json)。这是自宿主 JSON runner，不是 xUnit/TRX，未冒称运行 dotnet test。
- 较早的 `barrier-red.json` / `barrier-candidate.json` 使用移除 panel 的 Close 用例；Unloaded 延后，不能证明关闭已发生。它们被直接调用公开 Dispose/Close 的最终版本取代，原始文件仍保留。较早的时间边界只检查时钟，最终版还使用 0/500/1000/6000/6001/90000ms 已知列表检查精确游标。

下表 `E` 分别展开为 `DirectX`、`Win2D`；准确的所有名称在 summary 中。

| Requirement | Evidence |
|---|---|
| 已出队旧进度后 Pause/Resume 的最新意图 | `E_DequeuedPause`、`E_DequeuedResume`；最终 4/4，Pause 两种实现 red→green |
| Clear/SetList/新 Seek 失效旧进度 | `E_DequeuedClear`、`E_DequeuedSetList`、`E_DequeuedSeek`；最终 6/6，游标/时间不被旧进度覆盖 |
| 真实 Close/Dispose 后旧进度不能回写 | `E_DequeuedClose`；最终 2/2，关闭前时间保持，red→green |
| 0ms、5000/5001、后退、重复、空列表、末尾 | `E_Zero`、`E_Threshold5000`、`E_Threshold5001`、`E_Backward`、`E_Repeated`、`E_Empty`、`E_End`；最终 14/14，检查 lastTime/index/播放状态；不证明 GPU 接受每条弹幕 |
| 三次重挂/每引擎、卸载、hide/show、旧 VM 隔离 | [state-ui/results.json](evidence/state-ui/results.json)：原 remount runner 24/24，包含两项暂停 seek 和两项暂停切换**状态**检查。对应构建为相同两文件状态补丁的测试钩子版 |
| 暂停前 seek、双向暂停切换目标可见 | 最终未插桩 PID49480，[final-visual](evidence/final-visual)：4 张相应图空白，**FAIL** |
| seek 后暂停、普通播放运动 | `*-exact-pause-after-seek.png` 可见 EXACT TOP 20；`*-motion-1/2.png` 两引擎滚动向左，VM 20→21s。先在播放态 seek 再 Pause，不等于暂停态 seek 的首帧成功 |
| 暂停内容保持 | DirectX frozen 两图有限窗口内保持可见位置，Stopwatch 停止；Win2D frozen 两图白色实时弹幕消失，**FAIL**。Win2D 无同等 Stopwatch 观测，不把 flag=true 当时钟证明 |

边界：屏障位于“出队后、应用前”，没有覆盖每个 render 内部阶段、原生回调、所有多生产者交错、异常退出后重新提交、Stop/Restart 全部兼容行为或极端队列负载。Legacy GetDanmakuList 仍公开内部可变列表，外部直接修改不受新 gate 约束。没有复制调度算法替代真实引擎，也没有通过空白截图洗绿。

## 可见证据和失败归因更正

最终证据共 **24 张 PNG，逐张使用 view_image 查看**：state-ui 10 张（8 空白、2 实时可见），final-visual 14 张。[逐图记录及采样](evidence/visual-review.json) 分开记录可见内容、内部状态和时钟。没有图像编辑或像素相等测量；连续运动/静止是该有限采样窗的视觉观察，不能外推长时间正确性。DirectX top/rolling 同行重叠也保留在图中，未签署全视觉布局通过。

原 ui-49.md、ui-49-remount-fix.md、历史 red 和所有旧图未改。新探索轮也没有覆盖写掉：初次 Debug crash、logger 实验、部分 red 及两次渲染实验都留在 ignored artifacts，小日志在本目录 evidence。

**重要更正：PID51764/56004 不能作为引擎原生关闭失败证据。** 临时 EngineLogger 并发 AppendAllText 导致 IOException；PID56004 的 .NET Runtime 1026 明确给出该栈，虽然源文件已恢复 NullLogger，那个增量构建仍运行了旧适配器。之后屏障构建/最终宿主采用 NullLogger。不能把先前 commentary 的“源码重挂退出”解释为已确定引擎根因。最终有效红绿以 PID55292/2828 为准；渲染失败实验以各自独立 PID/事件记录为准。

## 构建、复放和本地包

所有命令在本工作区执行，`NUGET_PACKAGES=artifacts/engine49/packages`，`NUGET_HTTP_CACHE_PATH=artifacts/engine49/http-cache`（设置为绝对路径）。未写全局 NuGet 缓存。实际构建/打包命令：

```powershell
$root=(Get-Location).Path
$env:NUGET_PACKAGES="$root/artifacts/engine49/packages"
$env:NUGET_HTTP_CACHE_PATH="$root/artifacts/engine49/http-cache"
$source="$root/artifacts/engine49/source/danmaku-kernel-4d0f8691f08f1e143294402477e72306a1b38b6f"
dotnet build "$source/src/Danmaku.Core/Danmaku.Core.csproj" -c Debug -p:Platform=x64 -v:minimal
dotnet build artifacts/engine49/harness/Harness.csproj -c Release -p:Platform=x64 --no-restore -v:minimal
winapp run artifacts/engine49/harness/Harness.csproj -c Release --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory artifacts/engine49/layout-final
# Actual final UI PID was 49480, now exited. Do not reuse it.
./docs/performance-acceptance/engine-49-candidate/visual.ps1 -AppPid 49480 -ArtifactDirectory artifacts/engine49/final-visual
foreach($name in 'Share','Core','Legacy') {
 dotnet pack "$source/src/Danmaku.$name/Danmaku.$name.csproj" -c Release -p:Platform=x64 --no-build --no-restore -p:PackageVersion=0.0.1-preview6-local49-c9d5 -p:RepositoryCommit=4d0f8691f08f1e143294402477e72306a1b38b6f -o "$root/artifacts/engine49/feed" -v:minimal
}
python docs/performance-acceptance/engine-49-candidate/evaluate.py artifacts/engine49/barrier-red-final.json
python docs/performance-acceptance/engine-49-candidate/evaluate.py artifacts/engine49/barrier-final.json
```

Core baseline build、两引擎/宿主最后 build、三次 pack 均 exit0，0 warnings/errors；原始输出保存在 `evidence/build-*.txt`、`pack-*.txt`。各探索失败构建同样保留，不能把它们当功能 red。最终 visual 采集 exit0 只代表采集完成，视觉验收仍失败。

复放新目录：`./docs/performance-acceptance/engine-49-candidate/prepare.ps1 -Destination artifacts/<new-name> -Candidate [-Barriers]`。不用 `-Candidate` 得到原始源码；`-Barriers` 仅用于 26 项竞态 runner。随后在该目录下设置隔离缓存、build，再用新的 owned PID 执行 `run-barriers.ps1` 或 `visual.ps1`。准备脚本已实际运行到 `artifacts/engine49-replay-prepared`；补丁复放后两源码文件与测试源**字节相同**，见 [replay-result](evidence/replay-result.json)。git apply 使用 ignore-whitespace 并恢复 BOM/CRLF；有4行新增空白警告，原样记录，不影响复放。

本地最小包在 `artifacts/engine49/feed`，版本 `0.0.1-preview6-local49-c9d5`；Core/Legacy 依赖同版本本地 Share。它们只是候选归档，**没有通过本地 feed 重新 restore 验证**；运行验证采用 project reference。最终包内 DLL 与去钩子最终 UI 部署 DLL 逐字节相同，见 [source-verification](evidence/source-verification.json)。

| 最终 DLL | SHA256 |
|---|---|
| Danmaku.Core.dll | `b67972a3e09b3b548dffbe0c5d9e85a74b2322f886d30e7c228d9a8dd078dd56` |
| Danmaku.Legacy.dll | `68f030bbdafe03a28fc0ca3722d7f4c9c8cd8393964cfb4fb66f7bc64303c91d` |
| Danmaku.Share.dll | `923610fbd5fd97cdbf1116ab0f2c32f91dddb0681c53f337f7c9f9cb1a29cee5` |

源归档、原始 nupkg、插桩/未插桩 DLL、本地 nupkg/snupkg 的绝对路径/大小/完整 hash 均见 [artifact-hashes.json](evidence/artifact-hashes.json)。不提交这些大二进制。

## WinUI/同步自审：不建议集成

- **Error：暂停目标帧和内容保持仍失败。** 两个 render 没有保留修改；Core paused seek 收尾仍 stopped，Legacy 的 Update/Draw 与驻留时间语义仍待解决。恰好目标时间之前的弹幕如何重建、初始尺寸/资源未就绪时的丢弃也没有解决，未引入窗口预取策略来绕过。
- **Error：UI 等待边界未满足任务要求。** `_stateGate` 内仍调用 render Pause/Stop/Start 和 RenderDanmakuItem。列表锁替换与 state→queue 顺序一致，worker 在拿 state 前已释放 queue，没有新增反向 queue→state 持锁；但 UI Pause/Close 可等候正在渲染准备的 scheduler，DirectX 原 StartDispatcher/StopDispatcher 仍同步等待。因此不能凭 26 项通过声称 UI 不等待 worker/无死锁。必须在进一步采用前重新设计有序提交与 render 生命周期边界。
- **Warning：Close 证明有限。** 已出队旧进度不改时间通过；不等于所有 native callback 都安全。原 DirectX Dispose/GameLoopThread 等待和资源释放未重写，失败实验没有足够诊断支撑安全修改。
- **Warning：兼容覆盖不完整。** Stop/Restart、Clear 后正常短跨度播放、实时插入/外部列表别名、多生产者及异常路径仍需直接断言。当前时间/游标检查不证明所有目标被 GPU 绘制。
- 专门 WinUIAnalyzer 未安装/运行，不因本任务安装。没有生产 XAML、绑定、主题、本地化或可访问性改动；宿主仅增加实验按钮，已有生产文件直接复用。没有新的正式依赖或公共 API。

未重跑未修改应用的403全套；这403是继承基线结果，不是本轮 fresh 结果。完整播放器、真实视频时钟、登录/网络、解码、高密度长时运行、性能增益均未验。Native AOT 因既有完整 MSVC 工具链缺失仍 blocked，未安装或重试。

## 桌面和交付状态

最终 owned PID49480 通过 Close 退出（exit0）；此前 owned 测试进程均已退出。核对 InstallLocation 精确位于本 worktree layout-final 后移除独立测试包，remainingPackages=0。身份核对后 remainingOwnedProcesses=0；早期 PID30952 已在 06:11:05 被 `xray.exe` 复用，未对其执行退出/终止。PID9608、Dev/生产包、凭据保持不动。

最后同 workflow `engine49-c9d5` 的 `winapp ui yield --on local --json`：exit0，**`{"released":true}`**，此后无桌面操作。原始返回与身份核对记录见 [cleanup.json](evidence/cleanup.json)。bili-kernel gitlink/嵌套 HEAD 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，嵌套工作区 clean。

提交前 `git diff --cached --check` 对保存的补丁上下文空行、候选补丁内原样空白及 raw state-ui 输出尾部空行报告 whitespace，exit2；未把它描述为全绿，完整输出保留在 evidence/staged-whitespace-check-final.txt。补丁 apply --check 则 exit0，准备脚本和源字节比对通过。

本轮只交付可审查的部分候选和失败证据；没有完成可发布修复，也没有集成正式依赖。下一步需要先解决有序 render 提交/生命周期及暂停可见首帧，重新运行同一失败矩阵，不能仅发布当前包或增加公共 async API 来结束 #49。
