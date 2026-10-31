# 调速与关闭生命周期协调

2026-10-31；实现区 `t3-dc85fdad`。父基线 `bd02775bd9a48d7e62c169a6367eec58707dad8e`；固定候选 `7ac24a44f0e0bd8a388c4b413b564f22ffcd434d` 已 fast-forward 纳入本分支，仅作为待审候选。无委派、push、PR、issue 写入；未读取或采集进程内存，也未重做转储分析。

## 确认的缺陷及边界

应用原有八个 `Client.SetSpeedAsync` 调用：Overlay 的 debounce/快捷速度按钮；PlayerVM 的加速/减速/长按加速/恢复速度；播放结束恢复和右键松开恢复。所有发起点都在 UI dispatcher。原 CloseAsync 同步拒绝弹幕、等待初始化，随后 DisposeResourcesAsync 调 Player.DisposeAsync，但没有登记和排空调速任务。

只读核实 pinned-mpv 的源码：`MpvClient.SetSpeedAsync` 用 Task.Run 调 SetProperty；`MpvClient.DisposeAsync` 设置 IsDisposed、取消 event CTS、发送 stop 后 Task.Run Destroy，没有等待属性任务。`MpvPlayer.DisposeAsync` 先保存历史，再关闭定时器并 Dispose client。因此必须在 Player.DisposeAsync **之前**排空，不能先单独销毁 client 或仅先检查 IsDisposed。

受控反例使用原生产 IncreaseSpeedAsync、CloseAsync、DisposeAsync、TryCleanupAsync、DisposeResourcesAsync 方法体，挂起实际发出的替身客户端操作：旧代码在速度仍挂起时已经完成 CloseAsync。`red/results.trx` 的 CloseWaitsForAcceptedSpeedBeforeDisposal 明确失败。它证明应用生命周期缺口，不证明原 runtime5 AV 的 Destroy/SetProperty 交错。

## 最小修复

- 同一 PlayerViewModel 的 Speed partial 登记 UI 线程上已接纳调速，捕获 client 和现有媒体有效性谓词；finally 释放计数，未知异常原样传播。
- 复用 CloseAsync/DisposeAsync 已同步设置的关闭标志拒绝后续请求；Dispose 等初始化和调速结束，再走原有释放逻辑。直接 Dispose 同样覆盖，重复 Close 保留原共享任务。
- 八个应用发起点全部改走 ChangeSpeedAsync。异步长按和恢复回调仅在仍属当前媒体时更新后续状态；初始化复用 client 前等待旧速度写入。
- Overlay 的待发 debounce 保存媒体有效性谓词，换 VM/卸载时清空；tick 在 await 前清空并停止计时器，避免旧完成清掉后来输入。

未增加通用播放器调度框架、锁、架构层或依赖改动。协调遵从现有关闭方法的 **UI 线程约定**，不是一个可从任意线程调用的线程安全 API。覆盖的是应用提交的调速；依赖内部的读取、PlayAsync 的 InitialSpeed、timer/event/native 脚本等不在此计数中。不能据此保证全部 native 生命周期或宣称修复历史崩溃、.NET 11 缺陷。

## 受控测试

`python docs/engine-61-design-draft/player-speed-close/probe.py <fresh-output-name>` 提取生产方法体并编译 xUnit/VSTest 项目；adapter 仅替换 native client、窗口、托盘和事件清理边界。最终媒体有效性函数也直接提取生产实现。它不是完整 WinUI 初始化测试，完整消费者另行验收。

| Requirement | Evidence |
|---|---|
| 已接纳速度挂起时关闭不得销毁 | `CloseWaitsForAcceptedSpeedBeforeDisposal`；red 失败，green 通过 |
| 关闭进行中、关闭后拒绝速度 | `ClosingAndClosedPlayersRejectSpeed` |
| 重复关闭及多个在飞速度 | `RepeatedCloseJoinsAndDrainsEverySpeed` |
| 速度异常传播且不阻塞释放 | `SpeedFaultPropagatesAndStillReleasesClose` |
| 直接 Dispose 及重复 Dispose | `DirectDisposalAlsoRejectsAndDrainsSpeed` |
| 换媒体和换 client 分别不接受旧完成 | `OldCompletionCannotReportSuccessForNewMediaOrClient` 两个分区 |
| 重开独立实例不受旧关闭污染 | `ReopenedPlayerIsIndependentOfOldClose` |

