# #54 父 checkout 独立复核：适配器范围有限通过

2026-10-30，`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`，分支 `feat/performance-45`。**真实 PlayerWindow / native presenter 的本次矩阵 18 PASS / 0 FAIL；不是完整播放器或 #54 完整验收。** 生产代码未改。可聚焦 fixture 白窗定位并仅修复内容背景；原 notification oracle 的 red 保留，新 oracle 检查真实调用边界，未豁免关闭条件。

## 合入与历史

开始时 HEAD `4a05ee9b00394ef1b9886d12ae9d6f9d23d80ed8`，工作树干净。核对目标 `47f64a7583c2790c29ef64acb19b7f9257cd2959` **自身 delta**：120 文件，仅 harness-54、evidence-54 系列和 ui-54.md。执行 `git merge --no-ff` 后 merge SHA 为 `fc66f8af48d846548b4970207e0d4b00dd1b6c0d`；合并相对父基线的文件集合与目标自身 delta 一致，见 [merge-audit.json](evidence-54-recheck/merge-audit.json)。

已读 AGENTS.md、docs/agents 规则、ui-54.md 全过程、生产 PlayerWindow/PresenterChangeSession/CursorGrid 和实际 PlayerOverlay 结构，解析三组原始 events/results；[历史事件复核](evidence-54-recheck/historical-event-audit.json)。原 ui-54.md 及所有历史 evidence 文件保持逐字节不变。初版 18/18 缺乏 focus 正向对照、复验 10/18 与最小诊断 0/3 的历史结论都不回写。

应用 systematic-debugging、winui-code-review、winui-ui-testing、winui-dev-workflow、verification-before-completion；修改测试前读 code-testing，检查视觉结构时读 winui-design。范围限单个真实 PlayerWindow，直接自审，无子代理。没有完整测试工作流扩张；code-testing-extensions 未提供，采用现有 harness 约定。

## 调查顺序与单变量结果

1. 新 owned 身份 `BiliCopilot.Acceptance54.R1D19`，PID 29844。先只加 native 调用日志、header getter、visual-tree，不改背景或原 oracle。白窗复现；Loaded=true，UserControl 与 Content/Grid 都为 545.333×372.667 DIPs，Content 有 header/两行文本，非零文本尺寸，Visibility=Visible、Opacity=1、Dark。UserControl.Template=null，实际 visual child 就是 Content Grid；没有丢失 Content 或未布局。内容 Grid.Background=null。
2. 同一基线保留原 `Diagnose` 三次 **0 PASS / 3 FAIL**：[original-oracle/results.json](evidence-54-recheck/original-oracle/results.json)。新增调用探针共记录 18 次关闭前 SetFocus/SetForegroundWindow、零关闭后请求；有 live ResetFocus 调用栈、activation 和 header 读取。原失败仍来自 `lateFocus == 0`，不是新边界失败。实际执行 App/NativeProbe 源码保留。
3. 唯一绘制行为改动：`var content = new Grid { Background = Background };`。PID 40764 的 [background](evidence-54-recheck/background/visual-tree.json) 中背景刷为 #FF202020，文字 #FFFFFFFF，模板、Content 和尺寸未变。直接和屏幕截图恢复文字和暗色背景。**这个 fixture 没有把 UserControl 的 Background 实际画到透明 Content 背后**；把现有刷设置到绘制内容即可消除白窗。没有添加模板、改焦点能力、替换生产窗口或 presenter。
4. PID 31308，增加视觉专用 Grid 对照：从同一个 fixture 取出同一 Content Grid，交给同一个真实 PlayerWindow，记录 [grid/visual-tree.json](evidence-54-recheck/grid/visual-tree.json) 和两张图。布局与绘制正常，背景/文字像素比例与修正后的 UserControl 对照一致。此操作不参与行为矩阵；RunTests 每例重新创建可聚焦 UserControl。
5. 建立调用边界后，增加新的关闭 oracle、保留 Diagnose 的原断言。一次运行受影响的全部模式/关闭矩阵，18/18，通过结构化校验。未反复重试至绿。

实际生产 PlayerOverlay 是 `PlayerControlBase : LayoutUserControlBase<PlayerViewModel>`，通过 InitializeComponent 加载 XAML RootGrid，包含按 ViewModel 显隐的黑色 Rectangle、透明失活遮罩、媒体/弹幕/交互/标题栏控件。它不是本 fixture 的两段文字 UserControl。实际基类/生产源码均未改，也没有新造生产窗口实现；本次**未执行真实 PlayerOverlay 基类和完整媒体结构**，所以不能把 fixture 的背景问题认定为生产白窗缺陷，不能把修正后的图像当成真实播放器视觉通过。未发现确定需要父级修改的生产缺陷。

## 调用边界和 oracle

