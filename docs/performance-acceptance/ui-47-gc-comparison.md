# #47 同源 GC 暂停与有限对象存活验收

2026-10-30。**受控暂停实验完成，生命周期有限通过；自然分配 GC 下的完整存活验证、根分析及完整应用验收未完成，不关闭 #47。** 没有生产代码、依赖、kernel、系统策略或安装变更。未登录、未访问真实外网图片、未操作 PID 9608/Dev 包/凭据，未 push、PR 或 issue 操作，没有子代理。

## 基线与同源控制

初始分支 `t3/gc-pause-lifecycle-validation`，HEAD 和本地父 `feat/performance-45` 均为 `be555ee3e33073d56789e9da0a3c4ebca00a03a7`，初始 `git status --short` 无输出。结束前 `git merge feat/performance-45` 返回 Already up to date。正式 `src/ tests/ externals/` 与该基线无差异。历史 403 测试未重跑，不称 fresh；未重试已知缺 MSVC 的 AOT。

使用真实生产 MarkdownTextBlock、模板、Markdig、WinUIRenderer、MyImage 等，共 **57 个 Markdown 文件**复制到 owned 测试项目。当前臂逐字节等同生产；历史臂只在 `Clean()` 的 `_renderer?.Clear();` 后恢复原位置的 **`GC.Collect();`**，包括 BOM 在内的其他字节完全相同。不是旧整版与新整版比较。[精确差异](evidence-47-gc/controlled-difference.patch)、[八次来源核验](evidence-47-gc/verification.json)。原 `MyFlowDocument` 长期属于控件；此实验的 document 弱引用指其真实 **RichTextBlock 文档视图**，另跟踪首个真实 Block，未反射私有字段。

两臂新空目录构建，自包含 **Release JIT x64 / .NET 9.0.22 / workstation GC**，非 AOT。SDK 10.0.403、winapp 0.7.1、WindowsAppSDK 引用 1.8.251106002。两个最终 build 都退出 0，4 条继承的 CS8632 警告、0 错误；[current](evidence-47-gc/build-current-e.txt)、[historical](evidence-47-gc/build-historical-e.txt)。每轮新进程、新布局，唯一临时身份 `BiliCopilot.GC47D0E5`，轮末移除。实际加载模块、布局与 build DLL 的 SHA256 相同：

- current：`686EF504EB5F085FB666CE14E4708F044309656BC3DAF36BD408B4504706D70B`
- historical：`8051E02E01846652481D15DBB59EA83D4D0B1DBFED98CC7D07FE428AD62F6797`

每轮 `modules.json` 还保存实际 CoreCLR/WinUI DLL 路径、版本、hash。两臂测试宿主 hash 相同。固定 Markdown SHA256 `885A18CB6809E8846C700AE663414FE8B1EF2F6A389FDA8EFDC970A1915686F4`，包含标题、ALPHA、超链接、图片、FINAL-OMEGA。固定 150×150 packaged PNG hash `055BAE673A25DFCC6335E0AD8CB5247EC96001BB4296A6894033F79419D5F177`，显示 96×96 DIP。provider 仅匹配 fixture.invalid 并读本地包图片，await SetSourceAsync 后发布，零 HTTP 请求。链接只核验文本/真实 NavigateUri，未打开外部浏览器或验证外部导航。

## 诊断盘点、小探针与定义

[盘点](evidence-47-gc/inventory.json)：dotnet-trace/PerfView 不在 PATH；已有 WPR，但历史 `0xc5585011` 策略阻塞未重试。发现缓存 TraceEvent **3.1.23** 和 DiagnosticsClient **0.2.510501**，无需安装即可实现 owned PID EventPipe。初次离线 PackageReference restore 因缺 Microsoft.Win32.Registry 失败；改成直接引用两份缓存 DLL，框架依赖用本机已有 ASP.NET shared framework。初次 net9 采集器启动因缺共享 .NET 9 失败；采集器改为已安装 .NET 10，宿主仍是自包含 net9。没有下载/安装依赖或更改全局运行时。

先在自有 console 小探针分配并自然释放短命数组，**无 GC.Collect**。3 秒总分配 6,080,000,000 字节，集合计数从 [0,0,0] 到 [725,1,0]。运行时 **725 个 AllocSmall GC**，725 组 Start/Stop 和 Suspend/Restart 完整配对、丢事件 0。[探针摘要](evidence-47-gc/probe-summary.json)、[前十组原始事件](evidence-47-gc/probe-event-sample.json)。完整 raw 与 nettrace 保留在 ignored `artifacts/gc47/`，摘要记 hash。此探针只验证采集能力，不作为真实控件性能或自然 GC 存活证据。

