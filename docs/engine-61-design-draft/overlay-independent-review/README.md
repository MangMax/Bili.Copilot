# Overlay 重挂与 speed 子集：独立审查/WinUI 有界验收

2026-10-31；独立区 `t3-575992d4`，受审 HEAD `2ee14d2f`。`git merge-base --is-ancestor` 分别确认包含 `e6f6970b`、`656d3310`（均 exit 0）。固定生产审查范围 `git diff bd02775b...2ee14d2f -- src`，其中 Overlay 小修另看 `git diff e6f6970b...2ee14d2f -- src`。无子代理、生产修改、push、PR 或 issue 写入；未写父/来源区。

**结论：有限通过，未发现本次 speed/Overlay 局部修复新增的实质阻断，可并入本证据。** 实机执行父实际生产源，补齐 Playing 下调速/关闭与同实例重挂；实验 Core 的成功不等于正式 preview6 native 全面通过，更不证明历史 AV 已修复。

## Standards

0 个新增问题。局部接线与 UI dispatcher 约定一致，无新框架、锁或同步等待。八处调速入口归一为 `ChangeSpeedAsync`，唯一底层 `SetSpeedAsync` 在该方法；finally 排空、异常传播及释放前等待符合需求。读取 AGENTS、domain 指引及 code-review、winui-code-review、winui-ui-testing、run-tests；测试补充按 code-testing focused 路径执行，工具失败按 systematic-debugging 查首个原因，交付按 verification-before-completion 核验。无子代理要求优先于技能默认委派。

正常仓库构建分析器已执行；**专用 WinUIAnalyzer 未运行**。Release x64 为 JIT、PublishAot=false，不是 AOT 验收。

## Spec / 实际重挂语义

0 个新增阻断。首次基类生命周期回调与自身 Loaded/Unloaded 双回调由 `_isOverlayActive` 合并；后续自身事件继续接线。活动换 VM 先解除旧 ProgressChanged 再订新 VM；卸载时解除当前 VM；未挂载时换 VM 不提前订阅，重挂使用当前 VM。重复 Loaded 不重复加 Tick/ProgressChanged。Unload 清两队列、validity、停两 timer、退订并清字幕 ItemsSource。

`OnViewModelChanged` 清速度队列但未清 progress 队列：当前生产树没有 `_progressChangeList.Add` / `_progressChangeTimer.Start` 发起点，不把死路径升级为本修复新问题，也不借此合入全局 seek 候选。已受理速度继续由 VM drain 管理，控件卸载仅撤销尚未发出的意图，不取消 native 在飞操作。既有 CheckSpeedButtonState 延迟仍不是 attachment token 保证，本次没有扩张该承诺。

生成的 `OverlayTransportControls.g.cs` 对 ViewModel DP 注册变化回调，更新 Subtitles/Player 等子路径；XAML 的 ItemsSource 是 OneWay x:Bind，重挂赋值恢复已创建集合，没有删除一个运行时 Binding 对象。实机验证同一 ItemsRepeater 卸载置空、挂载恢复并重新显示条目。活动 VM 变化的订阅覆盖来自提取方法适配器与生成绑定静态审查，未冒称做了真实新 VM 替换矩阵。

## Requirement coverage

| Requirement | Evidence |
|---|---|
| 接纳调速完成后才释放 | `CloseWaitsForAcceptedSpeedBeforeDisposal` |
| 关闭拒绝后续调速 | `ClosingAndClosedPlayersRejectSpeed` |
| 重复关闭/多在飞速度 | `RepeatedCloseJoinsAndDrainsEverySpeed` |
| 异常传播并释放等待 | `SpeedFaultPropagatesAndStillReleasesClose` |
| 直接/重复 Dispose | `DirectDisposalAlsoRejectsAndDrainsSpeed` |
| 换媒体/换 client 旧完成失效 | `OldCompletionCannotReportSuccessForNewMediaOrClient` 两分区 |
| 重开独立实例 | `ReopenedPlayerIsIndependentOfOldClose` |
| 同实例订阅幂等/换 VM/卸载清理 | 扩展 probe：每轮 `single subscription`、`active VM change`、`unload clears all`、`inactive VM change`、`remount current subtitles`，3 轮 15 检查，failures=0 |
| 父必要回归 | 原父测试树一次运行，539/0/0；不是候选 563 |
| 实际播放中调速/重挂 | 下列 PID40572 日志、UIA 输出、截图、快照 |

复放命令（SDK 为仓库固定 .NET 11 RC3，进程局部 DOTNET_ROOT/PATH 指向已有 `t3-d48c749d/artifacts/dependency-upgrade/sdk11`）：

```powershell
python docs/engine-61-design-draft/overlay-remount-fix/probe.py independent
python docs/engine-61-design-draft/player-speed-close/probe.py overlay-review
python docs/engine-61-design-draft/overlay-independent-review/probe.py expanded
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=regression.trx" --results-directory artifacts/overlay-review/regression
```

原 probe failures=0；speed **Passed: 8, Failed: 0, Skipped: 0**；父回归 **Passed: 539, Failed: 0, Skipped: 0**。TRX counters 与实际发现结果数均独立解析。方法提取适配器不执行真实 WinUI/Timer/native；不拿它替代实机。

