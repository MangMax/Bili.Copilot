# 性能规格 #45 验收状态

最新 .NET10 **有限兼容采用**（2026-10-31）：指定独立 review `cec615167b60352b4a63a732709c5107af65d8c1` 以 `--no-ff` 合入为 **`5e628048815afc1602d882bcf7e9aa4cbde74b21`**，完整候选 `79ffd4f1`、稳定依赖/调查及 PR27/43 均保留，net11 候选未合入。kernel **`0ae5e4e000a41a07f6696746706df11c06e8835b`** 从已验证 bundle 恢复，取消/pageSize 祖先通过，仍 **local-only**。Fresh 全套 **487 passed / 0 failed / 0 skipped**；DebugClean/Release x64 JIT 各 **1 existing protobuf warning / 0 errors**，Visor Release JIT 0/0，runtime 文件门禁通过。每步立即独立封存原始输出、assets、runtimeconfig 和完整 raw-byte hash 清单。初次 Debug 共享输出有历史 DLL 残留，保留但不交接；全新 BuildOutDir 重建后的 DebugClean 与 Release 各 666 个 activation 文件、DLL 集合与 recipe 精确一致。见 [本轮集成与冻结产物](integration-net10-adoption.md)。

性能 **HOLD**，同源 net9 native crash 未解，非已证明 net10 修复或全面稳定性 PASS；**#49 pause FAIL、AOT BLOCKED、最新认证首页未验**。RC 仍实验且历史 production output 已覆盖；五份旧 JSON 哈希修复仍待父处理。能力审计仅“未建立足够适用性”，不是所有包不存在 ≤M 机会。本轮不部署/启动应用、不读凭据或触碰保护注册/layout/login/PID9608/Dev，无远端/PR/issue操作；冻结产物只供父后续同身份认证更新。以下记录均按各自历史 SHA 解读。

此前 PR27/43 本地集成（2026-10-30）：指定 `d3ab56c` 以 `--no-ff` 合入为 **`2b12951179fb757d6b2559a1ea2a97fe2c52003d`**，当时生产来源 `65e5312`，PR27 f19f030 / PR43 2f1ae8f 均为祖先。独立 Standards/Spec 审查无新增 blocker；fresh Release **487 passed / 0 failed / 0 skipped**，新测试 DLL 指向 merge，Debug x64 JIT **0 warnings / 0 errors**、显式 runtime 文件门禁通过。见 [独立集成复核](integration-pr27-pr43-review.md) 和 [原交接/重放基线](integration-pr27-pr43.md)。

kernel 更新为 **`98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`**，同时保留 2ca79bd 取消链与 f02c33f pageSize，父 nested clean，已用 committed bundle 恢复精确对象；**local-only，后续须发布专用 kernel 分支**。本轮没有 push/PR/issue、依赖升级、UI 启动或认证包操作。Controls 7/7 为 3838c9d 源码闭包证据；65e5312 Player 15/15、Main 4/4、20 循环及183样本仅复核已有受控证据，不等于 fresh UI 或认证首页/A-B收益。Caa6a7be 认证包仍旧 c3b32c5；AOT BLOCKED、#49 FAIL、#56预算及硬件/DPI/异常退出注入缺口继续保留。

截至生产代码基线 `778caac89fbb62414a43b99f566754e8dad55a05`，调查证据集成基线 `bcce5f3106088469db93196515bc233799f8934c`。最新 #49 文档诊断集成 `1cddcd6614591d1a28f6ac5a91cae484f57748c9`（父起点 `51154f0`），正式代码/依赖未改。这是进度账本，不是最终验收报告。所有工单保持开放，PR #60 保持 Draft。

本轮 #46 修复之前的完整生产集成验证为 #49 重挂修复合入 `7aa05616f990183261e21295e5c9a2a70a6323de` 后的新跑 Release **403 passed / 0 failed / 0 skipped**，TRX 已解析；Debug x64 与运行库门禁通过（Debug 零警告/错误，Release 保留 protobuf unused import 警告）。见 [#49 重挂集成](integration-49-remount-fix.md)。随后 #49 诊断与 #52 补充验收均为 docs-only，未重跑 403/build，不代表新增生产验证。kernel 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。

最新 #46 一行生产修复独立验证：Release **403 passed / 0 failed / 0 skipped**（TRX 403 条已解析）、Debug x64 及 runtime gate 零警告/错误；真实 VM 六入口离线 38/38 通过，详见 [Discard 修复复验](ui-46-discard-fix.md)。测试版本以该报告源码 hash 为准，原红保留；不改变认证、AOT 或性能未验边界。

