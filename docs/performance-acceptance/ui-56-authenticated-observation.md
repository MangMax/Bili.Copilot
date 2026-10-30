# #56 已登录完整应用缓存观测

2026-10-30，UTC+08。本轮为真实账户、完整应用、真实内容上的有限观察，**不是 synthetic，也没有完成可核验的真实业务50页**。请求边界不可观测，滚动次数不计为页数；没有具体缓存预算或泄漏结论。#56 保持开放。

## 来源与安全边界

开始 HEAD 和本地父 feat/performance-45 均为 `c49f738bdea215c5584ddebefbdc017d34d7b0bc`，工作区 clean。kernel 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。已读 AGENTS、docs/agents、#56（gh）和任务列出的六份交接文档；按父级任务约束自行工作，未启动子代理。

应用 winui-ui-testing（local/owned HWND、UIA过滤、截图核对、yield）、winui-dev-workflow（核验既有 packaged Debug x64/JIT 产物）、winui-code-review（分页、缓存、虚拟化及证据边界），提交前使用 verification-before-completion。为诊断辅助代码读取 code-testing/run-tests；未修改正式测试或运行 .NET test。只构建 docs 下的小观察器，不重跑403或完整应用build；**未运行 WinUIAnalyzer**，普通诊断工具编译不等于该分析器。

