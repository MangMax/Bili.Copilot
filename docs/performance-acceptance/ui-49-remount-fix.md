# #49 重挂修复验收；暂停缺陷继续阻断

2026-10-30。按用户中途明确收窄：本轮交付控件重挂修复、真实双引擎复验及只读引擎后续方案，依赖版本不变。**重挂生命周期结构与重挂后公开实时弹幕输出通过；#49 整体验收未通过，暂停 seek / 暂停切换保留阻断。**

## 来源与范围

- 工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-31f21833`，分支 `t3/fix-performance-49-dual-engine`。开始 clean；HEAD 与本地 feat/performance-45 均准确为 `b9698993df3437edaeb306753455c4c4b001a347`。
- 读 AGENTS.md、docs/agents 三份规则及 gh #45/#49；指定八个技能已读取和应用，宿主复用前也读 winui-design。未启动子代理。
- 核对 `82dfe1c1796e3f5696de750a30cfdd1c8a339b31` 仅新增 docs/performance-acceptance 下189个文件，cherry-pick 为 `eb9a6d0`。原 ui-49.md、harness-49、evidence-49-ui 与原提交逐路径 diff 为空，全部77张原图及 red 原样保留。
- 本轮 red 生产源码仍为 b9698993；green 实测源码提交 **`e4c1cfb7eafac07eb5b57df9f15a60e99c54928c`**（测试时为同一内容的未提交改动，随后只提交该文件；[源码哈希](evidence-49-remount/source-hashes.json)）。后续本报告/证据不改变源码。
- 执行 `git merge --no-edit feat/performance-45` 为 Already up to date，最新本地集成仍 b9698993。kernel gitlink/嵌套 HEAD 仍 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，嵌套工作区 clean。

## 最小修复与根因

只改 `VideoDanmakuPanel.xaml.cs`，17行新增/8行删除。实际 Richasy.WinUIKernel.Share preview3 首次卸载会注销自己的 Loaded/Unloaded；复核已有 [IL](evidence-50-53/base-lifecycle-il.txt) 与本轮实际部署 DLL [哈希](evidence-49-remount/package-hashes.json)，并用真实包/控件重现。不是以替代基类证明自身。

构造函数持有自身 Loaded/Unloaded 订阅，使用 `_isActive` 幂等进入/退出。每次卸载先失效排队重绘、解除全部 VM 事件，再释放两引擎并清空容器；每次挂载重新订阅当前 VM 并重绘。VM 替换和重绘入口/执行同时受活动状态约束。null VM 首次挂载也能进入活动状态，随后赋 VM 可走原更新路径（静态检查，未另做 null VM UI 用例）。

没有改 XAML、调度器算法、包引用、kernel、设置契约或引擎暂停行为；没有固定延时或生产反射。单一 dispatcher/UI 线程假设保留。

## 真实红绿与证据分层

独立身份 `BiliCopilot.Acceptance4931F2_y1mf40g4r9srg`，本工作区 ignored `artifacts/acceptance49/harness` 和 `layout`。原 harness 仅换身份/标题，链接真实控件、VM、parser、session、实际 NuGet 基类及两引擎；XAML 副本和生产哈希相同。组合根仍为内存设置、空字体服务、NullLogger；不使用账号、HTTP、真实视频或认证。反射仅沿用宿主的只读状态观察。

复现脚本与准备入口见 [harness-49-remount](harness-49-remount/README.md)。SDK 10.0.403，winapp 0.7.1；已知 Sandbox feature-disabled，用户授权本地独占。没有安装工具/环境。

- 新 red：owned PID57036，实际 b9698993；[red-02/results.json](evidence-49-remount/red-02/results.json)，18检查中8通过/10失败；结构8通过/8失败，两个暂停 seek 均失败。每引擎三次重挂均失败；替换 VM 后新建 renderer 的最终卸载也失败。最初采集早于窗口就绪，DirectX selector 找不到，保留 [ui-red.txt](evidence-49-remount/ui-red.txt)，不能算 red 功能断言；窗口就绪后才完成 red-02。
- 修复后：owned PID20256；[green/results.json](evidence-49-remount/green/results.json)，24检查中21通过/3失败，**结构20通过/0失败**。退出码1，因暂停阻断仍在，目录名 green 只表示修复后的生命周期轮次。
- green 增加双向暂停切换和重挂 hide/show 四项结构检查，并在初始操作前等真实 UIA 控件。red/green 不是相同总数的全量性能对比；六个重挂和两次最终卸载有直接失败→通过证据。

| Requirement | Evidence |
|---|---|
| 双引擎各三次重挂恢复唯一活动 renderer | `DirectX-Remount-1/2/3`、`Win2D-Remount-1/2/3`：Loaded、单层、互斥实例，位置20，5条 DETACHED 内容；red 全失败，green 全通过 |
| 排队重绘与卸载、脱离期间事件 | `DirectX-Unload-1/2/3`、`Win2D-Unload-1/2/3`：未挂载、零层、两字段null；DetachedUpdates 后仍无实例 |
| 重挂后可见性绑定 | `DirectX-RemountHideBinding`、`DirectX-RemountShowBinding`、`Win2D-RemountHideBinding`、`Win2D-RemountShowBinding` 全通过 |
| 旧 VM 事件不污染新 VM | `DirectX-ReplaceVMRejectsOldEvents`、`Win2D-ReplaceVMRejectsOldEvents`：NEWVM列表、位置2、未暂停，OLD-POISON/99/Pause 未写入 |
| 再次卸载释放 renderer | `DirectX-FinalUnload`、`Win2D-FinalUnload`：red失败→green通过 |
| 重挂后真实绘制能力 | 两张 `green/*-remount-visible.png`：第三次重挂后公开 Resume/AddDanmaku，均看见 PUBLIC VM REALTIME 从右侧进入。仅支持此操作的可见输出 |
| 暂停 seek（阻断） | `DirectX-PausedSeek`、`Win2D-PausedSeek`：VM paused=true，真实 render _isPaused=false；green均失败 |
| 暂停切换（阻断） | `DirectX-PausedSwitch` 本轮通过，`Win2D-PausedSwitch` 本轮失败；样本时序与历史方向不同，证明不能凭单次通过宣称竞态解决 |

测试名称中的引擎是该段起始选择；Switch 后 UnloadQueued 将 VM 选回起始引擎，所以 green 的三次重挂和 visible 图确实分别覆盖 DirectX/Win2D。状态采样不是原子跨线程事务；用公开操作后低优先级 fence 和 Sample 观察，不将 fence 视为引擎 seek 或 GPU 首帧完成。没有对 native disposed callback 做穷举证明。

**所有10张最终 PNG 已逐张 view**，[逐图记录](evidence-49-remount/visual-review.json)。其中8张空白仅保留结构/暂停失败证据，不能判视觉通过；2张实时输入确有文字。宿主按钮/状态文字可见，无意外布局遮挡；右侧滚动文字尚未完整进入画布属此截图时刻的运动裁剪，不证明完整动画时序。原77张历史图不重新解释或洗绿。

## Release、构建与 runtime gate

使用现有 xUnit2/VSTest，未发现 run-tests 仓库 overlay。执行：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter FullyQualifiedName~DanmakuRedrawSessionTests --logger 'trx;LogFileName=targeted.trx' --results-directory artifacts/acceptance49/tests -v:minimal
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=full.trx' --results-directory artifacts/acceptance49/tests -v:minimal
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-31f21833/src/ -p:GenerateAppxPackageOnBuild=false -v:minimal
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-31f21833/src/ -v:normal
```

均 exit0。定向 **Passed: 4, Failed: 0, Skipped: 0**；全套 **Passed: 403, Failed: 0, Skipped: 0**。已解析 [targeted.trx](evidence-49-remount/targeted.trx)、[full.trx](evidence-49-remount/full.trx)，详见 [trx-summary.json](evidence-49-remount/trx-summary.json)。这些纯逻辑测试不包含实际 panel，重挂回归由上述真实 WinUI 用例证明，没有复制控件实现或新增空泛 headless 测试。

Debug x64 app build：0错误、1个既有 protobuf unused import 警告，2:23.09；[日志](evidence-49-remount/app-build.txt)。宿主 red/green build 各0警告0错误。runtime gate 0警告0错误：[日志](evidence-49-remount/runtime-gate.txt)。从用户授权本地 Assets 来源只复制对应 x64 libmpv/ffmpeg/BBDown 至本工作区；未提交运行时资产，BBDown已有跟踪内容无差异。gate只证明资产存在，不证明解码加载。

Release app Native AOT 因已知完整 MSVC 工具链缺失仍 **blocked/未验**；没有重复尝试或安装。单元测试 Release 不等于发布态 WinUI 验收。

## WinUI 自审与交付边界

build/UI 后按 winui-code-review 自审：本次生命周期改动无新 must-fix；事件自持、幂等、解绑对称，卸载先失效旧队列再释放，回调不捕获已释放实例；无新 UI 阻塞、异步 void、依赖、XAML/主题/可访问性/本地化改动。真实 hide/show 也确认重挂后绑定可用。**Error（既有，未修）：UpdateDanmakuTime 的异步隐式 seek 与尾部 Pause 竞态**，继续阻断整票。专门 WinUIAnalyzer 未安装/未运行；遵从不改依赖约束，不声称该 analyzer 通过。

原规范的真实视频连续时钟、登录业务、网络、完整播放器导航、视频解码、真实高密度长时运行、性能增益、暂停静止首帧和 Release/AOT 不在本次证明范围。暂停引擎后续最小范围、兼容性及测试/交付方案见 [danmaku-49-followup.md](danmaku-49-followup.md)：内部最新播放意图和旧操作保护是必要项；新增 await/cancellation API 是否需要由具体调用方需求决定，不能替代内部正确性。

## 清理

owned PID57036/20256 已通过各自 Close 退出，复查0残留；核对 InstallLocation 精确属于本工作区 layout 后只移除 `BiliCopilot.Acceptance4931F2`，复查0包。执行同 workflow `winapp ui yield --on local --json` 成功，返回 released=false（未释放到活动占用）；操作间 grace 已经过期，本轮无后续桌面操作。**桌面独占槽已释放**。[cleanup.json](evidence-49-remount/cleanup.json)。

未触碰 PID9608、Dev/生产包、凭据或父 checkout；没有 push、PR、issue 写操作、引擎仓库写操作或 NuGet 发布。宿主/build/layout 留在本 worktree ignored artifacts 供复核，注册身份已移除。
