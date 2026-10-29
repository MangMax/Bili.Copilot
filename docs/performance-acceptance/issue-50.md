# Issue #50 — 首页推荐首屏不等待分区目录

日期：2026-10-30。实现及无界面行为验证已完成；**真实 WinUI 验收尚未完成**。

## 范围与来源

- 工单：<https://github.com/MangMax/Bili.Copilot/issues/50>；规格：<https://github.com/MangMax/Bili.Copilot/issues/45>。使用 `gh issue view --json number,title,body,comments` 读取完整正文及评论，两票当时均无评论。
- 已通过 `gh issue edit 50 --repo MangMax/Bili.Copilot --add-assignee '@me'` 认领。未关闭工单、创建 PR、推送或合入集成分支。
- scope-gate：M，单一首页加载行为切片。范围为首页初始化、栏目请求归属、侧栏选择就绪协调及本票验证。
- 分支：`t3/implement-issue-50`；工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-b063d912`。
- 初始 HEAD 为 `90f357733c30d7cc405a7f6f649922f3ab8ed29c`，修改前通过 `git merge-base --is-ancestor` 验证。提交前执行 `git merge --no-edit feat/performance-45`，返回 `Already up to date.`；当时集成分支仍为该提交。
- 阅读 `AGENTS.md`、`docs/agents/` 中的 issue-tracker、triage-labels、domain 指引、`docs/performance-plan-draft/issues/05.md` 和 `docs/performance-review-2026-10-29.md` F04。
- 检查开放 PR #27/#43/#44 的状态和改动清单，并读取 #27 对 PopularPageViewModel 的 diff。它们尚未包含在本票基线中，本票没有吸收其代码。
- `externals/bili-kernel` 保持 `6501985d0f30c55c28b3f78c6e045c2536e1d389`，没有修改子模块代码或 gitlink。

## 实现

1. 创建内建栏目后启动目录任务。推荐、热门和全站排行立即选择并排队加载视频；只有数字分区 ID 恢复等待目录。
2. 目录失败或目标不存在时回退推荐并保存该选择；目录等待期间用户主动选择优先于历史恢复。
3. 移除初始化中的固定 200ms 等待。侧栏在加载且具有布局尺寸后，将选择同步排到低优先级调度队列；布局事件仅安排工作。卸载后不执行选择，重新加载时恢复 ItemsSource。
4. 栏目切换、刷新递增请求代次。迟到结果不更新列表、游标或新请求的加载状态；尚未执行的旧切换回调被忽略。缓存只保存当前实际显示栏目拥有的列表。
5. 保留现有列表和缓存结构；未实现或复制 #27 的增量刷新、撤销及提示 UI。

## 技能及验证边界

应用：`scope-gate`、`tdd`（含 tests/mocking 指南）、`code-testing`、`run-tests`、`winui-dev-workflow`、`winui-code-review`、`winui-ui-testing`、`verification-before-completion`。

测试是单个 PopularPageViewModel 类的 focused 测试。使用票面已确认的边界：链接生产 ViewModel 三个 partial 文件，通过实际生成命令驱动，注入真实 `IVideoDiscoveryService` 接口的受控实现。服务接口和数据模型来自现有 bili-kernel 项目引用，没有复制服务接口或引入测试包。

Windows 调度、设置、日志、定位资源、展示项和基类使用本票专用 headless adapters。调度适配器捕获异步 continuation，显式 Drain，因而不依赖固定 sleep。测试观察请求、公开列表、保存选择、游标和加载状态；不使用源码字符串断言。适配器不验证 WinUI ABI、真实设置存储、卡片构造/图片加载或布局。

没有修改 XAML，故 `winui-design` 的 XAML 修改前置条件不适用。未提供 code-testing-extensions 参考目录，采用现有 xUnit 2.9.3 与邻近测试约定。未引入共享 CI 或跨票测试框架。

## 命令与结果

SDK：`dotnet --version` → `10.0.403`；测试项目 `net9.0`，VSTest + xUnit。WinApp CLI：`0.7.1`。

```powershell
# 修改前基线
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-50-baseline.trx'
# 首个红绿循环
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~PopularPageLoadingTests --logger 'trx;LogFileName=issue-50-red-startup.trx'
# 切换、刷新各自先失败后修复
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~SwitchingSectionsRejectsLateContentAndLoadsCurrentSection --logger 'trx;LogFileName=issue-50-red-switch.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~RefreshReplacesAnInFlightRequestAndResetsItsCursor --logger 'trx;LogFileName=issue-50-red-refresh.trx'
# 最终本票及完整套件
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --filter FullyQualifiedName~PopularPageLoadingTests --logger 'trx;LogFileName=issue-50-focused.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=issue-50-final.trx'
```

- 基线：Passed 282, Failed 0, Skipped 0，退出 0。
- 启动 RED：2 例失败，视频集合为空；最小初始化修复后 2 例通过。
- 切换 RED：1 例失败，旧请求占用加载标志导致新栏目无内容；归属修复后 3 例通过。
- 刷新 RED：1 例失败，刷新时加载标志阻止新请求；刷新代次修复后 4 例通过。
- 最终 focused：Passed 17, Failed 0, Skipped 0，退出 0。
- 最终全套：Passed 299, Failed 0, Skipped 0，退出 0。解析 `issue-50-final.trx` 确认 total/executed/passed 均为 299，failed/error/notExecuted 均为 0。
- TRX 位于本工作区 `tests/BiliCopilot.UI.Tests/TestResults/`（构建产物，不提交）。最初测试适配器编译错误已修复；上述 RED 指真正执行的行为断言失败，不指编译错误。

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-b063d912/src/ -p:GenerateAppxPackageOnBuild=false
git diff --check
```

