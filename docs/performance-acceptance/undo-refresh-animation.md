# 撤销刷新动画：固定提交独立 review

2026-11-01；审查 `f44c22aa..e2a35ed042b81244340de591234bf90ea79447ed`，实现 `040c56f6`；离线确认 0 behind / 2 ahead。本报告不是候选原验收报告的复述。未合入父分支。

按 AGENTS、code-review、winui-code-review、code-testing 与 verification-before-completion 审查；依用户要求自行完成，不启动子代理。未激活窗口、输入、采集、运行 native、安装、访问凭据或清理工作树。未运行构建、测试或 WinUI analyzer；以下运行结果均为历史记录核验，不是 fresh pass。

## Standards

未发现新增的硬性仓库规范违规。无 API 不存在的判断。主要风险是状态机与验证覆盖，见下节；不以命名或格式偏好阻断。

## Spec：阻断与可采用范围

**P1：隐藏祖先 guard 被已有过渡绕过。** `UndoRefreshButtonAnimator.Apply` 的 `running != null` 隐藏分支在 `!animate / !IsEffectivelyVisible` 前返回。源码路径：开始 Show → 祖先 Collapsed（不卸载）→ Apply(false) → 只设 HideAfterShow；若该隐藏子树不推进 Completed，则按钮一直 Visible，之后也不会收到重复的 CanUndoRefresh=false 通知。正在 Hide 时同样直接返回。父版本在每次隐藏请求先检查 guard；候选保留了函数却改变了生效范围。应在隐藏请求处理已有过渡前判断立即折叠条件，停止旧过渡、清理标记并还原 transform。此为明确分支反例，未声称本次实机复现；需下一桌面槽验证隐藏祖先期间的 show/hide 两种路径。

**P2：兜底重新显示分支漏释放旧 HoldEnd。** PlayHide.Completed 先 Take 移除登记，再在 isStillHidden()==false 时调用 Stop；Stop 已找不到旧 storyboard，因此旧 HoldEnd(0) 未被 Stop。Show 的新动画可能暂时盖住旧动画，不能据此保证旧时钟已释放。应显式 Stop 捕获的 storyboard，并在调用外部 predicate 后考虑重入造成的新 transition。当前两个 header 通常通过同步 PropertyChanged 的 Apply(true) 提前取消旧 hide，故未证明普通产品路径一定触发此分支；这是 animator 自身兜底契约的缺口，需定向用例。

可采用的设计：两处撤销按钮移除 Implicit.ShowAnimations，统一 Storyboard；正常 hide 用 HoldEnd(0)，折叠后写基础值再 Stop；show 完成先设置基础值与单位缩放再 Stop；重复 hide 不重启。正常 show→hide 等待完整 220ms show（Opacity 为 180ms、缩放为 220ms）后再启动 160ms hide，避免从中间值硬跳至 1。Cancel 先移除登记并 Stop，重挂 Attach 使用 animate:false 按当前 VM 摆放。Take 的引用比较拒绝已取消的旧 Completed；该实现没有 async 等待任务。普通取消、完成均重置 ScaleX/Y，固定 XAML 中单位 CompositeTransform 的还原成立。

不能将上述正常路径认可扩大为所有隐藏祖先、旧回调重入、页面级回归已经通过。新增 UndoClickChangesVisibilityOnceAndRepeatedClicksCannotUndoAgain 只测试 VM 通知/引用/游标，不测试真实 animator 或渲染。

## 原始证据离线核验

源目录：候选工作树 `artifacts/t3-4957630f/`，本次只读；原始文件未提交。直接读取 harness/App.xaml.cs、Harness.csproj、red/AnimatorRed.cs、run-scenario.ps1、manifest、frames.ndjson、samples.csv，并用 System.Drawing 从 JPEG 重算区域亮度（非仅信 summary）。Python Pillow 不可用，未安装，改用现有离线图像读取。

