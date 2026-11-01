# 固定候选 61745e24 独立审查

审查对象：61745e244dc84f6ed5f96c211eb330e4bb703ce2（实现 5a21071a，基线 3add3023）；整合目标 f44c22aad17780cfb01fff56503fd70e69ca62c4。未合并、未修改正式实现、未启动子代理、未使用 UI 槽。使用 code-review、winui-code-review、run-tests、code-testing、verification-before-completion 技能；按用户要求在本代理完成审查。

## 结论与阻断

1. **P1：布局重入取消后仍可执行旧 ScrollTo。** 候选 `RecommendationScrollAnchor.cs:57-68` 在 `UpdateLayout()` 前检查 version/IsLoaded，之后未重查。布局事件同步触发 VM 替换、卸载、Cancel 或新 Capture 后，旧 Low 回调继续执行 ResetToTop/UndoRefresh，可能滚动新页。应在布局返回后再次检查 version 和 IsLoaded。审查副本边界测试 `CancelDuringLayoutMustNotResetNewPage` 针对此路径。
2. **P1：Cancel 后迟到 Changed 可以重新安排旧 ResetToTop。** `Cancel()` 增加版本并清空 offset，但保留 `_change`；`Restore()` 重新读取取消后的版本，ResetToTop 又不受 `_hasOffset` 限制，因此 Capture→Cancel→Restore 仍滚到 0。需要独立 active-capture/pair token 或取消后 Restore 无效；不能只靠 Low 回调的版本比较。`CancelBetweenChangingAndChangedMustNotResetNewPage` 针对此路径。父已有 VM generation guard 能阻止部分迟到 Changed，但不能替代 anchor 的布局后检查；事件多订阅者重入仍需明确配对契约。
3. **P1 / 证据：报告的“8 条 InvalidOperationException 与本轮修复无关”没有可核验来源支持。** 当前本区 artifacts 原先不存在，Git 仅保留报告文字，未保留 real1/real2/real3 原始 JSONL/异常记录/截图/退出记录。20/20×3、exit0、WERclear、8 条类型均只能列为报告声称，不能列为本次历史核验通过。需提供每条异常实际类型、来源、时间、stack（缺 stack 必须保留归因未知），严格退出仍 BLOCKED。无需扩大无关矩阵，也不占用当前归 Win2D fa1a5e1f 的 UI 槽。

## 可采用的行为与测试界限

绝对 offset 方案正确针对报告中的真实根因：卡确实找到，`TransformToVisual=-10140`，原相对计算被夹成 0；不是空 lookup。现有 headless 回收窗口用例模拟卡不在树内，只能证明绝对 offset 不依赖 realized 卡，不能独立复现该 stale transform 原始记录。

Capture 位于请求返回、Apply 之前，因此刷新请求期间用户继续滚动时，保存的是 apply 前视口；Undo 不覆盖保存值。连续成功刷新会覆盖为最新 apply 前 offset；首次顶部 offset=0 合理。空批次/失败在 Changing 前返回，不消耗 anchor offset，仍可供旧快照撤销使用。queued reset 被后续 Undo Capture 的 version 作废；卸载/VM 更换必须同步 Cancel。分区 Detach 调用 Cancel；普通卸载也调用 Cancel。Popular SelectedSection 改变会 Cancel。静态检查不等于真实 WinUI 生命周期验收。

## 对 f44c22aa 的精确整合边界

只采用枚举文件、anchor 绝对偏移思路（先修上述取消边界）、两处事件类型及四处 Invoke 模式替换，以及相应测试/适配器。不要覆盖父 VM 整文件，也不要 merge 旧历史。

