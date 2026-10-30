# #61 proposed 切片与验收门槛

状态：**proposed，全部待独立评审；表中红绿是未来验收设计，不是本轮运行结果。** 关联[Spec](spec.md)、[ADR草案](adr-0001-render-ownership.md)。母票L不承载生产实现；不把原票五个大项机械标M。

## 1. 切片原则

所有切片建议由高推理模型处理，难度高；此处只记录分派建议，不启动代理。每项独立提交/PR可审可绿，范围只含指定引擎module与其针对性测试。后续实施应重新使用code-testing及适用的run-tests/WinUI技能。不得为了拆小而先合入已知unsafe中间态：内部实现先在隔离引擎源码及测试消费者使用，正式应用引用不变；激活接线切片只在前置资源协议都已绿后进行。不得引入全仓CI/共享测试基建。

红绿含义：先在父切片版本上获得该行为的受控失败，再在新版本获得通过；“新内部接口不存在”或编译失败不算行为red。对于仅增补协议、父版本没有相同行为的切片，先保留原实现对应失败轨迹，再用受控故障注入证明新断言会红，清楚区分两类red。已有通过的兼容用例应保持通过，不伪造red。测试不能复制生产算法作为唯一oracle。

每项UI矩阵都用独立WinUI宿主、真实控件及目标引擎；新增内部模块未接入前，可在专用消费者连接真实资源验证该模块，不假称正式应用已修复。无测试钩子的干净包完整视觉矩阵在V1再做。依赖表示实际代码/契约依赖，不要求其他无关#45票等待。

## 2. ≤M交付清单

