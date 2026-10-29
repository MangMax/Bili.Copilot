# #57 — 首段展示与邻段预取调查

结论：**两种实际引擎都能先显示首段，不必等待慢邻段；现有 preview6 接口不能直接组成正确、无缝的滑动窗口。** Legacy 的批量“追加”会丢掉旧列表；两引擎替换后都需重建消费位置，显式 seek 又会恢复播放。离线实验复现了这些差异，并出现未解释的暂停后空白现象。因此建议先落实引擎状态契约，再接入有界调度；当前不建议直接将每个到达分片交给 `Initialize` / `AddDanmakuList`。

本票只提交报告、可复现实验和三张 ≤M 后续草案。没有生产代码、依赖版本、kernel 修改，没有 push、PR、关闭 issue 或发布后续票。调查未超半工作日。

## 基线与范围

- 独立工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-a35e7dde`；分支 `t3/investigate-performance-57`。首次 clean HEAD 与本地 `feat/performance-45` 均为 `47f72537fb0cf8fb160042ea07ef395639f8cdd1`，已含 #52 首次集成。
- 已读 AGENTS.md、docs/agents 的 issue-tracker / triage-labels / domain 规则，gh 读取 #57/#45 全文及评论，并执行 `gh issue edit 57 -R MangMax/Bili.Copilot --add-assignee '@me'`。
- 已读 [#48 报告](issue-48.md)、[48 集成证据](integration-48.md)、[#49 报告](issue-49.md)、[49 集成证据](integration-49.md) 及相关生产代码。#48 提供六并发、取消与迟到结果隔离；#49 合并重绘并读取最新位置/暂停状态。此前纯逻辑测试、登录页启动证据不替代本票引擎验收。
- 两轮真实宿主编译基于初始基线核对的包，Debug x64 / JIT，Windows App SDK `1.8.251106002`，.NET SDK `10.0.403`，WinApp CLI `0.7.1`，Windows 11 build `26300`。宿主直接实例化真实 renderer，复用已有宿主部署做法和应用 logo/manifest；未引入完整 `VideoDanmakuPanel`、设置服务、播放器、网络与登录。这一边界不能证明真实播放器集成通过。
- 提交前本工作区已合入本地集成 `e6c12d7edc90f0e8390ec9550b127c2b23f261e5`，其中 `ae16b95` 增加 #52 修补和 #53，随后仅补集成报告。核对初始基线到该集成点的弹幕目录、VM、包版本无差异。kernel gitlink / 工作目录始终为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，未修改。
- 实际记录时间保留 UTC 原值：2026-10-29 18:09–18:17（本地 2026-10-30 02:09–02:17）是 UI 测量区间，不是整个调查耗时。
- 最后同步又合入本地 `20ccfa953eb2dc24551d1112eee986772d7ed991`（#53 排行修补、#54/#55 及集成说明）。弹幕目录、VM 和包版本仍与实际实验版本相同。实验不覆盖这些新合入功能；没有操作父工作区。
- 机器 CPU 为 AMD Ryzen 7 7840HS；系统枚举 RTX 4050 Laptop、AMD Radeon 与 GameViewer Virtual Display Adapter。未确认进程实际使用哪张 GPU，不按显卡推导性能或将捕获异常归因于特定驱动。最终宿主 App.xaml.cs 与实际编译临时副本 SHA256 均为 `EE0649A08B14876FD813C878E4569646F2E4163D583D2D1546D7707ED3C4C34C`。

## 按实际还原版本核对接口

应用 `obj/project.assets.json` 与独立宿主均还原 Core / Legacy / Share `0.0.1-preview6`。三包 DLL SHA256 见 [package-hashes.json](evidence-57/package-hashes.json)。Core、Legacy nuspec 指向同一个源码提交 `4d0f8691f08f1e143294402477e72306a1b38b6f`；不是从当前 main 推断旧包行为。

初次 `find-api` 因本工作区尚未索引失败，实际执行应用 restore、`winapp find-api refresh --project-dir src/Desktop/BiliCopilot.UI --json` 后，分别查询 `DanmakuRenderer`、`DanmakuFrostMaster` 成功。完整输出：[DirectX 元数据](evidence-57/api-core.json)、[Legacy 元数据](evidence-57/api-legacy.json)。工具提醒机器 SDK 28000 高于目标 22621、机器 Runtime 2.6 不参与应用 1.8 API 面；这里只使用明确来自包的引擎类型，并由真实宿主编译和运行交叉验证。

| 契约 | DirectX | Win2D / Legacy | 实施影响 |
| --- | --- | --- | --- |
| SetDanmakuList | renderer 先按 StartMs 排序，再调用 scheduler.Clear/赋新列表 | Clear 后直接保留传入列表，要求预先排序 | 两者均不重置 `_lastIndex`；不能把 Set 当完整 seek |
| AddDanmakuList | 按 Id 排除旧列表已有项，再 AddRange；不排序 | 计算本批不在旧列表的项，随后 `_danmakuList = items.ToList()` | Legacy 丢旧数据；DirectX 乱序早到项会留在列表尾部 |
| 批内重复 / 空 Id | 未提供可靠的批内稳定去重契约 | 同样没有 | 适配器按服务 ID 去重；空 ID 需保守复合键，不能把所有 null 当一条 |
| GetDanmakuList | 返回列表副本 | 返回内部列表 | 不可共享同一个 List 给缓存和 Legacy；Clear/Set 会清空原列表 |
| Seek | Stop → 重置游标到目标 → Resume | 同样会 Resume | 暂停 seek 必须恢复暂停，且不能让异步 UpdateTime 的隐式 seek 在后面又恢复 |
| UpdateTime | 后退或跳跃 >5000 ms 触发 Seek | 同类自动 seek 路径 | 不能把 UpdateTime 当无副作用的进度字段赋值 |
| AddRealtimeDanmaku | 立即渲染并标记实时，可插入列表 | 同类行为 | 不能用来补发整段历史弹幕，否则有提前显示/实时标记副作用 |

源码依据：[DanmakuRenderer](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Core/DanmakuRenderer.cs)、[DanmakuScheduler](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Core/DanmakuScheduler.cs)、[DanmakuFrostMaster](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Legacy/DanmakuFrostMaster.cs)。性能复杂度只能从实现看出追加存在嵌套 ID 搜索，不能据此给出实测耗时或优化比例。

## 实机受控实验与采样

可复现材料：[harness-57](harness-57/README.md)。合成源用真实异步 Task.Delay，真实引擎 DLL 输出到 WinUI Grid；请求数是该合成源实际启动次数，不是 HTTP 抓包数。无真实服务、凭据、网络分片或视频解码。

- 12 小时时间轴：120 个 360 秒段。Full 调度全部 120 段，最多六并发；每段 30 ms，末段 3000 ms。Window 仅取前两段。Primary 进一步让邻段耗时 3000 ms，但当前位置段单独提交。
- 普通每段 2 条；Dense 每段 20,000 条，共 40,000 条。捕获时只推进到 1001 ms，只有两条到期。这是高密度**数据集加载**，没有验收持续高密度同屏、GPU 饱和或长时播放。
- `submitMs` 用 Stopwatch 从 load-start 到同步 Set/Seek/UpdateTime 返回；包括合成获取、合并和排序，不包括前面的 renderer 创建，也不是首帧可见耗时。
- 可见等待使用人工查看的实际 PNG + UTC 时间：从指定起点到截图完成给出**宽上界**，包含 WinApp 命令、UIA、250 ms 观察暂停及截图开销。无逐帧采样，不能给出精确首帧时刻或 P95。
- 内存为进程在命名事件处 `Refresh()` 后的 PrivateMemorySize64 / WorkingSet64。无强制 GC，无峰值监视、堆转储、存活对象计数或 GPU 内存计量。顺序复用进程，旧 native 资源异步释放、GC 和工作集修剪都会影响读数；不拿这些快照声称节省了多少内存。
- 未重复 WPR `0xc5585011` 失败操作；未安装、升级工具、启用 Sandbox、变更系统策略或复制 runtime/认证文件。

第二轮（同一 PID 53144，屏幕区域捕获）原始事件在 [measurements.jsonl](evidence-57/measurements.jsonl)，完整命令时间在 [run2-captures.json](evidence-57/run2-captures.json)：

| 引擎 / 数据模式 | 提交等待 ms | 合成请求 / 条目 | 提交时 private / working set 字节 | 截图观察 |
| --- | ---: | --- | --- | --- |
| DirectX / Window | 47.80 | 2 / 4 | 366563328 / 327110656 | FIRST-0、FIRST-1；invoke 到截图上界 1495 ms |
| DirectX / Full | 3690.06 | 120 / 240 | 402350080 / 355225600 | 空白；4916 ms 仅为截图时间，不能作可见上界 |
| DirectX / Dense | 76.15 | 2 / 40000 | 428003328 / 392404992 | FIRST-0、FIRST-1；上界 1131 ms |
| Win2D / Window | 46.65 | 2 / 4 | 344727552 / 320143360 | FIRST-0、FIRST-1；上界 1143 ms |
| Win2D / Full | 3677.20 | 120 / 240 | 307482624 / 282988544 | FIRST-0、FIRST-1；上界 5606 ms |
| Win2D / Dense | 68.20 | 2 / 40000 | 286187520 / 264302592 | 空白；2779 ms 不能作可见上界 |

这些是单次事件值，不是随机化重复实验；不得当作完整应用 before/after 结论。第一轮默认捕获中 DirectX Full/Dense 也曾空白，而 Win2D Dense 可见。已保留两轮的不同结果，没有挑选某轮宣布全部成功。

Primary 慢邻段实验更直接：两引擎实际显示 `PRIMARY-BEFORE-NEIGHBOR`，截图完成均早于邻段完成；DirectX 提交 46.20 ms、首段可见宽上界 824 ms，Win2D 提交 46.91 ms、宽上界 855 ms，均启动两个合成请求。上界从 **primary-start** 起算，不含引擎创建。见 [primary-visible-bounds.json](evidence-57/primary-visible-bounds.json) 和 [probes2-captures.json](evidence-57/probes2-captures.json)。该实验不把邻段结果再写入引擎，因此不能证明连续跨段正确。

### 状态与可见结果

| Requirement | Evidence |
| --- | --- |
| 首段不等慢邻段 | `Primary` / 两引擎 `*-primary.png`，邻段完成事件在截图之后；真实 renderer 可见 |
| 追加保留旧项与排序 | `Append`：DirectX 4→6 条，旧 4 条保留，但 new@500 在未来项之后；Legacy 4→2，仅 new@500、future@360001。两轮公有 API 读数一致 |
| 防重复 | 输入包含旧 ID 1:0，两引擎未把该条再次存入；没有据此宣称所有可见重复已解决。批内重复、20 秒文本合并、跨段视觉重放还需后续验证 |
| 替换/时间轴 | `Replace` 后 count=1 但新 REPLACEMENT 不可见；`Seek` 显式回到 999 再 UpdateTime(1003)，两引擎各显示一条 REPLACEMENT。恢复消费位置有效；无缝保留旧动画未通过 |
| 暂停 seek 副作用 | `Rolling` → `SeekPaused`，未再次 Resume，但两张 seek 截图中条目向左移动，证实 Seek 会恢复动画 |
| 暂停稳定性 | 两轮 `paused-b` / 部分 Pause 截图消失或空白；第一次截图可见，第二次不能证明稳定冻结。**未通过 / 原因未定**，不把“Pause 调用成功”当视觉通过 |
| 关闭中断 | owned PID 64528（Win2D）及 43648（DirectX）分别 Full→Close；日志 closed/canceled，没有随后的提交，进程和窗口退出。仅证明宿主 lifetime，不替代真实 PlayerViewModel 生命周期验收 |
| 长视频与高密度 | Full/Dense 数据已实际交给两引擎；上述表含失败可见结果及原始内存快照。未完成 12 小时实播、持续高密度和多次 seek 后稳定性 |

截图工具的默认路径曾连工具栏一起漏捕，故补做 `--capture-screen`。第二轮仍出现部分空白，无法仅据此区分宿主准备时序、暂停/重绘行为与捕获链路；两轮原图保留，不把它全部归因于截图，也不冒称已经定位产品缺陷。只做了这次复测，没有靠不断增加等待“修到通过”。

## 调度模型（与实机分开）

[schedule-model.py](harness-57/schedule-model.py) 使用逻辑时钟，没有网络/WinUI/GPU/真实耗时或内存采样。[model-results.json](evidence-57/model-results.json) 保存运行结果。

- 在设定的 30/3000 ms 延迟下，慢第 2 段的全片屏障逻辑时间为 3000 ms，慢第 120 段为 3570 ms；当前位置独立发布的**模型值**为 30 ms。不是用户设备的性能测量。
- `seek-stale-and-order-dedup`：旧代次结果被拒绝；同时间项稳定按 ID 排序，重复 ID 合并。
- `long-traversal-cache`：遍历 120 段，缓存段数 ≤3；这不限制单段大小，不能代替字节预算。
- `close-late-completion`：关闭后拒绝迟到发布，缓存清空。
- `retry-budget` 明确为 DESIGN_ONLY；没有模拟测试假装验证实际服务重试。

## 建议边界与回退

1. **分片窗口**：段号使用服务元信息；本实验以 360 秒为 fixture，不将其默默硬编码成所有媒体契约。初始优先当前位置 k、再 k+1；按需保留 k-1，夹取到合法段号。当前位置完成即可发布，邻段失败/慢响应不能阻塞它。边界提前量先作为可调策略，未测得安全秒数，不给固定性能承诺。
2. **并发**：继承 #48 的会话取消和 dispatcher 代次检查；最多六个实际在途请求，初始窗口只需一到两个。按优先级队列出队，避免为全片先创建所有任务。取消忽略者仍可能占资源，不能把“每代六个”当跨代硬上限；播放器级 limiter 应计入尚未完成的旧调用。
3. **排序/去重**：canonical raw cache 按媒体 aid/cid/段号归属，服务 ID 去重；无 ID 用完整内容/模式/时间/颜色等复合键并记录限制。每次交付独立快照，按 StartMs + 稳定 ID 排序。现有 parser 还按内容在 20 秒内合并显示计数，须跨窗口保留该语义；不能把 ID 去重和文本合并混成一件事。
4. **缓存淘汰**：先限制原始分片为 k-1/k/k+1；额外加条目数和估算字节双预算。预算值需后续在真实数据/Release 上确定，当前进程快照不能推出最佳 MB。预算紧时先淘汰最远非当前段并暂停预取；单段超限不能静默截断弹幕，应保留既有正确路径或告知容量限制。UIA/渲染副本、解析对象和原始缓存都要纳入测量。
5. **失败重试**：保留 kernel 单请求最多三次尝试，调度层不要再套三次形成九次。记录 failed 段并允许用户重试/重新进入窗口时重试，邻段失败不阻止当前播放；取消不记失败、不重试。窗口离开和媒体变化取消旧请求，拒绝迟到结果。
6. **seek / 暂停 / 关闭**：捕获位置、暂停、显示/隐藏、引擎代次；只在匹配代次写入。加载结束时读取最新位置，不能重播请求开始时的时间。区分显式 seek（允许回看重放）和同位置补数（不得重放已经显示的项）。暂停/隐藏时禁止预取发布造成意外 Resume；不要使用固定延时“补一遍 Pause”。关闭先失效队列、取消，再释放引擎。
7. **引擎提交**：禁止直接使用 Legacy Add；DirectX Add 也只有严格递增时间且已经外部去重才有候选价值。可研究新快照 + 显式定位 + 状态恢复，但当前 Set 清场会中断在屏动画、重置窗口会导致消失/重放，这条路径尚未获得无缝验收。不要在每个邻段到达时全量 Initialize。
8. **回退**：保留现有全片六并发加载开关，默认采用已验证路径。增量适配器未通过对应引擎验收则该引擎不开启窗口功能。遇异常撤销当前窗口代次并清理，再在同一取消会话内走全片路径；避免把部分数据再追加到旧 renderer 导致重复。回退仍保留 #48 取消/过期保护与 #49 合并重绘。完整加载本身也有本报告发现的状态契约风险，回退不是宣称其所有暂停场景已经验收。

## 可执行后续草案（未发布）

### 57-A · 双引擎批量数据与 seek/暂停适配契约（M）

范围：仅弹幕引擎边界及离线契约 fixture，不做网络预取。依赖 #49 已合入的代次/重绘机制；不更改 bili-kernel。

交付：为每种引擎明确 replace、append、reposition、pause/hidden、dispose 语义；避免 Legacy 列表别名和 Add 丢旧项；记录可见项/消费位置的所有权。先复现本文 shorter-list、倒序追加、重复 ID、暂停 seek、暂停后空白，再实现最小适配。如果现有包无法保证原子性，结果应是保留禁用开关并提出单独依赖包修补票，不能偷偷扩大为重写引擎。

验收：两真实引擎各覆盖向前/向后 seek、空/短列表替换、批内/跨批重复、边界文本合并、暂停与隐藏后更新、连续替换/卸载；录屏或有效连续截图证明不丢时间轴、不意外 Resume、不重复显示和不清掉仍应显示的动画。无效捕获不能计 PASS。此契约未通过前不得启用 57-B 的生产窗口发布。

### 57-B · 会话内当前位置优先及邻段调度（M）

依赖：#48 取消链已合入；生产启用依赖 57-A。范围仅现有 IDanmakuService / DanmakuLoadSession / PlayerViewModel 的单播放器协调，不加全应用缓存框架。

交付：以有限优先队列替代全片任务展开；首段独立发布，k+1 预取；跨代在途上限六，重用完成缓存，保留失败状态和单层重试预算；切换/关闭停止调度且 dispatcher 再检查代次。

验收：受控 120 段，当前/邻段/远端三种慢响应顺序；当前可见不等待无关段；真实服务 handler 记录请求数、取消、失败尝试；seek 后只发布目标代次，关闭无后续发布；两引擎真实可见及回退验收。计数与首帧/提交指标分开，不用逻辑模型当验收。

### 57-C · 窗口缓存预算与长时密集验收（M）

依赖：57-B 的有界数据所有权。范围仅弹幕 cache，不涉及首页缓存/通用缓存层。

交付：最多三段之外的条目/字节预算、最远非当前段淘汰、超大单段和失败条目策略；保留跨边界 20 秒文本合并语义及回看重放。先固定代表性 fixture，再确定有依据的预算值。

验收：长时间顺序遍历和往返 seek、不均匀密度、单段超限、同 ID 重试，验证缓存上限、取消后归属及内容完整；Release 同构建重复采样，报告进程、managed allocations、native/GPU 指标各自口径。WPR 仍不可用时明确替代指标限制，不称 private bytes 为堆存活对象。预算导致内容缺失或额外可见中断时回退且不开启默认配置。

## 验证、技能审查与未验项

- 使用 winui-dev-workflow、winui-ui-testing、winui-code-review；实验 XAML 前已读 winui-design 并查询 Grid sample，接口使用还原项目的 find-api。两个宿主构建均 exit 0 / 0 warnings / 0 errors，日志已提交。未运行 Native AOT 或完整应用 Release 构建。
- 使用 code-testing 审查 fixture 的可观测边界和负例；用户禁止子代理，全部内联完成，未创建委派。缺少 code-testing-extensions 参考包，按仓库 xUnit 现状执行。实验的 ready 文本不作视觉断言、无生产源码文本测试。测试研究/计划/状态保存在本 worktree Git 元数据，不提交。
- 使用 systematic-debugging 分离默认捕获缺失与实机状态异常；未找到空白根因，不修改生产代码或依赖来掩盖它。最终使用 verification-before-completion 校验提交范围、gitlink、日志与 cleanup。
- run-tests：无仓库 overlay，SDK-style net9.0 / xUnit 2.9.3 / VSTest。合入 `ae16b95` 后运行 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter 'FullyQualifiedName~Danmaku' --logger 'trx;LogFileName=issue57-danmaku.trx'`，19/19 passed，0 failed/skipped；解析 TRX confirmed executed=19，其他错误/中止计数为 0。只有既有 protobuf unused import warning。精确测试名见 [dotnet-test-results.json](evidence-57/dotnet-test-results.json)；这些是取消/重绘边界，不是 GPU/PlayerViewModel 端到端通过。
- 最后合入 `20ccfa9` 后重复同一过滤命令，增加 `--no-restore`、日志名换成 `issue57-final.trx`：exit 0，TRX 再确认 19/19 passed、0 failed/skipped/aborted。见 [最终日志](evidence-57/dotnet-final.txt) 与 [逐项结果](evidence-57/dotnet-final-results.json)。没有扩大到无关测试或声称全套通过。
- WinUI review：实验按钮均有 AutomationId，await 不阻塞 UI，关闭取消后不再 Set，目标身份固定且 cleanup 校验安装路径。实验黑底、固定字体、英文诊断与 code-behind 仅用于可观测 fixture，不是产品 UI；没有引入生产 MVVM/主题/本地化变化。WinUIAnalyzer 未引用，遵守不安装约束，其专门诊断未运行。
- `implement-spec` 未在已提供技能及本机 skills 搜索中找到；按 issue 的调查规格执行，没有声称调用了不存在的技能。
- 未验：完整播放器的 mpv 时钟、真实 HTTP、真实高密度持续帧率、字幕/高级弹幕模式、20 秒文本合并在窗口边界的精确视觉表现、暂停稳定性根因、连续跨段不消失/不重放、跨代忽略取消的硬并发上限、真实缓存预算与释放后的堆存活量。负面结果足以阻止直接上线，不要求凭据才能得出此结论。

