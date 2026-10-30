# PR27 / #50 / #53：受控刷新撤销补证

2026-10-31，父本地起点 `1b425c0dfd86ed333eb88e3504f45d3c52a6e82f`。
**RC3 Release x64 JIT 受控补证有限通过：run4 12 PASS / 0 FAIL；退出 code 0 / PASS_BOUNDED。**
没有生产修改；没有公网请求、认证数据读取、push、PR/issue 或父工作树修改。
这补足固定批次的精确列表与空/失败停止证据，不把完整应用 PARTIAL、总体性能 HOLD 改成通过。

## 先清点既有覆盖，再补缺口

已读 AGENTS、docs/agents、三份指定集成/完整应用报告、run-tests、code-testing、winui-ui-testing、winui-code-review、verification-before-completion；runner 失败诊断使用 diagnosing-bugs。无子代理。
用 `gh` 只读 PR27、#50、#53 的约定。PR27 允许继续加载重复，撤销恢复列表与继续加载位置；最新集成保留分页期间 undo。

| 已有用例/证据 | 已证明的边界；本次处理 |
| --- | --- |
| `RecommendListDiffTests.Apply_DuplicateKeys_AreMatchedByOccurrenceAndKept`、`Apply_InsertionsAndRemovals_AppliesBothInSafeOrder`、`LongPrependAndUndoHaveBoundedAllocationAndPreserveReferences` | 纯算法的顺序/引用/重复键与 prepend/undo 分配界限；不重复造测。 |
| `RecommendVideoMergerTests.Merge_DuplicateKeysWithinCurrent_AreKeptAsIs`、`Merge_DuplicateKeysWithinIncoming_KeepFirstOccurrenceOnly`；`RecommendRefreshSnapshotTests.Capture_CopiesItemsSoLaterMutationsOfSourceDoNotLeak` | 合并与快照契约；本次只补完整真实 VM/控件组合的观测。 |
| `PopularPageLoadingTests.EmptyOrFailedPageDoesNotScheduleMoreLoadingAndManualRetryRecovers`、`EmptyRefreshPreservesListCursorAndPreviousUndo`、`RefreshReplacesAnInFlightRequestAndResetsItsCursor`、`OldCompletionCannotPublishOrClearNewLoadingState` | 真实页命令 + headless dispatcher/item 适配器，已有空/失败、游标、代次断言。不是公网未命中就重新发明逻辑测试。 |
| `IncrementVerticalScrollControlTests.ShortPagesFillViewportThenScrollAndResizeContinueUntilLastPage`、`DelayedChecksCoalesceAndCannotLoadWhileUnloaded`；`RecommendationKernelIntegrationTests.PageSizeAndCursorReachTransportAndCallerCancellationIsPreserved` | 排队、卸载、短页和内核传参已有覆盖；未扩大为新调度器/HTTP测试。 |
| Controls7：`PartitionSwitchPreservesCacheOwnershipAndCursor`、`PartitionEmptyRefreshPreservesUndoAndStopsNotification`、`PartitionLateRefreshDoesNotBlockChildOrPublishFailure`、`RealControlsRefreshUndoAndRemount`、两个 `HeaderUndoSurvivesRemount`、`CoverResizeUnloadFailureAndConsecutiveRecovery` | [既有报告](net11-player-timer-fix.md)保留其真实 NuGet 基类、控件与展示项适配器边界；其中已断言60项 SequenceEqual/引用。无需重跑 remount/cover。其单项分区及固定等待不能替代本次完整序列/并发屏障记录。 |
| [真实完整应用](full-app-net11-acceptance.md)、[独立复核](integration-full-app-net11.md) | UIA 6/32/13 不是 Items 总量，公网空页未观察到；本次不无限重试公网，也不把受控证据描述为真实公网命中。 |

上述历史结果仅引用闭包，不称本轮 fresh 487。生产未改，不重跑无关全套或生产 Debug/Release；本轮实际构建的是完整源码离线宿主 Release。

## 真实生产组合与冻结

在已有 `t3-e497cf43/artifacts/timer-fix/final/fixture` 完整离线宿主上最小扩展；重新复制当前 tracked UI 源。
真实 `PopularPageViewModel`、`VideoPartitionDetailViewModel`、`VideoItemViewModel`、生成命令、头部、列表主体、AppRepeater、IncrementVerticalScrollControl 和 NuGet 基类均参与执行。
没有镜像 VM、假列表、替代命令或 UIA realized count 充当全列表。
外部 Bili 接口沿用 offline overrides，推荐/分区接口增加固定 ID 批次和 TaskCompletionSource 屏障；设置只在宿主内存中。
测试只读私有游标/undo 快照，不写生产状态。引用编号来自 `ReferenceEqualityComparer` 字典；保留引用会影响存活，不能据此推导内存收益。

