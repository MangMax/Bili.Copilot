# #56 离线真实控件缓存观测（补充实验）

2026-10-30。**本报告不填充原协议“真实业务 50 页”验收，不签署 #56 全通过，不制定条目/MB/TTL 预算，不判定泄漏。** 实际生产页面、VM、卡片和图片链路上的固定合成服务数据，只回答受控场景的缓存引用和可视树事实。

## 来源及隔离

起点与受测父分支为 `6b9ebef52d2ec007112fd765648eea825c5c7214`；独立工作区 `t3-f57790f9`。kernel 固定 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。正式生产/测试/依赖/kernel 未改；没有 push、PR、issue 操作，没有子代理。

已读 AGENTS/docs/agents、issue-56 全文、integration-spikes-56-58、ui-50-53、issue-50-53-lifecycle-fix、status、ui-47-gc-comparison 和相关宿主。应用 winui-ui-testing、code-review（按用户要求自行完成两轴，不启动代理）、winui-dev-workflow、code-testing/run-tests、verification-before-completion；宿主异常使用 systematic-debugging。未安装专用 WinUIAnalyzer，不把普通编译检查称作其审查。未重跑未改生产的 403 suite，未重试缺 MSVC 的 AOT。

受测为 packaged **Release JIT x64 / .NET 9.0.22**，独立身份 `BiliCopilot.Cache56F577`，winapp 0.7.1 local；不是完整生产应用或 AOT。没有登录、复制认证、访问 Dev/生产包、操作 PID9608、采集其他进程、安装工具或改系统 DPI。窗口目标 1500×950，观测 RasterizationScale=1；主列表 viewport 1198×798 DIP。截图包含本窗口，不做桌面全屏采集。

真实源：PopularPage.xaml/code-behind、主/侧 header/body、PopularPageViewModel 全 partial、**生产 VideoItemViewModel 全 partial**、section VM/controls、VideoCardControl/三种真实模板、BasicCoverImage/BasicScaleEffect、IncrementVerticalScrollControl、真实 NuGet AppRepeater/生命周期基类。没有用简化条目 VM 或简单 DataTemplate 替换卡片。核对见 [152 个源文件 hash](evidence-56/linked-source-hashes.json)、各完整轮 production/fixture/dependency-hashes.json 和 modules.json。最终受测 Harness.dll SHA256：`AC365C611DCE032594CD52701A409689656A34BC2BA47B11A41AEF31DC052CA4`。

明确适配边界：应用组合根、内存布局设置/最后栏目、资源名称/数量/时长格式、未调用的播放/导航/菜单边界、IVideoDiscoveryService。后者每次创建实际 kernel VideoInformation/MediaIdentifier/PublisherProfile/UserProfile/BiliImage；每批30条，A/B各至多15批，C60条；前三栏目前10个业务Id故意相同，但服务每次返回新对象。仓库现有 fixture 未提供脱敏真实业务列表，故标为 **SYNTHETIC**，非流量重放。固定时间、标题、作者、Id 和生成逻辑在 [Fixture.cs](harness-56/Fixture.cs)。

封面/头像 URI 指向 fixture.invalid，HttpMessageHandler 只读本地 packaged Logo.png 返回字节，无网络发送；真实内核缓存、decode、绘制路径继续执行。初始化实际 ShareKernel logger，避免旧 #50/#53 的 logger-null 异常。仅两种固定图片 URI/相同小图，不能代表线上多样图片、解码/显存预算。

## 协议和计数 oracle

[宿主及复放说明](harness-56/README.md)、[runner](harness-56/run.ps1)、[分析](harness-56/analyze.py)、[来源校验](harness-56/verify.py)。固定顺序：A30→A150→A450中部→底部→B150→C60→A→十轮B/C/A→隐藏同页→显示→普通刷新A→10秒观察。加载通过生产生成命令和实际异步服务/集合通知，未改 CanExecute、分页状态或控件模板。

每次切换先**显式回顶部**，不冒充自动恢复锚点。直接执行真实 SelectSectionCommand，侧栏仍是真实绑定控件，但不是鼠标/ItemsView选中驱动：截图侧栏高亮停留 Recommend，主栏标题/内容随 VM 正确切换。这个适配边界不支持完整导航选择一致性验收。隐藏用真实页面 Visibility.Collapsed，**未托管 MainFrame**，不能升级为完整导航生命周期证明。