最新 #48 当前P时长修复独立验证：Release **403 passed / 0 failed / 0 skipped**（TRX解析）、Debug x64/runtime gate零警告错误；新完整真实VM诊断宿主原九项及三边界 **12/12**，宿主构建 **1522 warnings / 0 errors**。受控真实VM 12项及已登录完整应用普通多P有限行为均通过；完整包已更新为c3b32c5修复源码的Debug x64/JIT产物，新绑定为t3-1630ecee/artifacts/full-app-duration-fix/layout，旧layout保留。真实网络完整性未证。见 [时长修复复验](ui-48-duration-fix.md)及[完整应用集成复核](integration-full-app-duration-fix.md)。

历史验证记录：#52 生命周期修复合入 `ca362d7` 后，401 passed / 0 failed / 0 skipped，TRX 已解析；Debug x64 构建和运行库文件门禁通过，零警告/错误。本次生产改动仅 MyImage，kernel 仍为 `2ca79bd`；其他票未重做 UI 验收。测试通过不能替代下表的真实 UI、发布产物或性能证据。`21a9362` 搜索小改在 `84d2793` 合并后新跑完整 Release 401/0/0、TRX 解析及 Debug x64 零警告/错误；当时桌面复验发现卸载后迟到输入缺陷；`7516070` guard 修复的新独立复验已验证原顺序不再产生请求/结果，完整 Release 401/0/0 与 Debug x64 零警告/错误再次通过；#51 仍只有限验收。