[harness-54/Harness.csproj](harness-54/Harness.csproj) 继续直接 source-link 未改的 PlayerWindow.cs、PresenterChangeSession.cs、CursorGrid.cs。真实 AppWindow、DesktopWindowXamlSource、InputActivationListener、native presenter 和所有延迟算法保持原实现。

新增 [NativeProbe.cs](harness-54/NativeProbe.cs) 位于 PlayerWindow 命名空间，C# 名称解析使生产文件的 PInvoke 调用经由转发器，**每次仍调用实际 Windows.Win32.PInvoke 导入一次**。SetFocus / SetForegroundWindow 在调用前记录 HWND、closed 和栈；没有伪造成功值。其余 Win32 方法直接转发。App 中显式别名仍使用原始 PInvoke，避免观察自身 IsWindow 查询造成污染。

HeaderBar getter 是 adapter 的实际访问边界，生产每次获取都会记录；测试自己的检查改读 Probe 字段，不计入生产访问。同步 GettingFocus 和 activation action 记录 closed/disposed。三个 `RapidFinalNormal_R1/R2/R3` 各自强制 live 正向对照：native ResetFocus 请求 10 次、activation 1 次、header getter 5 次、GotFocus 6 次，并要求实际 padding 更新。不是只证明“关闭后没有事件”。

`VerifyClosed` 要求：

- Close/Dispose 后及观察结束 HWND 均不存在，IsClosed/IsDisposed=true，IsPresenterChanging=false；只读反射确认真实 session `_closed=true`。
- 主动再调用四个模式入口、ResetFocus、DelayUpdateTitleBarPadding；观测 1200/1800ms，并经过 Low priority barrier。
- **零关闭后 SetFocus/SetForegroundWindow 请求、GettingFocus、activation 回调、HeaderBar getter、alignment/padding 更新**。原先各阶段 alignment/padding 的后续断言全部保留并实际执行。
- GotFocus 通知单独记录。9 次迟到通知均有同实例更早的 live SetFocus 和 GettingFocus、销毁 HWND、消息循环派发栈，无新 native request。解析器拒绝缺乏前置请求/GettingFocus、HWND 仍活或生产 Show/ResetFocus 栈中的迟到通知；[notification-classification.json](evidence-54-recheck/matrix/notification-classification.json)。这不是逐事件 CorrelationId 一一对应证明，GotFocus 没有在此取得配对 ID。

**边界限制：** native 请求探针覆盖本源码的 SetFocus/SetForegroundWindow；header 是替代品，activation 的运行时观察点是 action 回调，非 sender.State getter。其余 WinRT 内部读取/原生内部调用未做全量拦截。生产静态审查确认 ResetFocus 在 HWND/SiteBridge 前、activation 在 sender.State 前、标题栏在 dequeue/每次 await 后均有 IsClosed/IsDisposed guard。因此结论是“直接观测边界通过 + guard 源审”，不是任意 native race 或完整应用无访问的证明。通知即使迟到也未自动豁免其余关闭条件。

## 验收矩阵

[run.ps1](harness-54/run.ps1) exit 0；[verify-recheck.ps1](harness-54/verify-recheck.ps1) exit 0。原 verify.ps1 保留严格历史 oracle。实际结果：[results.json](evidence-54-recheck/matrix/results.json)、[verification.json](evidence-54-recheck/matrix/verification.json)、[events.jsonl](evidence-54-recheck/matrix/events.jsonl)。1292 个事件，80 个 live GotFocus、9 个 late GotFocus、210 个 native requests，12 个关闭观测；零非预期异常。

| Requirement | Evidence |
| --- | --- |
| 快速全屏→小窗→正常、最后模式和 500ms 代次 | `RapidFinalNormal_R1/R2/R3` PASS；保留 180ms 间隔、200ms 最新代次检查、最终模式/还原位置/尺寸/清置顶及延迟稳定断言；含三个 live boundary 正向对照 |
| 两种模式还原、置顶、受控保存位置 | `RestoreBothModesAndSavedGeometry_R1/R2/R3` PASS，原生 presenter 和真实几何；设置仅进程内 adapter |
| 500ms 内关闭 | `CloseDuring500ms_R1/R2/R3` PASS，计时前置条件、原生销毁、session 关闭、无新关闭后请求/访问 |
| focus/title 已排队而同 turn 关闭 | `CloseBeforeQueuedFocusAndTitle_R1/R2/R3` PASS，零后续 header alignment；迟到通知独立归档 |
| 第一段标题栏 delay 中关闭 | `CloseDuringFirstTitleDelay_R1/R2/R3` PASS，达到 Left 后关闭，保持 Left 且无迟到 getter/更新 |
| 第二段标题栏 delay 中关闭 | `CloseDuringSecondTitleDelay_R1/R2/R3` PASS，达到 Stretch 后关闭，padding 次数不增长 |
| 原错误 oracle red 不丢失 | 新 baseline 的 `QueuedCloseDiagnostic_R1/R2/R3` 均 FAIL；原历史 red 全保留；当前 Diagnose 仍选择原断言 |

