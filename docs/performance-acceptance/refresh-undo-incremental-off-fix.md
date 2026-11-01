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