就绪条件：VM无加载、正宽 BasicCoverImage 均非 loading 且已存在解码 CanvasImageSource；请求/通知/几何/条目/卡片数指纹连续至少1200ms相同，单检查点20秒上限。尾部十秒是固定弱引用观察窗。缓存、游标、displayed section、protected image source 只读反射，未写状态。集合快照与去重集合仅存在于采样方法局部；输出标量/字符串/复制数组，弱引用不强持对象。

| 字段 | 精确含义与不可推导项 |
| --- | --- |
| items / cacheSlots / referenceSlots | 当前 Videos.Count、缓存 List.Count 之和、前两者之和；Capacity另记，不能把引用槽当独立对象 |
| uniqueVMs / uniqueIds | 仅活跃集合+缓存列表的 ReferenceEqualityComparer 去重 / Identifier.Id 去重；不是全堆存活计数 |
| realizedCards | VisualTreeHelper 在真实主栏子树遍历到的 VideoCardControl，包括保留/回收元素；不是 UIA count，也不是 ItemsRepeater 活跃 index 数 |
| visibleCards | 正尺寸、卡片自身Visible、边界矩形与ScrollView viewport相交，包含部分边缘卡片；页Collapsed强制0，不验证任意遮挡/像素可见性 |
| hiddenRealizedCards | 页Collapsed时树中仍存在的真实卡片数；不能说隐藏即释放 |
| cardWeak* / oldVM* | 仅检查点见过的卡片弱引用、普通刷新前450个旧VM弱引用；不是根分析，不证明所有实例都已遇到合适代次GC |
| managed/private/workingSet | 同目标进程瞬时标量；包含宿主、日志、JIT、XAML、图片/native、分配器；不相减推导native或独占bytes |

生产排序在此仅为 Popular服务顺序，没有子分区排序场景。两个offset、逐检查点Id序列SHA256、各缓存Count/Capacity均记录。PR #27 未合并，本次只有普通刷新，**撤销、增量diff峰值、有效快照pin完全另账**。未触碰已知子分区归属/迟到分页风险。

## 两轮完整结果及边界

最终完整轮 [run-4](evidence-56/run-4/records.jsonl)（PID56304）和 [run-5](evidence-56/run-5/records.jsonl)（PID8668）使用同一最终DLL、各自新layout/进程/注册包。各 **11/11 协议阶段完成、83个scalar检查点、30次无新增B/C/A切换检查**；runner均exit0，分析/来源校验均exit0，零failure/unhandled/logger warning记录。它们是机制oracle通过，**不是整票/完整视觉/性能预算PASS**。前面未完成轮不混入汇总。[机器摘要](evidence-56/summary.json)、[分析输出](evidence-56/analysis-result.json)、[来源验证](evidence-56/verification.json)、[比较图](evidence-56/comparison.html)。

两轮以下计数一致：

| 检查点 | 当前条目 | 缓存槽 / 总引用槽 | 不同VM / 不同Id | 树内真实卡片 / viewport相交 |
| --- | ---: | ---: | ---: | ---: |
| A首批 | 30 | 0 / 30 | 30 / 30 | 30 / 12 |
| A五批 | 150 | 0 / 150 | 150 / 150 | 117 / 12 |
| A十五批中部 | 450 | 0 / 450 | 450 / 450 | 223 / 16 |
| A十五批底部 | 450 | 0 / 450 | 450 / 450 | 223 / 10 |
| B五批 | 150 | 450 / 600 | 600 / 590 | 223 / 12 |
| C一次60条 | 60 | 600 / 660 | 660 / 640 | 223 / 12 |
| 返回A、每轮返回A | 450 | 660 / 1110 | 660 / 640 | 223 / 12 |
| 同页Collapsed | 450 | 660 / 1110 | 660 / 640 | 223 / 0 |
| 普通刷新A及尾窗 | 30 | 210 / 240 | 240 / 220 | 223 / 12 |

十轮中每个B为810总槽、C为720总槽、A为1110总槽；缓存始终A450/B150/C60，Capacity分别450/150/60。不同VM660与Id640的差来自明确的重复业务Id输入；多出的活跃引用槽未创建额外VM。两游标始终15/5，逐栏目序列hash保持，服务请求累计始终23（含目录、实际到尾空页等），未补入新数据；普通刷新后变24，A游标回1，B仍5。

实际树没有把450条全部实现为卡片；但223不是可宣称的恒定虚拟化上限。经历中部/底部后，切到较短B/C乃至刷新后30条仍保留223张树内卡片；此计数含回收/保留元素，不能等同于当前有效index。隐藏页也保留223。固定窗口内这些事实支持“条目数、树内对象数、视口内可见数不同”，并不支持全应用虚拟化预算通过。

