# #50/#53 重复挂载生命周期修复

2026-10-30，本工作区 `t3-68f070fa` / `t3/fix-50-53-lifecycle-remount`。三个控件的重挂故障已完成真实 NuGet 宿主 red/green：修复前生命周期矩阵 1 PASS / 8 FAIL；修复后完整组合矩阵 **20 PASS / 0 FAIL**。最终 Release suite **403 passed / 0 failed / 0 skipped**；Debug x64 原工程构建 **0 warnings / 0 errors**。结论限于下述离线组合，不代表完整生产应用、图片链路或 #56 预算验收。

## 来源与范围

- base / 最终本地集成均为 `13c29ff8d06d8559bb1c2540308286c77e952ea2`。已在本工作区执行 `git merge --no-edit feat/performance-45`，最终仍 Already up to date；[记录](evidence-50-53-lifecycle-fix/final-integration.txt)。未修改父 checkout。
- 先完整解析验收提交 `a53413fd7aa3d8a9d2832ff26d769af6a1aa9aae`，核对其全部 119 个文件仅在 `docs/performance-acceptance`，再 cherry-pick 为 `da7e862`；没有生产增量。原 [ui-50-53.md](ui-50-53.md) 和所有失败材料保留。
- kernel gitlink / checkout 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，工作树干净。没有修改 NuGet、kernel、全局基类、VM、其他生产控件或 XAML。
- 已读 AGENTS、docs/agents、GitHub #50/#53、指定八项技能及真实宿主/基类证据。测试 seam 已由任务指定；独立完成调试、测试和 WinUI review，没有子代理、push、PR 或 issue 写操作。
- 新证据固定在 [evidence-50-53-lifecycle-fix](evidence-50-53-lifecycle-fix)。[来源](evidence-50-53-lifecycle-fix/provenance.json)、[实机源链接哈希](evidence-50-53-lifecycle-fix/production-source-hashes.json)、[XAML 原样复制核对](evidence-50-53-lifecycle-fix/xaml-copy-hashes.json)、[最终三个源码哈希](evidence-50-53-lifecycle-fix/final-source-hashes.json)。窗口标题保留 baseline 字样，并非 green 源码身份。

## 根因与最小改动

真实 `Richasy.WinUIKernel.Share 0.0.12-preview3` 的 `LayoutUserControlBase.OnUnloaded` 首次卸载时先移除自己的 Loaded / Unloaded 订阅，再调用 `OnControlUnloaded`；以后不会再派发这些 override。见既有 [IL](evidence-50-53/base-lifecycle-il.txt)；本轮 [DLL SHA256](evidence-50-53-lifecycle-fix/base-hash.json) 与原证据一致（`BC68250223D14F53F87082B35428EE2144B7333D124A09AF2A172BC389A232B7`）。

`PopularMainBody`、`PopularSideBody`、`IncrementVerticalScrollControl` 原来都依赖 override 恢复订阅/active。真实 red 中重挂虽然 IsLoaded=true，主侧订阅仍为 0、scroll inactive，刷新只取一页。连续三轮 unload 的 loadVersion 卡在 1。

三个控件均改为构造时自持 Loaded / Unloaded 订阅，将原接线逻辑移入私有事件处理器，使用 `_isActive` 保证 attach/detach 幂等。主栏新增该标志，其他两个沿用原标志。业务 override 已移除，避免首次基类回调与直接事件同时执行。自身事件不会在卸载时移除，因此下一次实际 Loaded 仍可恢复业务订阅。

保留 `_checkPending` 物理队列槽直到旧回调排空；保留每次有效 unload 的 `_loadVersion++` 拒绝旧 500ms delay；保留 `_selectionQueued` 直到其回调排空、卸载时清除 `_selectionPending`、重挂时重新请求选择恢复。没有修改阈值、CanExecute、分页、终页策略或调度优先级。

## 测试保真与 red/green

原 `IncrementScrollTestAdapters` 直接反复调用生命周期 override，掩盖 NuGet 自注销。现在 Load / Unload 仅分发相应事件，基类的首次 Unload 处理器自移除两项订阅，再调用 override；派生类自己的事件仍存在。没有复制业务逻辑。它仍是 headless 展示面适配器，不模拟真实 XAML、binding、WinUI 事件顺序或 ABI；真实底层基类验证由下面的实机 fixture 承担，未替换其 DLL。