源比对仅五个已有组合/打包文件不同：App.xaml.cs、GlobalDependencies.cs、项目文件、两份 manifest。
所有其他 tracked UI 文件逐字节相同；额外 ControlledRefresh.cs 和既有 offline 测试文件只存在于 fixture。
新 identity `Mang.BiliCopilot.UndoControlled.T283.run4`；无生产注册更新。

复用现存 SDK `11.0.100-rc.3.26522.148`，绝对路径、进程内 DOTNET_ROOT，无全局安装。
kernel 最初仅 .git/index 未展开，初态保存在 ignored artifacts；从已存在 bundle 恢复到 **0ae5e4e000a41a07f6696746706df11c06e8835b，clean，local-only**。
未声称该提交远端可达。

最终完整冻结 **901 文件 / 363 DLL / 134 NuGet packages**；部署集合双向一致，除 manifest Identity.Name 外逐字节一致，manifest 其余 XML 相同。
运行结束前采集131个包目录内已加载模块并复核各 SHA256，不冒充所有阶段模块跟踪。
运行 UI DLL SHA256：`4bffc1e01b88d61251ebe52b3ac9e4bea57d25b03b42216b24b711b20cf9a3f9`。
见 [verification](refresh-undo-controlled/verification.json)、[文件闭包](refresh-undo-controlled/frozen-files.json.gz)、[源对照](refresh-undo-controlled/source-hashes.json.gz)、[fixture输入](refresh-undo-controlled/fixture-input-files.json.gz)、[包闭包](refresh-undo-controlled/package-closure.json)、[加载模块](refresh-undo-controlled/run4/loaded-modules.json)。

## 最终行为证据

[原始结果](refresh-undo-controlled/run4/controlled-results.json)、[逐步请求/响应/完整Items/引用/游标/通知](refresh-undo-controlled/run4/controlled.jsonl.gz)。gzip 内为逐行 JSON，可直接解压回放审查；每次请求必须由测试明确放行。

| Requirement | Evidence |
| --- | --- |
| 非空列表中段刷新、批次重复 ID、旧内容重复、精确 undo | `PopularExactSequenceReferencesCursorUndo`、`PartitionExactSequenceReferencesCursorUndo`：真实 ScrollView 到600后刷新；33项合并序列、未变化对象引用、undo的31个原引用及41游标全部断言。 |
| 空/失败刷新保留当前内容、上次快照及游标，手动可恢复 | `PopularEmptyRefreshPreservesSnapshotAndManualRecovery`、`PopularFailedRefreshPreservesSnapshotAndManualRecovery`、`PartitionEmptyRefreshPreservesSnapshotAndManualRecovery`、`PartitionFailedRefreshPreservesSnapshotAndManualRecovery`：终止批次仅1次请求，集合通知和列表更新不增加；撤销回原列表，再次刷新成功。 |
| 切换栏目/子分区后旧返回不能写入 | `PopularOldReturnAfterSectionSwitch`、`PartitionOldReturnAfterSectionSwitch`：先放行新栏目700，再放旧刷新999；实际集合保持700、新栏目无undo/refreshing残留。 |
| 刷新期间旧自动分页迟到 | `PopularOldAutomaticPageArrivesDuringRefresh`：真正滚动到底引发cursor41的旧分页；新刷新请求已挂起时才放旧999/999。Items仍31项、cursor41、refreshing=true；新批次随后落地到cursor73，未混入999。 |
| 空/失败自动分页停止、手动加载恢复 | `EmptyAutomaticPageStopsAndManualLoadRecovers`、`FailedAutomaticPageStopsAndManualLoadRecovers`：短页100引发真实自动续页；空/失败后请求总数2、更新1，随后约2秒观察窗口保持不变；手动恢复为[100,200]、cursor42、请求3、更新2。 |
| 真实输入与可见撤销，撤销后自动续页契约 | `VisibleRefreshUndoThenAutomaticAppend`：winapp真实 click生产刷新及撤销按钮；完整集合原引用恢复后滚到底，自动请求仍从41开始，追加[100,800]，允许重复100，cursor42。 |

固定初始 S 为 `[100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,105,115,116,117,118,119,120,121,122,123,124,125,126,127,128,129]`。
刷新输入 `[101,900,900,901]`，期望 `[101,900,901] + S去掉101`，其中两次旧105原样保留。流行页随后受控空批次终止多页抓取；分区页原契约只抓一页。
两页实际31→33→31；cursor41→73→41。列表更新均1→2→3，集合通知32→36→40；初始32含初始化Clear，非本次刷新Reset。
流行页请求1→3→3，分区页1→2→2。每个对象的稳定引用编号在原始状态记录中，不是用ID相等冒充引用相等。

**撤销同步恢复快照，但不禁止之后自动追加。** Undo发出VideoListUpdated，后续满足触底条件会继续取恢复后的游标。
可见用例同步恢复31项、cursor41，然后真实滚动触发第4个请求，最终33项、cursor42；这不是撤销失败。
分区页在分页忙碌时拒绝刷新，流行页会切换代次替换旧请求；此差异本次不擅改产品语义，不宣称分区也能同时刷新。