采集器使用 `DiagnosticsClient.StartEventPipeSession`，provider `Microsoft-Windows-DotNETRuntime`，keywords **0x1 GC**，Informational，64 MiB buffer，`requestRundown:false`。不采 heap dump，不请求 GC，不加载 profiler，不使用反射，不把强制收集作为测量清场步骤。PID 必须为本轮 launch 返回且可执行文件位于 owned layout，明确拒绝 9608。采集从 UI 预检后开始，覆盖预热、测量及空闲窗，之后 Stop 并等待流落盘，再关闭应用。

度量严格分开：

1. **暂停区间** = `GC/SuspendEEStart` → `GC/RestartEEStop`，使用事件自身时间戳，包含挂起协调/恢复开销；不是采集回调的到达时间，也不声称所有线程在完整区间内同时停止。
2. **完全挂起区间** = `SuspendEEStop` → `RestartEEStart`，单独保存在 paired.json / fullySuspendedTotalMs。
3. **Collection duration** = 同 Count 的 `GC/Start` → `GC/Stop`，另列 collectionMs，绝不当作暂停。
4. GCMemoryInfo 的 Index/Generation/PauseDurations 只是采样时**最后一次 GC**，不累加、不代替事件流；GC.CollectionCount 只交叉核验事件数，不推算时间。

按 UTC 边界选取完全落在 measure-start/measure-end 内的事件；每轮测量 Start/Stop 数与 gen0 累计差一致，首尾未配对、内层挂起缺失、丢事件检查均为 0。全部捕获 GC 为 NonConcurrentGC；没有将背景 GC 的整个执行段冒充暂停。原始 `events.json` 保留所有 Start/Stop/Suspend/Restart/Triggered 记录；其他 GC 事件仍在原 nettrace。每轮 trace-hash.json 保存路径及 SHA256，大 trace 留在 ignored artifacts，未提交二进制 trace。

## 固定协议与新结果

每进程先独立执行初始呈现、清空、重新赋值、3 次同实例相同 Text 卸载/重挂、空 Text 重挂后再赋值。每个内容状态要求完整 4 blocks、文本、真实链接、当前图片和正尺寸；空状态要求 0 blocks、卸载状态要求 !IsLoaded。等待布局与两次 CompositionTarget.Rendering 后重新检查当前 Source；有界 10 秒，不重设内容，不保留 Image 字典，临时事件订阅 finally 解除。

随后 **5 个预热周期、50 个测量周期、10 秒空闲观察窗（每秒一次 scalar sample）**。每周期新建真实控件并等待就绪 → Text="" → 断言清空 → 移除、等待 dispatcher、断言 Unloaded/清空 → 两帧 → 100 ms 固定间隔。历史臂每周期两次原清理路径共 **100 次明确的应用 GC.Collect 调用**；当前臂为 0 次。周期数/等待规则相同，实测窗口因布局和调度为 7.628–7.868 秒，空闲窗为 10.011–10.024 秒，不伪称相同精确 wall-clock 时长。无额外分配压力、无观察窗强制 GC。

顺序为 **C,H / H,C / C,H**；每轮一新进程。固定最终宿主八次均 **176 个功能断言通过**（合计 1408；其中预热也有断言），每次50测量周期。仅 final2 和无采集器 final7 各有一次 readiness recheck，raw 保留；没有未处理异常/失败记录。

| 顺序 / 模式 | 50周期 GC 次数（均 gen2） | 暂停总 ms | 最大单次暂停 ms | Text="" 调用耗时中位 ms |
| --- | ---: | ---: | ---: | ---: |
| 1 current | 2 | 16.148 | 10.386 | 0.167 |
| 2 historical | 101 | 107.309 | 2.280 | 1.513 |
| 3 historical | 101 | 109.434 | 2.435 | 1.510 |
| 4 current | 2 | 8.250 | 5.042 | 0.131 |
| 5 current | 2 | 9.633 | 5.982 | 0.127 |
| 6 historical | 101 | 93.527 | 1.731 | 1.270 |

精确数值以[机器摘要](evidence-47-gc/summary.json)为准；[比较图](evidence-47-gc/comparison.html)。**该固定小样本支持删除清理强制 GC 降低总暂停和同步空文本清理耗时，但不支持改善最大单次暂停**：current 的最大暂停反而更长。没有整体吞吐/交互延迟收益或统计显著性声明，样本量仅三组，桌面进程和系统负载未隔离。

### GC 原因归属与采集扰动

