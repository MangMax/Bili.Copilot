# Legacy 暂停 seek 最小候选：交独立审查

本片正常构造真实 Legacy/CanvasAnimatedControl，恢复原源码并只修改两个引擎文件。相同最终宿主：原源码包 **Passed: 4, Failed: 8, Skipped: 0**；候选包 **Passed: 12, Failed: 0, Skipped: 0**。这是限定暂停 seek/队列交错候选，不是 #49、全模式生命周期或正式发布验收。正式应用依赖、gitlink、父与来源工作区未修改。

## 来源与修复

[origin.json](origin.json) 固定源码 `4d0f8691f08f1e143294402477e72306a1b38b6f`，ZIP SHA256 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`。从该归档恢复全部 45 文件，逐文件原字节校验。未采用其他区未提交源码。原源由本地保存的归档确认，未重新认证远端仓库。两个必要覆盖文件在 [source](source/)，精简审查视图为 [candidate.patch](candidate.patch)，前后 hash 在 [changes.json](changes.json)；原许可保留。

原路径：`UpdateTime → queue → Updater_DoWork → Seek → Stop/Pause → Resume`。应用在 UpdateTime 返回后 Pause，不能约束队列之后无条件 Resume；原 Close 后 Resume 还会清除 renderer stopped 状态。改变如下：

- Pause/Resume 保存用户意图；Seek 的临时 Pause/Stop 不改意图。内部 `Start(bool paused)` 在同一提交中设置暂停与 stopped 状态，没有先 Resume 后补 Pause 的窗口，公开 API 不变。
- 一个 `_playbackGate` 串行化意图、seek 提交、worker native Render 与 Close。列表路径始终先 list 后 playback；Pause/Resume/Close 的直接实现不取 list 锁。关闭后的 worker 提交和 Resume 被拒绝，关闭清队列并唤醒 worker。没有等待 worker 退出或新 owner 框架，也没有释放仍可能被 worker 使用的事件句柄。
- worker 内部 seek 不再清掉之后排队的更新；队列尚有元素时补信号，避免 AutoResetEvent 合并通知后停留在较旧目标。显式公开 Seek/Stop 的清队列语义保留。
- 第一版状态修复实测为空白：暂停使 `OnCanvasUpdate` 直接返回，新项 `IsFirstRenderTimeSet` 永远为 false。第二版允许暂停时首帧布局，将位置步长置零，并从条目年龄扣除暂停 tick；这也是后续 Resume 不立刻耗尽 TOP 寿命所必需。未增加目标前活跃项重建。

集合 alias、Set/Get/Add/Clear 算法、事件异常契约均未改。使用 C1a 固定提交 `597dff0d4d1a034906b89154e936aa1df8dadb1e` 的原 fixture，新构建源程序集及实际 nupkg DLL 各 **23/23**。211 行冻结公开 API 逐字节相等。实际包 Legacy PE/PDB 身份一致，7 个 PDB 文档全部匹配；Share 9 个也匹配，见 [compat-package/loaded.txt](compat-package/loaded.txt)。这里同时加载原 Core 作集合对照，不是 Core native 验证。

## 实机与红绿

独立身份 `BiliCopilot.LegacySeek1085f94e`，所有 Root/layout/进程均在本工作区。宿主从已提交的前次 host ZIP 取 App.xaml/manifest 外壳，新的 [Probe.cs](Probe.cs) 直接调用真实 master 的公开 API；没有编译应用 panel/VM、旧 seek 候选或创建媒体播放器。这证明引擎行为，不是正式应用进度条端到端测试。正常构造创建实际 renderer，未使用 uninitialized object 替代 native。反射只读实际 renderer 状态/条目；既有 ILogger 的 `Reseek after` 回调用屏障固定出队后顺序，无生产测试钩子。日志回调持有的 list 锁不妨碍这些 Pause/Resume/Close 操作返回。

[summary.json](summary.json) 保留每条断言；[source-red](evidence/source-red/) 与 [final](evidence/final/) 的 Program.cs 和当前 Probe.cs 原字节一致。不是用更弱的最终 oracle 获得绿色。

| Requirement | Evidence |
|---|---|
| 暂停可见 21→20 后，迟到 Seek 不 Resume | `PausedBackward_RemainsPaused`：原源码 FAIL → 候选 PASS |
| 新建 TOP 首帧非空，暂停超过默认 3 秒寿命后位置/内容仍稳定 | `PausedBackward_NonemptyStableFrame`：FAIL → PASS；两次只读条目快照相隔 3.5 秒，截图逐张检查 |
| 暂停短前跳 20→21 保持暂停 | `PausedShortForward_RemainsPaused`：两边 PASS |
| 连续 20→22→21 最终到 21 且暂停 | `PausedRepeated_RemainsPaused`：FAIL → PASS；断言 time=21000，不仅断言暂停位 |
| 连续 seek 后非空稳定画面 | `PausedRepeated_NonemptyStableFrame`：FAIL → PASS；相隔 3.5 秒的快照 |
| 随后 Resume 正常启动，保留剩余寿命并到期 | `ResumeAfterPausedSeek_Runs`、`ResumeAfterPausedSeek_PreservesRemainingLifetime`、`ResumeAfterPausedSeek_ExpiresNormally`；候选全 PASS，剩余寿命原源码 FAIL |
| 已出队 seek 后再 Pause，以最新暂停为准 | `DequeuedSeekThenPause`：FAIL → PASS |
| 已出队 seek 后 Pause→Resume，以最新播放为准 | `DequeuedSeekThenResume`：两边 PASS |
| 已出队 seek 后 Close，不恢复 stopped；worker 退出 | `DequeuedSeekThenClose`：FAIL → PASS；候选 lastTime 保持 21000，原源码迟到改为 20000 |
| Close 后 Resume 不重新启用 renderer | `CloseThenResume_RemainsClosed`：FAIL → PASS |
| 集合/异常与 API 兼容 | [compat-package/results.json](compat-package/results.json) 中全部 23 个原名案例及冻结 [public-api-baseline.txt](public-api-baseline.txt) |

初次正式 preview6：3 PASS/5 FAIL，runner exit 1；fixture 漏 BaseFontSize，文本过小，截图记 FAIL，不当视觉通过。修正宿主字体后，第一版包 `.1` 为 8 PASS/1 FAIL（暂停首帧为空），runner exit 1。第二版首次运行完成 2 项后因人工截图审阅等待超过 5 分钟退出，**未完成，不计通过**；后改成自动完成 UI 交互、事后逐张审阅，没有更改引擎来规避该超时。第二版完整运行与最终重跑均 12/12。初次构建残留了归档中默认 XAML 页面，编译失败；明确排除后恢复 0 warnings/0 errors，日志均保留。上述工具/fixture 失败不混作引擎根因。

## 包消费、执行和退出

实际候选 `Danmaku.Legacy 0.0.1-legacyseek1085f94e.2`，Share 使用同版本、源码未改。只写本地 feed，不发布。Legacy nupkg 条目、NuGet cache、运行 layout hash、进程报告的加载路径/hash/MVID 四方相同，见 [closure.json](closure.json)。最终 Legacy DLL SHA256 `27a3ec6dd222a4acb242e82799f0b83fcf5fab2fa40a9a5704575b7b8c5fab1c`。实际包 DLL/PDB 提取到独立 compat 输出执行了上述 23 项与文档校验；不是拿另一次源构建 DLL 替代包来源证明。缓存共享，不声称全隔离干净机器 restore。

实测 SDK `11.0.100-rc.3.26522.148`，引擎仍原 net8 Windows TFM，独立宿主 net11/Release/x64，宿主 AppSDK 2.6.4、Win2D 1.4.0。这不是将故障归因于 .NET11。正式应用没有升级依赖。

最终实际命令（仓库根）：

```powershell
dotnet build artifacts/legacy-paused-seek/host/Spike.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false --configfile artifacts/legacy-paused-seek/NuGet.Config
./artifacts/legacy-paused-seek/run.ps1 -RunName final
dotnet artifacts/legacy-paused-seek/compat-package/Fixture.dll artifacts/legacy-paused-seek/compat-package-results
python -B docs/engine-61-design-draft/legacy-paused-seek/verify.py
```

构建 0 warnings/0 errors；native runner exit 0，**Passed: 12, Failed: 0, Skipped: 0**；compat runner exit 0，**Passed: 23, Failed: 0, Skipped: 0**。native 是自有 JSON/WinApp runner，不是 dotnet test。源码红测 runner exit 1，原 45 文件保持 hash；不是重写算法的模型红测。

复原到新目录并构建可执行候选：

```powershell
python -B docs/engine-61-design-draft/legacy-paused-seek/prepare.py --name independent
# 仅在持有 local 桌面授权时，执行 prepare 输出的 run.ps1 -RunName native。
```

`--baseline` 可恢复原源码红测；`--archive` 可提供同 hash 归档。必须新 name，生成独立 Appx 身份/唯一 prerelease。prepare 不启动 UI。`--name restorecheck` 已实际恢复、打包、构建成功；未运行它的 UI，不把构建当 native 证明。

所有运行保留 exit handle，正常关闭，未强杀。最终 [retainedexit.json](evidence/final/retainedexit.json) exit 0；[wer.json](evidence/final/wer.json) 在退出后 **32.026 秒**查询 Application，精确 PID/path AppError/WER 无匹配。原源码红测也完成 ≥31 秒查询。初次 official-red 的原查询仅 30.9998 秒，保留原记录，不偷改为满足门槛。各自 Appx 已移除、进程退出、`winapp ui yield` 已释放；本地 nupkg/源产物保留供审查。没有操作 Caa6a7be、Dev 或其他进程，没有 dump/内存调查或平台拒绝任务重试。

## WinUI 自审与交付边界

按 systematic-debugging、tdd、code-testing、run-tests、winui-code-review、winui-ui-testing、verification-before-completion 自行完成，没有子代理。使用已授权公开调用 seam；没有不可用的 code-testing-extensions 指引。run-tests overlay 不存在。

针对本片直接状态路径未发现新的阻断项；源码和实际包 API 保持，集合与异常兼容检查通过。宿主交互控件有 AutomationId，实际 UIA invoke、状态等待与截图均完成。未跑专用 WinUIAnalyzer、AOT、主题/辅助功能全矩阵；不称这些通过。

**Warning / 限制：** `_playbackGate` 会使 Pause/Close 等待已进入的单次 native Render/Stop 完成；没有非阻塞退休或延迟上界保证。未验证任意用户 logger 回调跨线程重入、Close 与 AddRealtime/configuration 的所有交错、canvas callback 全生命周期，不能以这次有界进程退出证明所有 GPU retirement。若独立审查要求这些也属于本片接受条件，应保持 HOLD，最小下一步是为确切入口补同样的真实屏障用例，再决定是否需要改变关闭提交方式；不能直接复制 Core owner。当前候选只申请本表已测状态行为的审查。

本片 TOP 首帧和寿命实证不泛化为全模式正确性。目标前活跃项重建、配置切换、双引擎发布、正式 panel/完整媒体路径等既有要求继续保留。历史 AV、UNKNOWN_EXIT_NOT_PASS、B1-XAML/跨模式图像 FAIL、Present hang、AOT/#56、#49 的原结论未删除或改绿。

基于父 `7ec17b2269d122c496730ff25028f78b86565edc`，交付前再次合入父最新本地 tip。kernel 既存 HEAD `f02c33f639addd5d421700c43fa66382beafe323`/474 条异常先备份到 ignored `artifacts/legacy-paused-seek/kernel-backup`（index/worktree binary diff、untracked ZIP、HEAD/status）；本任务不需要构建正式应用，故未恢复或操作该子模块，gitlink 仍 `0ae5e4e000a41a07f6696746706df11c06e8835b`。不宣称整个工作区 clean。无 push/PR/issue 写入。交回父方安排一次独立 review，不由本实施代理自行签独立通过。