初始化先等待真实命令完成，再挂载填充后的控件；观察到的挂载续页用屏障完成并排空队列。20秒是失败上限，不是成功条件。
唯独“不持续空转”使用约2秒有界负向观察（四个生产500ms延迟周期），明确不是无限时证明或猜测就绪的等待。
记录服务请求/列表更新及集合通知；没有把所有PropertyChanged通知混称为已统计。

## 真实截图与退出

[动作](refresh-undo-controlled/run4/actions.jsonl)保留所有selector、时间和工具退出码。
最终三张及历史四张截图逐张打开查看：最终[刷新前](refresh-undo-controlled/run4/before-refresh.png)显示推荐100/101；[刷新后](refresh-undo-controlled/run4/after-refresh.png)显示900/100与撤销按钮；[撤销后](refresh-undo-controlled/run4/after-undo.png)恢复100/101且撤销入口消失。
封面刻意为空，显示受控标题；不声称验证公网封面解码、物理DPI或所有项同时可见。全Items证据来自VM集合记录。

run4 PID **38552**，精确路径/creation FILETIME与retained Process.Handle绑定；真实CloseOwned关闭宿主，非强杀，exit0。
查询从启动前Application bookmark覆盖运行期及退出后；仅精确路径/包相关1000/1001进入安全门禁。
最后查询开始距退出 **30.5187332秒**；`lastQueryUtc=2026-10-30T19:08:35.320165Z`，独立 `collectionEndUtc=2026-10-30T19:08:35.3573904Z`。
查询全部成功，安全事件集合空、managed unhandled0；[退出原始记录](refresh-undo-controlled/run4/exit-observation.json)和[门禁](refresh-undo-controlled/run4/exit-verdict.json)为PASS_BOUNDED。
未来精确异常可撤销结论；未用进程消失代替退出码。没有修改旧报告run2/3“最后查询约29.5秒”的事实。

## 保留的失败与修正

- run1：runner把混合斜杠路径直接与注册路径比较，门禁拒绝；关闭按钮尚未就绪，未知退出码，owned强制清理，**BLOCKED**。随后只修路径规范化，并补清其唯一注册；不计行为通过。
- run2：场景切到热门后写入内存保存栏目，下个新VM恢复热门；请求日志和截图均证实。8项早期通过不能掩盖后续红例，整体失败。修复每场景重置离线保存栏目，无生产修改。
- run3：11通过/1失败。分区初次挂载自动请求仍在途，测试过早刷新而被现有loading guard拒绝；保留超时、请求与快照。修正初始化/布局屏障。可见交互已成功，但不把该轮重标为12通过。
- run2/3各自retained code0与严格末次查询通过，只说明其退出有界通过，不覆盖行为失败。原始失败、旧测试源码及冻结哈希保留于相应目录/ignored历史产物。

三次宿主Release构建均exit0，最终 **1567 warnings / 0 errors**；保留[build3](refresh-undo-controlled/build3.log.gz)，未新增NoWarn或安装分析器。
按WinUI/code-testing对新增测试边界、异步处理、全序列与引用断言、真实输入、清理范围做自查；没有宣称全应用静态审查零警告。

## 重放、清理与保留边界

从repo根运行（使用现存fixture、SDK/NuGet和native资产；不是下载或全局安装指南）：

```powershell
python docs/performance-acceptance/refresh-undo-controlled/prepare.py C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-e497cf43/artifacts/timer-fix/final/fixture
$env:DOTNET_ROOT='C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-d48c749d/artifacts/dependency-upgrade/sdk11'
& "$env:DOTNET_ROOT/dotnet.exe" build artifacts/refresh-undo/fixture/BiliCopilot.UI.csproj -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$((Get-Location).Path)/src/" "-p:BuildOutDir=$((Get-Location).Path)/artifacts/refresh-undo/references/" -nr:false -m:4
python docs/performance-acceptance/refresh-undo-controlled/freeze.py artifacts/refresh-undo
pwsh -NoProfile -File docs/performance-acceptance/refresh-undo-controlled/run.ps1 -Run fresh5
```

prepare/freeze拒绝覆盖现存目录；本次已构建冻结的宿主可直接以新Run名重放最后一步。
`verify.py run4`复核本轮含失败历史的证据集，不是把任意新运行冒称run4。
没有新增dotnet单测，本轮测试入口是上述真实WinUI runner；没有把历史487写为fresh。

最终owned进程/注册均清理；run1另有cleanup-followup。最后 `winapp ui yield` exit0、**released:false**（grace已过），不写成released:true。
Caa6a7be仍注册到既有 `t3-4d615e72/artifacts/full-app-net11/layout-v2`；未操作PID9608、Dev或认证数据。
父本地tip仍1b425c0d，交付前同步检查；仅提交本报告与小型安全证据，完整运行产物保留ignored。
**#49既有FAIL、AOT BLOCKED、真实公网未观察到空页、#56预算/任意大重排限制和总体perf HOLD全部保留。**