### 内存、GC与旧对象

原始bytes见每个检查点。以下为十轮前A-return→第十轮A的整个宿主标量，**非每条成本、非优化前后对照**：

| 轮次 | managed bytes | private bytes | working set bytes |
| --- | ---: | ---: | ---: |
| run-4 | 18,510,280 → 38,222,040 | 440,643,584 → 645,308,416 | 538,611,712 → 733,986,816 |
| run-5 | 16,469,016 → 36,176,456 | 437,051,392 → 652,849,152 | 521,383,936 → 731,373,568 |

**同数量重复切换未证明整体资源趋稳。** 弱引用跟踪的累计见过卡片增至2566，采样时仍活223–296/297；树内却固定223，说明存在创建/回收过程，不能仅看固定树数量得出无分配/无保留增长。观察器本身保存弱引用列表、CWT与日志并分配内存；图片缓存、XAML/native、JIT、分配器也在同一进程中。没有根证据，不能把增长归因于 `_videoCache`，也不能把process减managed当图片/native字节。

复用 #47 Trace工具及已缓存DiagnosticsClient/TraceEvent，仅收当前owned PID，GC keyword=1、Informational、64MiB、rundown=false；没有heap dump/GC.Collect/清场或额外分配压力。原nettrace/full parse位于ignored `artifacts/cache56/`，各轮trace-hash/raw-events-hash记路径/hash。提交的events.json仅保留GC/Start和GC/Stop；不提供暂停预算结论。

| 轮次 | GC Start/Stop完整配对 | AllocSmall | InducedNotForced | Induced | 首末sample内GC数=gen0计数差 |
| --- | ---: | ---: | ---: | ---: | ---: |
| run-4 | 663 | 251 | 406 | 6 | 658 |
| run-5 | 679 | 258 | 415 | 6 | 674 |

两轮均零报告丢事件；Start/Stop Count一一配对。运行时诱发事件没有调用栈，**不指认是哪段应用/WinUI/采集代码触发**。含BackgroundGC，collection duration不是暂停时长。

普通刷新前跟踪450个旧A VM。刷新已清A缓存，首个就绪sample与随后10秒最后sample均**223仍活/227不再活**，两轮一致。刷新sample→observe-10之间分别有4次（2 AllocSmall、2 InducedNotForced）/5次（2 AllocSmall、3 InducedNotForced）GC；不是“没有GC所以什么也没测到”。不过自然分配GC只代表对应代次，后台/WinRT生命周期、保留元素与其他根未枚举，不能断言剩余223应已回收或一定泄漏。也**不能把集合去重240当成刷新后全堆只剩240个VM**。对象根、独占/共享retained bytes仍未知。

### 视觉复核

**31张PNG全部逐张打开**，另查看固定Logo源图；逐图hash和判断见 [visual-review.json](evidence-56/visual-review.json)。中部可见A0224附近、底部A0440–0449，边缘裁剪符合滚动；B/C标题和合成前缀相符；Collapsed只剩宿主工具栏，重新显示恢复页面。

六张图片状态不能签完整视觉通过：run-4/state-1部分封面缺失、state-11封面缺失；run-5/state-1、state-7、state-10、state-11部分/全部封面缺失。文字/卡片框仍在，结构sample的解码源非空。后续完整图不能覆盖这些失败；绘制与截图捕获原因未隔离，停止扩展图片场景，未修改生产/模板或以简单控件绕过。首次与末态缺图、侧栏高亮适配边界、无完整导航/多DPI验证均保留。因此本报告只签计数/缓存/受控内存观测，**不签全部图像呈现可靠性或完整UI验收**。

## 失败历史（保留）

- build-a：宿主缺 Core 的 Build 扩展引用、直接访问 protected CanvasImageSource 编译失败；改为已缓存且正式应用同版本 RichasyKernel.Core 0.0.1 与只读属性观测。
- build-b：仅加入 using 仍不足，确认扩展在 Core包；build-c通过。首次启动 attempt-a：构建为 bin/x64、启动求值为 bin，缺manifest；启动显式传 Platform=x64。
- attempt-b：空服务 KernelBuilder.Build 抛 No services registered；按生产方式注册当前UI Dispatcher，未扩大到真实账户服务。build-final 新目录成功，65 warnings/0 errors，保留 nullable、MVVM source-generator等警告，不冒充零警告。
- run-1：UI预检20秒未找到窗口，raw只有launch；无控件测量。失败抓图缺HWND且未交付PNG，错误保留；清理成功。后续预检改为60秒，未改变受测DLL。
- run-2：原runner的整段十轮上限180秒过短，应用仍Running，记录至第九轮，未完成隐藏/刷新尾部；不是整体PASS或生产崩溃。原样保留failure.png/trace/records。后续仅将外层整段限额改为420秒；每个稳定检查点20秒限额和DLL不变。
- run-3：应用已记录step-pass:6，UIA状态读取出现 `stale_element: Element is no longer accessible`；原始错误、failure.png 和终态均保留。之后只将runner完成等待改为宿主step-pass/failure标量日志，UIA仍执行按钮和抓图。受测DLL及协议不变。并未修复/掩盖生产错误。