## 证据位置与桌面释放

文本原始证据在 [evidence-57](evidence-57/)。PNG 不提交二进制，保留于本工作区忽略目录 `artifacts/issue57/`；`run1`、`probes` 是默认捕获，`run2`、`probes2` 是屏幕捕获。所有 PNG 已逐张查看，SHA256 清单见 [screenshot-hashes.json](evidence-57/screenshot-hashes.json)。原始 capture JSON 中 `_artifacts` 路径是采集时路径，随后整体移入 `artifacts`；按末尾子目录/文件名对应即可。原始 `visualReview=PENDING` 是脚本未作图像断言，人工结论以上表及 [visual-review.json](evidence-57/visual-review.json) 为准。

独立身份 `BiliCopilot.Spike57.A35E_y1mf40g4r9srg`，布局 `%TEMP%/BiliSpike57-a35e-layout`。owned PID 64528、43648、53144 均已通过自己的 Close 按钮退出；窗口查询为 []，最后进程查询计数 0。先复制日志，再检查精确包名和 InstallLocation 匹配 owned 布局，仅移除该包，复查包计数 0；已执行同 workflow `bili-spike57-a35e` 的 `winapp ui yield --on local`。**桌面已释放。** PID9608、Dev/生产包、认证数据及父集成工作区均未操作。临时布局和源码只作本地证据，不提交 runtime 二进制。