- **明确应用诱发**：historical 在既有 Clean 路径恢复的一行，每50周期执行100次；同源差异与功能边界可核验。该臂101个 GC/Start 均报 Induced；无调用栈，不能把逐个101事件都强行绑定到某次应用调用，多出的1次保留为未归因。
- **运行时 Induced 但未定位调用者**：current 每轮2次，分别 Induced、InducedNotForced。不能因代码无 GC.Collect 就标作 AllocSmall 自然分配 GC，也不能猜为 WinUI/interop 的确定调用。没有完整调用栈/root，因此保留归因缺口。
- **Profiler induced**：采集 API 路径未请求 GC，未采用 heap dump/强制清场。无采集器对照 current 仍有 [2,2,2]，historical 仍有 [101,101,101]；因此不能将 current 的两次全部归咎于本次 EventPipe。**这不是用无调用栈 trace 证明所有事件的 profiler 原因绝对为零**。
- **Natural allocation GC**：小探针明确看到 AllocSmall；真实宿主的六轮 trace 中未见 AllocSmall/AllocLarge 原因，10秒观察窗没有任何新 GC。真实控件“自然分配 GC 之后全部对象可回收”未证实。

额外独立无采集器 C,H 两轮（不混入暂停表）相同协议。Text="" 耗时中位 current **0.1318 ms**、historical **1.10425 ms**；三轮有采集器约0.13–0.17 / 1.27–1.51 ms。采集改变调度/事件写入，观察与标量日志也分配内存；此一对对照只揭示扰动可能，不能精确扣除或宣称零开销。未采样轮没有完整暂停总量，不能从计数/GCMemoryInfo补算。

## 对象存活与内存界限

每个 measured 控件创建3个非泛型 WeakReference：控件、RichTextBlock 文档视图、首个 Block，共各50个。Track/采样使用 NoInlining；日志只含标量、字符串和复制数组。每周期退出后不把对象写入集合；宿主的 shown 字段在测量前置空。仍可能存在异步状态机/JIT/WinRT 包装生命周期影响，所以不把弱引用计数称为根分析。

| 模式 | measure-end 活控件/文档/首块（50个中） | +10秒 | 空闲窗 GC |
| --- | --- | --- | --- |
| traced current 1 | 17 / 17 / 16 | 相同 | 0 |
| traced current 4、5 | 17 / 17 / 17 | 相同 | 0 |
| traced historical 2、3、6 | 1 / 1 / 0 | 相同 | 0 |
| untraced current 7 | 16 / 16 / 16 | 相同 | 0 |
| untraced historical 8 | 1 / 1 / 0 | 相同 | 0 |

current 至少33/50的控件/文档视图已不再存活，证明这批对象并非全被清理逻辑永久强持有；末尾16–17个仍活着而后续未发生 GC，**不能判泄漏**。historical 残留1个同样不能判泄漏。未证明每个对象已经历其释放后的可回收代次收集，也没有获取持有根。实验不通过额外 GC/反射制造全部消失。

records.json 同时记录进程 private bytes、working set、GC.GetTotalMemory(false)、累计 managed allocation、计数和最后GC统计。它们包含整个宿主、XAML、native decoder、JIT、ready/记录开销和分配器复用。**没有 native image allocation 数据**，不以 process−managed 相减冒充 native 图片内存，不以立即下降或对照差证明泄漏/无泄漏。图片固定小尺寸，不能推及大图/远程/长列表预算。

## 可见证据、失败保留与覆盖

[24张逐图复核及hash](evidence-47-gc/visual-review.json)。完整 current 目标采用 [final7初始](evidence-47-gc/final7-current-untraced/state-1.png)、[清空](evidence-47-gc/final7-current-untraced/state-2.png)、[重挂再赋值](evidence-47-gc/final7-current-untraced/state-3.png)、[观察完成](evidence-47-gc/final7-current-untraced/state-4.png)。完整 historical 初始/清空/重挂/终态见 final2 的 state-1…4.png。整个标题、ALPHA、超链接、粉色logo、尾行均完整，不裁剪目标。屏幕抓图包括窄窗口边框/背景边缘。

四张捕获不完整原样保留：final1/state-1 只有左端字符；final4/state-4 状态只显示左端；final5/state-4 和 final8/state-4 客户区全黑，连工具栏也缺失（final8使用screen capture也如此）。这些**不是完整视觉通过**，也不能据此认定生产文档丢失。final7/full 与 final2/full 是另一次独立观察，不能改写失败原图。本次不做新一轮截图故障修复；真实成因仍未隔离。