- 原 12 个测试不改断言：[red-original-12.trx](evidence-50-53-lifecycle-fix/red-original-12.trx) 为 9 PASS / 3 FAIL，失败为 `ReloadBeforeQueueDrainsStillHasOneCurrentCheck`、`UnloadDiscardsQueuedLoadAndReloadChecksCurrentView`、`OldDelayedCheckCannotLoadIntoReloadedControl`。
- 新增 `RepeatedLoadedEventsKeepOneSubscriptionAndUnloadRemovesIt`：每轮重复 Loaded 后 ViewChanged/SizeChanged 各一个订阅，重复 Unloaded 后均为零，三轮均可加载。
- 新增 `ThreeRemountsRejectOldDelaysAndAcceptCurrentDelay`：三轮均拒绝跨卸载的旧 delay，接受本轮 delay 并执行一次加载。
- 新增后的 [red-14.trx](evidence-50-53-lifecycle-fix/red-14.trx) 为 9 PASS / 5 FAIL；修复后 [green-14.trx](evidence-50-53-lifecycle-fix/green-14.trx) 为 14 PASS。第一轮 green 的 adapter 委托 nullability 警告通过改用与真实 API 同形的 RoutedEventHandler 消除。
- 最终 [release-final.trx](evidence-50-53-lifecycle-fix/release-final.trx) 为 403 PASS，含全部 14 个滚动用例；[独立 XML 核对](evidence-50-53-lifecycle-fix/final-trx-validation.json) 确认非零发现数、403 条结果、无失败/跳过。所有早期日志/TRX 保留。

## 实机矩阵与计数

使用原真实 PopularPageViewModel 全部 partial / 生成命令、main / side / scroll、NuGet LayoutUserControlBase / AppRepeater、真实 Dispatcher、500ms delay、WinUI 布局和滚动。主侧/滚动 XAML 原样复制。服务为固定离线数据及受控完成，不访问真实 API/凭据，不伪造终页 CanExecute=false。

red PID 49300、green PID 37352，均为独立 `BiliCopilot.Acceptance5053D26C`。宿主/适配器/生产 XAML 在 red 与 green 间不变，只有三个控件代码改变。runner 的旧页哨兵由模糊匹配改为精确匹配；几何测试使用真实 PART_ScrollPresenter（先前已证明的脚本修正）。新增 OwnedLayout 路径边界保护和三轮生命周期检查。未调用私有生产方法。

[red 结果](evidence-50-53-lifecycle-fix/red-ui/results.json)：1 PASS / 8 FAIL，exit 1。第一轮卸载通过；三次重挂/刷新失败，第二、三次卸载 generation 未递增。它们属于同一根因的重复观察，不称为八个独立生产缺陷。

[green 结果](evidence-50-53-lifecycle-fix/green-ui/results.json)：20 PASS / 0 FAIL，exit 0；[完整 JSONL](evidence-50-53-lifecycle-fix/green-ui/events.jsonl)。每个 fence 先允许 1500ms 有限尾随工作，再验证后续至少 2950ms 的请求、通知、ViewChanged、SizeChanged 均无增长；全部 23 个窗口满足要求，见 [fence-summary.json](evidence-50-53-lifecycle-fix/fence-summary.json)。

| Requirement | Evidence |
|---|---|
| 推荐/热门不等待目录 | `SlowRecommend`、`SlowHot`；首卡时目录仍 pending，目录请求至首次已布局卡片渲染观察约 1090.37ms / 184.70ms，[时间记录](evidence-50-53-lifecycle-fix/first-card-timing.json)。单次受控时序，非性能基准。 |
| 分区恢复与缺失/失败回退 | `Saved restore`、`Missing restore`、`DirFail restore`；目录待定时 1 请求/0 条，之后分别为分区13 / 推荐 / 推荐。 |
| 空页/异常后静止且刷新恢复 | `EmptyThenRefresh`、`FailureThenRefresh`；首次累计5请求/3通知，刷新后9请求/6通知，均三条内容；存在实际空响应/异常，不通过终页假命令掩盖。 |
| 非空全局/分区排行静止 | `Rank idle`、`Partition idle`；均2请求/1通知，稳定窗不增长。 |
| 旧页隔离 | `StaleSection`；所有新结果为 HotVideoList，精确旧哨兵 `STALE MUST NOT APPEAR` 不存在。宿主直接调用 VM 的 SwitchHot，不据此声称完整用户点击导航选择验收。 |
| 实际滚动与 resize | `BurstGeometry`；166次 ViewChanged、24次 SizeChanged，60条数据，最终3请求/1通知，底部可见 #58/#59，随后稳定。 |
| 三次卸载拒绝延迟请求 | `UnloadDelay1`、`UnloadDelay2`、`UnloadDelay3`；active=false、主侧订阅0/0，版本依次1/2/3，请求保持卸载前的5/10/15。没有在途服务请求，因此不是服务取消证明。 |
| 三次恢复主侧订阅及scroll | `ReattachMainSide1`、`ReattachMainSide2`、`ReattachMainSide3`；实际 loaded/scrollLoaded/active=true，主侧各1订阅，侧栏 ItemsSource 恢复。 |
| 重挂刷新仍多页补齐 | `RemountRefresh1`、`RemountRefresh2`、`RemountRefresh3`；每轮新增4请求/3通知，最终均3条，累计请求10/15/20、通知6/9/12。 |
| 重复事件、物理队列合并、旧延迟 | 上述两个新增 headless 用例及原12例全部通过；`ReloadBeforeQueueDrainsStillHasOneCurrentCheck` 保证旧槽跨快速重挂保留。真实 Dispatcher 未植入队列探针，不能仅凭实机静止提升为每一瞬间物理队列上限证明。 |

