# #56 首页列表缓存调查

日期：2026-10-30（Asia/Shanghai）。范围：scope:spike，半工作日上限；本次仅静态追踪和后续票草案，没有生产改动。关联 [#56](https://github.com/MangMax/Bili.Copilot/issues/56)、[规格 #45](https://github.com/MangMax/Bili.Copilot/issues/45)。

## 结论与验收状态

可确认有意的长生命周期持有与无条目预算的增长路径；不能据此宣布内存泄漏。建议优先验证“保留活跃列表和有效撤销，按完整栏目淘汰非活跃缓存”的候选方案。没有生产测量，**不推荐任何具体条目数、MB、TTL 或收益百分比**。分页窗口暂不作为可直接实施的优化：现有游标与返回定位契约不足。

调查文档已交付，但 #56 的真实长列表、存活对象、卡片数量和内存验收未完成，issue 应保持开放。WinUI 代码审核完成；WinUI 运行验收为 **BLOCKED / 未验证**，不是 PASS。

## 基线、权限和可复查命令

- 独立工作区：`C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-d2786c3c`；分支 `t3/investigate-issue-56-performance`。
- 开始 HEAD 与本地 `feat/performance-45`：`8cf40300d533ef15acd1c034f6a2630bbb4c88d0`。工作区开始干净，`git merge-base --is-ancestor 8cf4030 HEAD` 返回 0。
- 只读 PR [#27](https://github.com/MangMax/Bili.Copilot/pull/27)，头为 `863a941aa98b164778f862935e0c632c4cecd829`；[#37](https://github.com/MangMax/Bili.Copilot/issues/37) 已关闭，其实现提交 `d8c441f`。`git merge-base --is-ancestor 863a941 HEAD` 返回 1：PR 功能不能当成本地性能分支已有功能。
- 按 `AGENTS.md` 与 `docs/agents/{issue-tracker,triage-labels,domain}.md` 执行。已用 `gh issue edit 56 --repo MangMax/Bili.Copilot --add-assignee '@me'` 认领，并以 `gh issue view 56 --json assignees` 确认 assignee 为 MangMax。
- 未修改父集成、Markdown 文件/诊断、生产代码或系统设置。未部署、启动、输入、检查桌面、操作 PID 9608、复制认证或安装工具；未重试已知失败 `0xc5585011` 的 WPR。未 push、创建 PR、发评论、创建后续 issue 或关闭 issue。

核心取证命令（工作目录为上述独立工作区）：

```powershell
git status --short
git rev-parse HEAD feat/performance-45
git merge-base --is-ancestor 8cf4030 HEAD
gh issue view 56 --repo MangMax/Bili.Copilot --json number,title,body,labels,comments
gh issue view 45 --repo MangMax/Bili.Copilot --json number,title,body,labels,comments
gh pr view 27 --repo MangMax/Bili.Copilot --json title,body,headRefName,headRefOid,files
gh issue view 37 --repo MangMax/Bili.Copilot --json title,body,state,comments
git show 863a941:src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendRefreshSnapshot.cs
git show 863a941:src/Desktop/BiliCopilot.UI/Toolkits/Pure/RecommendListDiff.cs
git show 863a941:src/Desktop/BiliCopilot.UI/ViewModels/View/PopularPageViewModel/PopularPageViewModel.cs
git show 863a941:src/Desktop/BiliCopilot.UI/ViewModels/View/PopularPageViewModel/PopularPageViewModel.Requests.cs
git show 863a941:src/Desktop/BiliCopilot.UI/ViewModels/Components/VideoPartitionDetailViewModel/VideoPartitionDetailViewModel.cs
git show 863a941:src/Desktop/BiliCopilot.UI/ViewModels/View/VideoPartitionPageViewModel/VideoPartitionPageViewModel.cs
rg -n 'ToList|Cache|Offset|CurrentPartition|IsRecommend' src/Desktop/BiliCopilot.UI/ViewModels/Components/VideoPartitionDetailViewModel
```

部分初期 `rg` 调用把 Windows 路径通配符作为字面路径，产生“路径不存在”，随后通过 `rg --files` 定位具体文件并读取；这些失败查询不作为缺失代码的证据。

## 实际持有关系

以下路径均相对 `src/Desktop/BiliCopilot.UI/`，除另注外引用 `8cf4030`；方法名作为稳定定位点。

| 根 / 持有者 | 直接持有 | 释放/保留事实与影响 |
|---|---|---|
| `Controls/MainFrame.xaml.cs:29`，`NavigateTo` | `RootGrid.Children` 中已访问页面 | 第 91 行加入页面，第 103 行只设 `Collapsed`，导航不移除页面。隐藏不能等同于释放页面、控件或图片。 |
| `GlobalDependencies.cs:100–106` | Navigation、Popular、VideoPartition 单例 VM | 页面即使移除，DI 仍可保留 VM；单独“清页面缓存”不能解决数据持有。 |
| `Pages/PopularPage.xaml.cs:30`、`Controls/Popular/PopularPageControlBase.cs` | 从容器获取同一 Popular VM | `PopularMainBody.xaml` 的 AppRepeater 绑定 `Videos`，模板经 ContentControl 到 VideoCardControl。 |
| `ViewModels/View/PopularPageViewModel/*.cs` | `Videos`；`_videoCache[section]`；Sections；两个 offset | `SelectSection:90–119` 离开时 `Videos.ToList()`，命中时 Clear/Add 回现有集合；命中不移除 cache，因此活跃集合与缓存可同时持有同批对象。刷新移除当前 key 并重置对应 offset（第 177–208 行）。没有总条目预算或 LRU。 |
| `ViewModels/View/VideoPartitionPageViewModel/*.cs` | `_partitionCache[Partition] -> VideoPartitionDetailViewModel`；SelectedPartition | `SelectPartition:67–77` 缓存每个访问过的主分区 detail，没有淘汰；每个 detail 自己的 Videos 即使主分区非活跃仍被保留。 |
| `ViewModels/Components/VideoPartitionDetailViewModel/*.cs` | `Videos`、推荐列表副本、子分区列表字典、子分区 `(Offset, PageNumber)` 字典 | 子分区切换复制列表引用，主分区切换则保留整份 detail。排序清空子分区列表和游标字典；非推荐刷新清对应 key 与游标；推荐刷新重置推荐 offset 和缓存。无总预算。 |
| `ViewModels/Items/VideoItemViewModel/VideoItemViewModel.cs:35–65` | 基类 Data（VideoInformation）、标题/作者等字符串、Cover/Avatar URI、命令等 | 不能只按一个对象头估算条目大小；此构造函数没有给每个条目创建解码位图。模板 `Controls/Components/VideoCardControl/VideoCardControl.xaml:20–23` 另以 BasicCoverImage 绑定 Cover。图片/原生资源须另测，不能以 URI 数乘像素大小。 |

`ToList()` 新建列表/引用数组，元素仍指向同一个 VideoItemViewModel。统计必须同时记录引用槽数量、**按对象身份去重**的 VM 数量、不同资源 Id 数量，三者不等价。刷新产生的新 VM 即使 Id 相同也可能与旧快照中的 VM 不同。缓存字典键数受已访问栏目集合限制，不代表各栏目累计加载条目有界。

正常保留：已访问页面、单例 VM、返回用的数据和有效撤销快照。数据增长：持续分页、新栏目或增量刷新导致可达对象增长。真正泄漏需要在应当失效/淘汰后找到不合理根及重复周期的存活证据；本次没有此证据。

## 游标、锚点和撤销契约

### 本地性能分支

- Popular 推荐/热门各有一个 offset；排名为列表为空时加载整批。清除 cache 而不重置对应 offset 会让空列表从旧尾部续页，不能作为安全淘汰。
- #50 已引入 `_sectionVersion` 和 `_displayedSection`，拒绝过期 Popular 请求/排队切换。后续与 PR #27 集成必须同时保留这套保护和刷新代次，不能用较旧 PR 文件覆盖。
- 子分区分页依赖 `(主分区, 子分区, 当前排序, offset, pageNumber)`；当前排序变化通过全清子分区缓存维持一致。后续缓存键必须包括排序或维持这一全清语义。
- `PopularMainBody.xaml.cs` 与 `VideoPartitionMainBody.xaml.cs` 的更新事件只调用 `DelayCheckItemsAsync`。`IncrementVerticalScrollControl.xaml.cs` 提供读位置/回顶部，但本次两页代码追踪未见每栏目稳定 Id + 像素偏移的持久锚点。页面留存和框架隐式锚定可能保留位置，不构成实测保证。

### PR #27 的已实现能力（只读，未在本工作区重新运行）

- `RecommendRefreshSnapshot<T>.Capture` 复制列表引用并保存 offset；不是深拷贝，也不含滚动锚点。推荐刷新和撤销使用 `RecommendListDiff.Apply`；#37 的最小差异更新无需重新开票实现。
- 新刷新先捕获局部 snapshot，成功后写 `_recommendRefreshSnapshot`，恢复时同时还原列表与 offset。等待请求时的局部快照也是临时强根，不能只数字段。
- `ClearRefreshTip` 清快照、隐藏提示、递增刷新代次。栏目切换、子分区切换、主分区切换（离开 detail）、非推荐刷新、排序、手动关闭、撤销会触发失效。完成态下继续加载调用 `DismissRefreshTipForLoadMore` 清快照；“始终无限保留撤销”不是当前契约。
- 再次刷新会递增代次，成功替换快照；失败分支不清原快照而把提示置 Failed。不要把“再次刷新”简单解释成入口即释放所有旧快照。是否调整失败后保留需独立行为决定。
- 顶层 MainFrame 隐藏页面不调用两页的 `ClearRefreshTip`；不能假定切去其他功能页就取消撤销。
- PR 报告的历史实机刷新/撤销成功属于其构建证据，不是本次长列表虚拟化或内存证据。
- `RecommendListDiff.Compute` 分配 `int[currentCount + 1, targetCount + 1]`，还有列表副本和 key 数组；长列表刷新需测瞬时峰值，不能只测稳定缓存。二维 int 数据区理论大小为 `4*(n+1)*(m+1)` 字节（不含头与其他对象），**这是源码推导，不是测量或预算**。不在本票改动 diff 算法。

## WinUI 代码审核发现

使用 `winui-code-review` 检查持有链、集合通知、绑定与生命周期；使用 `winui-ui-testing` 审核证据要求及后续实验步骤。未提出新 WinUI API 替换，无需以机器 SDK 代替项目引用验证。未运行 app build、analyzer 或自动化 UI，不把清单审核称作全应用验收。

1. **Warning：子分区缓存归属顺序。** `VideoPartitionDetailViewModel.cs:95–108` 在保存旧 Videos 前先按目标更新 IsRecommend。推荐 R -> 子区 C 时，旧 R 被存入 child 字典的 R key；C -> R 时，旧 C 被写入 recommend cache，随后作为 R 恢复。上述是按源码执行顺序推导，尚无真实 UI 复现；PR `863a941` 的 `ChangeChildPartitionAsync` 也保留该顺序。应先保存离开列表归属，再切换目标，独立草案 F1 验证，不能让 LRU 把已有错配掩盖。
2. **Warning：分区普通分页结果缺少归属守卫。** 同文件 `LoadChildPartitionVideosAsync:171–186` await 前读取 CurrentPartition 发请求，await 后再次读取可变 CurrentPartition 写游标并追加 Videos。切换期间旧结果可能污染新分区；PR 刷新守卫不等于普通续页守卫。只读发现，交由 F2 复现处理。
3. **Note：虚拟化未证实。** `PopularMainBody.xaml:35–53` / `VideoPartitionMainBody.xaml:17–32` 使用 AppRepeater + `VideoLayout`；`App.xaml:72–79` 定义 UniformGridLayout；外层 `IncrementVerticalScrollControl.xaml` 为 ScrollView/ContentPresenter/Grid 链。不是简单 foreach 创建全部卡片，但静态布局声明不足以确认有效视口、回收或隐藏页卡片保留。不能据此直接换 ListView 或宣布虚拟化失效。
4. **Note：缓存解除引用不保证显存或工作集立即下降。** MainFrame 页面/已实现元素、图片层和在途请求可能另持资源；Popular 的 Loaded/Unloaded 订阅成对，VideoPartition 的 ViewModelChanged/Unloaded 负责解绑，但 Collapsed 不应当被当成一定触发 Unloaded。需真实树和堆验证，未判定事件泄漏。

## 候选方案与失效规则

| 方案 | 能解决什么 | 必须保留的语义 | 风险/结论 |
|---|---|---|---|
| 仅保留活跃列表，不压缩它 | 当前滚动、续页、刷新撤销路径改动最少 | 有效快照完整保留；集合身份不替换 | 活跃列表和隐藏页面的选中列表仍可增长，不能宣称硬内存上限；作为下一方案的前提 |
| 限制非活跃完整栏目缓存 | 减少历史栏目和主分区 detail 持有 | 淘汰 items、cursor、排序上下文、锚点应为同一事务；命中继续原链，miss 从初始游标重载 | 优先候选。Popular 与分区应各自小票；仅减引用数组可能不减少独占 VM；活跃快照不得被静默删掉 |
| 活跃列表分页窗口 | 理论上可限制长时间连续浏览的条目 | 页边界游标、可回取历史、稳定 Id/出现次序锚点、像素偏移、撤销覆盖区 | 推荐流不保证历史可重复获取；只删除头部会丢返回内容/锚点，保留尾游标也不能恢复历史。需要新规格/契约调查，不直接实施 |

建议缓存单元使用明确上下文：栏目/主子分区、排序、会话代次、完整列表、下一游标、可选锚点、最近使用序号。当前不新增此类型。

失效规则：显式刷新按既有行为重置本栏目；排序变更清对应排序上下文及游标；缓存 miss 清旧游标后从头加载；账号/会话变化不得复用不同身份的缓存（现有跨账号清理尚未核验，后续必须核验）；迟到结果以发起时上下文和代次拒绝；淘汰不应触发活跃 ObservableCollection Reset。仍可撤销的上下文和在途刷新暂时 pin，预算不足时先报告压力，不能暗中损坏撤销。

顶层页面虽隐藏，其“选中列表”是否可被视为非活跃，必须显式通知 VM 并处理返回与撤销；第一版不能仅按 `SelectedSection != key` 推导整个应用的活跃性。不同页面淘汰政策不得形成绕过总预算的多个无界独立池。

## 测量状态与预算方法

本次没有操作许可中的真实 WinUI 会话，未采集运行应用数据。没有创建玩具 VM/模拟堆实验：它不能回答真实 VideoItemViewModel、WinUI 实现元素与图片资源占比，也不能为预算提供有效依据。无窗口可完成的源码/提交比较已经完成。以下 **N/A 是缺失，不是 0**：

| 指标 | 本次数据 | 原因 |
|---|---|---|
| 固定长列表实际条目数、缓存槽数、不同对象/Id 数 | N/A | 没有获许可的真实列表会话 |
| VideoItemViewModel 存活对象数、根路径、独占保留字节 | N/A | 没有生产堆样本 |
| 已实现 / 视口内可见 / 隐藏页卡片数 | N/A | 没有实际 WinUI 树和视口证据 |
| Managed heap、private bytes、working set、原生/图片资源 | N/A | 未采样目标进程；WPR 不可用且未重试 |
| 长列表切换、刷新、撤销前后差值/耗时 | N/A | 没有运行受控操作 |

预算计算方法（待测参数，不是建议值）：设 `B` 为产品接受的该场景总内存上限，`Mbase` 为同构建基础内存，`Mactive` 为活跃列表+已实现元素的增量，`Mundo` 为快照独占对象和引用槽，`Mpeak` 为刷新/差异计算峰值余量，`Msafety` 为由重复实验波动得出的安全余量。可用非活跃预算为 `max(0, B-Mbase-Mactive-Mundo-Mpeak-Msafety)`。

先用堆按身份去重确定每栏目可回收的独占保留量（共享对象不能重复计费），再通过真实淘汰前后测量校验；数量上限只能从该字节预算和观测分布转化，需同时限制条目与栏目。若只测到 managed heap，不得把所得上限当进程总内存上限。TTL 可约束陈旧内容，但不能单独约束内存。若活跃列表本身超 B，非活跃淘汰无法达成硬上限，应回到产品取舍，而不是强行删活跃项。

## 获桌面许可后的固定实验协议（未执行）

1. 记录最终集成 SHA、包版本、构建配置、架构、运行时、目标/PID、屏幕尺寸/DPI、账号环境（不记录凭据）、页大小与网络条件；同一构建冷/热各重复至少三次，此次数只是实验设计。先用 `winapp ui <verb> --help` 确认当前工具契约，绑定获许可目标，禁止复用 PID 9608。
2. 用真实推荐列表先采首批，再固定累计到 10 页与 50 页；这些是梯度刺激而非预算，服务端不足时记录实际数量/停止原因，不能填充模拟条目。记录逐页 Id 序列散列、对象身份数、offset；采样在加载完成、布局稳定的检查点，不依赖睡眠猜测完成。
3. 固定推荐 A -> 热门 B -> 总排行 C -> A，重复十轮且停止主动分页；另做主分区 X/Y、X 内推荐 R/子区 C 往返。每步记录 Videos.Count、每缓存 Count/Capacity、总槽数、按身份去重数、所有下一游标与当前排序。列表循环无新增时仍增长需定位根，不直接称泄漏。
4. 在长列表中部记录首个可见条目的 Id/出现次序/像素偏移；分别推荐增量刷新、撤销、普通刷新、继续加载、切走切回、排序、请求尚未完成时切换。验收内容顺序、游标、撤销失效规则和锚点，不只看滚动百分比。
5. 实际 WinUI 可视树取已实现 VideoCardControl 数；结合边界矩形/视口交集取真正可见卡片数，另计 Collapsed 页仍实现元素。UIA 未列出的回收项不能当存活数。记录 ElementPrepared/ElementClearing 或调试器树证据与滚动前后截图；若工具只能提供 UIA，则已实现数量继续标未知，不能强行通过虚拟化验收。
6. 各检查点同步记录 managed heap、private bytes、working set；授权的堆工具取得 VideoItemViewModel 根路径、共享/独占保留量。正常稳定观察与另行获准的强制 GC 实验分栏，不能在 app 里加 GC 以制造下降。工具不可用就报告缺口，不安装或调整策略。WPR 故障不再重试。
7. 对未来预算实现与基线用相同序列对照；稳定集合重复切换应趋稳，新增内容按实际数量解释。真正“释放”需根/存活证据，工作集下降不是唯一标准；虚拟化需已实现数相对视口而非总条目的实机证据。逐张查看截图检查布局、提示遮挡、焦点和刷新撤销状态。

建议数据行字段：`run, SHA, target, checkpoint, context, loadPages, items, referenceSlots, uniqueVMs, uniqueIds, cursor, undoItems, realizedCards, visibleCards, managedBytes, privateBytes, workingSetBytes, anchorId, anchorOffset, evidencePath`。本次没有伪造填充的数据行。

## 后续 ≤M 工单草案（未发布、未实施）

所有草案必须含独立 WinUI 代码审核和实机验收，不得以测试绿代替。实施前重新检查 PR #27 与最新集成。涉及测试执行先读 `run-tests`，新增/修改测试先读 `code-testing`；本次没有新增或运行测试，不需整套 app build。

### F0 · [Spike/S] 补齐 #56 真实列表与持有证据

交付：仅按上述协议采同构建真实数据、树/截图和根路径，给出参数化预算的实测输入。依赖：独立桌面时段和有权限的目标/采样工具。验收：所有 N/A 都取得证据或明确工具限制；不补填模型数据；给出可复现命令及 WinUI 审核结论。无需生产改动，可直接续办 #56，无需重复建调查母票。

### F1 · [Fix/S] 子分区切换按离开列表归属保存缓存

问题：ChangeChildPartitionAsync 先改目标 IsRecommend 再保存旧 Videos。交付：只修保存与切换顺序，列表和游标归属不串场；不引入淘汰。验收：R -> C -> R 与 C -> D -> C 均保持各自内容及续页上下文；实际 WinUI 切换看到正确栏目，刷新/撤销保持 PR #27 契约。测试验证可观察切换行为，不做源码文本断言。独立可合入。

### F2 · [Fix/M] 分区续页按请求上下文拒绝迟到结果

问题：普通分页 await 后用可变 CurrentPartition 写缓存。交付：仅分区普通分页的归属/失效处理，不重写全应用取消框架。验收：受控完成顺序中切换/排序/刷新后的旧页不污染内容或游标，正常续页可继续；真实 WinUI 快速切换验收。与 F1 可单独验证，预算实现应在二者确认后推进。

### F3 · [Perf/M] 流行页限制非活跃完整栏目缓存

前置：F0 给出预算输入、确认缓存 miss 返回顶部/重载的产品行为；与 #27/#50 集成确认。交付：仅 Popular 非活跃栏目缓存的可测预算/淘汰，成组清列表与对应游标，保活跃列表及有效撤销。验收：超预算后按约定淘汰，命中续页与 miss 初始页正确，迟到请求不能复活已淘汰会话；真实 WinUI 锚点、刷新撤销及内存对照。没有测量输入时不可填写生产默认数值。

### F4 · [Perf/M] 视频分区按完整 detail/子栏目限制非活跃数据

前置：F0、F1/F2 证据与真实返回契约。交付：只约束 VideoPartitionPageViewModel 管辖的非活跃数据，含每 detail 的 Videos 和子缓存，统一核算而非每层各一个无界池。验收：X/Y、多子栏目、排序和分页往返不串数据/游标；淘汰对象确无该缓存强根；活跃撤销 pin 有效，隐藏页返回有明确行为；独立 WinUI 验收。若需要同时重做 MainFrame 或全局缓存，重新定级并另拆票。

### F5 · [Spike/S] 验证推荐历史页回取与窗口锚点可行性

交付：仅验证服务端游标是否支持稳定回取、重复项和推荐更新时的锚点语义，形成可实现/不可实现结论。禁止直接做活跃窗口裁剪；如需要新 API 契约或跨页基础设施，另立规格。WinUI 验收需授权后验证真实返回定位，不能用假数据演示作为业务可行结论。

## 交付复核

提交前执行 `git merge --no-edit feat/performance-45`，从 `8cf4030` 快进至 `47f72537fb0cf8fb160042ea07ef395639f8cdd1`。父分支引入的 Markdown 诊断与 #52 改动仅随合并继承，本票未编辑这些文件。针对上述首页 VM、MainFrame 和 DI 文件执行 `git diff --name-only 8cf4030 HEAD -- <paths>` 无输出，持有链结论仍适用；本票相对该集成仅新增本报告。

使用 `verification-before-completion` 核对文档 diff、生产文件零修改、基线与集成关系。最终同步结果、提交 SHA、工作区状态在交付消息列出（避免文档自引用其提交）。本票不宣布运行验收完成，保留完整待验项：真实条目/对象/根、可见及已实现卡片、虚拟化、图片/原生资源、稳定与峰值内存、锚点、认证会话切换、请求乱序、刷新失败后快照、PR #27 与 #50 合并后的行为和生产预算。
