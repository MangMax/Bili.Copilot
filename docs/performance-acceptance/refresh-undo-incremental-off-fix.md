# 关闭增量刷新后撤销入口与首屏补页修复

2026-11-01，父 HEAD `3add3023fe71284ef8c513f070b09db1c5daa205`，kernel `62b376ec9dba705b4280f5d4ac61dd4589551034`。
本轮只改设置/快照/数据加载必要路径，不改依赖、版本、NoWarn、身份或清理规则；没有子代理、push、PR/issue、安装或桌面输入。

## 复现边界

- 只读核对已注册 AotDev：`Mang.BiliCopilot.AotDev 2.2611.1.4`，EXE
  `BiliCopilot.UI.exe` 111,534,080 bytes / SHA256
  `C82D08FB84C71CC88BD0A407E3F6F0DDE419D77EE864382D569408F9F9290E2C`。
  该包由 a0ee201f 打包；本轮相关生产文件在 `git diff a0ee201f HEAD` 中无差异，故用它定位真实症状，未操作该身份、窗口或登录数据。
- kernel 初态异常（空工作树、474 项 staged deletion、HEAD `f02c33f`）。先记录，再按文档只在本区从
  `docs/performance-acceptance/dependency-upgrade/kernel.bundle` 取 `0ae5e4e`、
  `docs/kernel-article-null/kernel-article-final.bundle` 取 `62b376e`，显式 ref fetch 后 reset；
  现为 clean `62b376e`，未触碰父 checkout。
- 复现使用真实生产视图模型与命令（`PopularPageViewModel`、`VideoPartitionDetailViewModel`、
  `VideoPartitionPageViewModel`），只替换服务、调度器和本地设置宿主；不是镜像逻辑，也不是源码字符串断言。

## 根因

1. **非增量刷新仍点亮撤销。** `RefreshRecommendVideosAsync` 无论模式都捕获快照并在成功后
   `CanUndoRefresh = true`。非增量是替换刷新，本次没有可撤销的增量结果；替换后旧快照也会残留。
2. **设置关闭没有传播路径。** 设置页与流行/分区页共用窗口，页面只切换 `Visibility`，不重新 `Loaded`。
   `SettingsPageViewModel` 只写本地设置，页面视图模型收不到通知，已有撤销入口继续显示。
3. **替换后变短不触发补页。** 刷新成功后只在 `Videos.Count > previousCount` 时发 `VideoListUpdated`；
   非增量从多页替换为服务端一页（约 6 条）时数量下降，主体收不到通知，`IncrementVerticalScrollControl`
   不会检查视口，页面停在 6 条。这里不是把“一次返回 6 条”当故障：单页仍是一页，
   故障在刷新后的加载触发缺失。该服务 API 以返回 `(Items, Offset)` 续页，没有单独 `HasMore`。
4. **隐藏页淡出可能卡住。** 设置关闭时流行/分区页处于 `Collapsed` 子树，淡出 Storyboard 不保证推进到
   `Completed`，撤销按钮可能一直保持 `Visible`。不可见时直接折叠，避免把状态留给一次未完成的动画。

## 修改与共享边界

- `PopularPageViewModel` / `VideoPartitionDetailViewModel`：快照只在增量模式捕获；只有增量成功且落地时设置仍开启才
  `CanUndoRefresh = true`；非增量成功、失败或空结果不会留下旧快照；刷新成功（含变短）都发 `VideoListUpdated`。
  新增 `OnIncrementalRecommendRefreshChanged` / `InvalidateUndoSnapshot`。增量模式的引用、游标、撤销与 anchor 路径未改。
- `VideoPartitionPageViewModel`：设置关闭时逐个收敛缓存的分区详情实例。
- `SettingsPageViewModel`：先写设置，再通知两个页面视图模型（页面常驻，不能依赖重新加载）。
- `UndoRefreshButtonAnimator`：不可见子树直接折叠，不做无法完成的淡出；可见时仍保留原淡入/淡出。
- **任务 4（回顶部）共享方法边界**：`RefreshRecommendVideosAsync`（两页）、`RefreshVideoAsync` / `RefreshAsync`、
  `ClearRefreshSnapshot`、`InvalidateUndoSnapshot`、`VideoListUpdated` 发布条件。动画器改动只影响撤销按钮自身，
  不改变刷新按钮位置；回顶部若改 `IncrementVerticalScrollControl` 或滚动位置，请勿依赖旧的“仅增长才通知”假设。