| ID / scope / 难点 | 单一交付及依赖 | 独立红→绿与WinUI实机矩阵 | 失败停止条件 |
|---|---|---|---|
| C0 / S / 高：兼容oracle | 固化D1–D5裁决及公开表面、两引擎特征用例；依赖独立Spec/ADR审查 | 不改行为；F8/F9按原源特征通过；UI跑Clear/Stop/Restart、Set/Get别名、实时添加基线，行为冲突记录red而非改期望洗绿 | D项未裁决就停对应实施；不把characterization通过当修复 |
| Q1 / M / 高：原子接收与迟到batch | 仅内部纯数据admission/intent/generation与终态；依赖C0中D1/D4 | 出队、选择结束、发布前屏障：旧Seek不能覆盖新Pause/Resume，Clear/Close封闭旧代次；故障注入拒绝入队必须faulted/closed。UI宿主阻塞纯数据worker时持续heartbeat且可发布Pause/Close | 发布需要worker/render锁、终态悬挂、0ms遗漏即停；不接native资源 |
| X1 / M / 高：队列单owner和退出 | Core内部执行器替代双通道协议，尚不迁移渲染；依赖D1 | 原T1/T2轨迹与enqueue拒绝red；新执行器全部动作一个真实thread、初始化/异常/shutdown终态。WinUI从UI构造/关闭，阻塞owner时UI heartbeat仍推进 | 两执行线程、UI等ready/drain、超时算Closed即停 |
| X2 / M / 高：场景与对象保活 | Core scene/layout/槽位/缓存/池的owner adapter，不含swapchain接线；依赖X1 | 原引用快照→Clear→draw使用释放对象轨迹；新adapter延迟归还到帧租约结束。WinUI真实D2D资源消费者执行prepare/draw/clear、配置变化、失败准备清理 | 非owner native访问、重复释放或回池后借用即停 |
| X3 / M / 高：back-buffer退役 | Core swapchain/resize/device域adapter与UI attach/detach握手；依赖X1 | 暂停在Draw和resize间，旧buffer仍被借用时不得释放；0尺寸保留最新值、失败Resize终结。WinUI快速非零→零→非零、DPI、资源未ready就Close、UI队列拒绝 | UI执行Resize/等worker、旧epoch绑定新panel、设备失败重用旧资源即停 |
| X4 / M / 高：接线完整性 | 仅Core facade/render接入Q1+X2+X3及退役协调器；依赖三者 | 真实公开API跑原Core13项与准备后/帧前/Present中关闭、晚到size事件；UI三次重挂、快速切换退出、阻塞owner时Close返回且completion待定，释放后completion完成 | 仍有绕过owner入口、清理失联、native异常即停；不混入首帧/active-set修复 |
| L1 / M / 高：动画线程能力 | 仅Legacy动画owner接收/执行/清理握手module；依赖D1 | 实际线程ID验证Update/Draw/清理串行，UI队列不能冒充；WinUI测试暂停、初始未ready、卸载后清理可达、CreateResources重复/设备失效和入队失败。对无清理ack故障注入应红 | 无法在当前包证明动画调度与退役路径即停并补设计；不得先迁移native代码 |
| L2 / M / 高：资源域与回调寿命 | Legacy render target/列表/槽位/尺寸变更迁入L1 adapter；依赖L1 | Draw内batch未Dispose时Clear不得销毁target，准备失败仅回收本代次；WinUI真实CanvasAnimatedControl resize/clear/close、0尺寸、设备重建 | 任一target跨device使用、UI释放在途target、DrawingSession跨帧保留即停 |
| L3 / M / 高：公开入口收敛 | 仅Legacy master/render接Q1+L2及退役协调器；依赖Q1/L2 | 原Legacy13项+旧batch/Close/Realtime竞争；UI三次挂载、关闭动画回调被屏障保持时UI可操作，释放后完整退役、旧实例不清新画面 | 输入绕过owner、关闭后清理永不回调、VM意图被旧工作覆盖即停 |
| T1 / M / 高：逻辑时钟 | 仅Legacy暂停/恢复activeElapsed，不改seek集合；依赖L3 | 真实时钟适配器受控推进：Pause期间动画TotalTime增长但age/X固定，Resume首步不含停顿。WinUI预先可见Top/rolling暂停>3s墙钟再恢复，观察剩余寿命/连续位移 | 仅flag正确、恢复年龄跳变、无可见项就宣称冻结通过即停 |
| P1X / M / 高：暂停准备 | 仅Core等时Top显式/隐式seek的零步首帧；依赖X4、D2最小Top规则 | 原paused-seek空白red；Prepare后再Pause/Close屏障；WinUIPause→Seek(20000)，不发tick；Top可见居中、age=0，零尺寸/ready延后仍提交最新版 | 临时Resume、游标已消费但未画、dirty提前清除即停 |
| P1L / M / 高：动画首帧 | 仅Legacy同等Top准备；依赖L3/T1及D2最小Top规则 | 同P1X，另证明paused也处理准备/Draw；WinUI资源延迟、重复CreateResources、暂停目标连续两图 | 依赖后续Update tick或动画年龄非0即停 |
| A1 / M / 高：寿命与槽位重放 | 仅普通Top/Bottom活跃集合；依赖P1X/P1L、D2 | 独立手算s=t、t-1、t-d、t-d-1及自定义d，age/remaining严格边界；WinUI两引擎Top/Bottom无重叠与密度满/释放，暂停双向切换，恢复后按剩余寿命移除 | 任意回看常数、出生时间重置、槽位无规则即停 |
| A2 / M / 高：轨迹/宽度 | 仅Rolling/Reverse活跃重建；依赖A1与D2速度/尺寸裁决 | 手算恒速与两种退出阈值，重放槽位oracle；WinUI长/短文本、目标前不同年龄、速度/字体/resize后seek、双向切换、v边界 | 以出生不可见误判失败、以空白误判绿、历史/当前配置混用即停；额外模式另拆 |
| R1 / M / 高：一次性提交 | 仅Realtime与新scene重建交互；依赖两引擎接线、A1/A2及D5 | 同对象/空ID连续两次Add分别计事件，insert true/false、正常tick不双发、前后seek按裁决；WinUIplaying/paused、Clear后实时、Close竞争，旧实例实时不流入新实例 | 以全局ID去重吞掉合法项、IsRealtime永久漏放、Clear后stopped丢新项即停 |
| V1 / M / 高：产物可信度 | 独立干净本地包消费者，仅包消费与完整离线WinUI验收；依赖所有上述及D3裁决 | 同失败fixture在精确original包/新包红绿对照，完整第3节矩阵；不含正式引用升级或发布 | PE钩子残留、源hash不明、包未实际加载、任何视觉/关闭红即停 |

这些是各自限定的M上限而非工期承诺。X2如果同时需要改字体语义/布局算法，或X3扩大到通用device恢复框架，就继续拆新规格；X4/L3仅允许接线及本协议错误路径，不能承担前置模块缺失的设计。A1/A2若要求完整历史配置和所有特殊模式，升L并先追加Spec，不把它们塞进M。D3若决定统一AddList差异，追加一个仅AddList的M兼容切片，独立别名变化再一片；当前清单不含这些隐式修复。

Q1/X1/L1可在契约批准后独立进行；Core和Legacy资源路径不互相代码依赖。A1/A2需要双方首帧是因为输出承诺同一普通模式目标场景；两引擎实现仍可分别提交。若引入共同纯场景模块，先验证两adapter确实可共用，不提前建抽象框架。

## 3. 最终WinUI矩阵与oracle