| Requirement | Evidence |
| --- | --- |
| 真实内容、链接结构、受控图片 | `Initial`、`MeasuredRender`（每轮50次），完整截图如上；不含外部导航 |
| 空文本清理并重新显示 | `EmptyClears`、`Reassign`、`CycleEmpty` |
| 卸载和同实例不变Text重挂 | `Unload0/1/2`、`SameInstanceUnchanged0/1/2`、`CycleUnload` |
| 空Text重挂后再赋值 | `EmptyRemount`、`AfterEmptyRemount`；记录真实 IsLoaded=true 和 blocks=0 |
| 新控件重复创建/清理 | 5×`WarmupRender` + 50×`MeasuredRender`，每轮精确数量核验 |
| 暂停与GC数 | `analyze.py` 完整配对/丢事件/计数一致断言；每轮 events.json 与 paired.json |
| 弱引用固定窗 | measure-start/end + observe-1…10，活数和集合计数；未强制GC |
| 来源/隔离/清理 | `verify.py` 逐字节单行差异、实际加载DLL/hash、8轮结果与process/package absent |

尝试历史不覆写：

- 初始 current-a 构建因测试 csproj 的第二个 Compile Remove 移除了 Markdown 显式 Include 而失败；修正为仅在 Markdown Include 前 Remove 默认 glob。非生产构建错误。
- r1-current（current-b 宿主）：`EmptyRemount` 早期复合断言失败，尚未采集；原断言没有记录哪个子项失败，不能声称确定是 IsLoaded。后续等待真正 Loaded 事件并记录独立值；原2张截图和records保留。
- c1-current（current-c 宿主）完成一次测量，但不是最终固定宿主，**排除六轮比较表**。
- historical-c 构建检查发现 BOM 丢失；未运行，改为保留 BOM 的 fresh historical-d，最终差异只有GC一行。
- h1-historical 预检 timeout，只留下fixture，未记录功能终态/trace；原因未确定，不称通过。
- h2-historical：初始图像在两帧后丢失ready，失败已记录，稍后失败截图内容完整。最后宿主按已有 #52 模式做有界当前Source重核验；不重设内容，finally解除事件。两个最终build均为新目录，旧artifact不复用。

已读/应用 dotnet-trace-collect（完整及 EventPipe reference）、winui-ui-testing/code-review/dev-workflow、code-testing（report-safe contract）、run-tests、verification-before-completion、故障时systematic-debugging；参考 AGENTS/docs/agents、status、#47、46/47 fixed/diagnosis、最新#52报告。专用 WinUIAnalyzer未安装/未跑，不以编译器冒充；测试夹具UI不是产品功能，没有更改正式XAML/主题。

## 复放、收尾与剩余边界

入口 [replay.ps1](harness-47-gc/replay.ps1)：传当前repo、全新scratch/evidence目录，先本地缓存采集器build和小探针，再两臂fresh build、顺序 C,H,H,C,C,H,C(no trace),H(no trace)。只允许已缓存工具；旧临时身份存在会拒绝接管。可按 [run.ps1](harness-47-gc/run.ps1) 单轮执行；精确本次路径/PID/DLL见每轮 provenance/launch。重放仍需桌面独占授权，不与别的UI任务并行。

```powershell
./docs/performance-acceptance/harness-47-gc/replay.ps1 -SourceRoot $PWD -Scratch C:/new-owned-gc47 -Evidence C:/new-owned-gc47-evidence
python ./docs/performance-acceptance/harness-47-gc/analyze.py ./docs/performance-acceptance/evidence-47-gc
python ./docs/performance-acceptance/harness-47-gc/verify.py ./docs/performance-acceptance/evidence-47-gc
```

最后两条本次新运行均退出0；verify检查8次run、57源文件、fixture、build/layout/加载DLL、5预热/50周期和cleanup。原始Trace提取命令为 `Trace.exe parse <trace.nettrace> <events.json>`；`compact-events.py` 仅剔除本分析不使用的GC事件类型。`artifacts.py` 从摘要重建比较图；visual-review.json是本次逐图人工记录，不自动给新截图签通过。

所有 owned UI 进程（含失败尝试）均结束，临时身份不存在。各轮最后yield原文保留；final8返回 **released:true**，后续独立最终检查再次yield返回 **released:false**（没有再次释放一个租约），不能把后者改写为true。[最终yield](evidence-47-gc/desktop-final-yield.json)。**桌面独占槽已释放**。未动其他进程/包，没有安装、策略更改或全局采集。

完成：无安装EventPipe能力验证、真实WinUI同源暂停对照、有限生命周期与弱引用观察、复放脚本/小raw/分析、hash和cleanup。未验：诱发事件逐个调用栈归因、真实控件自然分配GC后全部对象可回收、对象根、native图片内存、长期无泄漏、完整app/auth/provider、Release AOT、全部捕获可靠性、广泛视觉/性能预算。WPR的已知策略问题与AOT工具链限制依旧，但**不再把WPR阻塞当作本票无法得到任何暂停trace的理由**。
