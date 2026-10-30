# PR27 / PR43 独立集成复核

2026-10-30，仅本地合入。父起点 `bd8f51638a77185a562158a8d42a2d92cce4e68e`；指定来源 `d3ab56c7984524339f93dc324b64422c726f206d`，以 `--no-ff` 合入为 **`2b12951179fb757d6b2559a1ea2a97fe2c52003d`**。merge 树与指定来源完全相同；本报告及 status 更新仅为后续文档提交。最新生产改动 `65e531238764c847b8361d82a31a76f8621dd1e6`，后续来源提交只有 docs/fixture/evidence。

只读 `gh pr view` 核验 PR27 `f19f030934741cff805445cc2834aead1b39b93e`、PR43 `2f1ae8f98453017a52ad5db0032f47aee42f9e1b` 未变，均为来源和 merge 的祖先。未 push、修改 PR/issue、启动子代理或升级依赖。原 [交接及重放入口](integration-pr27-pr43.md) 保留历史来源边界；旧 t3-5a9e2d03 工作树未修改。

## Standards 轴

未发现阻断本次集成的已证实缺陷。核对 AGENTS、docs/agents、src/.editorconfig 与 WinUI 审查规则；不把编译器已覆盖的格式项重复列为发现。

- 两个新头部各自持有已订阅 VM；Attach 先 Detach，Loaded/Unloaded 和 VM replacement 都走相同所有权边界。分区 Selector 也先解绑，覆盖真实 NuGet 基类首次卸载后不再管理后续挂载的情况。
- 封面 decoder 与 GetPixelData 均不再传 cancellation token 给 AsTask；两个 await 完成前 stream/random 仍在 using 范围内，之后检查 token。没有同步等待 UI；仅当前 request 且 IsLoaded 才发布。CanvasBitmap/drawing session 确定释放，共享 CanvasDevice 不擅自 Dispose；卸载清 Source 并解绑，底层共享下载不会被某个消费者取消。ImageSource/native backing 的最终释放仍由平台管理，不能称已证明所有 native 内存即时回收。
- Pinner 的 x:Load 可空 Repeater 在 Unloaded 有空值保护。托盘 icon 同步创建并自持释放，避免旧异步 IconSource 在 Dispose 后写回。
- 维护性 Note：两页刷新/撤销仍有重复流程；任意大规模中间重排的 LCS 仍可能二次方。无新增重构，不把 prepend/undo 的局部改善外推为通用线性算法。

专用 WinUIAnalyzer 未运行；现有编译器检查不替代它。没有以技能建议为由新增包或普遍迁移 ObservableProperty 写法。

## Spec 轴

在用户指定的最新 PR 方向下未发现新增合入 blocker；这是有限集成批准，不是 PR43 全验收或 #45 完成。

- PR27 的最终设计为刷新按钮内进度与左侧撤销，取代历史 toast/新增计数规格。默认关闭、旧内容保留、快照对象身份和 offset 恢复保留；分页不清 undo，空/失败恢复旧快照。section/generation 守卫拒绝旧成功/失败及 finally 对新状态的写入；AllowConcurrentExecutions 配合当前 IsRecommendRefreshing 的 CanExecute，避免旧栏目请求锁死新栏目刷新。
- pageSize 和多页批次属于用户指定最新 PR 的演进；不是按原 #22「不改 kernel」单独评价这一新头。热门/排行没有新增撤销功能。分区切换先保存离开栏目的缓存，再改变 IsRecommend。
- PR43 默认 opt-in、同窗口恢复、设置保存→播放器关闭→托盘释放→应用退出的顺序保留。CloseAsync/DisposeAsync 在任何 await 前取消 #48 弹幕会话；关闭任务幂等，历史保存先于 client dispose。#54 PlayerWindow 与 #55 下载策略文件相对父起点未改。
- 新封面仅用于推荐视频卡，按尺寸/DPI计算并监听 resize/root/device recovery；#52 Markdown 文件/loader/provider 未改，不借封面测试宣布 #52 完成。物理 DPI 切换、硬件 device removal、native fallback/Account 退出异常注入仍缺证据。

## kernel 对象与可重建性

gitlink/父 nested HEAD 均为 `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`，nested clean。`git bundle verify` 成功，再从已提交 kernel.bundle fetch 到父 nested 新分支 `integration/copilot-pr27-pr43`，然后 detached checkout 精确 SHA；没有 reset 或覆盖原有分支。

