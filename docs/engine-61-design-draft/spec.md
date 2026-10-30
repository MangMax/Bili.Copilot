# #61 弹幕暂停场景与渲染生命周期 Spec

状态：**proposed，待新的独立审查，不是 accepted 或实现授权**。2026-10-30。
关联：[母票 #61](https://github.com/MangMax/Bili.Copilot/issues/61)、[验收 #49](https://github.com/MangMax/Bili.Copilot/issues/49)、[性能 #45](https://github.com/MangMax/Bili.Copilot/issues/45)。范围 L，难度高：跨线程资源寿命、暂停意图排序和可见兼容契约。母票只跟踪；下文切片只在规格独立评审及各自待决项解决后进入实施。

本轮核验应用起点 `4861038a87bf10e0c92722a0944377c779730384`，与本地 `feat/performance-45` 相同。按仓库 `docs/*-draft/spec.md` 草案惯例放置；架构理由见 [ADR 草案](adr-0001-render-ownership.md)，独立交付与红绿矩阵见 [acceptance.md](acceptance.md)。不更改工单、外部仓库或正式依赖。

## 1. 目标和证据效力

保持 #49 的用户目标：活动引擎就绪立即安排重绘，合并设置更新；暂停、当前位置、样式和内容正确；切换/卸载后旧工作不再污染新实例。#61 解决其引擎层阻断，不重做已集成的应用重挂/重绘会话修复，不引入预取、全局调度框架或顺手统一两引擎全部行为。

以下标记 **F** 为本轮从精确源或应用调用者核实的事实，**H** 为历史运行证据，**P** 为本草案推荐，**D** 为必须裁决的产品/兼容语义。P 不自动成为既有契约。

- H：历史原26屏障为 original 9/26、candidate 26/26；只支持当时受测调度行为。暂停前 seek、双向暂停切换首帧仍 FAIL。最新 [集成纠正](../performance-acceptance/integration-engine-49-diagnostics.md) 优先于历史报告冲突表述。
- H：两张历史 Win2D frozen 图渲染区 RGBA 相同，**撤回白字消失结论**；trace 有限窗口未复现。这里不把消失作为已确认缺陷或“修复”目标，也不归因 GPU/driver。
- H：历史“无钩子”DLL 声明不成立；后来的 clean original/candidate 元数据证据不能追认旧视觉运行，也未完成 clean UI 验收。失败 candidate/rejected render patch 都不是本设计依赖。
- 本轮只读源码、文档、`gh issue view`，不构建、运行测试/应用、安装下载、操作桌面或读取登录数据。小证据 [source-verification.json](source-verification.json) 是45个 original 文件和归档 hash 的 fresh 核验，不是 DLL 来源或运行证明。

## 2. 可复核源码索引

`U/` 均指引擎提交 `4d0f8691f08f1e143294402477e72306a1b38b6f` 的 `src/`，**不使用 candidate scheduler 行号**。本机原源位置及逐文件 SHA256 在 source-verification.json；归档 SHA256 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`。它们是既有 ignored 本地材料，不随文档携带；缺失时可审阅 hash 和历史证据，但不能声称重核源文件。

`A/` 指应用起点的 `src/Desktop/BiliCopilot.UI/`。下表路径和行号均为该基线，不将后来文档合并行号套到别的源码版本。

| ID | F：实际源码事实 | 可复核路径与行号 |
|---|---|---|
| F1 | UpdateTime 进队；0被当空；倒退/跨度>5000隐式 Seek；Seek Stop 后无条件 Resume；显式 Seek 只移游标 | U/Danmaku.Core/DanmakuScheduler.cs:80–94,138–156,256–330；U/Danmaku.Legacy/DanmakuFrostMaster.cs:169–186,222–248,325–364 |
| F2 | Core 同步事件进入 render；Legacy 调度是 Windows ThreadPool，实时添加直接调用 render；并非都在 UI | U/Danmaku.Core/DanmakuRenderer.cs:256–274；U/Danmaku.Legacy/DanmakuFrostMaster.cs:64,269–280,392–405 |
| F3 | GameLoop 自建线程与 dedicated Dispatcher 是两个通道；构造/Start/Stop 等事件；TryEnqueue 返回未处理；Dispose等5秒，shutdown等2秒 | U/Danmaku.Core/GameLoopThread.cs:80–94,97–180,196–217,220–330 |
| F4 | Core 画帧复制可变 render item 引用后解锁；Clear 可以回池/Dispose；UI queue执行Resize | U/Danmaku.Core/DanmakuDirectXRender.cs:514–565,608–616；U/Danmaku.Core/Models/RenderLayer.cs:80–104；U/Danmaku.Core/DirectXResourceManager.cs:414–444,479–499 |
| F5 | paused挡住Core RenderFrame；stopped挡住两引擎添加；首次时间和Top X在Update里初始化，Draw跳过未初始化项 | U/Danmaku.Core/DanmakuDirectXRender.cs:155–159,419–450,533–534,762–781；U/Danmaku.Legacy/DanmakuRender.cs:186–190,634–707,802–805 |
| F6 | Legacy Pause只改自有flag并Invalidate，非canvas.Paused；Update用动画TotalTime计年龄，Draw不以自有paused早退；Close才设canvas.Paused并移除视觉树 | U/Danmaku.Legacy/DanmakuRender.cs:546–595,634–678,748–835 |
| F7 | Core Top/Bottom默认寿命或DurationMs，严格大于寿命删除；rolling按宽度调速，X小于-1.2w删除；Legacy为X小于-w | U/Danmaku.Core/DanmakuDirectXRender.cs:430–474；U/Danmaku.Legacy/DanmakuRender.cs:684–732,885–888 |
| F8 | Core facade SetList排序并复制容器；GetList浅复制；scheduler持条目引用。Legacy Set/Get直接别名，AddList用新项列表替换旧列表 | U/Danmaku.Core/DanmakuRenderer.cs:196–214；U/Danmaku.Core/DanmakuScheduler.cs:207–242；U/Danmaku.Legacy/DanmakuFrostMaster.cs:302–322 |
| F9 | Clear清列表并Stop render；Stop清进度队列但不重置游标；Restart调用Seek(0)故原实现恢复播放；实时添加修改传入对象标记并可插列表，后续调度清IsRealtime并跳一次 | U/Danmaku.Core/DanmakuScheduler.cs:117–134,161–201,308–320；U/Danmaku.Legacy/DanmakuFrostMaster.cs:202–215,251–255,269–299,375–380 |
| F10 | 应用卸载/切换同步Close/Dispose后置null、清RootGrid；立即可以创建新引擎；没有资源释放完成观察者 | A/Controls/Danmaku/VideoDanmakuPanel.xaml.cs:48–86 |
| F11 | 重绘SetList→UpdateTime→样式→Pause/Resume；进度走UpdateTime，未调用公开Seek；实时文本insertToList=true | A/Controls/Danmaku/VideoDanmakuPanel.xaml.cs:143–162,180–198,250–305 |
| F12 | VM保留IsPaused和Position；播放器进度整数秒转入VM；播放状态另走Pause/Resume | A/ViewModels/Core/DanmakuRenderViewModel/DanmakuRenderViewModel.cs:30–61；A/ViewModels/Core/PlayerViewModel/PlayerViewModel.Methods.cs:518,666–682 |
| F13 | 应用解析器确实接收mode 7 Advanced，读取独立平移/透明度寿命；不是只有普通模式 | A/Controls/Danmaku/CommonDanmakuParser.cs:127–135,166–247 |

本轮 WinUI 静态审查：F3/F4 为 Error（UI等待和资源寿命协议缺口），F5为 Error（无法保证暂停首帧），F6/F8为 Warning（时钟及公共兼容待决）。此分级不是崩溃因果鉴定。没有生产XAML改动；未运行 WinUIAnalyzer、find-api 或 AOT。下面没有声称新增 WinUI API 已可用于当前包。

## 3. 方案比较与推荐

| 方案 | 优点 | 不能满足的条件 / 代价 | 判断 |
|---|---|---|---|
| A：继续扩stateGate、同步Stop/drain | 改动表面少，可序列化部分状态 | UI会等文本准备/绘制；移出锁又允许旧Start覆盖新Pause；引用快照仍无保活 | 拒绝；不是失败candidate的第四轮补丁 |
| B：全部UI线程执行、同步完成 | 容易理解调用顺序 | native准备/Present/重建占用UI；Legacy仍有动画回调，强迁移实际上更换渲染架构 | 拒绝 |
| C：纯数据调度 + Core单owner + Legacy动画owner + 异步退役 | 将意图/场景和native寿命分开；UI只发布；可测迟到提交与资源完成 | 需要显式终态、版本、兼容裁决和真实线程证据 | **推荐**，内部实现选择，不代替产品D项审批 |

仅在引擎内部设置命令/场景 seam：真实 scheduler 产生不可变数据，两个 render adapter各自管理资源。测试经公开操作及该内部提交/完成界面观察，不复制另一套算法，不把每个flag变成测试接口。公开 void 签名优先保持；“接收”与“完成”必须文档化，不用同步等待伪装兼容。

## 4. 有序入口和提交契约（P）

每个实例有 lifetime、contentEpoch、sceneRevision、intentRevision、resourceEpoch、递增sequence。每次公开调用在短纯发布临界区原子发布完整记录；那里只允许状态/队列操作，禁止遍历列表、用户回调、render、等待worker锁。大列表值捕获属于调用成本，不能放进共享发布锁；输入复制期间禁止外部并发写的兼容限制见D3。关闭与入队的最终校验必须同一线性化点，不能“先读closing，再无条件enqueue”。

调度 owner 只处理纯数据；批次带lifetime/content/scene/resource版本及目标时间，不带“处理结束时Resume”。选择可分段响应过期。renderer在接收、准备完成、帧开始三个点验证版本；被替换的资源留到owner退役。Pause/Resume只更新最新意图，不使正确seek场景失效；旧seek完成不能恢复旧意图。新Seek/Clear/SetList/Stop使旧场景提交失效；Close封闭整个实例。样式/resize版本让旧尺寸布局不能提交到新目标。

示例：Seek(g1)选完→Pause(i2)→Resume(i3)→旧batch到达：若g1仍当前，提交场景但使用i3；若期间Seek(g2)，g1终结superseded，不能改游标/时钟/画面。Close与生产者竞争：只有Close之前原子接收的命令可有已开始工作，其余closed；已开始工作也不能把结果接回新实例。

每个接收记录终态为 applied / superseded / closed / faulted；准备中不是applied。TryEnqueue拒绝、初始化异常、device-loss必须有终态及原因，不能留下永不完成的TCS。完成续体异步运行。UpdateTime可合并同一代次连续进度，但必须处理所跨区间，不能只丢中间项；不跨Seek/Clear/SetList/实时添加合并。保持现有5000/5001判别作为回归边界，不借机改预取。控制命令不可静默丢弃；队列负载边界需以突发测试验证，不建共享队列框架。

Pause返回表示意图已发布，不承诺取消已在途帧；PauseApplied在owner完成当前帧后冻结。后续过期seek无权覆盖。视觉验收从PauseApplied关联截图；另测UI发布不等待。若产品要求Pause返回当刻屏幕绝无最后一帧，属于新增强语义D1，不冒充现有保证。

## 5. Close / Dispose 与资源退役（P，D1约束）

逻辑状态：Open/Initializing → Closing → Closed；失败另记 FaultedRetirement，不能把“已拒绝输入”误报“全部释放”。重复Close幂等，复用同一完成结果。Close后的旧引用操作推荐无副作用拒绝，拒绝原因供内部诊断；不引入未评审的公开异常变化。

1. UI调用Close/Dispose在线性化点封闭admission、推进lifetime；同步脱离本实例的UI事件和视觉目标。对象引用移交retirement协调器保活。不得Wait/Join/GetResult、获取render list锁或在UI释放在途native资源。构造也不能等 `_startedEvent`；初始化有pending状态。
2. owner检查关闭令牌，不再开始新场景；在途CPU帧完成后丢弃旧提交，释放准备失败/过期的临时资源。已经提交GPU/合成器的内容无法被取消令牌撤销；旧panel脱离，新实例有独立目标。不能保证Close返回前所有显示管线像素已消失。
3. owner完成场景、缓存、target/context资源退役；UI-only detach与owner清理通过异步确认衔接，不能相互同步等待。调度worker退出后才释放其信号。最后从存活协调上下文异步发起并观察dispatcher shutdown。
4. 内部 `RetirementCompletion` 区分 NativeResourcesReleased、UiDetached、SchedulerExited、QueueShutdownCompleted，全部满足才Closed。不是公开新API承诺；正式外部消费者若需要等待，则单独评审可异步观察的公开扩展。超时只报告故障/保留所有权，不抢先Dispose或宣称成功。

不把CPU队列排空等同GPU完成。资源由合法API引用/释放规则保活；需显式GPU同步的操作必须使用该资源类型要求的completion，不能用2s/5s睡眠。device-lost仍需退役旧资源域；无法恢复则故障终结，不能跨device复用bitmap。

应用F10支持“逻辑关闭后换实例”方向，但没有证明所有第三方消费者接受异步Dispose；这是D1。关闭协议不将整个现有Dispose包进Task.Run（其中含XAML操作）。

## 6. 两引擎的资源与时钟（P）

### Core

保留单一、常驻到退役完成的真实执行通道，推荐专用Dispatcher owner；去掉T1手动执行与T2队列二选一。Pause/Stop只改变场景，不启停native队列。所有文本layout、槽位、render item/池、D2D context、D3D immediate context、target、Draw/Present及释放在该owner串行执行。这是本设计选定的所有权，并非断言所有COM资源都天生线程亲和。

XAML创建/挂接、尺寸/DPI采样、事件订阅、SwapChainPanel绑定/脱离留UI。UI将尺寸值与版本交给owner；owner完成创建后发UI绑定请求，旧lifetime的绑定必须被拒绝。不跨线程读取XAML属性。

resize不在UI执行：旧帧结束→解除旧target绑定及全部back-buffer引用→按API要求ResizeBuffers→重建target→发布resourceEpoch→以最新场景请求帧。尺寸为0则挂起准备、保留最新目标，不消费后丢弃。Draw/clear/resize统一顺序后无需无锁可变引用快照；若保留快照，必须持有真正scene/resource租约，数组复制不算。帧queued-or-running合并，成功绘制/提交后才确认dirty版本；失败不清dirty。持续失败进入faulted，不能自旋重试。

### Legacy

真实执行路径必须分别记账：UI master创建CanvasAnimatedControl；Windows ThreadPool运行调度；CanvasAnimatedControl触发Update/Draw。**UI Dispatcher不是动画线程的替代品**。F6和历史 [trace](../performance-acceptance/engine-49-trace/README.md) 说明自有paused与动画TotalTime不同；不能从自有flag推出动画时钟停止。

推荐把native准备、列表/槽位、动画逻辑时间和清理排入同一动画owner，在Update/Draw入口先处理命令，暂停仍处理零步准备与Draw。UI只发尺寸/意图、负责视觉树；CreateResources设备通知通过resourceEpoch进入owner，不假设其回调必在UI或始终同一线程。实施首关记录真实managed/native线程ID和回调序列，若目标包不提供可证明的动画owner排队/退役能力，停在该切片，补充设计，不偷换成UI执行。

DrawingSession只在当前Draw回调使用，由框架管理；SpriteBatch在回调内Dispose；CanvasRenderTarget不跨device域，最后一次引用/batch使用完成后退役。Close不能先canvas.Paused/RemoveFromVisualTree导致清理队列永远没有回调：先封闭输入及从可见父容器脱离，保活动画owner至清理ack，再在UI完成终止/RemoveFromVisualTree。卸载让回调不再可达时，必须有已核验的异步调度/终止完成路径；这是实施能力门槛，不是假设已存在API。

逻辑时钟推荐由owner维护activeElapsed：播放才累计，暂停为0，恢复第一步重建动画采样基点，排除全部暂停墙钟间隔。帧中所有项共享同一采样。seek设置scene目标与各项age，不将绝对动画TotalTime直接减旧FirstRenderTime。暂停前已可见Top超过默认3s墙钟后恢复，不应立即过期；rolling也不能积累暂停位移。速度是否影响Top寿命维持原不受rolling速度影响的行为，其他媒体速度语义见D2。

## 7. 暂停首帧与活跃集合（P，D2约束）

将场景状态 Empty / PendingResources / Preparing / Prepared 与播放意图分开；Stop不再作为“暂停准备”的实现。显式Seek(t)本身必须请求准备与Draw，无需后续UpdateTime。隐式seek与显式seek走同一场景提交协议；当前应用Redraw的多次公开调用仍按顺序收敛到最终配置/意图，不许旧准备覆盖最后Pause。

第一可验收增量是单条Top@20000ms：暂停→Seek(20000)→资源就绪→布局(delta=0)、设置age=0和居中X→Draw/Present→PausedPrepared。没有临时Resume、额外tick或以空白通过。这个切片不等于完整#49内容正确；rolling出生X=画布宽，完全不可见可能是正确出生位置。

完整目标场景建议采用“目标配置下的确定性重建”，不是“保留播放前史绝对一致”：输入是list/content版本、t、配置/尺寸版本，输出active items及age/位置/剩余寿命、槽位、密度决定。以下规则待D2裁决后才能实现：

- Top/Bottom：s=StartMs≤t；a=t-s；d=DurationMs>0时用定义值，否则3000ms。保留a≤d（遵循现有严格`>`移除）；remaining=max(0,d-a)，下一正时间步越界才删除。X=(W-w)/2；Y沿各引擎既有槽位和bottom margin规则。t前1ms项的age为1，不能当新出生再活满d。
- Rolling：按目标配置测得w，以本引擎速度函数v(w,配置,DPI)重建位移。恒配置时X=W-v*a；Reverse为X=-w+v*a。Core rolling保留至X≥-1.2w，Legacy至X≥-w，reverse均在X≥W删除。剩余时间按相应退出位置计算，严格边界保持源语义。v≤0时不能用有限回看窗，必须保留未退出项或拒绝该配置（D2需裁决）。可视相交集合与活跃资源集合不同，出生rolling不可见不代表不活跃。
- 寿命并非统一5000ms。先按每项实际寿命或轨迹排除已退出项；密度/槽位会受更早项影响。推荐从确定起点按稳定(StartMs,原始序号)重放布局事件到t，或使用同content/config版本的可信检查点。不能只筛数学active项再随意分槽并宣称等价；没有证明上界时允许可取消的全数据重建，不暗设固定回看常数。性能若不可接受另做索引切片，不丢内容换绿。
- 动态速度/字体/尺寸：使用当前目标配置重建整个场景可确定，但不等于历史速度积分。另一选项是保存配置历史/检查点重放，可保留历史轨迹但新引擎无该历史、成本大。**推荐前者仅用于seek/rebuild**，正常播放沿用实时速度变更；双向切换是否必须精确同位置、密度淘汰项是否重现，仍为D2，不能据F7自行定案。
- Advanced/Subtitle不能套rolling公式；本次仅定义普通Top/Bottom/Rolling/Reverse的规则。F13确认应用能输入Advanced，故普通模式通过仍不足以签署全内容兼容；需单独裁决并补充Advanced平移/透明度/延迟重建规格及≤M切片，或者明确收窄本次验收且继续保留#49未完成。Subtitle公共入口的消费者范围也待审，不能把未覆盖模式称为全量通过。

媒体时间与动画时间不能默认等价：上述a=t-s是推荐的seek目标年龄定义；正常播放目前由动画/Stopwatch经过时间驱动，应用ExtraSpeed只乘rolling速度（A/Controls/Danmaku/VideoDanmakuPanel.xaml.cs:243–247）。在倍速下，两者可能不同。D2必须决定seek使用媒体年龄还是按播放速度换算的视觉年龄，并明确切换时位置要求。推荐以媒体年龄定义目标重建，正常播放保留现有速度规则；这是可预期但可能改变倍速seek画面的选择，**尚未批准**。验收需含0.5×/1×/2×，不能只用1×证明兼容。

## 8. 兼容决策表与未决项

| 项目 | F：实际行为 / 应用证据 | P：推荐 | D：独立评审必须明确 |
|---|---|---|---|
| Close/Dispose | F3/F10，返回后应用清视觉树；无可靠drain证明 | 返回=逻辑关闭，另有资源退役完成；UI不等worker | D1：库外消费者是否依赖同步释放；公开完成观察是否必要 |
| Seek与Pause/Resume | F1隐式Resume是阻断；F11/F12要求保持VM意图 | 最新intent独立，Seek保留意图，prepared暂停首帧 | D1：返回/Applied与最后在途帧边界是否接受 |
| Clear | F9清数据/画面，原游标未重置、render stopped；VM Clear不修改IsPaused | 空场景、失效旧工作，游标重置，保留意图；后续Add/SetList短跨度可恢复准备 | D4：Clear之后无需Resume即可准备是修正后的语义，不称原已保证 |
| Stop | F9保留列表/游标，清队列与画面 | 暂停意图、空场景、保留当前位置/列表；UpdateTime可记位置但不重新显示，Resume按当前位置重建 | D4：Stop后Update/Seek何时解除停止，不能从未使用推断 |
| Restart | 原Seek(0)无条件Resume；应用panel未调用 | 原子设置t=0并显式播放，区别于Seek保留意图 | D4：确认保留原行为，测试paused Restart |
| Set/GetList别名 | F8，Core容器复制但条目共享，Legacy容器也共享；panel传新解析列表，无后续修改 | 命令输入捕获值快照、Get返回快照；保留签名和稳定排序 | D3：这改变Legacy别名和两引擎条目别名。未经明确批准不得宣称兼容或推广包 |
| AddList | Core追加ID去重，不保证重排；Legacy仅留下新项；panel未调用此入口 | 本票先特征测试并保留差异，若统一另立兼容切片 | D3：追加、同ID、空ID、乱序策略；不悄悄修Legacy“丢旧列表” |
| Realtime | F9/F11，立即render且可插入，IsRealtime跳一次；Core facade未使用可选layerId，Legacy用默认layer | 当前scene显示一次；insert=true进入时间线，后续普通tick不重复；seek重建按时间线规则，insert=false仅本scene；保留路由差异 | D5：暂停实时文本是否立即静止显示（推荐是）；seek回访、空ID稳定身份、调用对象标记副作用是否保持 |

D1–D5均需新独立审查给出“采纳推荐 / 改选项 / 保留兼容限制”，并记录依据。用户已经授权自行设计；这里不要求重新描述目标，不把待决写成用户已同意。涉及别名的无同步外部写入无法被引擎短锁自动保护；“容器仍返回List”不代表线程安全。若要求外部任意并发修改与唯一数据所有权同时成立，现接口不足，重新定级，不能仅靠快照重试掩盖。

主要未决风险：动画owner在卸载/设备丢失时的可达退役机制；异步Dispose的库外兼容；目标配置重建与播放前史的差异；高密度确定性重放的成本；所有模式覆盖范围；历史失败实验崩溃因果仍未定位。以上任一点不允许靠26项绿、构建成功或未复现关闭#49。