- `runs2/red-takeover`：622×232 JPEG，undoRect=(354,102,88,48)，重算序列含 **1.74→144.65→0**；manifest 为 90 samples、13 images、29.94fps。查看回亮原图可见完整 Undo/Refresh，区域确实覆盖按钮，黑背景有对照。它证明该轮发生回亮；final/red-takeover 及其后多轮不具备同一回亮，不可称必现。
- `final/green-takeover`：同一区域重算 0→26.43→87.33→131.93→141.94→144.63→144.62→59.25→20.33→0；这是先完成淡入再淡出，不能说整个 show/hide 序列全程单调下降。
- **原报告“remount 均单调到 0”不成立**：final/green-remount 原 JPEG 最后为 59.25，frames.ndjson 后续直至 2975ms 复用 imageIndex=7；samples.csv 最后虽然 Collapsed/Opacity=1/ScaleX=1，也不能代替最终 DWM 像素证明。应将此轮标为渲染采样未覆盖终态，撤回全场景到零的结论。
- `final/aot-green-takeover` 原 JPEG 重算末段 144.62→20.33→0，与记录一致。process.json 为 PID 34740，18:53:13.9525533—18:53:47.8606828 +08:00，退出码 0；path 字段为 null。

录制脚本实际调用 winapp ui record --frames --fps 30，以 manifest/ndjson 指向的 JPEG 计算，而不是把 ElementVisual.Opacity 当 DWM 像素。窗口截图只证明保存时的目标表面；没有逐帧前台窗口、最小化或遮挡日志，不能外推所有桌面可见性。区域定位使用 width-4、底边-6 推算客户区，当前截图吻合，不是任意 DPI/窗口装饰通用校准。刷新区也是邻带估算，应限制在本布局。

30fps 存在约 33ms 采样盲区，重复图像不代表每个显示刷新都被记录。harness 用 DispatcherQueueTimer：repeat 为一次 Tick 内三次 Apply；busy 为启动 hide 后同一 Tick 阻塞 700ms；remount 是移除/Cancel/插回/Apply(false,false)；不是实际刷新按钮输入、网络加载、完整页面切换或随机时序压力。尚无隐藏祖先用例，也无 fallback predicate 反转与旧 Completed 重入覆盖。

## AOT、历史测试与桌面时段边界

AOT 仅为自建非 MSIX UndoRefreshHarness：csproj 链接 AnimatorSource，并非生产完整页面。aot/UndoRefreshHarness.exe 的 SHA256 `B41DBBCDAF647F2E0FF816DDAAA95296A83ABE2C7E36A4CFF78124D7EB005EAD` 与 Release/win-x64/native 链接产物一致，且有 ilc/link rsp；不能误认 Debug JIT exe。

手补 aot/App.xbf 的 SHA256 `46D7E74A5EA785EAA20CD7F95D5A1C554C19AB5FA0A8316A4E42897BFA3207AD` 与 Debug/bin、Debug/obj 一致，和 Release 的 A23F268D…不同；aot/UndoRefreshHarness.pri 的 `6EF5645D10A91723BF9FC6ABAC1D4190371D03C1935B0F8E823204934A6092E9` 同样匹配 Debug，和 Release 的 6BE84F42…不同。来源可限定为同一 harness 的 Debug 资源补件；未发现可闭合全部 DLL/资源、发布命令和运行时模块路径的逐项清单。不能把该混合资源宿主存活/零退出描述成正式 AOT 包或完整页面验收，也不能仅凭这些文件断言无 WER。

候选树离线搜索未找到 trx 或 build/publish log；“591 通过”和“0 警告/0 错误”在可访问原始记录中不可独立核验。本次没有 fresh 测试结果，不能沿用 591 作为本次通过数。

harness 源码存在 --activate 的 Window.Activate 路径；“无合成输入”与“曾激活窗口”可以同时成立。可确定红回亮录制在 18:29:57.553—18:30:00.567 +08:00，final green takeover 在 18:47:56.669—18:48:05.133 进程区间，AOT 区间如上；缺少其它实验的对齐原始时段，无法判断是否重叠，不擅自认定污染。当前 fa1a5e1f 独享桌面，本 review 未占用；进一步真实源码/隐藏祖先复验须由父任务安排下一槽。

结论：Standards 0 项硬性违规；Spec 有 P1 guard 阻断及 P2 HoldEnd 释放缺口。建议先修 guard、补定向生命周期验证，修正 remount/591/AOT 的证据表述后再决定采用；允许采用单 animator 与正常 HoldEnd 设计思路，不批准完整验收结论。