| 票 | 已交付证据 | 尚需完成的主要验收 |
| --- | --- | --- |
| #46 AI 流式更新 | Discard 同步清进度一行修复；新独立身份真实 VM 六入口/产品面板离线 10/10 场景、38/38 断言，原 DiscardClearsProgress 红→绿；取消/清空/迟到异常/重新生成/切源/生产全文复制与实际输入通过。fresh Release 403/0/0、Debug x64/runtime gate 通过，8 图逐张复核。见 [独立复验](ui-46-discard-fix.md)，原 37/1 和全部证据保留 | **有限真实 VM/产品面板离线通过，未关闭**：生成中 Discard/Regenerate 按钮禁用，竞态为命令级；认证真实供应商、完整应用、Release/AOT UI 和实际性能仍未验。代理调度样本不证明输入延迟/收益；专用 WinUIAnalyzer 未运行；owned 进程/包清理且 yield released:true |
| #47 Markdown 清理 | 删除强制 GC；真实清理/重挂载修复保留。新增同源 Release JIT 三组 EventPipe 对照（仅恢复历史 GC 一行）：当前总暂停8.25–16.15 ms，历史93.53–109.43 ms/50周期，零报告丢事件；八轮各176功能断言，有限弱引用观察，父级独立重算、六份原nettrace离线解析与八轮57源文件/实际DLL hash复核一致，见[集成复核](integration-ui-47-gc.md)及[原GC对照](ui-47-gc-comparison.md) | **有限实验通过，未关闭**：当前最大单次暂停更长；真实控件自然分配GC后完整存活、诱发事件逐个归因、对象根/native内存/长期无泄漏/full app/AOT未验。原4张不完整记录保留，但父级读取相同hash原图与历史文字存在分歧，未签全部捕获可靠性通过；earlier readiness失败保留。已有EventPipe pause trace，WPR失败不再等于无暂停证据。未重跑403/build |
| #48 弹幕取消链 | 当前P时长一行修复；受控真实VM原九项及0/360/361秒边界12/12通过，历史Release403/0/0、Debug x64/runtime gate通过。修复版已登录完整应用P1/P2/P8及重开P1有限行为通过，稳定计数3119/2639/662/3119，两次关闭exit0；新包注册、build/layout及两次实际加载记录hash独立复核一致。见[完整应用集成复核](integration-full-app-duration-fix.md)与[受控复验](ui-48-duration-fix.md) | **受控真实VM与真实登录多P有限行为均通过，未关闭**：新窗口0分片错误、2个独立搜索取消块；历史1953分类不变。计划2/2/1段不是网络观测，零错误不证明全部HTTP成功、网络全取消或计数完整性。连续Reload仍为命令级；PGC本轮未测；#49 FAIL不变。后续绑定t3-1630ecee/artifacts/full-app-duration-fix/layout，旧layout及登录数据保留；本次独立集成未启动应用或重跑403/build |
| #49 重绘合并 | 自持幂等 Loaded/Unloaded 修复已合入；历史重挂结构 20/20、父级 403/0/0 和 build 记录保留。新增 docs 调度候选、独立架构审查与 trace 证据；历史 26 项屏障结果不等于完整验收。父级独立重算确认 frozen 两图白字/渲染区域像素相同，撤回“该两图证明白字消失”；历史含钩子 DLL 与新 clean/trace 产物明确分账。见[当前权威更正与集成复核](integration-engine-49-diagnostics.md)及[原重挂集成](integration-49-remount-fix.md) | **整票 FAIL/未关闭，候选不可发布**：其他暂停 seek/双向暂停切换首帧失败、render 线程所有权、UI 等待与关闭安全仍阻断。trace 有限窗口未复现不是修复；clean 对照未做 UI 行为验收。patch 仅在 docs，未应用到正式引擎/依赖。下一步先按架构分切片设计，不标原 M 票完成；完整播放器/登录/性能/AOT 缺口保留。本轮未重跑 403/build 或动态 UI |
| #50 首页独立加载 | 已合入 `2b71b0f`，真实 NuGet 离线组合 20/20：慢目录首卡、保存分区/缺失/失败回退、旧页隔离和三轮主侧重挂订阅恢复；父级新跑 Release 403/0/0、Debug x64/runtime gate 零警告错误。见 [集成审查](integration-50-53-lifecycle-fix.md) | **有限离线实机通过，未关闭**：单次首卡时序不是性能基准；跨进程持久化、完整真实导航/账户/API、PR #27 刷新/撤销兼容未验。150 次图片 logger fixture 异常保留，不算图片链路或 #56 采样通过 |
| #51 搜索防抖 | `7516070` 卸载后输入/focus guard 独立复验：真实 Keyword=q → Unloaded → 延迟 UserInput（loaded=false）顺序成立，2.739 秒卸载窗口零请求/结果；重挂载输入及空框聚焦 2/2、重复卸载生命周期 4/4、真实输入/IME 7/7、鼠标选择 1/1。首次 Loaded 后输入正常。fresh Release 401/0/0、Debug x64 零警告/错误；见 [新复验报告](ui-51-late-input-fix.md) | **有限通过，未关闭**：已覆盖迟到输入 blocker；历史 red、旧脚本依赖旧 bug 的前置失败均保留。20 张截图已逐张查看，old-rejected 缺失输入/弹层像素和 fixture 工具栏裁剪仍记缺口；真实区域导航、完整视觉矩阵、发布/AOT UI 与性能未验。owned 进程/包清理且桌面 yield |
| #52 图片资源 | 原生命周期修复及历史证据保留；新增六项清晰视觉与 23/23 结构检查。三路 SVG 等价输入/path 对照及官方文档定位平台 text 限制；portrait 完整、同为 96 DPI 的双屏移动通过；六对资源代理测量共 84 边界独立重算通过。见 [补充集成复核](integration-ui-52-completion.md) | **有限通过，未关闭**：visible café glyph 未通过，但不是本次 charset 回归；native allocation/优化收益未证，异 DPI、200%、长期性能、Release/AOT、full app 仍待验。不合作 SVG 槽位边界保留。attempt-b Source 读取 NRE 与 harness readiness 修订保留，单次最终通过不能排除生产生命周期问题；原失败及视觉分歧不覆写 |
| #53 滚动检查 | 已合入三个控件自持幂等生命周期修复；原 14 headless red/green 核验，实机 20/20 包括短页补齐、空/异常刷新恢复、排行静止、滚动/resize、三轮卸载及重挂；23 个稳定窗无请求/通知/几何事件增长。见 [集成审查](integration-50-53-lifecycle-fix.md) | **有限离线实机通过，未关闭**：精确 pending 槽/旧 delay 交错主要由 headless 证明，未测真实 Dispatcher 瞬时队列上限或在途服务取消；完整生产应用、图片、视觉矩阵、AOT 和 #56 性能预算未验。本次不部署、不启动、不输入 |
| #54 模式切换 | 合入 `47f64a7` 历史验收（merge `fc66f8a`）；父 checkout 新复核真实 source-linked PlayerWindow/native presenter 18/18，12 个关闭边界，live 调用正向对照；仅修 fixture 内容背景，17 张新图逐张复核，原 8+3 red 及新原 oracle 3 red 保留。见 [复核报告](ui-54-recheck.md) | **有限适配器实机通过，未关闭**：9 个迟到 GotFocus 单独分类，零观测到关闭后新 focus/activation/header 请求；不是全量 WinRT 访问跟踪。实际 PlayerOverlay/HeaderBar、完整独立/集成播放器、PR #43 托盘组合、跨进程设置/多屏、性能与 AOT 未验；生产未改，不重跑 full suite/AOT；owned 进程/包清理且桌面释放 |
| #55 下载等待 | 合入 `d43c8f4`（merge `99a306b`）；真实 source-linked ViewModel/runner/控件有限适配器实机 7/7：成功/禁用目录、非零/启动失败、复制无启动、关闭窗口宿主存活及宿主退出 child 存活两种边界。父级只读 verifier 与原始事件交叉校验 7/7、8 个生产源码 hash 匹配；见 [集成审查](integration-ui-55.md) | **有限适配器实机通过，未关闭**：Explorer 仅拦截记录；完整生产 MainWindow/播放器/托盘/应用生命周期、真实 BBDown/账号下载、通知视觉、持久化/视觉矩阵、性能和 AOT 未验。宿主退出未执行托管 Dispose/回调，退出码未取得；有限句柄样本不证明长期无泄漏。生产未改，本次未重跑 app suite/build 或桌面实验 |
| #56 缓存预算调查 | 原离线两轮各83点及来源/trace复核保留；新增已登录完整应用53个检查点、十轮30点，摘要与图表只读复算一致；历史加载4模块、新旧layout各6项hash匹配，来源c3b32c5。见[认证观测集成复核](integration-ui-56-authenticated.md)、[原认证观测](ui-56-authenticated-observation.md)及[离线复核](integration-ui-56-cache.md) | **有限证据集成，未关闭**：UIA不是VM缓存/全树/存活数；EventSource禁用使请求页数unknown，未签真实50页。固定等待非稳态，早期批次重叠保留；0固定错误不证明全请求成功。对象根/独占bytes、预算、泄漏/收益、长期稳定、PR27撤销与完整导航/视觉/AOT未验；截图本次只核hash，历史视觉分歧保留。不操作应用/桌面，不读凭据或原日志；新旧layout与登录数据未触碰，未重跑403/build |
| #57 预取可行性调查 | 实际包接口、离线两引擎实验、负例及后续草案 | 暂停空白根因、可移交截图、真实 HTTP/播放器时钟和连续跨段；不能启用生产无缝预取 |
| #58 解析复制调查 | 112 成功批次、42 异常批次、22 契约与15取消回归历史记录核验 | 后续 S 票应补目标 runtime/真实脱敏重放及配置兼容；当前只支持局部 offset 候选，不支持 JSON 合并解析或整机收益 |