## 尚未满足的原验收

| 原 N/A | 本轮能补充什么 | 仍缺什么 |
| --- | --- | --- |
| 条目/引用/身份/Id | 合成服务上的真实生产集合与缓存字段观测 | 真实授权业务列表、原首批/10页/50页及不同账户分布 |
| 卡片/viewport/隐藏页 | 真实页面/模板/可视树、局部交集和Collapsed观测 | 全应用导航、不同窗口/DPI/主题、完整回收索引/所有堆中卡片 |
| 存活VM/根/保留bytes | 可达集合身份计数、有限WeakReference与GC事件 | 对象根、独占/共享保留bytes、全堆枚举、长期泄漏判断 |
| managed/process | 受控宿主同步标量与重复协议 | native/image/GPU分摊、真实稳定与峰值预算、可归因的优化收益 |
| 刷新/撤销/返回 | 普通刷新与显式回顶部后序列/游标 | PR27集成、撤销和失败快照、隐式锚点、分区与排序、认证会话 |

没有产品B、真实Mactive/Mundo/Mpeak、独占保留量和代表性波动，不能给缓存淘汰默认值。本实验没有实施缓存预算/淘汰，也不能由同数量下进程增长直接认定缓存泄漏。

## 复核、复放与桌面交还

标准轴自审：新增文件限本报告、harness-56与evidence-56；不接入根构建，不改正式依赖。缓存反射只读，无强制GC，无生产状态注入；所有实际控件/数据模型链接及XAML复制可核验，WeakReference/CWT不强持被测对象。没有已确定需要生产修复的结论。Spec轴：本次授权的离线补充采样与重复对照已交付；原真实业务/根/预算、完整导航/图片视觉仍明确未通过，不用适配器结果替代。

新运行命令及成功输出：

```powershell
python docs/performance-acceptance/harness-56/analyze.py docs/performance-acceptance/evidence-56 run-4 run-5
# validatedRuns:2, samples:[83,83], each 30 loop checks; exit 0
python docs/performance-acceptance/harness-56/verify.py docs/performance-acceptance/evidence-56 run-4 run-5
# 两轮实际build/layout/loaded DLL、152源、fixture、trace hash及cleanup；exit 0
git merge --no-edit feat/performance-45
# Already up to date; parent remains 6b9ebef52d2ec007112fd765648eea825c5c7214
git diff --exit-code 6b9ebef -- src tests externals
# exit 0
```

两轮使用相同App/C#/XAML/csproj，verify额外逐字节比较当前harness文件与实际build目录。run-5另有实际加载的ShareKernel、kernel模型、Win2D、ComputeSharp等 [loaded-dependency-hashes](evidence-56/run-5/loaded-dependency-hashes.json)，与layout文件hash一致；run-4只记录启动时三个实际模块及layout依赖hash，不冒充它有同等完整加载清单。`replay.ps1`为上述已运行组件命令的便利入口，最终wrapper及所有PowerShell/Python脚本完成语法校验，未为它再跑第三对实验。

所有失败与完整轮owned进程/注册包均清理，collector已Stop/WaitForExit；未递归删除临时构建目录。最后run-5 `yield.json` 为 **released:true**；额外最终 [yield](evidence-56/final-yield.json) 为 **released:false**，表示已无可释放租约，不改写为true。最终owned包不存在，桌面独占已交还。报告提交SHA和最终clean状态在交付消息提供，避免自引用。

最终harness/报告whitespace检查通过；全暂存 `diff --check` 仅因attempt-b与run-1原始failure-capture-error.txt的尾空白/空行非零，为保留原失败输出未改写。来源hash是本机原样字节；跨checkout若发生Git行尾转换，需先核对规范化文本差异，不把CRLF/LF变化当作生产行为漂移。