## 离线证据

- 新增/调整的真实视图模型测试 5 个。修复前定向运行：**6 失败 / 1 通过**（增量用例通过）；
  修复后全量 `dotnet test`：**585 通过 / 0 失败**。
- 覆盖：非增量替换不提供 undo、变短通知、后续真实加载从刷新游标继续、设置关闭丢弃已有快照、
  请求进行中关闭设置后迟到结果不点亮入口、分区缓存逐个收敛、增量 undo 仍恢复原引用。
- `dotnet build BiliCopilot.UI`（Debug x64，显式 `SolutionDir`）：**0 warning / 0 error**。
- Native AOT 发布：见下节。

## Native AOT / WinUI

对修复后的本工作区源码做了 fresh `dotnet publish`（Release / win-x64 / Platform=x64，显式 `SolutionDir`，
未签名 `PublishAppxPackage=true`）。**exit 0**，日志实际出现 `Generating native code`，仅保留既有 protobuf
unused import 与缺少 `mspdbcmf.exe` 不生成 symbols 两个 warning，**0 error**；未新增 NoWarn、trim root 或依赖改动。
沿用既有 AOT opt-in 本地包 `Richasy.MpvKernel.WinUI.0.0.2-preview2-aot-local.1`
（nupkg SHA256 `916E00A1D49DDDDE35FFBB4E1EE321C64C0FC6422F6E0AC98A17250E67DC62ED`），仅经
`CustomAfterMicrosoftCommonTargets` 生效，不写回仓库依赖。

- EXE 111,536,640 bytes / SHA256 `1972259EEE37E1CF76B697409ACC0109EA428E3557FF696D281F9B6095E95EC7`；
  PE machine `8664`、CLR directory `0`，publish 与 MSIX 内 EXE 哈希一致。
- MSIX 167,999,793 bytes / SHA256 `AE2704433BF63A46EBB4E383E7925FA44E8B0A9DA94FCC7AFA85AA41A83AC345`；
  284 entries，194 个 publish 文件逐路径/hash 一致；无 managed PE、无
  `coreclr/clrjit/hostfxr/hostpolicy` 或应用 deps/runtimeconfig；16 个程序集 XML 文档按 schema 确认为构建文档后不随包分发。
- 该发布使用仓库原 manifest，未签名、未安装、未注册；没有替换已注册 AotDev 或正式包。

真实桌面绑定、视口与动画证据需要独立 owned 身份和桌面槽；本轮未获槽，未启动、点击或关闭任何应用，
也未读取凭据、URL、token、cookie、正文或用户内容。AotDev/其它现有身份与登录数据未被操作或升级。
因此本报告只把 AOT 编译/包闭包列为离线通过；真实绑定/视口/动画与独立退出/WER 观察仍待授槽。

## 清理

原始日志、binlog、TRX、JSON 与截图全部在 ignored `artifacts/`；仅提交上述最小源码、测试与本报告。
构建残留的 `src/_build`、`externals/*/bin|obj` 为仓库既有 ignored 产物；kernel 保持 clean `62b376e`。


---

# 固定候选 3f33b365 独立评审

评审基线：父 `3add3023`，候选 `3f33b365815d71b1e89620b43fe17de64d58393f`，kernel `62b376ec9dba705b4280f5d4ac61dd4589551034`。仅离线；未启动子代理、操作桌面/AotDev/登录数据、安装、合并、push 或 PR/issue。读取 AGENTS、code-review、winui-code-review、code-testing、run-tests、verification-before-completion；按用户指令独立执行两轴评审。

## Standards

本片未发现新增硬性规范问题。collapsed guard 仅遍历祖先 Visibility/检查 IsLoaded，并沿用 StopRunningFade/Hide；范围与本片隐藏页失效相关。但没有 WinUI 运行或 analyzer 结果，不能据此宣称动画验证通过。

## Spec：暂不建议直接合入

### P1：关闭再开启会复活已经失效的在途 snapshot

