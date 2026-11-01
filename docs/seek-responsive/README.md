# Seek 响应候选（尚未切换正式依赖）

本切片以父 `0d3a22f61575550e23f935028ec5f6376cac22ce` 为基线。正式代码只改 `VideoDanmakuPanel.xaml.cs`：隐藏时取消重建，跳过 seek、时钟、样式和列表重建；恢复显示时使用最新 VM 意图。允许清空后重建，不引入双缓冲。

**完整可见弹幕收益需要本目录候选引擎。** 正式 `Directory.Packages.props` 和 `packages/danmaku` 冻结包未改，用户安装包未更新。所有运行使用独立 `Bili.Seek870b` 身份、自有布局、离线服务和真实本地 MPV 媒体。不能把只合入面板改动称为完成可见密集 seek 修复。

## 根因与修改

冻结 Legacy 在同步 `SeekRequested` 调用栈中重放历史，持有播放锁；`ChangePositionAsync` 在弹幕事件返回后才更新进度 VM。无网络的 12000 条可见测试仍阻塞 6.63 秒，排除了网络作为这段阻塞的必要条件。普通 300 条也阻塞约 468 ms。原面板即使隐藏仍提交重建。

两引擎均逐条推进整个历史活跃列表，密集数据可形成平方成本，并为后来过期的条目生成纹理。Core 已有异步调度，不具有 Legacy 的同步 seek 阻塞，但原生重放仍有成本。候选直接计算目标年龄：固定弹幕按实际寿命过滤；滚动/逆向按原生测得宽度和现有速度公式判断存活，再创建资源。不按固定秒数截断历史。布局改为目标存活集合，不承诺恢复完全相同的历史轨道。

Legacy 使用原有更新线程处理最新 seek，逐项检查版本，播放锁只覆盖单条原生准备；停止、关闭、替换列表使旧任务失效。没有用 `Task.Run` 搬运 UI 资源。暂停意图在提交时读取。Core 保留既有 owner 线程和取消机制。

测量区分媒体 setter 返回、同步调用返回、进度 VM 通知、16 ms Dispatcher 心跳、原生就绪和关闭。**未用 profiler 将每毫秒进一步拆成文本测量/纹理/GPU 时间**，不能据此宣称某百分比来自纹理；源码证明无谓资源生成，前后实际运行证明整体成本和 UI 耦合改善。没有归因到 .NET 11。

## 证据及边界

`evidence.zip` 保留原始事件、构建日志、TRX、受保护包前后快照、每次 retained exit、退出后至少 31 秒精确 PID/path WER 查询和 cleanup/yield。`verify.py` 校验归档、包、PDB 文档哈希、运行判据并生成 `summary.json`。

- `red` → `green`：只改隐藏门禁，冻结 Legacy 12000 条隐藏 seek 从约 5088 ms 降至 0.89 ms，最大心跳间隔从约 6875 ms 降至 32 ms。
- `baseline-exact` → `final`：真实冻结 DLL 与候选 .5，Legacy 12000 条可见 seek 从 6628 ms 降至 1.34 ms；媒体确认→进度从约 6653 ms 降至 2.94 ms；心跳间隔从 6658 ms 降至 46 ms；新弹幕就绪约 1360 ms。
- 同组 Core 密集 seek 本来已快速返回；候选就绪约 870 ms，冻结约 456 ms，**此短时间密集样本没有证明 Core 就绪更快**。3600 秒弹幕时间轴的 12000 条测试中，Core 就绪从约 437 ms 降至 67 ms，Legacy 从约 16491 ms 降至 597 ms。
- `coverage2` 补验慢速滚动历史、长固定寿命、过期剔除、暂停坐标不动、恢复后寿命推进、快速 seek 最新目标、隐藏播放时钟、延迟服务返回、替换列表隔离、实际 pending 重建中关闭。最终数值见 `summary.json`。
- 同步父首页修复后，已有 seek/load/redraw 相关 44 项测试通过，0 失败、0 跳过；未重复全部历史审计。

延迟网络对照用离线服务 Task gate 控制完成时机，并非真实 HTTP 网络。长时间轴测试使用 3600 秒弹幕数据，实际媒体是 60 秒本地文件，长目标直接提交弹幕引擎。普通/密集分别 300/12000 条，不以小数据替代密集场景。原生就绪依据内部任务/目标时钟与活动资源，不是截图光学判定。

失败/无效实验没有删掉：`red/green` 的早期 PDB 观察器错误不作原生画面证据；`supplement` 的时钟断言和 PDB 观察器错误、`candidate3` 的列表引用比较错误均是夹具问题；`visible-red`、`visible-baseline` 因 WinApp recipe 从候选缓存取 DLL，实际仍是候选重复运行，不作红证据；`coverage` 在初始化前访问 Player 导致启动观察器异常，正常 ExitOwned 退出。真正红对照 `baseline-exact` 使用独立 recipe 指向冻结 DLL，启动后核对布局/加载哈希，未覆盖缓存。

仍有 O(N) 原生宽度测量；单个超长条目的资源准备仍可能短暂占锁；候选不保证所有数据、设备都绝对无感。未完成 AOT 全量资格验证、正式依赖接入和独立 review。此目录交给父任务做一次独立 review；不自动发布或更新用户包。

## 恢复及复核

候选唯一版本 `0.0.1-seek870b.20261101.5`，六个 nupkg/snupkg 在 `candidate-feed/`，SHA 在 `packages.json`。不得覆盖该版本，后续更改使用新版本。

- Core：冻结 `698d546e5c9edf5c96b6871357dc532542457c45` → `e094d330a69dbedeba9b0260b6bb499391d643c4`。
- Legacy/Share：冻结 `d7984643f4792133e7664c55e01df6e95e9cbc21` → `221bbb01917b71f2d305506572c0526138950e28`。
- `core.bundle`、`legacy.bundle` 是独立完整 Git bundle，可 clone 到新空目录；`.patch` 是对冻结源码的最小差异。`source-snapshot.zip` 另存精确构建源码和配对 PDB 全文档（含生成文件），`metadata.json` 记录 DLL CodeView/MVID，无诊断 hook。

在仓库根运行 `python -X utf8 docs/seek-responsive/verify.py`，以及 `python packages/danmaku/verify.py`。核查不会安装或启动软件。`prepare.py`、`build.py`、`launch-retain.ps1`、`cleanup.ps1` 用于本机复现：要求不存在 `artifacts/seek-responsive/full`，并依赖只读的 `../t3-39df52c6/artifacts/combined-60cd/full` 原生资产/离线 kernel 宿主；这不是跨机器自包含安装器。只在自有桌面租约和独立身份可用时启动。启动脚本持有退出句柄，完成 WER 后再运行 cleanup；禁止强杀用户程序。

`app-source-closure.json` 对照正式源码与实测宿主；仅 App/依赖注入及 Playback 的测量日志有宿主差异。`kernel-unchanged.json` 证明本轮保留 kernel 初始状态，未改变正式 gitlink。
