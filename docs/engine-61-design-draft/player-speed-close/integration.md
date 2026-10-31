# 父工作区 speed 子集集成记录

2026-10-31，在父共享工作区 `t3-1d19cc80` 完成一次有界集成，无子代理。
起点 HEAD `bd02775bd9a48d7e62c169a6367eec58707dad8e` 与 kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b` 均核实 clean。
来源候选 `bb9c45dd37053327f7cd1c1c176757968a3d8296`，生产
`161a47c3873927865a48fb35daf419b6d1b68e8e`，独立审查
`d5c519dd4924997ae41ab0358c728b63763e8f7e`。

## 精确边界

先检查并应用 `git diff 7ac24a44 161a47c3 -- src`，五个文件：
OverlayTransportControls.xaml.cs、PlayerViewModel.Commands.cs、PlayerViewModel.Methods.cs、
PlayerViewModel.cs、PlayerViewModel.Speed.cs。另仅将候选 Playback.cs 的
`CapturePlaybackValidity()` 原方法体加入 Speed partial。审计使用临时独立 index 从父基线
重新应用同一 patch，加同一 helper 后逐文件比较，允许 checkout 换行差异。
没有依赖未合入的 seek/pointer 类型；实际正式依赖构建是补充的编译证据。

保留父正式 preview6 依赖、gitlink、Playback.cs、PlayerInteractivePanel、弹幕 seek、
SponsorBlock/章节路由和 tests 树。没有合并整链或引入已知 Legacy seek P1。
仍使用父关闭标志和共享任务，先等待初始化、排空已接纳 speed，再按父顺序 Player.Dispose
保存历史并释放，必要时 client fallback。未引入线程安全调度框架。

## Fresh 验证

执行前读取 AGENTS.md、docs/agents、run-tests、code-testing、winui-code-review、
verification-before-completion；本次仅调整既有 probe 的 helper 来源，八项断言不变。
runner 为仓库固定 .NET 11 RC3 SDK，xUnit 2/VSTest。进程局部 DOTNET_ROOT/PATH 指向
`t3-d48c749d/artifacts/dependency-upgrade/sdk11`，未改变机器 SDK 配置。

```powershell
python docs/engine-61-design-draft/player-speed-close/probe.py parent-subset-fresh
python docs/engine-61-design-draft/player-speed-close/validate-integration.py Tests
python docs/engine-61-design-draft/player-speed-close/validate-integration.py DebugClean
python docs/engine-61-design-draft/player-speed-close/validate-integration.py Release
python docs/engine-61-design-draft/player-speed-close/validate-integration.py RuntimeGate
python docs/engine-61-design-draft/player-speed-close/audit-integration.py
```

`validate-integration.py` 复用既有 integration-net11-player-timer-fix 执行器，改为本次独立
输出根和 TRX 名，追加工作树源码 hash/diff；Tests → DebugClean → Release → RuntimeGate
串行，各阶段立即冻结。Debug/Release 均为 x64、Rebuild、PublishAot=false、正式包配置，
禁用签名与打包；并非 AOT 发布或 UI 验收。原始命令、SDK、日志、时间和冻结文件 hash
见 integration-summary.json 与 integration-evidence/*.gz。

| Requirement | Evidence |
|---|---|
| 已接纳调速排空后释放 | `CloseWaitsForAcceptedSpeedBeforeDisposal` |
| 关闭期间/关闭后拒绝新调速 | `ClosingAndClosedPlayersRejectSpeed` |
| 重复关闭与多个调速共同排空 | `RepeatedCloseJoinsAndDrainsEverySpeed` |
| 异常原样传播且释放不挂起 | `SpeedFaultPropagatesAndStillReleasesClose` |
| 直接及重复 Dispose | `DirectDisposalAlsoRejectsAndDrainsSpeed` |
| 换媒体/换 client 后旧完成无效 | `OldCompletionCannotReportSuccessForNewMediaOrClient` 两分区 |
| 新实例独立于旧关闭 | `ReopenedPlayerIsIndependentOfOldClose` |
| 父现有全套不回归 | fresh `speed-subset-suite.trx`：539/0/0 |
| 精确子集及历史原件 | `audit-integration.py` 临时 index 比较、原始归档 96 文件 hash |

fresh probe **Passed: 8, Failed: 0, Skipped: 0**；fresh 父全套
**Passed: 539, Failed: 0, Skipped: 0**。已解析 TRX counters 和每条结果，不能将候选历史
563 项当作本次父结果。probe 使用替身协作者，不编译真实 Overlay 或完整初始化。

最终 DebugClean 和 Release JIT 全量构建均 exit=0，各 1 个既有 protobuf unused-import
warning、0 errors。ValidateRuntimeDependencies exit=0，仅证明 x64 libmpv/ffmpeg/BBDown
文件存在，不是媒体运行验收。证据审计 exit=0：Tests 347、DebugClean 1890、Release 1887、
RuntimeGate 26 个冻结文件逐一验证长度/hash及双向集合；历史归档 96 文件保持原样。
生产 diff 精确五文件，核验未修改正式依赖、gitlink、父测试及未纳入的 seek/pointer。
fresh 证据归档为 25 份原字节 gzip，原始输出保留于
`artifacts/speed-subset-integration/bd02775bd9a48d7e62c169a6367eec58707dad8e-fresh/`。

工具失败保留说明：首次复制 helper 时 Python 默认 GBK 解码 git 输出失败，已应用的 patch
和已复制文档未丢失；改用 UTF-8 后仅继续未完成步骤。首次验证执行器读取中文 diff 同样
解码失败，在任何测试启动前退出；保留原空目录，改 UTF-8 后在 `-fresh` 根执行。
这些是工具失败，不是产品红例。原候选 red TRX、失败记录和独立 review 小文件保持原样。

## WinUI 自审与剩余边界

构建后检查接线、UI dispatcher 约定、finally 排空、await 后媒体有效性、Overlay 队列在
await 前取走/清空及换 VM/卸载撤销。八个入口、唯一底层 SetSpeedAsync 已核实；未发现本次
新增阻断。正常编译分析器按仓库配置运行，未另装或运行专用 WinUIAnalyzer；无 XAML 改动。

既存 P2 待办：Overlay 同实例 Loaded → Unloaded → Loaded 的 Tick 退订后没有重订阅。
后续应补对称、幂等订阅并验证同实例重挂；本次不顺带扩修。媒体有效性谓词也不是控件
attachment token，CheckSpeedButtonState 既有 300ms 延迟仍不在本次完整视图有效性保证内。

本次没有 UI/native 重跑、安装注册、登录身份或桌面操作。历史 native 仅证明当时实验依赖
和受测范围：普通加速在 Playing；快捷按钮、slider、快速20次关闭在 End；重开后沿用设置
不算 Playing 下 slider 操作证据。底层操作永久不完成仍会使关闭等待。
历史 runtime5 AV 根因未证明；#49 pause FAIL、G1 FAIL、AOT BLOCKED、认证组合未验、
总体性能 HOLD 继续保留。不 push、PR、issue。交还父代理，不继续扩张集成范围。