## 逐图复核与像素

本次 **17 张 PNG 均通过 view_image 逐张查看**，未裁切/修图。独立像素分析读取原 PNG：[pixel-analysis.json](evidence-54-recheck/pixel-analysis.json)。原始 direct 与 capture-screen 两种渠道均保留元数据。

| 文件 | 实际观察 |
| --- | --- |
| baseline/direct.png、baseline/screen.png | 全白内容，无可见文字；白像素分别 98.727%、95.134%，#202020 像素为零。不是仅截图失败 |
| background/direct.png、background/screen.png | 内容背景和标题/中心两行文字恢复；#202020 分别 97.845%、94.236% |
| grid/direct.png、grid/screen.png | 与修正 UserControl 同内容/位置，像素比例一致；仅视觉对照 |
| matrix/initial.png、initial-screen.png | 正常窗口背景、文字、标题按钮可见，无文字裁剪 |
| matrix/Top.png、Top-screen.png | 同正常视觉；置顶由原生断言证明，非图片推断 |
| matrix/Full.png、Full-screen.png | 全屏铺满、标题按钮消失；screen 右下有 T3 系统通知覆盖，不能当播放器 UI 或无叠层图 |
| matrix/Compact.png、Compact-screen.png | 小窗文字与关闭按钮可见，最小化/最大化消失；screen 底边少量后方控制器像素属于窗口区域捕获边缘 |
| matrix/Normal.png、Normal-screen.png | 正常尺寸/文字/按钮恢复 |
| matrix/complete.png | 控制器完整显示 Suite complete 与 Exit，没有控制器按钮裁剪；通过结论来自 results，不来自这行文字 |

## 构建、保留失败与清理

准备使用当前父 checkout：

```powershell
./docs/performance-acceptance/harness-54/prepare.ps1 -SourceRoot . -Destination C:/Users/hp/AppData/Local/Temp/bili-54-recheck-1d19
winapp run C:/Users/hp/AppData/Local/Temp/bili-54-recheck-1d19/Harness.csproj --no-restore --arch x64 -p Platform=x64 --detach --json
./docs/performance-acceptance/harness-54/run.ps1 -AppPid 31308 -Evidence <fresh evidence directory>
./docs/performance-acceptance/harness-54/verify-recheck.ps1 -Evidence <matrix directory> -RequireFocus
```

初次探针编译失败 CS9363/CS1061（HWND.Value 指针），保留 [build-baseline.log](evidence-54-recheck/build-baseline.log)，修为显式 IntPtr 转换。随后 baseline、background、matrix 三次 Debug JIT 构建均零 warning/error，launch 日志保留。像素分析最初缺 PIL、随后 PowerShell 7 编译 helper 缺程序集引用，见 pixel-tool-note.txt；最终使用系统已安装 Windows PowerShell/System.Drawing，读取全部 17 图成功，没有安装依赖。没有修改图像或为工具失败重跑 UI。

生产三文件 hash 见 source-hashes.json；测试时 merge SHA 见 tested-sha.txt。最终执行的 harness hash/源代码保留；App 最终仅清理了尾部空行，未再改执行语义。WinUI review 检查通过范围内 source-link、生命周期 guard、主题/内容和断言边界；专用 WinUIAnalyzer 未安装/未运行。**生产未改，不重复 full suite 或已知失败的 AOT**；Release/Native AOT 继续 BLOCKED（现有 MSVC linker/vcvarsall/lib/x64 缺失）。

PID 29844、40764、31308 均由各自 Exit 退出，按进程路径/InstallLocation 核验 owned 身份后只移除 `BiliCopilot.Acceptance54.R1D19`。三组 cleanup.json 均 processGone/packageGone；最终 [cleanup-yield.json](evidence-54-recheck/matrix/cleanup-yield.json) released=true，**桌面释放**。PID 9608、Dev/生产身份、真实账号/API、系统设置未操作；未安装工具，无 push/PR/关闭 issue/其他 worktree/子代理。

未验项目保持：完整独立/集成播放器、实际 PlayerOverlay/HeaderBar 与媒体/认证/导航生命周期、跨进程设置、多屏/DPI/主题矩阵、PR #43 托盘组合、AOT 发布运行、线程池/CPU 性能收益。#54 和 #45 均不能标为完成。

收尾核对补记：实现/文档 diff 检查通过；完整 staged diff 的两个 `executed-App.cs.txt` 快照报告末尾空行，作为实际执行源码原样保留，不修饰历史证据。最终补充 yield 返回 `released=false`（此前 matrix cleanup 已 `released=true`，其后无桌面重新获取）；这不是新桌面操作失败或仍持有桌面的证据。