- 必须保留父 `_undoSnapshotVersion`，设置关闭立即失效、关闭再开不复活在途快照的检查和测试。
- 两个 VM 的刷新与 Undo 中，保留 `Changing→generation检查→RecommendListDiff.Apply(..., isCurrent)→generation检查→Changed→generation检查`，以及引用恢复循环和后续 offset/快照/通知写入的检查。
- 保留 `RecommendListDiff.Apply` 两个 overload 的 optional `Func<bool>? isCurrent` 与每次 RemoveAt/Insert 前中止。新枚举只替换 bool 模式：false 刷新映射到 incremental ? ResetToTop : PreserveVisibleCard；true Undo 映射 UndoRefresh。
- 保留父非增量不生成 Undo 快照语义；保留普通无增长分页不通知，成功刷新即便变短也发 VideoListUpdated。不能把旧候选的“仅增长通知”带回。
- 中止代次不能发布旧 Changed；页面 Cancel 也必须让已经排队的回调和迟到 Restore 无效。需测试 Changing/集合事件/Changed 重入各路径，不强求已取消代次继续发 Changed 来机械配对。

## 证据来源与运行限制

仅从保留 Git 导出候选到 ignored `artifacts/review-61745e24/candidate`。候选所需 kernel `62b376ec9dba705b4280f5d4ac61dd4589551034` 在本地子模块 Git 不可解析（not a tree object），未访问远端。为探针使用当前 kernel `f02c33f639addd5d421700c43fa66382beafe323` 的 Git 导出，**属于依赖适配的审查副本，不是原受测候选原字节**。原实现源码未改；追加边界测试也仅放审查副本。原始日志/TRX/探针源码均留 ignored artifacts。

AOT 严格保留：报告称 ILC 完成 native code generation；MSIX pack 因缺 `Assets/libmpv/x64/libmpv-2.dll` 失败，无可运行 AOT 包、无运行资格。当前无原始 publish 日志/二进制可复核该历史声明，未进行安装、发布或触碰用户 AotDev。JIT/native fixture 的“native”不构成 NativeAOT 运行证明。

运行结果待附。

## 独立复放结果

首次复放编译被当前 kernel 缺少 `BiliHttpClient(HttpMessageHandler)` 阻断（CS1729，`key.log`）。审查副本额外从本地保留提交 `2ca79bd` 导出且仅覆盖 kernel 两文件：`src/BiliKernel.Core/Http/BiliHttpClient.cs` 和 `src/Services/Services.Media/Core/DanmakuClient.cs`，恢复测试注入传输及取消支持。其余 kernel 为 f02c33f。未改候选 UI/VM/anchor 字节。zip 导出保留历史时间戳，覆盖后刷新这两文件 mtime 使 MSBuild 真正重编译；初次未刷新时的失败保留于 key-adapted 日志的前次尝试说明，不算通过证据。

在此明确适配的副本执行：

```powershell
dotnet test artifacts/review-61745e24/candidate/tests/BiliCopilot.UI.Tests -c Release --no-restore --filter FullyQualifiedName~RecommendationRefreshScrollTests --logger 'trx;LogFileName=key-adapted.trx' --results-directory artifacts/review-61745e24/results
dotnet test artifacts/review-61745e24/candidate/tests/BiliCopilot.UI.Tests -c Release --no-build --no-restore --logger 'trx;LogFileName=full-adapted.trx' --results-directory artifacts/review-61745e24/results
```

- 关键集：Passed: 11, Failed: 0, Skipped: 0。
- 全套：Passed: 591, Failed: 0, Skipped: 0。
- 随后仅在审查副本追加 `ReviewAnchorBoundaryTests.cs`，运行 `--filter FullyQualifiedName~ReviewAnchorBoundaryTests`：Passed: 1, Failed: 2, Skipped: 0。两个 P1 探针均预期 400、实际 0；`UnloadedRootMustIgnoreQueuedReset` 通过。测试失败是发现缺陷的证据，不是全绿交付。
- 对连续刷新、首次顶部、Undo 前 queued reset、失败不发 Changing、三态映射的既有覆盖已独立执行。刷新等待期间滚动、成功后再空批次/失败保留旧 offset、真实 VM 更换生命周期没有新增独立动态用例，仅上述静态路径审查；不将其包装成 native 覆盖。
- 未做 f44c22aa 组合运行；父 guard 的边界来自固定 diff 审查，合入后需用父测试验证。现有 11/591 结果不能套给组合树。

Standards：未发现本差异的新增硬性仓库规范违规；本轮未运行 WinUI analyzer 或真实 app 编译。Spec：两项取消/重入缺陷与一项证据归因阻断。可采用三态模式及绝对 offset 设计，当前候选不能原样批准合入。