当前注册身份 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be`，family `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`。核验指定新 layout 后按 `shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App` 激活；按规范化 EXE 完整路径发现 PID48012/HWND4524952。首次路径比较因正反斜杠不同未发现进程，随后规范化再发现，未重复启动其他身份。

被测源码是交接构建 `c3b32c527cf7f80f2c0aaea81e92a822480efb56`，不是本次文档提交产生的新应用。实际加载 UI DLL SHA256 为 `BE5A365A577756B29B7E3EDDF3C4EF3A7F1D12350DD92269522C1E19EF265D68`，与原 build/layout 一致；四项实际加载模块见 [modules](evidence-56-authenticated/modules.json)，注册/运行时来源见 [provenance](evidence-56-authenticated/provenance.json)。源工作区 src/tests/externals 相对 c3b32c5 无差异。

没有重新部署、卸载、读取/复制凭据、触碰生产/Dev/PID9608、安装工具、改DPI或强制GC。新旧 layout、包注册和登录数据保留。没有播放、点赞、收藏、评论、关注、下载或发送操作。截图包括私人推荐与头像，只在本工作区 ignored `artifacts/auth56` 留存，不提交图片或原始UIA树。

## 可观测性先行检查

| 指标 | 本轮口径 |
|---|---|
| 真实业务请求页/批次、游标、响应条目数 | 未取得，不能从滚动、卡片增量或内存推算 |
| 活跃 Videos.Count、缓存槽/Capacity、对象身份去重 | 未取得；外部UIA不提供VM字段 |
| 实际数据条目、业务Id去重 | 未取得全量；只记录UIA可访问卡片及文本散列代理，文本散列不是业务Id |
| UIA RootCard | 当前 scoped树中 AutomationId=RootCard 节点数；包含不可见/零矩形节点，不是 VisualTreeHelper 或全堆计数 |
| UIA viewport相交 | RootCard非offscreen、正尺寸且与主PART_ScrollPresenter矩形相交；含部分边缘卡片，不验证遮挡或实际像素 |
| 完整已实现卡片/隐藏页面卡片 | 未取得；UIA不暴露的回收或隐藏对象不能算作已释放 |
| private/working set | 同一owned进程Get-Process瞬时bytes，包含完整应用、图片/native、分配器和诊断影响；不是独占缓存成本 |
| managed heap、存活VM、对象根、共享/独占retained bytes | 未取得；未安装堆工具或抓取可能含认证的dump |

复用已缓存 DiagnosticsClient/TraceEvent 库创建 [小型HTTP观察器](auth56-observer/Program.cs)，精确EXE guard拒绝PID9608，live解析仅允许推荐/热门/排行端点，输出类别、时间、ActivityID、状态码，不落原始trace、URL、query、headers或body。先后三次试验均未收到HTTP事件，前两次ready检查早于就绪且早期滚动不能作为有效采集窗口；第三次等待ready后也无事件。

最终读取实际 layout 的 runtimeconfig 发现 `System.Diagnostics.Tracing.EventSource.IsSupported=false`，同时 `System.StartupHookProvider.IsSupported=false`。这解释了HTTP EventSource方案的不可用性；零事件/零lost**不代表零请求或缓存命中**。没有更改runtimeconfig、启用注入或扩大诊断架构。三次失败观察结果原样保留于provenance，观察器只是未能采数的实验，不是生产性能工具验收通过。

## 真实操作及证据

见 [53个检查点](evidence-56-authenticated/samples.jsonl)、[后半程动作日志](evidence-56-authenticated/actions.jsonl)、[复算摘要](evidence-56-authenticated/summary.json)与[内存图](evidence-56-authenticated/memory.html)。采样时间14:34:38–14:52:07附近，以JSON中的完整时间为准。窗口截图1728×1014像素；没有改窗口尺寸或系统DPI，未测量DPI数值。首批6个检查点只记UIA offscreen标记，随后加入矩形相交/文本散列，不回填前6行。

操作为推荐初始内容→正常底部滚动浏览→显式回顶部→热门B→全区排行榜C→推荐A→十轮B/C/A→向下滚动一次→视频分区页→返回流行推荐→普通刷新一次→10秒尾窗。初次视频分区页显示科技推荐，未切换子栏目或排序。截图确认侧栏选择、标题和实际内容一致。各回访使用真实侧栏click，没有直接执行VM命令。

采样在操作后固定等待3–8秒再读取，截图和尾窗另耗时；没有VM loading或网络完成oracle，不能称每点严格布局/网络稳定。完整请求页数保持 unknown；没有用刷新重复计页，也没有以服务末页或限流作为无证据的停止理由。分页阶段因无法核验真实批次而停止扩大刺激，未到已确认的服务末页。不能用随后十轮切换或53个检查点凑成50页。

| 检查点 | UIA RootCard / viewport相交代理 | private bytes | working set bytes |
|---|---:|---:|---:|
| 初始推荐 | 30 / 未采矩形（offscreen=false为20） | 229,281,792 | 375,349,248 |
| 首次回访推荐A | 181 / 20 | 796,131,328 | 928,825,344 |
| 第十轮返回A | 181 / 20 | 1,118,642,176 | 1,235,247,104 |
| 离开顶部、导航前 | 226 / 20 | 942,632,960 | 1,058,803,712 |
| 从视频分区返回 | 226 / 20 | 895,438,848 | 1,012,228,096 |
| 普通刷新后 | 30 / 20 | 901,382,144 | 1,017,073,664 |
| 刷新后尾窗 | 30 / 20 | 897,556,480 | 1,013,256,192 |

十轮A/B/C各10个检查点的UIA序列散列、可见文本散列均与对应首次回访一致，代理数分别181/28/88。这是可访问内容保持证据，不是Videos.Count、缓存命中数或零新增请求。UIA代理数随滚动可增加也可下降，所有53点最大226；最大private样本1,123,688,448 bytes，最大working set样本1,240,150,016 bytes，均非真实峰值。

离开顶部后导航前/返回的UIA内容序列、20个可见文本散列及首卡屏幕Y=342相同，原图也显示相同切片及边缘裁剪。只签该次位置保持，不等于稳定业务Id/像素偏移锚点契约。刷新后内容序列和可见文本变化、回到顶部，尾窗内容散列保持；未证明旧对象已释放。进程包含完整应用及观测影响，没有根/GC代次或归因对照，不从波动推导泄漏、每条成本或预算。

[白名单日志](evidence-56-authenticated/list-errors.json)窗口为[14:33:00.000,14:52:25.425)，精确包固定日期Logger共享只读，三类固定应用错误（推荐/热门/全站排行）共0。读取实现整文件在内存分块，输出仅固定分类/时间/hash；未读取认证存储。它不覆盖所有错误，也不能证明HTTP全部成功、没有限流或真实请求数。

10张本窗口PNG均逐张查看：初始/长列表、B/C/A首次回访、第十轮A、导航前/视频分区/返回、刷新后。可见内容、封面及侧栏一致，边缘行裁剪符合滚动；长标题存在模板省略号，不升级为完整视觉矩阵通过。原PNG路径、SHA256和有限判断见 [visual-review](evidence-56-authenticated/visual-review.json)，没有提交私人截图。

### 诊断与执行偏差（保留）

- HTTP观察器三个版本普通编译均成功（0警告/0错误），但无HTTP事件；最终源对应第三版显式keywords=-1/Verbose，前两版记录不重写为成功。没有对失败工具增加认证提取或更改应用配置。
- 两次Scroll百分比/一次SizeOfSet查询未得到属性（CLI调用失败），原输出未打印，不能据此声称属性值为0或列表为空。早期路径通配符查询失败后用具体文件读取，非生产失败。
- 长浏览时误在前一批尚未完成时启动下一批，14:41:17存在browse-6检查点夹在browse-3/4之间；发现后中断第二批，原检查点保留。该段不是严格串行基准，不根据标签编号推导批次。其后十轮B/C/A全部等待前批结束后串行执行，30个样本逐项复算。动作文件从14:41后才启用，不冒充覆盖最早滚动的完整动作审计。
- 最终Python语法检查第一次按本机默认GBK读UTF-8源失败；显式UTF-8后语法与分析重跑成功，不属于应用失败。新增PowerShell脚本语法检查通过。

## 审查与未验项

源码复核 PopularPageViewModel 的离开时 ToList 缓存、命中时Clear/Add、普通刷新移除当前缓存并重置offset，生产未改。实际UI行为只能支持可见内容保持/变化，不证明该路径的全部引用或网络副作用。AppRepeater/UniformGridLayout 静态声明和UIA代理数都不能签有效虚拟化预算。未发现本轮足以确认并需要扩修的生产缺陷。

原 [issue-56](issue-56.md) 的完整非活跃栏目淘汰候选、活跃列表保留及分页窗口风险继续适用；本轮没有实际游标、产品内存目标、独占bytes或代表性多轮数据，仍不能制定条目数/MB/TTL默认值。后续保持F0补齐可观测性、F1/F2归属/迟到结果复现、F3/F4完整栏目预算、F5窗口回取契约这些独立切片，不发布新issue。

PR #27 未合入，本轮不验撤销/快照/diff峰值。真实50页、完整VM缓存/存活/根、真实请求数、返回中部稳定Id锚点、分区子栏目与排序、冷/热各三轮、长期稳定、GPU/native归因和Release/AOT仍未验。没有push、PR或issue关闭。

## 清理及提交核验

14:52:23 持有已验证的owned进程句柄调用CloseMainWindow，退出码0；owned进程0，三个观察器均已退出。`winapp ui yield --on local --json`退出0，**released:false**（当时无可释放租约），不改写为true。此后无桌面输入。见 [cleanup](evidence-56-authenticated/cleanup.json)。包仍注册到指定新layout，旧layout保留；后续仍需按AUMID激活并重新发现PID/HWND，不复用本报告句柄。

生产src/tests/externals与固定kernel未变。提交前复算53行、十轮30点、来源hash、新旧layout和PowerShell/Python语法，检查新增文档链接、敏感输出与diff whitespace；具体见 [verification](evidence-56-authenticated/verification.json)。仅docs实验，没有重跑403或完整应用build。最后同步本地父分支并报告最终提交SHA/clean状态，避免自引用。

父工作区的后续独立只读复算、来源核验与未验边界见 [集成复核](integration-ui-56-authenticated.md)。原视觉、登录与清理记录不升级为合并阶段的新运行验证。