最终 controlled：Passed: 8, Failed: 0, Skipped: 0，`final2/results.trx`。
全套：`dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=suite.trx" --results-directory artifacts/speed-close/suite`，Passed: 563, Failed: 0, Skipped: 0。

初次 probe 运行的 Python GBK 解码失败已修正为显式解码，随后取得 red TRX。没有把工具失败称为产品失败。测试工程的未用替身事件警告与 kernel protobuf unused import 警告保留。

## 构建与真实验收

完整离线消费者由 `prepare.py` 在本区建立：使用本区生产源码、224 的 synthetic 服务/媒体、同一实验 Core/Share feed 和本地 native assets，只修改实验 App 启动、DI、身份、日志 Root 和验收控制器。没有替换 PlayerVM partial、CloseAsync 或 native client。新增 SpeedClose 控制按钮连续提交 20 次真实调速再关闭，日志注明当时实际 pending 数量，不把该数字当作 native 指令时间戳。

新身份 `Bili.SpeedCloseDc85`；仅操作本区 owned PID/path。Sandbox feature InstallState=2（禁用），因此使用已授权 local 桌面。未操作 Dev、Mang.BiliCopilot.PerfAcceptance.Caa6a7be 或其他实例，未读复制凭据。

最终完整 Release x64 JIT 构建通过，0 warnings / 0 errors。命令：

```powershell
dotnet build artifacts/speed-close/full/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release --no-restore -p:Platform=x64 -p:AppxPackageSigningEnabled=false -p:PublishAot=false -p:SolutionDir="<本区>/artifacts/speed-close/full/src/"
```

不是 AOT 验收；正式 preview6 依赖没有改动，也没有把实验 Core 变为正式依赖。首次完整 build 的实验 App.Id CA1823 错误已修正，随后生产候选通过；最后只为补齐验收增加控制器 NormalSpeed/ShowControls 并换身份再构建，生产文件没有变化。`full-build.log`、`full-build-final.log`、`acceptance-build.log` 均保留。

| 实机操作 | 结果 |
|---|---|
| runtime2 普通加速命令 | 11:26:14 UTC，控制器调用生产 IncreaseSpeedCommand，native PlaybackRate=1.25，仍 Playing |
| 真实 Overlay 快捷按钮 | 11:26:51 UTC，UIA 操作播放器实际 1.5x ToggleButton，native PlaybackRate=1.5 |
| 真实 Overlay slider/debounce | 11:26:58 后 UIA RangeValue=1.75，native PlaybackRate=1.75；`runtime2/speed.png` 已查看，实际弹窗/slider 可见 |
| 快速调速关闭 | 两轮都有 pending=20 后关闭；runtime2 11:27:07.103 进入，07.187 disposed=true、全部任务完成，owner1 退役 |
| 重开 | runtime2 11:27:14.893 再次完整 initialized，随后 Playing；新实例沿用共享速度设置是预期行为 |
| 实际卸载 | runtime2 11:27:27.011 owner2 retired，之后 visual panel=false/xload=false；未用 stub unload |
| runtime / PID33628 | 正常 exit 0，forced=false；退出后31.0118803秒准确PID/path查询 Application1000/1001，无匹配 |
| runtime2 / PID44244 | 正常 exit 0，forced=false；退出后31.014975秒同类查询，无匹配 |

快速20次关闭发生在媒体 End 状态；不冒称覆盖了每种播放状态或每一个 native 指令交错。runtime 第一轮有两次 SpeedClose 进入（第二次 pending=0），共享关闭正常结束。未知历史 AV 的因果链仍未证明，两次成功不能冲销历史失败。

第一轮控件隐藏，SpeedButton 查询失败；一次 focus 被 `foreground_not_target` 拒绝，未绕过保护。键盘发送返回成功但速度未变化，明确不算普通调速通过。第二轮实验 ShowControls 只设置生产 IsControlsVisible，随后直接操作**原播放器内**的真实 Overlay 控件，没有另造控件或直接调用 handler；弹窗按钮改用当前 UIA 名称/PID 定位后成功。启动时曾尝试设置不存在的 runtimeconfig 属性，PowerShell 报错；本次 `PublishAot=false` 的两个功能采用 JIT 默认值，应用随后正常启动，不是应用启动崩溃。

