# 推荐刷新回顶部与撤销视口恢复（最小修正）

2026-11-01。用户问题 4：推荐视频列表滚动后点击刷新不回顶部，撤销也不恢复原视口。

## 基线

- 本区 `t3/485a5725` 受测字节基于父 `3add3023fe71284ef8c513f070b09db1c5daa205`，kernel `62b376ec9dba705b4280f5d4ac61dd4589551034`（初始为空/474 条 staged 删除，按固定 bundle 仅恢复本区）。
- 父在集成非增量修复后继续前进到 `f44c22aa`（新增 UndoEpoch 防关闭再开复活、刷新/Undo 的 `Changing→Diff→Changed` 及后续状态写入代次检查、`RecommendListDiff.Apply(Func<bool> isCurrent)` 逐项防重入），并明确要求本轮不立即重基、不得覆盖已受测字节。因此本报告证据只对应上述基线，不把这些新 VM 改动当作已 native 验收；父最终 merge 时需保留这些语义与测试，且新组合不能复用本报告的 native 结论。
- 快照语义未改：增量/非增量何时保留撤销仍由调用方负责，本区只把布尔事件负载换成三态枚举。

## AotDev 只读核验

- 注册包 `Mang.BiliCopilot.AotDev_2.2611.1.4_x64__z90r5zjzw296g`，`InstallLocation` 在 WindowsApps。
- `BiliCopilot.UI.exe` ProductVersion `1.0.0+a0ee201f6c9cdc0affffe5f76ddb4d03241b2353`，SHA256 `C82D08FB84C71CC88BD0A407E3F6F0DDE419D77EE864382D569408F9F9290E2C`。
- 仅只读查询版本/哈希；用户 AotDev 进程 PID 2368 全程未操作、未关闭、未升级，未读取登录数据或凭据。

## 根因（运行期证据，非源码推断）

真实 WinUI fixture（生产 UI 源码 + 仅外部 discovery 服务受控）记录到 `UndoRefresh` Low 回调瞬间：

```text
change=UndoRefresh found=True realized=9 offset=0 target=600 currentTop=-10140 itemTop=166
```

原“查找当前已实现卡片再算相对偏移”路径确实找到了卡片，但重置到顶部后该卡片的 `TransformToVisual` 是回收后的失效值（`currentTop=-10140`），
`Math.Max(0, 0 + (-10140) - 166)` 被夹到 **0**，所以撤销仍停在顶部。不是空查找，也不是需要放大缓存才能规避的问题。
刷新 Capture 时的真实视口为 600；撤销恢复该绝对偏移即可，与卡片是否仍 realized 无关。

## 修正

- `Toolkits/Pure/RecommendationListChange.cs`：新增 `PreserveVisibleCard` / `ResetToTop` / `UndoRefresh` 三态。两个推荐页共用，供本任务与父的非增量任务共同引用。
- `PopularPageViewModel`、`VideoPartitionDetailViewModel`：成功增量刷新发 `ResetToTop`，非增量保持 `PreserveVisibleCard`，撤销发 `UndoRefresh`；失败/空批次仍不发事件。
- `RecommendationScrollAnchor`：Capture 时保存刷新前绝对 `VerticalOffset`；`ResetToTop` 回 0，`UndoRefresh` 恢复该偏移；非增量仍走原“保持可见卡片”逻辑；`Cancel` 同时丢弃未执行的回调与偏移。未新增反射、绑定、缓存或依赖。

## 证据

### Headless（red → green）

- 红：把撤销临时退回“只找 realized 卡片”的实现，`UndoRestoresPreRefreshOffsetWhenAnchorCardWasRecycled` 与分区对应用例均为 **600 → 0 FAIL**（TRX 在 ignored `artifacts/refresh-undo/headless/red/`）。
- 绿：恢复绝对偏移后 11/11 通过（`.../headless/green/`、`.../headless/final/`）。
- 全套 `dotnet test tests/BiliCopilot.UI.Tests -c Release`：**591 passed / 0 failed / 0 skipped**（`.../headless/full/full.trx`）。