正式构建（保留正式 preview6）：

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release --no-restore -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false "-p:SolutionDir=<checkout>/src/"
```

最终 exit 0，0 warnings/0 errors，`formal-build-final.log`。前一次编译成功但 native 文件 gate 缺 libmpv；补本地已验证 assets 后成功。初始 kernel 缺树时的 restore 失败单独保留，不能算产品测试失败。

## 完整离线 host 与实机

`prepare.py` 源自指定 dc85 脚本并在本区适配。使用父 HEAD 全部源码、本区 kernel、既有224离线服务/实验 Core+Share feed、本地 native assets。仅实验 App 启动/DI/身份/Root/验收控制器和实验包版本不同；**正式依赖文件没改**。本次生产没有 SeekRequested，移除了来源验收观察器对该未合父事件的引用；没有带入 seek/pointer 候选。最终 host build exit 0、0 warnings/0 errors，`host-build-final.log`。

27 个 PlayerVM/Core 控件源文件与 checkout 文本一致（部分 git archive 换行归一）；所有受测 C# 的消费者字节 hash 在本次 portable PDB 中。PE CodeView GUID 与 PDB 一致，build DLL、部署 layout DLL 和实际 loaded module 的磁盘文件 hash 相同。`source-equality.json`、`consumer-pdb.json`、`runtime/modules.json`。元数据读取仅文件，不读取进程内存。

本区用 ffmpeg 生成 600 秒 testsrc2 本地媒体。Sandbox InstallState=2，使用授权 local；唯一 owned identity `Bili.OverlayReview5759`，PID **40572**，路径只在本区 `artifacts/overlay-review/full/layout`。未操作 Dev、受保护 Caa6a7be、其他窗口或凭据。

| 操作 | 新证据与边界 |
|---|---|
| 快捷按钮与 slider | UIA TogglePattern 激活真实 1.5x，RangeValue 设置真实 slider=1.75；当时 native PlaybackRate 实际变更且 Playing |
| 同一 Overlay 三轮重挂 | instance **16575658**；实际父 Grid RemoveAt/Insert 同一对象，每次卸载750ms、重挂750ms；reloaded-1/2/3 后 slider 分别实效1.25/1.75/2.25，均 Playing |
| 重挂无重复 | 方法提取精确计数每 timer/ProgressChanged 恰1；实机每轮速度稳定，无观察到重复效果。**没有 native 调用次数埋点，不宣称实机精确计数了 SetSpeedAsync** |
| 卸载清理 | 4次真实卸载快照 active=false、两timer=false、两queue=0、subtitleSourceNull=true，重挂 sourceMatches=true |
| 字幕可见重开 | 合成 SubtitleItemViewModel 条目 “Review subtitle 5759”，真实原控件菜单重挂后可见；`subtitle-visible-tree.json` / `subtitle-visible.png`。证明集合/菜单，不是 native 字幕渲染测试 |
| 待发 debounce 取消 | 额外第4轮尝试 UIA set2.75 后立即卸载，但到 before-unload 已 rate2.75、queue0；**没有命中 pending 窗口**。清队列取消只由提取适配器/源码支持，不伪报实机覆盖 |
| Playing 快速调速/关闭 | **11:58:05.071 UTC** state=Playing、pending=20；05.219 disposed=true/completed=true；控制器检查不是Playing就报错，未放宽 |
| 重开 | **11:58:06.901 UTC** 完整 initialized；11:58:10 新 Overlay instance39455214、Playing；新实例不是前三轮同实例证据 |
| 退出/WER | retained handle，**exit0**、forced=false；退出后 **31.0206098秒**精确PID/path Application1000/1001查询可读、matches=[] |
| 清理 | owned包0、PID不存在、yield released=true |

已逐张查看本次截图。speed菜单/slider和字幕条目可见；测试视频中的灰色移动图形属于 testsrc2，不当作控件遮挡。没有做全面主题/无障碍验收。

工具失败均留存：启动早期 UIA 空树（应用尚未ready）；第一次 focus 漏 selector；跨窗口和已有 Flyout 切换导致 slider/字幕首次未出现，按当前树正常 focus/再打开后成功，无前台保护绕过。`ui-batch.py` 是本次分段续跑脚本，`batch*.json` 包含中断尝试，不是整批全通过声明；最终手动续跑 UIA 输出与 events/截图由 verify.py 汇总核验。没有因自动化失败修改生产控件。

## 归档及保留边界

`verify.py` 一次核对测试、源/PDB/loaded DLL、三轮+额外清理轮、Playing关闭/重开、退出/WER/清理。`validation.json` 是独立汇总；`evidence.zip` / manifest 保留原日志、TRX、生成方法、生成绑定、截图、UIA、启动/退出/事件、实验输入及 kernel 初始备份，不含native二进制、dump或认证材料。

kernel 初始 HEAD f02c33f6 且大量删除；先保留 HEAD/status/staged和unstaged binary patch，再仅本区从 dc85 本地源 fetch 并恢复指定 `0ae5e4e000a41a07f6696746706df11c06e8835b`。首次恢复因本地无对象失败未改动他区，记录保留。最终 gitlink 与父一致，生产 diff 为空。

正式 Legacy seek P1、#49、G1、AOT及总体性能历史 HOLD 保持。历史 AV 因果链未证明；本次一次成功 native run 不能抵销历史失败。新增框架/大调查/新dump/内存调查均未进行。

复现长媒体准备（在 prepare.py 后、launch 前；原短媒体被本区长媒体替代）：

```powershell
& artifacts/overlay-review/full/src/Desktop/BiliCopilot.UI/Assets/ffmpeg/x64/ffmpeg.exe -hide_banner -y -f lavfi -i 'testsrc2=size=960x540:rate=24' -t 600 -c:v libx264 -preset ultrafast -crf 35 artifacts/overlay-review/full/fixture-long.mkv
Copy-Item artifacts/overlay-review/full/fixture-long.mkv artifacts/overlay-review/full/fixture-media.mkv -Force
```

PE/PDB校验工具第一次对部署layout查找同名PDB失败（layout不含PDB），随后读取本次build目录DLL/PDB并与部署/loaded DLL的SHA256比较；未将该工具文件缺失计为产品问题。所有既有自动化缺口与失败不通过重写结果掩盖。