`source-equality.json`、`consumer-pdb.json` 与 `verify.py` 核实所有目标生产 source 与消费者逐字节一致、C# checksum 在本次 PDB 中、consumer layout DLL 与 PE/PDB 匹配。runtime2 保存实际已加载模块路径及对应磁盘文件 hash：Core/Share 与224相同实验包，MpvKernel.Core=`7e0b03fa…`、coreclr=`46a09d97…`；没有读取进程内存。runtime1没有补称保存了 loaded-module hash。最终 native source/launch/runtimeconfig 与证据一同归档。

两个 owned 包已按准确身份/安装路径移除，无本任务进程遗留；UI yield 返回 released=true。保护身份未操作。没有新增 native 失败，已有 HOLD 仍保留。

## 依赖与交接

本区 kernel 初始 HEAD `f02c33f639addd5d421700c43fa66382beafe323`、474 项删除；在 TEMP/bili-dc85fdad-kernel-initial 保存 HEAD、status、staged/unstaged binary patch 后，仅本区从现有本地来源 fetch 并恢复 gitlink 指定 `0ae5e4e000a41a07f6696746706df11c06e8835b`。没有改父区/来源区、正式依赖或 gitlink。

历史 runtime5 FAIL、G1 FAIL、AOT/正式依赖及其他既有 HOLD 均不由本任务取消。本片不扩展多触点矩阵。交还父代理进行一次独立审查；本实现代理按指令不创建审查子代理。

完成前已执行 `git merge --no-edit feat/performance-45`，父最新本地 tip 仍为 bd02775b，返回 Already up to date。WinUI 自审确认八处调用接线、UI线程接纳约定、finally异常传播、关闭/直接释放顺序及延迟回调，不存在仅 IsDisposed 先检后用的方案。最终 diff check 通过；独立审查尚未执行。

`evidence.zip` 含红绿原始TRX/编译输入、全套TRX、构建日志、两轮JSONL/截图/退出与事件证据、source/PDB metadata、实验输入和kernel初始备份；不含dump/native二进制或认证材料。`collect.py` 仅归档自区，`verify.py` 校验已有证据而不重新启动应用或重复跑测试。


## 父工作区 speed 子集集成（2026-10-31）

以上为候选原始历史记录，原样保留，包括 red 和历史 563 项；不代表本次父测试数量。
本次从 clean `bd02775bd9a48d7e62c169a6367eec58707dad8e` 出发，kernel 为 clean
`0ae5e4e000a41a07f6696746706df11c06e8835b`。按独立审查
`d5c519dd4924997ae41ab0358c728b63763e8f7e` 的边界，先 `git apply --check`，再应用
`git diff 7ac24a44 161a47c3 -- src` 五文件 patch，仅额外复制候选 Playback.cs 的
`CapturePlaybackValidity()` 到 Speed partial。未 cherry-pick 或合并整个候选链。
probe 直接编译 Speed partial（含 helper），不再从 Playback.cs 提取；原八项测试断言不变。
正式依赖、gitlink、父 Playback/seek/pointer 源码、测试工程和关闭共享任务/历史保存语义保留。

fresh 定向：Passed: 8, Failed: 0, Skipped: 0。
fresh 父全套：Passed: 539, Failed: 0, Skipped: 0。
精确命令、TRX、构建及门禁结果见 [integration.md](integration.md) 和
[integration-summary.json](integration-summary.json)；原字节 gzip 存于 integration-evidence。
标准构建脚本复用于本目录 validate-integration.py，记录当前源码 hash/diff，不能把父 HEAD 源码树当成已修改工作树。

**范围纠正与待办**：原 native 记录只有普通加速在 Playing；快捷按钮、slider 和快速关闭发生在 End。
重开后的 Playing/1.75 是沿用设置，不证明 Playing 下操作 slider。本次没有 UI/native 重跑。
Overlay 同实例 Unloaded 后 Tick 退订、Loaded 未重订阅为既存 P2；待后续单独补对称幂等订阅并验证同实例重挂，本轮不扩修。
未带入候选 Legacy seek P1；父既有 #49 pause FAIL、历史 runtime5 AV 根因未明、G1 FAIL、AOT BLOCKED、
认证组合未验和总体性能 HOLD 等仍保留。测试证明应用调速接纳/排空边界，不能证明全部 native 生命周期安全。
本轮无 push/PR/issue、登录身份、桌面操作或子代理。