位置：`PopularPageViewModel.Requests.cs:71`、`PopularPageViewModel.cs:255`；`VideoPartitionDetailViewModel.cs:262,296`（均为候选行号）。

关闭处理只清字段，未改变在途请求局部 snapshot 的有效性；落地仅检查设置当前值。增量请求挂起 → 关闭设置并通知 → 再开启 → 返回成功，旧局部 snapshot 被重新提交，CanUndoRefresh 又为 true。违反“关闭使已有及在途 snapshot 失效”；此处不能只验证关闭后一直保持关闭。

建议单独维护 undo epoch：关闭时递增，请求捕获 epoch，提交 snapshot 时必须匹配。不要简单复用列表 generation 丢弃整个刷新结果，否则改变目前允许刷新内容正常落地的语义。

反例：`DisableThenReenableMustNotResurrectInFlightSnapshot`、`DisableThenReenableMustNotResurrectPartitionInFlightSnapshot`。

### P2：列表通知重入后仍提交旧刷新状态（既有缺口，合并边界必须约束）

位置：`PopularPageViewModel.Requests.cs:66-82`、`VideoPartitionDetailViewModel.cs:257-277`；undo 路径也有同类通知后写 cursor 的顺序。

generation 仅在通知前检查。RecommendationListChanged 同步回调中切换栏目，会清快照/推进 generation；旧栈返回后仍写 snapshot、cursor、清缓存并发 VideoListUpdated。流行页反例切到 Hot 后仍得到 CanUndoRefresh=true，随后 undo 可把推荐列表带入 Hot。Changing 回调或集合通知也需要同样考虑。本片没有新引入这些回调，但新增通知及与回顶部任务共改提交顺序时不能据“已有 generation guard”宣称重入安全。

建议在外部事件边界复核所有权；确定提交 cursor/snapshot 与 anchor 通知的原子边界，避免旧栈覆盖新栏目。反例：`SectionSwitchInsideListChangedMustNotPublishOldUndo`。分区同形代码为源码推断，未声称已有对应行为复现。

## 已核对范围与证据边界

- 两页非增量路径不 Capture，成功/空/失败清旧快照；增量精确引用 undo 的恢复循环未改。
- 设置页先持久化，再通知流行与分区页；分区页遍历缓存并覆盖 SelectedPartition。真实 SettingsPageViewModel 菜单绑定未运行，headless 直接调用通知不能代替它。
- 刷新成功后包括变短均发 VideoListUpdated，且先写 cursor；空/失败不发。正常续页仍限定 Count 增长才通知，本片没有把该条件改成无条件，因此未见新增空页每 500ms 自激循环路径。重复非空服务页不断追加是更早的边界，不能把条数增长等同于资源增长或据此宣称服务异常也必停止。
- 取消/切换前的迟到请求有 generation/version 拒绝；事件同步重入缺口见 P2。
- 历史 585 tests 和 NativeAOT 编译不作为本轮行为证据；本轮不重新发布。菜单、绑定、viewport、可见/隐藏页动画仍需父安排下一桌面槽后 native 实测，当前槽属于回顶部任务 b3c08188，本评审未占用。

## 最终可合边界

本片拥有设置传播、undo epoch/失效、刷新成功变短后的通知；回顶部任务拥有滚动目标/用户触发时机。共同核对两页 RefreshRecommendVideosAsync、RefreshVideoAsync/RefreshAsync、UndoRefresh、ClearRefreshSnapshot、RecommendationListChanging/Changed、VideoListUpdated 与 IncrementVerticalScrollControl.DelayCheckItemsAsync。保留普通分页的无增长不通知、旧 generation/version 不写回、刷新 cursor 提交早于补页通知。collapsed guard 仅合本片必要祖先隐藏判断；其它淡出取消/完成/再显示行为交由动画任务统一，不能用本片覆盖其实现。

## 本轮运行

原始输出仅在 ignored `artifacts/refresh-review/`。候选由 git archive 独立展开，kernel 由本地 clone 加仓库 bundles 提供对象再 archive，不改原子模块工作树。保留的反例测试源码应叠加候选后运行；父基线未含新增通知 API，不把仅报告提交当作可独立构建的功能分支。


