# #61 proposed 设计独立审查

2026-10-30。结论：**请求修改；不能整体 accepted，不能进入生产接线或发布。** 支持保留最新播放意图、消除 UI 等待、资源串行使用/退役、暂停零步首帧这几个方向。当前可先准备 C0 的只读兼容记录，以及独立的 API 能力 spike；不能把全部抽象预先合入，再用 26 项绿证明设计完成。本审查只交付文档，不执行这些后续实验。

## 范围、来源与证据效力

- 父基线 `feat/performance-45` / `4861038a87bf10e0c92722a0944377c779730384`。在独立分支 `t3/engine-61-design-review` cherry-pick 指定草案 `57fd13cd27815415fd711e072f3ee178e23da1ca`，本地对应 `dd26759`；四个原文件保持原样。审查比较为 `git diff 4861038...HEAD`，不改父分支。
- 读取 AGENTS、docs/agents、应用 `.editorconfig`，按 code-review 的 Standards / Spec 两轴、scope-gate、winui-code-review、verification-before-completion 自行审查；用户明确禁止子代理，未采用技能的并行代理步骤。未发现相关 glossary/正式 ADR 或额外编码标准。这里只审设计，不制造 XAML 格式、主题或 MVVM 问题凑数。
- GitHub 仅只读 [#61](https://github.com/MangMax/Bili.Copilot/issues/61)、[#49](https://github.com/MangMax/Bili.Copilot/issues/49)。#49 明确要求不改变可见效果，只允许必要的单一重绘会话边界；#61 允许先做架构设计，不授权全部行为重定义。
- 本文 `U/` 指 [source-verification.json](source-verification.json) 所列 original 根目录下的 `src/`，精确源提交 `4d0f8691f08f1e143294402477e72306a1b38b6f`。本轮重新计算全部 45 个列出文件的 SHA256，零不匹配；ZIP SHA256 为 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`，匹配记录。实际阅读 scheduler/master、render、资源管理器、GameLoopThread、RenderLayer、包配置等原源，不用 candidate 行号冒充原源。ignored 材料依赖原机器，不是提交携带的源码。
- `A/` 指父基线 `src/Desktop/BiliCopilot.UI/`。实际核对 panel、XAML、VM、播放器时间/状态调用及解析器。`VideoDanmakuPanel.xaml:13` 的可见性已有显式 OneWay；没有发现本草案引入绑定模式错误。风险在控件与 native 对象寿命，而非应当机械改写 x:Bind。
- 已审阅 [集成纠正](../performance-acceptance/integration-engine-49-diagnostics.md)、[根因审查](../performance-acceptance/engine-49-root-cause-review/README.md)、[失败候选](../performance-acceptance/engine-49-candidate/README.md)、[trace](../performance-acceptance/engine-49-trace/README.md)。**撤回“历史 frozen 白字消失”结论保持有效**；原渲染区 RGBA 相同，新 trace 未复现。没有新的截图/trace，不能归因 GPU，也不能声称修复消失。历史 9/26→26/26、暂停首帧失败、新 clean 产物与含钩子旧产物分别记账；本轮没有重新运行或复验 DLL。

## Standards 轴：按严重度

### S1 · Error：Legacy 退役顺序依赖尚未成立的卸载能力

**位置：** [spec.md](spec.md) 第 69–72、92–94 行；[acceptance.md](acceptance.md) L1–L3；ADR 的 Close 方案。

第 94 行要求“从可见父容器脱离，保活动画owner至清理ack”。引用保活不等于动画线程保活。`A/Controls/Danmaku/VideoDanmakuPanel.xaml.cs:77–84` 在 Close 后立即 `RootGrid.Children.Clear()`，而 OnUnloaded 本身已是关闭入口（65–74）；不能要求应用等下一帧后再卸载。原 `U/Danmaku.Legacy/DanmakuRender.cs:577–595` 又在 Close 中暂停 canvas、解绑事件并 RemoveFromVisualTree。这是必须改变的具体协议，不是多加一个协调器引用即可解决。

官方 [WinUI3 RunOnGameLoopThreadAsync 文档](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_UI_Xaml_CanvasAnimatedControl_RunOnGameLoopThreadAsync.htm) 说明未 Loaded 时返回取消、卸载时未完成任务取消，执行还依赖 CreateResources 完成；async delegate 的返回也不能当完整清理 fence。官方 [CanvasAnimatedControl 文档源码](https://github.com/microsoft/Win2D/blob/winappsdk/main/winrt/docsrc/CanvasAnimatedControl.xml) 描述卸载停止 game loop，GameLoopStopped 在该线程退出前发生，控件 Unloaded 在其完成之后。**推断：**先 detach 再靠普通排队清理不可靠；同步触发框架卸载也可能破坏“owner 被阻塞时 UI heartbeat 仍推进”的承诺。文档不能证明当前 1.3.2 包的实际行为，更不能证明当前父容器事件顺序。

**建议：**将 L1 降为能力 spike，先核对目标包恢复资产/API，再验证正常 Close、父容器先卸载、从未 Loaded、CreateResources 未完、device loss 各路径。比较常规排队清理与预先订阅 GameLoopStopped 的兜底清理；事件中不能等待 UI。确认资源所有权能否在动画线程停止后安全移交，或是否需要延后实际 detach 的另一个关闭方案。后两者均须补设计，不能直接选成实现。必须测 Close 和紧随其后的父容器清除期间 heartbeat；只测 Close 方法耗时不够。未证明前不得进入 L2/L3，也不承诺唯一动画 owner 在卸载后仍可执行。

### S2 · Error：Core UI 挂接消息没有定义资源保活与拒绝确认

**位置：** spec 第 69–74、82–86 行；acceptance X3/X4。

单 owner 能修复 `U/Danmaku.Core/DanmakuDirectXRender.cs:514–565` 引用快照与 `Models/RenderLayer.cs:80–104` 回池并发，以及 `DirectXResourceManager.cs:479–499` UI resize 与 Draw/Present 并发；方向成立。但 UI attach/detach 是例外执行域。只检查 lifetime 尚不足：owner 创建 swapchain → UI attach 排队 → Close/初始化失败让 owner 释放 → UI 消息读取 native pointer，必须有消息自身的资源保活；被拒绝的 attach 也必须交还它。现源码 `DirectXResourceManager.cs:245–290` 在 UI 互操作中直接读取 `SwapChain.NativePointer`，而 Dispose 的 539 行起只释放对象，没有对应 SetSwapChain(null) 握手。把原调用简单搬进 UI continuation 会留下同类窗口。

**建议：**X3 合约具体列出 pending attach 持有的强引用/COM 引用、UI 拒绝和成功回执、关闭后仅对该实例 panel 的 detach、UI queue 拒绝时的持有者与故障终态。UI 与 owner 不同时改变 swapchain/target；DPI MatrixTransform 也归 owner（原 `OnCompositionScaleChanged:451–473` 在 UI 执行 native 变换）。resize 必须在帧结束后解除 D2D target、所有 buffer view/context 引用再执行；[官方 ResizeBuffers 文档](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-resizebuffers) 要求释放直接及间接 back-buffer 引用。零尺寸不提交给 composition swapchain，保持最新待处理尺寸。

Windows App SDK 的 [SetSwapChain 接口页](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/win32/microsoft.ui.xaml.media.dxinterop/nf-microsoft-ui-xaml-media-dxinterop-iswapchainpanelnative-setswapchain) 确认接口表面；[Windows.UI 同名接口文档](https://learn.microsoft.com/en-us/windows/win32/api/windows.ui.xaml.media.dxinterop/nf-windows-ui-xaml-media-dxinterop-iswapchainpanelnative-setswapchain) 明确 UI 线程、增加引用、null 解除引用。后者是跨命名空间的参考，**不能据此直接签署当前 WinUI3 ABI/释放实现**。本轮未运行 restored-project find-api；null detach 的目标接口与引用效果、阻塞 Present 时的 UI detach 可达性仍须在 X3 前验证。不得以 `UiDetached=true` 的纯状态模拟代替这些检查。

### S3 · Warning：版本和终态缺少最小转换表，存在可复现的解释分歧

**位置：** spec 第 55–63、86 行；acceptance Q1。

六个字段不是天然错误，但尚无逐命令的推进者/失效者/终态表。资源尚未 ready 时 batch 带旧 resourceEpoch，ready/resize 推进 epoch 后拒绝该 batch，谁重新调度最新场景、原 Seek 何时 applied？若未明确，既可能永久 pending，也可能仅丢掉首帧。Close 命令的 applied 是 admission 封闭还是完整退役？文中区分了 retirement 状态，却未把它映射到“每个记录只有一个终态”。

另一个具体歧义是第 61 行允许合并连续 UpdateTime 又要求保留 5000/5001 判别。原 `U/Danmaku.Core/DanmakuScheduler.cs:283–295`、Legacy master 351–364 按相邻已处理时间判别；从 0 连续到 4000、8000，每步都不 seek，压成 8000 则会 seek，改变选择区间。单写“处理所跨区间”不能确定如何保留这些分段边界。

**建议：**先用 instance identity、内容/目标代次、最新 intent 和 owner 本地资源版本说明足够性；只有确实有独立失效原因才保留额外 revision。写出 Seek/UpdateTime/Pause/Clear/Close/resize/ready 的转换与每种 ack 对象；暂停意图 applied 不需要等待不存在的首帧，Seek 的 prepared/draw 与控制命令应用分账。初片禁用进度合并，或保存足以重现分段判别的信息；同时覆盖 0→4000→8000、倒退、ready 前 Seek→resize→ready。属于可能的 Speculative Generality/协议缺口判断，不是违反命名规范。

### S4 · Warning：切片“≤M”尚不能按表整体成立

**位置：** acceptance 第 17–36 行。

C0/S 同时固化五组兼容裁决和两引擎 UI 基线，包含仍未知的 API 能力，按 scope-gate 不属于明确单点 S。Q1 声称只依赖 D1/D4，却使用 spec 第 55 行输入值快照，实际依赖 D3。X1“替代双通道”但“不迁移渲染”，X2/X3 专用消费者绿，并不证明 X4 真实公开入口能安全激活。若为证明 X2 的帧租约提前引入 Q1、完整 scene 协调器和全部兼容语义，则已不是独立 M。

**建议：**能力验证独立 spike；C0 先拆只读表面/特征记录与各项决策。每个内部模块注明“未激活、不能改善正式应用”，不以未被生产调用的接口存在为 green。先一个引擎的最小 owner/资源闭环，再局部接入；接线 diff 若承担此前未实现的错误路径或跨域协调，要重新判 L。V1 不应强制依赖所有新增 active-set/实时语义才能验证最小修复包；按声明的有限功能范围先做独立包消费验收，#49 全验收仍保持未完成。

## Spec 轴：按严重度

### P1 · Error：D3 的兼容变化已成为前置架构依赖，不能只留在待决表

**位置：** spec 第 51、55、123–124 行；acceptance Q1 与第 34 行。

草案正确承认别名变化，但前文已要求“输入捕获值快照、Get返回快照”，后文又说当前切片不含独立别名变化。`U/Danmaku.Core/DanmakuRenderer.cs:204–206` 复制排序容器，scheduler 207–224 的 Get 浅复制、Set 持有条目；Legacy master 302–310 直接返回/持有同一列表。Legacy Clear 253 行还会清空调用者容器。Legacy render 202–205 修改原条目 TextColor，两个实时入口也会写 AllowDensityControl/IsRealtime。快照不只是性能细节，改变调用者可观察副作用与回写。

`A/Controls/Danmaku/VideoDanmakuPanel.xaml.cs:266–273` 使用新解析列表，没有调用引擎 Get/AddList 后再修改，也没有保留实时模型用于读回（143–162）。**可决定的是应用内部限定输入可以值捕获；不能据此决定外部包 Get/Set 的别名可以消失。**

**建议：**最小修复先不改 Get 返回、AddList 合并差异、Clear 对已传列表的既有行为。在内部 scene 提交处捕获渲染所需值，并明确其相对公开修改的观察边界；若这仍改变公开别名的可见时机，列为有限应用消费者实验约束，不称外部兼容。若坚持所有入口即深快照，D3 必须成为 Q1 的显式先决条件，并作为单独兼容变更审核；不得借“签名不变”过关。

### P2 · Warning：完整重放、倍速和特殊模式变成了修复的总前置条件

**位置：** spec 第 104–112、120–125 行；acceptance A1/A2/R1/V1。

等时 Top 首帧及保留 Pause 是 #49 必需修复。目标前活跃内容也是 #61 必须定义的验收议题，但确定性全历史槽位重放、v≤0 新拒绝策略、普通模式同场景承诺，不能自动等同“不改变可见效果”。原 Seek 直接跳到 StartMs≥t（Core scheduler 138–155，Legacy 222–248），并没有可沿用的完整重建契约。

倍速具体例子：应用 ResetSpeed 243–247 将 ExtraSpeed 乘进 rolling 速度。恒定 2× 时媒体过去 2 秒、视觉时间约 1 秒，正常位移约为 2v×1；草案以媒体年龄 2 秒再乘当前 2v 得 4v。Top 正常寿命不乘 ExtraSpeed，媒体年龄重建同样可能提前淘汰。草案已声明这是待决变化，不能由审查员把它自动批准为修复期望。

Advanced 能解析不等于两引擎已完整显示：Legacy `DanmakuRender.cs:808–832` Draw switch 没有 Advanced/Subtitle 分支，Core `419–475` 更新分支也不含其完整动画。F13 仅证明输入，不能据此要求此次补齐所有模式动画。Clear 后自动恢复准备、Stop→Resume 全量重建也不是原调用链已经证明的契约。

**建议：**先保留现有正常播放速度、模式路由和 Clear/Stop 行为；暂停准备不要再调用公开 Stop，但不要顺手重定义 Stop。P1X/P1L 明确仅等时 Top 与既有场景冻结，不能签署完整 #49。A1/A2 后续先给恒配置 1× 的年龄/边界 oracle，再独立裁决倍速和配置变更后的重建差异；不为满足完整矩阵扩建历史配置存储。Advanced/Subtitle 先记录现状并防回归，新增动画/seek 支持另立范围；#49 内容验收未覆盖项继续列出。

## D1–D5 推荐与精确缺证

| 决策 | 本次推荐、源码理由 | 应用内部能决定 | 仍需确认/缺证与阻塞范围 |
|---|---|---|---|
| D1 Close、Dispose、Seek/Pause | 采纳最新 intent 独立于 Seek；Close 逻辑封闭与资源完成分开，保留可观察 retirement，严禁 UI 等 worker。F10 调用立即换实例，Core 原 5s/2s timeout 也不是可靠同步释放保证（GameLoopThread 196–217、291–305）。 | 当前应用未等待资源完成，可选择有限消费者的非阻塞入口；PauseApplied 后冻结，允许已在途帧完成。 | S1/S2 未闭合，尚不能保证同步 detach 非阻塞。外部消费者是否依赖 Dispose 返回后资源归零/构造即 ready、是否需要公开 DisposeAsync/CloseAsync 必须由包维护者明确兼容策略；本审查不代替发布授权。阻塞 L2/L3、X3/X4 的激活。 |
| D2 暂停首帧/活跃集合 | 采纳等时 Top 零步初始化与无需额外 tick；采纳暂停墙钟不进入恢复年龄。Top 严格 `>` 过期、各引擎 rolling 原阈值保留。拒绝此时冻结全历史重放及媒体年龄×当前倍速为产品契约。 | 可确定 VM Pause 要保持（VM 47–61）、样式与位置按最新请求；可以定义有限 Top oracle。 | 缺少倍速/变配置 seek 的期望对照和允许视觉差异、密度淘汰重现/切换位置容差；源码不能决定用户期望。A1/A2 在这些分支不得验收为全量兼容。Advanced 新功能不是先行条件。 |
| D3 列表/条目别名与 AddList | 保留公开差异；应用内部 scene 捕获可以采用。Core Get 浅复制、Legacy 全别名及 AddList 替换旧列表均有源码依据，不能混同。 | panel 创建列表后不外改、不读回，能约束本地试验消费者；快照需含实际渲染字段而非只复制 List。 | 外部别名依赖、修改条目后何时应可见未知；禁止无同步并发写不等于可废除顺序别名写。改变 Get、Clear 副作用、AddList 规则需显式兼容确认。Q1 若入口全快照须先解决。 |
| D4 Clear/Stop/Restart | 不采纳 Clear/Stop 的整套新语义作为本次默认。Clear 先保持清数据/画面且失效旧工作；SetList 后准备的必要修复与 Clear→Add 自动恢复分开。Restart 保留显式从 0 播放，避免 Seek 保留意图后令其悄悄变为暂停。 | panel 使用 Clear，但不调用公开 Stop/Restart；内部 seek 不借 Stop 可自主设计。 | 应用没有调用不构成外部 unused 证明。Stop→Update/Seek/Resume 的精确兼容要特征记录；如新协议不能保留，应明确另列变更，不纳入首帧片。Q1 仅冻结失效规则，不预决 Stop 重建。 |
| D5 实时添加 | 保留每次调用的独立事件及原 layer 路由，不按全局 Id 去重；当前应用新模型没有填 Id（143–162）。暂停新项静止准备是合理推荐，但不顺手统一回访/对象副作用。 | 应用 insert=true 且不读回模型标记；可以给本地一次即时显示、下一普通 tick 不双发的 oracle。 | 同一对象重复 Add、空 Id、insert=false、seek 回访前后的 IsRealtime 清除时点有可观察差异（Core scheduler 173–201、305–308；Legacy 269–299、377–380）。完整语义需特征测试/兼容决策；不能以 R1 提前强迫 A1/A2 全重放上线。 |

“应用内部能决定”是技术设计建议及已见调用证据，不是用户产品批准。外部兼容确认与外部仓库/包发布授权也是两件事，均未由本审查取得。

## 最小安全先行切片与准入

1. **现在仅建议 C0a（S，文档/只读）及能力 spike（?，高）**：记录既有表面、别名及调用边界；将 S1 的动画停止/清理能力和 S2 的 attach 引用握手分别验证。spike 不合并生产实现。本轮按约束没有执行宿主/API 实验。
2. **第一生产候选应为收窄 Q1（≤M 有条件）**：仅真实 scheduler 的原子 admission、latest intent、旧代次失效及 0ms/队列通知，不接 native、不改公开 Clear/Stop/别名、不建全部 active-set 模型。先补 S3 转换表和 D3 边界。若无法在不调用旧同步 render 的情况下独立接入，则保持隔离候选，不能宣称应用安全；不要把另一套纯数据模型测试当真实 scheduler 回归。原 26 加迟到发布用例仍只是调度证据。
3. **资源路径按引擎分别闭环**：X1 可在初始化失败/拒绝/退出语义明确后做内部执行器；X2/X3 必须各有真实资源消费者和限定持有关系。L1 能力通过后才设计 L2/L3。任何 UI 依赖框架卸载等候 worker 的结果都要求改方案，不把等待隐藏在 Children.Clear 或 RemoveFromVisualTree。X4/L3 接线必须证明所有入口收敛，无遗漏样式、resize、实时添加或失败释放；不能先激活 unsafe 中间态。
4. **随后 P1X/P1L（分别 ≤M）**：只做等时 Top、零步准备、最新意图、恢复连续性，依赖各自资源协议。真实 panel 的 UpdateTime 隐式 seek 路径、暂停双向切换也必须验证，不能只测应用未用的公开 Seek。T1 的暂停逻辑时钟可单独验收，但不是白字消失修复。
5. **A1/A2、R1 扩展和最终完整验收暂不准入**：先裁决 D2/D5 或明确有限范围。可先做有限修复的独立干净包消费证据，不能升级正式依赖；#49 的完整播放器、全部内容兼容、真实时间/业务与性能结果继续单列。AOT 既有 blocked 不由 JIT/26/403 替代。

每个可进入的实现仍须单独授权、细化失败停止条件并按 scope-gate 重新判级。本报告不授权运行桌面、改依赖或实现。

## 验证与批准边界

本轮只做原源 hash、只读源码/证据/官方文档核对及 docs 集成审查；未运行 403、26、build、WinUI、trace、find-api、WinUIAnalyzer 或 AOT，未读取凭据、改 kernel/包/layout，未 push、PR 或 issue 写入。官方文档提供能力约束，未证明当前恢复资产具备或满足新增协议。后续先查 restored 项目包/API；禁止为了核验顺手 restore/升级。

Standards 轴 4 项（最高 Error：卸载清理可达性、UI attach 资源握手）；Spec 轴 2 项（最高 Error：快照兼容变化已进入前置协议）。认可的证据纠正与局部方向不等于整体设计批准。必须保留真实 WinUI 首帧非空且目标正确、帧/resize/Close 竞态、阻塞 owner 时 UI heartbeat、释放完成可观察及干净包实际加载矩阵；新抽象或屏障绿不能替代。提交前同步本地父分支到本审查分支、检查仅 docs 和原草案不变；最终 SHA/clean 以交付消息的 fresh Git 核验为准。