Release/Native AOT：**BLOCKED（工具链）**。最终 publish 退出 1，缺少 `vcvarsall.bat` 和桌面 `lib/x64`，报 Platform linker not found；现存 UI DLL 仅为带 CLR header 的托管中间物，没有 Native AOT EXE/MSIX，未部署或启动，不能签运行通过。前两次调用错误及后续增量残留不等于最终工具链阻塞，详见 [发布证据](release-aot.md) 与 [集成审查](integration-release-aot.md)。

共同缺口：工具链阻塞解除后的完整 AOT/trim 警告审查、Release/Native AOT 发布产物运行、真实端到端性能对照，以及各票明确要求但尚未取得的 UI 证据。专用 WinUIAnalyzer 未运行，不能用现有编译器检查代称。现有集成与每票文档保留确切命令和测试保真度限制。

## 整分支审核（固定 4344ae7…bcce5f3）

Standards 轴未发现可证实的文档规则违规。维护性 Note：AIViewModel.Chat.cs 六个入口重复当前代次清理/异常归属逻辑，尚未证实运行错误，不为消除重复而扩大重构。资源 Warning：非取消型 SvgToImage 若长时间不返回会持续占用四个共享加载槽；迟到发布有保护，但不等于底层工作可取消。真实渲染器行为待 #52 验收，不能直接称泄漏。

Spec 轴发现 #53 空页/失败通知反馈链，已做最小修正与 8 项红绿回归；修正后完整 401 项通过、Debug x64 零警告/错误。详见 [两轴审核与修正](final-review.md)。独立修正复核、真实交互与测量仍待完成。认证限制、工具限制、缺失测量与确定生产缺陷须明确区分。历史 Markdown 即时观察失败原样保留，图片就绪终态通过不反向改写历史失败原因。
