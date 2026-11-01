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