实际 parents 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d` 和 `f02c33f639addd5d421700c43fa66382beafe323`，两个 ancestor 检查通过。相对前者只增加 pageSize 相关四文件；相对后者保留 HTTP caller cancellation 和 danmaku no-retry 两文件。bundle 的 prerequisite 是 `6501985d0f30c55c28b3f78c6e045c2536e1d389`，不是无前置条件的全仓备份。

**合并对象仍 local-only。** 本地 remote-tracking refs 无一包含它，未宣称远端可达。后续发布父分支前，需要发布专用 kernel 分支并核验可取回性；本轮未外部发布。

## 历史原生证据复核

- provenance.json 五个 artifacts hash 全部匹配，包括原 final-pinner TRX 和 Debug 日志。原 TRX 为 487 executed/passed、0 failed/notExecuted。现存原测试 DLL ProductVersion 为 `1.0.0+3838c9daa73d71918ae9da37fbc70c14b8484687`；3838c9d→65e5312 唯一生产变化为未链接进测试工程的 Pinner，故不宣称原 DLL 自带 65e5312 版本戳。
- 全部 tracked 生产 cs/xaml 与保留 Player fixture 做规范化换行比较，仅 App.xaml.cs、GlobalDependencies.cs、FontCatalog.cs 三项差异，与交接一致。Pinner 双方 SHA256 均为 `A75C6468C9A006AC09CCEB548FACDA7F6DAEA0E824CAC8D33C52F8887C58BD60`。
- 最新 Player build DLL 与 `bili-latest2743-player-final-layout` DLL hash 相同：`B9981419818381C36A1248841FB14D5D6F1A4A15997E453705929AEB29E45649`。fixture DLL 仅有 1.0.0 版本；layout/source-provenance.txt 为完整 65e5312，结合源码比较与构建记录建立来源。旧 `bili-integration2743-player-layout` 属 5814fe58，不混用。保留 fixture 1540 warnings/0 errors，不伪称生产构建警告数。
- Player 15/15 结果与 committed player-green.json hash 相同；Main 4/4、20 次同 HWND 循环、带一名播放器显式退出及 zero unhandled 的小证据与原始记录相符。Controls 7/7 属 3838c9d；后续 Pinner 在其源码闭包之外，不等于 7 项全部在新 SHA 重跑。
- 183 个样本重新计数为 visible/hidden/restored 各 61，只有一个 HWND，scale=1.5，全部 visibility 与 phase 匹配。来源标记为完整 65e5312。受控 SettingsPage、禁通知、无播放下载；先执行 15-case Player，再关闭播放器进行采样，保留该 warm-up 历史。早期 3838c9d fresh-process 样本不可直接作 A/B。

本轮没有启动 UI、占用桌面、访问认证包或更改注册/layout/login。上述为已有证据复核，不是 fresh native runtime pass；原生硬件/认证/视觉缺口继续存在。

## 本地 fresh gates

合并后 SDK 10.0.403，xUnit/VSTest：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=review.trx' --results-directory artifacts/integration-pr27-pr43-review
```

**Passed: 487, Failed: 0, Skipped: 0**，TRX 已解析。新 DLL ProductVersion 为 `1.0.0+2b12951179fb757d6b2559a1ea2a97fe2c52003d`。完整日志和 TRX 位于父工作树 `artifacts/integration-pr27-pr43-review/`。

Debug x64/JIT build 退出 0，**0 warnings / 0 errors**（1:42.95）；随后显式 ValidateRuntimeDependencies 退出 0。命令如下，完整输出保留在同目录 debug-build.log/runtime-gate.log；没有启动应用，runtime gate 仅核对必需文件存在。

```powershell
$root = 'C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=$root/src/"
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 "-p:SolutionDir=$root/src/"
```

本轮日志、TRX、DLL 与运行库 SHA256 固化于 [review-gates.json](integration-pr27-pr43/evidence/latest/review-gates.json)。增量构建未输出 protobuf 警告；这不抹去历史构建中的 unused-import 警告。未另跑 Release app build 或 Native AOT publish。

## 后续依赖 / .NET 11 基线

本次 merge 树是升级前的固定代码基线。按原交接 prepare/replay scripts 重建隔离 fixture，保留当前 packages hash、SDK/runtime、x64 JIT、150% DPI、同一页面和 Player→Main 顺序，再比较等价 workload；pure-list 100/1000/5000 prepend/undo 的十次样本单列，不解释为 UI frame latency。

受控 baseline 不代表认证首页或 A/B 收益。受保护 Caa6a7be 的实际认证包仍为 c3b32c5，最新组合未经认证完整应用验证；本轮未读取它。AOT 工具链 BLOCKED、#49 pause FAIL、#56 cache budget/任意中间重排成本仍在。升级 H.NotifyIcon、Richasy.WinUIKernel、Win2D/AppSDK 时须复核订阅生命周期、WIC stream lifetime 和退出/模式协同，不能仅靠 unit tests 绿批准升级。