首次构建（含 restore）已输出 BiliCopilot.UI.dll，但打包依赖校验报缺少 `Assets/libmpv/x64/libmpv-2.dll`，不能计为构建通过。随后从主 checkout `C:/WorkFile/Project/Bili.Copilot` 的同名 Assets 路径复制已有 libmpv-2.dll（121108480 bytes）和 ffmpeg.exe（107860992 bytes）至本工作区；未下载、安装软件、修改系统配置或提交二进制文件。BBDown.exe 已在 checkout 中。

再次执行上面的构建命令：**退出 0，0 警告，0 错误，56.51 秒**。日志位于 `C:/Users/hp/AppData/Local/Temp/issue-50-build-final.log`；首次失败日志为同目录 `issue-50-build.log`。这是 Debug/x64 构建验证，不是运行验证或 Release Native AOT 发布验证。`git diff --check` 通过。

## 行为覆盖

| Requirement | Evidence |
|---|---|
| 推荐/热门不等待目录且已发布列表 | `BuiltInContentAppearsWhilePartitionsArePending`（2 例） |
| 分区恢复等待真实目录并请求正确分区 | `SavedPartitionWaitsForDirectoryThenLoadsThatPartition` |
| 分区消失或目录失败明确回退 | `UnavailableSavedPartitionFallsBackToRecommend`（2 例） |
| 内建栏目不受目录失败影响 | `DirectoryFailureDoesNotReplaceBuiltInContent`（2 例） |
| 用户选择优先于迟到的历史恢复 | `UserSelectionWinsOverPendingPartitionRestoration` |
| 排队切换和缓存归属 | `QueuedSwitchesKeepCacheOwnedByDisplayedSection` |
| 重复初始化不复制内容 | `ReinitializationDoesNotDuplicateSectionsOrContent` |
| 恢复未结束时刷新不抛异常 | `RefreshBeforePartitionRestorationIsHarmless` |
| 四类旧请求不串栏、不清除新加载状态 | `OldCompletionCannotPublishOrClearNewLoadingState`（4 例） |
| 切换可立即启动当前栏目 | `SwitchingSectionsRejectsLateContentAndLoadsCurrentSection` |
| 刷新替代在途请求并使用新游标 | `RefreshReplacesAnInFlightRequestAndResetsItsCursor` |
| 控件就绪、实际布局、真实卡片展示时间 | 未验收，见下文 |
| #27 增量刷新/撤销联合行为 | 基线没有该实现，未验收；未复制或重写该功能 |

