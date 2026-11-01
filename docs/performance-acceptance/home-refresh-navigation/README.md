# 首页刷新与首次导航回归（2026-11-01）

基线：`8c797bfdd3854f00171b18cd8f121a210298ff1e`。用户报告包为 AotDev `2.2611.1.1`。本次未更新或启动用户包，未使用登录凭据。

## 定位和修复

首次进入分区页时，请求已经发出。`MainFrame.NavigateTo` → 页面 Loaded/Initialize → 恢复父分区 → 明细 Initialize 发出请求；真实 ItemsView 随后回显选中项。`SelectPartition` 把 `Partition` 与包装它的 `PartitionViewModel` 比较，误判为切换，调用 `ClearRefreshSnapshot` 推进代次。原请求完成后被代次隔离丢弃，列表仍为空。修复为比较两边的领域 `Partition`。重复 Loaded 和同分区回显不再作废首个请求或撤销快照。

刷新开关持久化没有丢失：实际设置控件 TwoWay → SettingsPageViewModel → SettingsToolkit LocalSettings → 新 toolkit 实例重读及命令读取均正常。Popular 的 `Requests.cs` 读取也正常。分区推荐正在分页时，按钮仍可点击，但 `RefreshRecommendVideosAsync` 因 `IsVideoLoading` 直接返回，旧页随后追加，两个开关值都表现为追加。修复使显式刷新接管分页，保留刷新防重入并推进现有代次，旧页不得追加或清除新刷新状态。

生产改动仅两个方法。没有改 XAML、绑定、列表合并算法、缓存、依赖版本或 gitlink。

## 行为证据

| 要求 | 证据 |
| --- | --- |
| 首次进入、侧栏回显、重复初始化 | `SidebarSelectionEchoPreservesFirstPendingResponseAndRepeatedInitialization`：基线红（空列表），修复绿 |
| false 替换、true 增量、下次读取新设置 | `PersistedRefreshModeWinsOverPendingLoadMoreAndNextSettingChange(false/true)`：基线两个红（没有刷新请求），修复绿 |
| 旧页不覆盖游标或新刷新状态 | `OldPaginationCannotClearPendingRefreshOrOverwriteItsCursor` |
| 同分区回显不破坏 undo | `SamePartitionEchoPreservesUndoSnapshot` |
| 完整测试 | 新鲜基线 572/572；相关 33/33；修复后全套 577/577，无跳过 |
| 真实 WinUI 红 | `pendingred2`：首次请求后 count=0；离开返回仍空；分页中刷新 false/true 均没有生效，旧项追加为 61，5 项失败 |
| 真实 WinUI 绿 | `green`：24/24，包含慢目录期间 Popular 独立加载、实际导航/返回、卸载再 Loaded 不重复请求、设置持久化、分页中按钮刷新、可见容器/卡片 VM |
| 原生 AOT 绿 | `aotgreen`：27/27，包括上述链以及分区/推荐可见卡片实际标题；原生 EXE 的 CLR RVA/size 均为 0 |
| 构建 | 完整生产源码 Release 构建成功，0 警告/0 错误；隔离真实 host NativeAOT publish 成功（播放器既有警告见下） |

原始日志、TRX、结果 JSON、截图、退出/WER 和清理收据在同目录 `evidence.zip`；SHA-256 清单由 `validate.py` 校验。完整本机运行材料留在 `artifacts/home-regressions`。

## 适配器边界

headless 测试链接生产 VM，但设置与基础 VM 使用现有测试适配器，不能证明 WinUI 绑定或持久化。真实 host 使用生产 MainFrame、页面、控件、VM、SettingsToolkit 和编译绑定；仅替换启动入口与 `IVideoDiscoveryService`（类型明确，无动态代理），提供离线目录/内容和可控延迟。设置写入独立包 LocalSettings，不读用户包数据。通过可见 ItemsRepeater 当前索引及视口交集核对容器、Content、卡片 VM；最终 AOT 还检查实际标题 Text。