### 真实 WinUI（owned 身份，本轮独享桌面槽）

- fixture 由生产 UI 源码复制构建（仅 `App.xaml.cs` 启动入口替换为受控服务，`Fixture.cs`/`ControlledRefresh.cs` 在 ignored artifacts），真实 `ScrollView`、`AppRepeater` 绑定、真实刷新/撤销按钮。real1 使用未插桩的生产源码；real2/real3 只在 ignored fixture 的 anchor 副本加入只读回调追踪，行为代码不变。
- `PopularExactSequenceReferencesCursorUndo`、`PopularLaterRefreshUndo`、`PopularConsecutiveRefreshUndo`、`PopularInitialRefreshAtTop`、`PopularUndoBeforeQueuedReset` 及分区同名用例全部 PASS。
- 真实按钮 `VisibleRefreshUndoThenAutomaticAppend`：UIA 点击生产 `RefreshRecommendButton`/`UndoRefreshButton`，600 → 刷新 0 → 撤销 600，原引用/顺序/游标恢复，PASS。
- 三轮运行 real1/real2/real3 均 **20/20 PASS**；截图、`controlled.jsonl`、锚点诊断在 ignored `artifacts/refresh-undo/real*/`。
- 退出：owned PID 正常 Close，exit code 0，未强杀；精确 PID/path 的 WER 查询完整覆盖到退出后 31.3 秒，无 fault/WER 记录。`cleanup.json` 确认 owned 进程与注册均不存在；`yield` 返回 `released:false`（grace 已过期），桌面槽随后交回。
- 严格退出门禁仍为 **BLOCKED / managed-unhandled**：8 条都来自分区场景，类型 `InvalidOperationException`，与本轮浏览/撤销修复无关，也不能归因为干净运行；父审查此前已见相同模式，本区未修、未掩盖。

## AOT

- 未改动 AOT/绑定/反射面；本轮真实运行是 JIT fixture。
- 对修正后生产树执行离线 `dotnet publish`（Release/win-x64、`PublishAppxPackage=true`）：ILC 实际执行 `Generating native code` 并完成 native publish。`artifacts/refresh-undo/aot-publish/BiliCopilot.UI.exe` 为 x64 PE32+，CLR directory rva/size 均为 0，SHA256 `A3AF993630EA0829B896805F8434424E0FCA43C5653F5C19F174FA09D84D3346`，111,524,864 bytes；publish 输出不含 coreclr/hostfxr/hostpolicy/clrjit 或受管 `BiliCopilot.UI.dll`。
- ILC 仍报告既有依赖面的 `MpvKernel`/`FluentIcons` `SymbolIcon.UseSegoeMetrics` will-always-throw 警告；本轮未新增或掩盖这些警告，也不归因于本补丁。
- 随后的 MSIX 打包被项目目标拒绝并 exit 1：本工作区缺少未跟踪的 `Assets/libmpv/x64/libmpv-2.dll`（既有构建前置条件）。因此本轮没有可安装/可运行的 AOT 包，未安装、未替换用户 AotDev，也没有把 JIT 结果当作 AOT 运行。原始日志 `artifacts/refresh-undo/aot-publish.log`。

## 组合注意

- 本区 anchor 依赖 `RecommendationListChanging` 与 `RecommendationListChanged` 成对出现。若新父在 `Changing` 后因 `isCurrent` 中止 `RecommendListDiff.Apply` 却仍发 `Changed`，anchor 会按已捕获的偏移执行一次 ScrollTo；组合时应保持两者的同一代次边界，或取消时不发 `Changed`。这不改变快照语义。
- `RecommendationListChange` 是共享方法改动：父的非增量任务继续决定何时保留快照/撤销，本区只负责把模式映射到视口行为。

## 限制

- 受控外部服务、单桌面/单窗口；不覆盖真实网络、登录态、多屏 DPI、动画打断全部组合。
- 未扩大矩阵：只覆盖用户点名的 600→0→600、连续/后续刷新、初始顶部、queued reset 与真实按钮。
- 未 push/PR/issue，未清理其它工作区，未操作用户既有身份或登录数据。