## 请求时序与性能证据

受控服务的可复现顺序是：`GetVideoPartitionsAsync` 开始且保持未完成 → 推荐/热门请求 → 对应视频进入公开 Videos 集合 → 测试才允许目录完成。失败目录测试证明目录报错不替换已发布的视频。分区恢复则在目录完成前保持无选择、无视频，完成后只请求目标分区。

这证明了依赖关系改变，不等于实机首批卡片计时。**窗口可见时间、卡片首次绘制/可交互时间、慢目录实网请求时间戳均未测量**，没有性能百分比结论。删除的 200ms 是代码中的固定等待，不是测得的整体收益。

## WinUI review 与 UI 验收状态

- 按 winui-code-review 自查了 MVVM/绑定、事件订阅和卸载、布局重入、调度队列、请求归属、缓存、设置回退与现有 XAML。未发现本 diff 中尚未修复的静态阻断项；发现的空选择刷新及旧请求串栏问题已由行为测试覆盖。
- 侧栏继续使用现有 ItemsView/模板/绑定，不改主题、样式、可访问性或视觉结构。布局回调只排队，Select 在布局外执行；挂载/卸载对称处理 LayoutUpdated。
- `winapp find-api members ItemsView --filter Select --json --project-dir src/Desktop/BiliCopilot.UI` 确认项目表面包含 `Select(Int32)`。工具提示目标 SDK 22621 未安装而参考 SDK 28000，以及已安装 Runtime 2.6 不等于引用 App SDK 1.8.39；未据此声称运行环境已验证。
- WinUIAnalyzer 未被现有项目引用，因此没有其检查结果；没有为此票新增包或改变版本。
- `winapp target snapshot sandbox --json` 只读返回 running=false、`sandbox_target_stale`。这不证明 Sandbox 不可用，本票没有启动/修复 guest。
- 当前已安装 Mang.BiliCopilot 与 Mang.BiliCopilot.Dev 等包，但没有用旧安装包代替本次构建进行验收。
- **父任务明确为 #48 预留共享桌面与 Sandbox，本票未启动应用、未注入 UI 输入、未部署替换现有包。**没有 UIA PASS、截图、录屏或视觉验收通过声明。

待父任务释放桌面后，在本构建对应实例上验收：保存推荐/热门及具体分区后重启；受控延迟/失败目录；切换栏目、刷新和返回页面；窗口尺寸变化下验证选择与卡片无布局异常；采集目录开始/完成、内容请求和首卡可见的时间戳及截图。Release/AOT 需另行发布实测，不能用本次 Debug 构建代替。

## 集成风险与剩余事项

1. #27 同时修改 PopularPageViewModel 三个 partial；未来合入必须保留其 `_isRecommendRefreshing`、`ClearRefreshTip`、差异更新、快照及撤销语义，并将本票请求归属失效与刷新/撤销协调。不能用本票的旧基线普通刷新覆盖其推荐刷新分支。本票未声称已完成联合运行验证。
2. 生产 ViewModel 链接测试在未来新增 WinUI 依赖或 #27 的服务签名变化后，测试适配器可能需要同步；不能通过删掉场景或替换生产逻辑来维持通过。
3. 老请求目前被拒绝发布，但不主动取消网络；这是本票的结果归属保护，不是 #48 的取消链实现。
4. 仍未完成实际 WinUI 布局/保存恢复/慢目录首卡计时、#27 联合验收和 Release/AOT 采样。交给父任务安排，**不应据本报告勾选全部工单验收或关闭工单**。