| 维度 | 必测操作与通过证据 |
|---|---|
| 原26回归 | 两引擎DequeuedPause/Resume/Clear/SetList/Seek/Close；Zero、Threshold5000/5001、Backward、Repeated、Empty、End全部保留。真实调度器、同一输入；新异步实现用完成终态收集，不能睡眠后读私有flag |
| 晚到提交 | 屏障设在出队、纯选择结束、render接收前、准备后、帧开始前；各处插入Pause→Resume及Resume→Pause、新Seek、Clear/SetList、Close。必须核对最终场景ID/时间/意图和旧资源退役 |
| 暂停首帧 | Pause先于Seek；公开Seek无额外tick；VM.UpdatePosition触发隐式seek；paused Win2D→DirectX和反向切换；t=0和20000，目标等时Top；t前1ms Top/Bottom/rolling及过期项。paused两图都有正确内容与位置，不能空白相等通过 |
| 寿命/轨迹 | Top默认/自定义寿命边界与remaining；rolling已可见位置暂停>3s后恢复不跳；Reverse；0.5×/1×/2×媒体速度和尺寸变化按D2，显式区分媒体年龄与动画年龄。记录逻辑age与预期X，渲染容差预先固定（如位置≤1物理像素，文本量测另记），不在失败后调阈值 |
| 资源/关闭 | ready延迟、零尺寸、resize/DPI、配置连发、正在Draw/Present时Clear/Close、TryEnqueue拒绝、shutdown失败、device-lost；UI heartbeat在屏障保持worker时仍增长，Pause/Close可发；completion在释放前不得完成，释放后必须终态 |
| 生命周期 | 每引擎三次重挂、hide/show、卸载后旧VM与旧size回调、快速双向切换；旧Close不移除新子树；重复Close；初始化未完Close；资源计数退役后归基线。超时作为失败探测，不作为完成证据 |
| 兼容 | playing/paused各Clear→Add/SetList→短跨度Update；Stop→Update/Resume、paused Restart@0；Set/Get容器和条目别名按D3；AddList旧/新/同ID/空ID；Realtime true/false、暂停即时显示、seek回访、多生产者顺序 |
| #49原场景 | 活动引擎ready时不因另一字段null固定等500ms；快速样式请求合并、最新样式及内容正确；引擎切换/卸载过期请求无效。重用真实panel/VM/redraw session，不用孤立内核结果代替应用验收 |

UI heartbeat测试要在保持屏障时等待UI自己产生可观察事件，并同时验证操作回执；之后才释放worker。单纯“方法耗时短”不能证明无等待链。源版本/测试副本必须隔离；测试失败有上限超时，保存一次有效失败就停止该切片，不连续试修native崩溃。

## 4. 包、视觉来源与完成边界

1. 原源commit+归档SHA、补丁/变更commit、全部源manifest、配置、工具链、依赖锁定一起记录。fresh空输出目录及隔离缓存构建；`--no-incremental`不能单独证明clean。
2. 屏障/trace副本与clean副本完全分离。clean PE扫描所有types/fields，至少拒绝`_test*`、`_trace49*`及新增诊断入口；同时审源manifest和编译输入，不能把“未找到两个字段”当作无全部插桩证明。不得应用rejected-render patch。
3. 本地feed将Core/Legacy/Share同版本重新restore到独立消费者，禁止ProjectReference或旧layout冒充包消费。记录nupkg/hash、包内DLL、restore资产、部署DLL、实际加载路径/hash/MVID，全部串联。签名/依赖表面兼容单独比较。此验收不授权外部发布。
4. 每张图记录runID、PID及启动时间、引擎实例、scene/content版本、item ID/Mode、目标媒体时间、逻辑age、配置/尺寸/DPI、源manifest hash、实际DLL hash、PNG hash与采集UTC窗口。逐图查看；必要时对未编辑PNG解码RGBA比较指定区域，记录区域和阈值。像素一致只证明采样区一致；不能证明两图之间从未变化。
5. PreparedAck是布局完成；DrawSubmitted/BatchDisposed是CPU调用完成；GPU完成/合成器present及截图是不同证据。不能把任一单个ack写成“用户已看见”。目标ID与截图关联窗口必须能核对；没有精确frame ID就注明窗口关联。
6. Release JIT离线宿主、完整播放器/真实视频时钟、登录业务/真实网络、高密度长时性能、Native AOT分别记账。原26不是它们的替代。AOT工具链已知blocked保持单列；本轮未尝试补装。完整播放器验收另有权限/环境时再执行，不能仅V1通过关闭全部#49条件。

任何源码/映像不一致、白图、旧目标帧、首帧通过但active-set不符、worker阻塞UI、退役悬挂、native异常，都停止并保留原始证据；不得发布、升级正式依赖或关闭#49。历史白字消失解释保持撤回，不将“再次未复现”作为修复通过指标。

## 5. 本轮交付核对

仅设计文档和小型hash核验；未运行原26、403、build、WinUI、AOT或新红绿测试，未安装包、不占桌面、不读取账号数据。未改src/tests/externals，kernel固定`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。Git提交前检查新文档相对链接、diff whitespace和改动范围，合并最新本地父分支；最终commit/clean由交接消息给出。新独立审查负责对上述P/D及切片范围提出异议，本轮自查不代替独立批准。