重挂自身各产生一次新的空页检查，累计请求从5→6、10→11、15→16，再经手动刷新各增加4。这是 Loaded 触发的有限工作，未误判为无限循环；稳定窗零增量才是停止证据。

## 视觉、保真限制与未验项

本轮 **38张PNG全部逐张打开检查**：red 15张、green 23张，均附 UIA JSON。red 重挂左栏消失，刷新后仅一张卡片；green 三轮侧栏恢复，列表恢复三条。resize 后为两列，第三张位于下一行，视口下缘裁剪符合滚动布局；长列表底部 #58/#59 可见。SlowHot 截图捕获了加载中的遮罩，不能据此称完整视觉设计通过。

真实 VideoCardControl / BasicCoverImage / BasicScaleEffect 和三个生产卡片模板参与布局，但展示用 VideoItemViewModel、内存设置、页面导航基类、菜单/播放命令、资源本地化、数值格式化、应用组合根为适配器。封面/头像仍是占位，按钮的禁用态来自无操作 fixture。图像 HTTP handler 拒绝网络。

与原验收相同，宿主未初始化图片 NuGet 全局 logger，本轮记录150次 `ArgumentNullException(logger)`，均来自 ImageExBase 的图片链路；[异常分组](evidence-50-53-lifecycle-fix/runtime-exceptions.json)保留完整堆栈，未设置 Handled=true。应用完成全部状态检查，但**不称为异常干净的完整生产运行或图片解码通过**。没有为修生命周期扩大到该链路。

未验：真实账户/API、生产 Dev 包/完整导航、保存设置跨进程持久化、#27 增量刷新/撤销组合、DPI/主题/高对比/无障碍完整矩阵、Native AOT、图片加载预算、内存/CPU/虚拟化上限及 #56 生产预算。重复 Loaded 的合成事件断言和跨重挂500ms旧delay精准时钟控制属于 headless 测试；实机三次 unload/remount 为真实移除/重新插入视觉树，没有用伪基类替代。

## 构建、WinUI review 与清理

最终命令（在本工作区执行）：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=release-final.trx' --results-directory docs/performance-acceptance/evidence-50-53-lifecycle-fix
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-68f070fa/src/ -p:GenerateAppxPackageOnBuild=false
```

最终 [Release日志](evidence-50-53-lifecycle-fix/release-final.txt) exit0，403/0/0；[Debug日志](evidence-50-53-lifecycle-fix/debug-x64-03.txt) exit0，0 warnings / 0 errors。第一次 Debug 的 IDE0005 来自多余的 Microsoft.UI.Xaml using，已删除并在测试适配器提供 global using；实机 green 与最终生产源码只差这个无行为 using。第二次原工程代码编译通过，但依赖门禁因本 worktree 缺少 libmpv 失败。仅从既有 TEMP/bili-deps 压缩包解压 libmpv / ffmpeg 到本工作区被忽略的 Assets，第三次原命令通过；[来源及哈希](evidence-50-53-lifecycle-fix/runtime-assets.json)。没有下载、安装、策略修改、复制生产包、绕过门禁或改项目。前两份失败日志完整保留。

WinUI 自审：本次生产差异只涉及 UI 生命周期接线；没有新增业务逻辑、阻塞调用、后台线程触碰 UI、跨控件共享状态或异常吞噬。attach/detach 对称且幂等，active 在卸载清理前失效，pending/版本/selectionQueued 语义保留。XAML/binding/虚拟化/主题/可访问性结构未改。无本次变更的阻断 finding。Note：现有 WinUIAnalyzer 未引用，其专用检查未运行；未按技能安装新 analyzer，以遵守用户不安装及不改依赖要求。普通项目分析器正常运行，最终无警告。

两次独立自有进程均关闭、临时注册包移除、winapp ui yield 完成；green 最终清理 UTC `2026-10-29T20:32:59.2698707Z`：[cleanup](evidence-50-53-lifecycle-fix/green-ui/cleanup.json)、[关闭](evidence-50-53-lifecycle-fix/green-ui/close.json)、[yield](evidence-50-53-lifecycle-fix/green-ui/final-yield.json)。没有操作 PID9608 或生产/Dev 包。临时构建目录和本工作区忽略的构建依赖留作可检查产物，无存活 owned 窗口/进程/注册包，**桌面独占已释放**。