这覆盖离线真实导航链，未覆盖完整认证启动、线上接口与播放器。正式冻结依赖不变；AOT 使用正式 MpvKernel `0.0.2-preview2`，不是此前用户打包专用的 `aot-local.1` 别名。ILC 对播放器 SymbolIcon.UseSegoeMetrics 的缺失 API 有警告，本次不进入播放器，不能把首页验收外推为播放器验收。

同步假服务曾掩盖首次加载时序，后改用异步返回复现。早期 AOT 广泛扫描可视树把回收池容器误当当前卡片，其两项失败已排除，未据此修改绑定。`pendingred` 冻结了未完成构建的旧驱动，不作为红证据；使用完成构建后的 `pendingred2`。所有这些尝试原始材料保留，不以它们宣称通过。

## 可重放步骤

需本机 .NET 11、WinUI 工具链、Python、winapp；运行现有 `run-tests` 约定的测试入口。`prepare.py` 只导出选定提交和其精确 kernel gitlink，要求新的输出目录。`--kernel-repo` 指向已有包含该对象的只读仓库；`--runtime-assets` 指向已准备的 Assets（libmpv/ffmpeg/bbdown），不下载、不更新包。

```powershell
python docs/performance-acceptance/home-refresh-navigation/prepare.py --ref HEAD --kernel-repo <kernel-repo> --runtime-assets <Assets> --output artifacts/home-replay
$base = (Resolve-Path artifacts/home-replay).Path
dotnet test "$base/source/tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj" -c Release -p:Platform=x64 -p:NuGetAudit=false --logger "trx;LogFileName=all.trx"
dotnet build "$base/fixture/BiliCopilot.UI.csproj" -c Release -r win-x64 -p:Platform=x64 -p:NuGetAudit=false -p:PublishAot=false -p:PublishTrimmed=false -p:AppxPackageSigningEnabled=false "-p:SolutionDir=$base/source/src/"
python docs/performance-acceptance/home-refresh-navigation/freeze.py "$base/fixture/bin/x64/Release/net11.0-windows10.0.22621.0/win-x64" "$base/frozen"
./docs/performance-acceptance/home-refresh-navigation/run.ps1 -Run jit -InputDirectory "$base/frozen" -ArtifactRoot artifacts/home-replay
dotnet publish "$base/fixture/BiliCopilot.UI.csproj" -c Release -r win-x64 -p:Platform=x64 -p:NuGetAudit=false -p:AppxPackageSigningEnabled=false "-p:SolutionDir=$base/source/src/" "-p:PublishDir=$base/native/"
Copy-Item "$base/fixture/bin/x64/Release/net11.0-windows10.0.22621.0/win-x64/AppxManifest.xml" "$base/native/AppxManifest.xml"
./docs/performance-acceptance/home-refresh-navigation/run.ps1 -Run aot -InputDirectory "$base/native" -ArtifactRoot artifacts/home-replay
```

红证据重放：`--ref 8c797bfdd3854f00171b18cd8f121a210298ff1e`，在导出 tests 中复制当前新增测试及 csproj 的三个 Compile 链接；运行同样测试/host。不要在构建完成前 freeze，不要同时运行构建和验收。

runner 使用全新 HomeRegression 身份及 GUID，只清理自己精确路径的包；保留进程句柄核对退出码，退出后至少 31 秒查询 WER，以 PID/路径门禁裁决，最后 yield。重跑需新 Run 名称，避免覆盖证据。

保护核对：任务开始记录的官方和 AotDev 注册属性未变；用户 AotDev PID 51748、启动时间和路径未变。Caa6a7be 未包含在最初快照中，不能声称有完整前后差异证明；本任务所有注册/注销均限定 HomeRegression 身份和自有布局，没有操作 Caa6a7be 或 Dev。kernel 初态已保存到临时目录，最终 porcelain v2 完全一致，保留原有脏状态；索引 gitlink 与正式依赖未变。

## 自审

WinUI 自审检查了入口、Loaded 时序、VM 赋值、同选中项回显、刷新按钮命令及异步代次。未增加事件订阅/Dispatcher/反射绑定；继续保留现有离页隔离、撤销、目录与推荐解耦、空页停止和缓存路径。独立 review 留给父线程，本实现线程没有启动子代理。
