# #56–#58 调查集成审查

日期：2026-10-30。父规格 [#45](https://github.com/MangMax/Bili.Copilot/issues/45)。本次接受三票的调查材料进入集成分支，不等于签署生产性能、完整 WinUI 验收或关闭工单。未实施任何生产优化，后续草案未发布。

## 集成范围与保护

唯一写入 checkout：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`，分支 `feat/performance-45`。开始 HEAD 为 `778caac89fbb62414a43b99f566754e8dad55a05`，porcelain 输出为空。已读 AGENTS、docs/agents 三份规则、winui-code-review、verification-before-completion，并用 gh 读取 #56/#57/#58/#45 全文及评论。

合并前分别检查 `git show --stat <tip>`、`git log 778caac..<tip>` 和 `git diff --name-status 778caac...<tip>`：每个分支相对起点仅有该调查提交，新增路径全部位于 `docs/performance-acceptance/`。独立实验工程、脚本和 fixture 不接入根构建，没有生产、根构建或 kernel delta。

| 工单 | 唯一指定调查提交 | 顺序执行的 --no-ff 合并提交 |
|---|---|---|
| #56 | `73c1953cce4f5f19ec26858da7c82683a54cb793` | `669c92efb3e9272bcc321fe889bce131910b22cd` |
| #57 | `62f701de2a28c93ec8f262636213fd76d37f13e7` | `c4c5093fc1a202ee89b5d1811f3d203c6aaf96fc` |
| #58 | `db693d866a97b5b67ab6d994428249e0b2012fcc` | `bb07e973fd63fced877cbb15fc192c050900fbb6` |

三次均无冲突，无覆盖或回退既有集成代码/fix。kernel gitlink 和工作目录 HEAD 固定 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。本次未访问其他 worktree，未创建子代理，未 push、创建 PR/issue、关闭 issue 或发送评论。桌面由 #52 独占，本次没有启动、部署、输入、截图或运行实验宿主。

## #56：静态调查成立，预算和泄漏判断缺证据

依据：[原报告](issue-56.md)及当前父 checkout 源码。

- 已满足：明确正常保留、可达数据增长、泄漏证据之间的区别；比较活跃列表保留、非活跃完整栏目淘汰、分页窗口；列出游标、排序、撤销、返回锚点的兼容规则和 ≤M 草案。报告承认没有生产采样，不推荐条目数、MB、TTL 或收益比例，结论适当。
- 只读复核：`GlobalDependencies.cs:100–106` 注册单例；`MainFrame.xaml.cs:91,103` 保留页面并隐藏；Popular 的 `_videoCache` 复制列表引用，分区 `_partitionCache` 持有 detail。`ToList` 不复制每个 VM，不能把槽数当独立对象数，也不能按 URI 数推算位图内存。当前 Popular 的 `_sectionVersion` / `_displayedSection` 保护仍在。
- **Warning，源码确认的归属顺序问题，非实机复现：** `VideoPartitionDetailViewModel.cs:95–108` 先把 IsRecommend 改成目标，再保存旧 Videos。R→C 会把旧推荐列表保存到 child 字典 R key；C→R 会把旧 C 写进推荐缓存并恢复为 R。可独立修复并做行为验证，不应藏进预算改造。
- **Warning，源码确认的竞态窗口，实际触发尚待验证：** 同文件 `LoadChildPartitionVideosAsync:183–185` 在 await 前按旧上下文请求，返回后按可变 CurrentPartition 写游标并追加。`IsVideoLoading` 仅阻止重入请求，不能阻止切换上下文；推荐加载路径也没有发布归属检查。疑似后果是迟到页污染新栏目，不能宣称已经复现。
- 未满足：真实长列表的条目/身份去重存活对象、根路径、已实现与可见卡片数、虚拟化、managed/native/image 内存、峰值和重复切换采样。没有这些证据，无法确定生产预算或宣布泄漏。静态 AppRepeater 布局不是虚拟化通过证明。
- PR #27 的 `863a941` 不是当前 HEAD 祖先（检查 exit 1）。其快照/增量差异契约作为后续兼容输入，不冒充本集成已有实现或性能测量；不重复开票实现 #37 的差异工具。缓存淘汰须成组处理数据与游标，保留活跃内容和有效撤销，返回锚点必须先定义并验证。

判定：接受调查文档；#56 的生产测量验收仍缺失，应保留未完成状态。

## #57：离线引擎证据有用，不能签署无缝窗口

依据：[原报告](issue-57.md)、[宿主源码](harness-57/App.xaml.cs)、[原始事件](evidence-57/measurements.jsonl)、[人工观察记录](evidence-57/visual-review.json)。

本次解析 142 条事件，核对两轮 Append：DirectX 保留旧 4 条并得到 6 条，但 new@500 位于未来项后；Legacy 仅剩新 2 条。公有列表输出支持“不能直接按到达顺序批量追加”的结论。实际 preview6 的三个本机 DLL SHA256 与提交清单一致。宿主文本转 LF 后 SHA256 为 `EE0649A08B14876FD813C878E4569646F2E4163D583D2D1546D7707ED3C4C34C`，与报告一致；当前 CRLF checkout 字节哈希不同是行尾转换，不能误判源码漂移。

替换后 count=1 不证明可见；人工记录称两引擎短列表替换为空白，显式 seek 后出现 REPLACEMENT，暂停 seek 后动画继续。两轮暂停/部分 Full、Dense 空白均被保留，不能挑选通过图。它们支持继续调查消费位置和暂停恢复，不足以判定空白根因，更不能归因于特定 GPU/驱动。

**Warning，证据可移交性限制：** PNG 未随提交保存，只有哈希、采集时间和人工结论；本次遵守不访问其他 worktree，未重新查看原图。视觉判断属于原调查者观察记录，并非本次独立视觉复验。可分享原图/录屏应成为下一轮契约验证交付物，哈希不能替代图像。

Primary 两引擎的提交事件早于慢邻段完成；根据 captureEndUtc 重新对照事件，可见上界记录约为 824/855 ms，且均早于邻段完成。该上界依赖人工可见结论，不是精确首帧。**Note，计数口径更正：** `Primary()` 直接设置 `requests = 2`，只运行两个 Delay，没有 Segment/HTTP handler，也没有真正缓存邻段数据。其事件名 `neighbor-completed-cached-only` 不构成缓存验证；应称“两项合成延迟任务”，不可作为实际请求数测量。Full/Window 的计数来自 Segment 函数进入次数，也不是 HTTP 观测。

已满足离线接口、追加/替换/seek 风险调查和有边界的回退草案；部分满足慢分片、长时间轴、高密度加载观察。Dense 的 40000 条中仅两条在采集时到期，不是持续高密度渲染。进程瞬时 private/working set 不能推导存活堆或窗口节省。模型最多三段不限制单段字节，模型排序去重也不是实际渲染契约通过。

历史 `dotnet-final-results.json` 逐项记录 19 Passed（取消/会话/重绘）；本次仅解析核对，不宣称新运行通过。缺失真实播放器时钟、HTTP、连续跨段、稳定暂停、边界 20 秒文本合并、可见不重放、native/GPU 资源及真实预算。不能启用生产窗口；保留 #48 取消/过期发布保护、#49 重绘协调和全片回退，回退也不是所有暂停场景已通过的声明。

判定：接受可行性与负面证据；首段独立展示有受限历史支持，无缝预取、真实播放器端到端验收未通过。

## #58：支持一个小范围 offset 候选，不能换算整机收益

依据：[原报告](issue-58.md)、[实验与契约](issue-58-replay/)、固定 kernel 源码与本机 NuGet 包 XML。

- 已满足受控 HTTP 输入→业务结果/异常的重放调查。合成 JSON 20/5000 项与真实 proto 类型的 gRPC 20/5000 条，不含真实账号流量；四个 kernel 源文件哈希与当前文件一致，两个正式运行的 fixture 长度/哈希均重新核对一致。
- 本次只读解析 run-2、run-3 各 56 行，共 112 批，每 fixture/variant 为 14 批，逐行确认 sends=iterations、checksum=iterations×条目数。重新计算的分配和 CPU 中位数与报告相符；大 gRPC 局部净分配差约 214589 B/op，不能解释为工作集下降或视频/整机提速。
- run-1 与还原/构建重叠，是排除样本，不进入汇总。另一次文件锁构建失败不是通过证据；正式成功测试记录与采样分开。六类异常共 42 批，各 500 次发送/异常；异常循环包含 CTS、捕获与实验侧手动清理，不能推作生产失败路径性能。JSON 候选实现不变，只是噪声对照，不支持 JSON 单解析优化。
- 重新解析三个 TRX 的逐项结果和 Counters：契约 22、#48 HTTP/retry 8、#48 session 7，全部记录 Passed，failed/error/aborted/notExecuted 等为 0。**这是已有运行记录核验，不是本次重跑。** 并非 22 项都逐一对比候选：候选覆盖成功、gRPC 长度/短输入、字段及坏 protobuf；多数 JSON/所有权/取消检查针对基线。后续生产候选仍须重跑相关边界并补配置/未知字段覆盖。
- XML 核实 Google.Protobuf 3.29.3 的 net5.0 和 netstandard2.0 均有泛型 `MessageParser<T>.ParseFrom(byte[], int, int)`；已有候选编译/执行记录交叉支持可用性。不能仅按 app props 的 3.27.2 推断实际还原包，也不把 net8 重放和 #48 在 .NET 10 的执行当成 WinUI Release 发布验收。
- `prepare.ps1` 只复制固定 kernel 到实验 obj，除类名改名外，仅把 payload 去头复制改为 `ParseFrom(bytes, Math.Min(5, bytes.Length), Math.Max(0, bytes.Length - 5))`。这保留直接 ParseAsync 短输入的空消息语义，以及经 SendAsync 时长度 <5 的拒绝行为。继续使用传入 parser，局部数组在同步解析期间存活，不外泄借用缓冲。
- JSON 验证/业务解析重复读取不等于重复下载；gRPC 候选只去掉 payload 数组复制，两层 ReadAsByteArrayAsync 分配仍在。响应由成功调用方持有，ParseAsync 不释放；验证失败路径没有确定性释放属于源码确认的所有权风险，不等于长期泄漏已证实。ParseAsync 无 token，依靠 #48 解析后及发布时检查拒绝过期结果；HTTP 层预取消仍可能进入注入 handler，与服务层预取消不调度是不同边界。

判定：调查足以支持单独 S 票进行 kernel protobuf offset 优化；JSON 合并解析、共享缓冲/池化、response Dispose 和协议校验增强均不在该票。尚缺真实脱敏分片、目标客户端 runtime/Release 重放及用户可见回归，不能声称生产收益已验收。

## 可发布后续建议（草案，均 ≤M）

| 建议 | 范围/前置 | 关键验收与停止边界 |
|---|---|---|
| 56-F0 首页长列表与切换资源采样（M 调查） | 先取得独立桌面时段，同构建固定操作；不改缓存政策 | 同时记录条目、身份去重对象/根、可见/已实现卡片、稳定/峰值内存，重复操作。无样本就不给生产预算 |
| 56-F1 子分区缓存按离开上下文保存（S） | 仅归属顺序；兼容未来 #27 | R→C→R、C→D→C 内容/游标不串，行为测试及真实切换；不夹带淘汰 |
| 56-F2 分区续页拒绝过期结果（M） | 受控乱序先复现；仅分区请求/发布边界 | 切换、排序、刷新期间迟到页不改新内容/游标，正常续页可继续；不做全局取消重构 |
| 56-F3/F4 非活跃缓存预算（各 M，条件草案） | F0 数据、F1/F2 及返回/撤销契约明确后分别限 Popular、分区 | 数据/游标一起淘汰，活跃与有效撤销保留，命中续页/miss 初始加载正确；需要全局缓存或 MainFrame 重构即另拆 |
| 57-A 双引擎状态契约复现与最小适配（M） | 无网络预取；先解决暂停/替换负例，交付可移交图像 | 两引擎验证 append/replace、seek、pause/hidden、卸载和不重放；包无法保证时保留禁用并另拆依赖修补，禁止扩大为引擎重写 |
| 57-B 当前位置优先调度（M，条件草案） | #48/#49 保留，生产发布必须等待 57-A | 真实 handler 计数、取消不重试、跨代在途上限、当前位置不等邻段、关闭无发布、两引擎回退；不将 Primary 预设计数当验收 |
| 57-C 弹幕窗口预算（M，条件草案） | 57-B 数据所有权明确 | 条目/字节双预算、单段超限、跨边界文本合并、长时与往返 seek；真实 Release 采样后定值，不静默截断 |
| 58-S kernel protobuf 解析避免 payload 去头复制（S） | 单独 kernel 提交/发布与客户端消费；本集成继续固定 2ca79bd | offset 重载、短响应、parser 配置/未知字段/坏数据、错误类型、#48 取消重试均保持；补目标 runtime 真实脱敏重放。不并入 JSON、池化、Dispose、协议或认证变更 |

给父级的精简发布建议：先发布 56-F0/F1/F2、57-A 和 58-S；预算/调度票保留条件草案，待各自证据成立再发布。#56 不以静态文档关闭，#57 不以离线首段可见签署无缝预取；#58 可作为调查完成候选，由父级按上述边界决定状态。本任务不代为建票或关闭。

## 本次验证与交付证明

本次只运行 Git、gh 只读查询、源码/包元数据读取及 Python 标准库解析 JSON/CSV/TRX/哈希，不执行实验、app suite、build 或 analyzer。未运行新的 .NET 测试，因而不需要为了重跑不变代码消耗桌面或构建资源；历史通过与本次核对明确分开。WinUI 审查聚焦持有链、异步归属、引擎状态和证据范围，专门 analyzer 诊断未运行。

相对原 HEAD，`git diff --exit-code 778caac HEAD -- . ':(exclude)docs/performance-acceptance'` 返回 0。下列 tree ID 在原 HEAD 与合并后逐一相同，覆盖生产、既有测试和全部 externals；根构建也在上述全路径排除检查内：

| 树 | 原 HEAD = 集成后 |
|---|---|
| src | `606114276e84778490c08e75fbda33d060dab420` |
| tests | `22f2e1c87a0ddb5964b58c2ceb879c93e6900cb4` |
| externals | `7c2327335bf0817e11baec77c4fffebad3d579d3` |

本报告提交后再次检查父工作区/子模块 porcelain、固定 kernel SHA、上述零 diff 与提交父链；最终报告提交 SHA 和 clean 状态在交付消息提供，避免自引用。
