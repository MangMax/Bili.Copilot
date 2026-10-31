# Legacy 完整应用退出：两轮有界诊断

**未定位根因，不提交生命周期修复；原始退出 FAIL 保留。** 基于父 `d36f948b`（包含 `e20eba2d` / logger 小修 `a97d4024`），本片不改正式 src、依赖或 gitlink，不回退 logger 修复，无 push/PR/issue/子代理。未读取进程内存、采集 dump 或进行 native 逆向。

## 只读结论

- 原始 PID34652：CloseFull 12:52:46.966Z → disposed=true / remaining=0 12:52:47.049Z → observer closed=true 12:52:47.166Z；WER 12:52:48.117Z，CoreMessagingXP.dll 12.6.4.0 offset adf5e / c000027b；retained exit -1073741189。原始日志和 WER 完整复制到 evidence.zip 的 original/，不覆盖原失败。
- 宿主 `ObserveVisual()` 先检查 `vm.Window.IsClosed`。生产 `PlayerWindow.OnWindowDestroying` 设置 IsClosed=true；`PlayerViewModel.CloseAsync` 复用关闭任务，关窗后 DisposeAsync，最终移出 Players。观察器在 CloseFull 后继续读取托管状态，但上述日志不能证明它继续读取已销毁的 XAML。
- 成功 Overlay 宿主也在 CloseAsync 之后停 observer，使用同样 IsClosed 检查；其 DirectX/实验 Core 与本次 Legacy 不同，不能当作同臂因果对照。本片没有引入实验 Core。
- 生产面板 Unloaded 会 invalidate redraw、退订 VM、Close/Dispose renderer、清 RootGrid。仅从源码和故障模块名无法判断这里是否为原始异常调用。

## 实际诊断与两轮结果

复用 legacy-logger-review 准备/启动脚本，所有写入位于本工作区 `artifacts/legacy-exit34a1`。独立身份 `Bili.Exit34a1`，合成本地账户与原 600 秒媒体，不访问登录 Caa6a7be/Dev 或其他应用实例。kernel 在任何准备前保存 HEAD/status/index/worktree/untracked ZIP，保留原 f02c33f6 / 474 条异常；只在隔离应用内归档指定 0ae5e4e0。

固定原 Legacy/Share `0.0.1-preview6.loggerreview4e91.1` nupkg；Core 仍正式 preview6。两轮使用同一次 Release/x64/JIT 构建，build exit 0 / 1 个 protobuf 未使用 import 警告 / 0 错误。两个 layout 与 build 的应用、Legacy、Share、Core DLL SHA256 均相等；Legacy SHA256 与原验收一致。原 consumer 中 901 个 tracked C#/XAML 归一换行后相同，唯一差异 App.xaml.cs 是下述诊断改动（另有实验 OfflineAcceptance 与身份路径）。不宣称 AOT 或正式打包验证。

诊断只增加普通应用日志：App.UnhandledException、TaskScheduler.UnobservedTaskException、CloseFull 开始后限定窗口内的 FirstChanceException；记录 UTC、托管线程 ID、类型、HResult、Message、StackTrace/ToString。FirstChance 递归保护，不 SetObserved。**原生产 App.OnUnhandledException 设置 Handled=true；仅诊断副本去掉该行，避免吞错，正式源码未动。** 退出步骤分别记录 CloseAsync 返回、observer 停止、托盘释放、control.Close 与 app.Exit 返回。

两轮有效命令一致：Playing → PauseResume → Seek20 → Back1 / Forward1 / Seek21 → Seek25 → Repeated → PauseResume 恢复约3.6秒 → PauseResume → UnloadDanmaku / LoadDanmaku → Seek20 / Repeated / Seek25 → CloseFull → 约2秒后 ExitOwned。所有行为均由自有 PID 范围 UIA 调用宿主按钮，按钮调用真实生产命令。

这是固定的有界诊断序列，**并非原来约3分钟手工验收的逐毫秒回放**：未重演原进度条坐标输入和长时间视觉驻留。第一轮启动空树失败与隐藏 PlayPauseButton 的 element_not_found 均留在 sequence 证据；后者没有执行动作，随后通过 PauseResume 调用相同生产命令并继续余下序列。第二轮完整一次执行。同步日志 I/O 与取消 Handled=true 也可能影响观测时序/异常行为；不能把两个成功视为原失败的 red/green 修复验证。

| 轮次 | PID | retained exit | 延后 WER | 关闭日志 |
| --- | --- | --- | --- | --- |
| 1 | 42056 | 0 / forced=false | 31.0215353 秒，精确 PID/path 无匹配 | disposed=true，六个退出步骤均返回 |
| 2 | 38176 | 0 / forced=false | 31.0093124 秒，精确 PID/path 无匹配 | disposed=true，六个退出步骤均返回 |

两轮均无 first-chance-closing / unhandled / unobserved-task / observe-error；关闭后 observer 只输出 closed=true 的 state，没有 visual-enter。这缩小了本轮观测范围，但**没有获取原始失败的首个抛错调用**，也不能排除仅 native 传播或时序相关异常。没有证据支持修改宿主定时器、生产引擎或 .NET；无生产候选，故不制造红绿测试、不重跑539。

## 下一可执行的最小步骤（本片未执行第三轮）

若继续授权，优先在相同隔离副本、相同依赖与上述普通异常日志下补齐**原手工长驻留与 CloseFull→ExitOwned 的实际间隔**；原记录没有足够精度确定该间隔，不能声称已经精确回放。只增加三个现有生命周期边界的 enter/return 日志：`VideoDanmakuPanel.OnUnloaded/ReleaseRenderer`（区分 master.Close 与 RootGrid.Children.Clear）、`PlayerWindow.DisposeAsync` 的 `_xamlSource.Dispose`、宿主 DispatcherQueue.ShutdownStarting/ShutdownCompleted。保留上述退出阶段标记，用第一次缺失的 return 或真实异常栈定位具体边界，再决定最小候选；不先调整关闭顺序或升级运行时。无需内存读取/dump，也不重跑重绘/短跳/重挂的全套验收。

## 复核与清理

`python -B docs/engine-61-design-draft/legacy-exit-diagnostic/collect.py` 实际 exit 0：断言原始非零退出、两轮 retained exit/31秒 WER/退出顺序、三组 DLL 字节一致、kernel 初始全部 tracked/untracked 字节未变、自有 packages=0 / processes=0。`validation.json` 提供精确时序；`evidence.zip` 与 `evidence-manifest.json` 保留原始输入/输出、UI 调用失败、构建日志和清理结果。未包含凭据、native 包或 dump。

重放需先准备一个新的空 artifacts 根（同步修改脚本 Root/身份，勿覆盖现有证据），运行 prepare-app.py，再 launch-retain.ps1 与 run-sequence.py；清理由精确 package identity + InstallLocation 校验后进行。准备脚本只读旧来源 feed/kernel/assets，输出只在当前工作区。主机 desktop 授权与新 PID 必须重新核实，不能盲用历史 PID。

本片依 systematic-debugging / implement-spec / WinUI code-review、UI-testing、verification-before-completion 执行，无需修改 .NET 测试。专用 WinUIAnalyzer 未运行；正常构建分析器已执行。最后复核宿主没有新增 Handled=true/SetObserved/生命周期修复；退出资源及真实错误码均保留。

原短跳2秒门槛、暂停稳定重绘 FAIL、暂停重挂空画面、历史 AV / #49 / AOT 边界均不变。两轮退出成功不抵销原始 FAIL，不构成发布许可。工作区仍包含初始 kernel 异常，不报告 clean。