运行入口（SDK 11 / VSTest xUnit 2）：

```powershell
$project = 'artifacts/refresh-review/candidate/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj'
$filter = 'FullyQualifiedName~PopularPageLoadingTests|FullyQualifiedName~PartitionLoading|FullyQualifiedName~RecommendRefresh|FullyQualifiedName~IncrementVerticalScroll'
dotnet test $project --filter $filter --logger 'trx;LogFileName=baseline.trx' --results-directory artifacts/refresh-review/results
dotnet test $project --no-build --filter "($filter)&FullyQualifiedName!~DisableThenReenableMustNotResurrect&FullyQualifiedName!~SectionSwitchInsideListChangedMustNotPublishOldUndo" --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/refresh-review/results
```

首轮 exit 1，Passed: 69, Failed: 3, Skipped: 0；三项反例都在 CanUndoRefresh 的 Assert.False 失败（实际 true），没有编译错误。排除新增反例后 exit 0，Passed: 69, Failed: 0, Skipped: 0。已解析两份 TRX 核对结果条数及 outcome。

| Requirement | Evidence |
|---|---|
| 非增量变短、无 undo、刷新 cursor 续页 | 两类 `NonIncrementalRefreshReplacesListWithoutUndoAndNotifiesShrink` 通过 |
| 关闭已有及缓存 undo | `DisablingIncrementalRefreshDropsExistingUndoSnapshot`、`DisablingIncrementalRefreshDropsCachedUndoSnapshots` 通过 |
| 在途关闭且保持关闭 | `DisablingIncrementalRefreshDuringRequestPreventsLateUndo` 通过 |
| 关闭后再开，在途 snapshot 必须永久失效 | `DisableThenReenableMustNotResurrectInFlightSnapshot`、`DisableThenReenableMustNotResurrectPartitionInFlightSnapshot` 失败 |
| 通知重入切换栏目，不提交旧 undo | `SectionSwitchInsideListChangedMustNotPublishOldUndo` 失败 |
| 空/失败页停止通知、可手工重试 | `EmptyOrFailedPageDoesNotScheduleMoreLoadingAndManualRetryRecovers` 8 例通过 |
| 空刷新保留列表/cursor、增量撤销原引用 | `EmptyRefreshPreservesListCursorAndPreviousUndo`、`SamePartitionEchoPreservesUndoSnapshot` 通过 |
| 旧分页不覆盖刷新与 cursor | `OldPaginationCannotClearPendingRefreshOrOverwriteItsCursor`、`RefreshReplacesAnInFlightRequestAndResetsItsCursor` 通过 |
| 500ms 调度、卸载延迟、视口补页 | `ThreeRemountsRejectOldDelaysAndAcceptCurrentDelay`、`ShortPagesFillViewportThenScrollAndResizeContinueUntilLastPage` 通过；仅 headless 适配层行为 |

两轴结论：Standards 0 项新增硬性问题；Spec 2 项，其中 P1 为本次失效契约遗漏，P2 为既有同步重入缺口。真实 UI 验收仍未执行。

## Parent corrective integration

The parent reproduced all three independent counterexamples (3 failed). Added a separate undo-snapshot version: invalidation advances it, and a request may publish its captured snapshot only while this version still matches. This deliberately does not discard the refreshed content when the setting changes.

Refresh and Undo now recheck list ownership across Changing/Changed notifications. Diff application accepts an optional ownership predicate and stops subsequent collection mutations after notification reentry invalidates the caller. The original callers remain unchanged. Added Changing and collection-notification variants to the section-switch regression.

Fresh Release UI suite: **590 passed, 0 failed, 0 skipped**, including the two DisableThenReenable counterexamples and all three SectionSwitchInsideListChanged boundaries. Raw TRX/logs remain in ignored `artifacts/refresh-mode-integration/`; no temporary evidence is added to Git.

Production Release JIT build verification is recorded below after completion. No desktop/AOT execution by the parent; the candidate's AOT publish remains historical and does not cover these follow-up guards. The installed user package is unchanged. Return-to-top/precise undo viewport and full animation remain separate active tasks.

Parent production Release x64 JIT build: exit 0, one existing protobuf unused-import warning, zero errors. No UI/AOT run or package update.
