# #49 有界 Legacy trace 与 clean 产物核验

2026-10-30。**本轮没有修复引擎，也没有重现所报告的 `CONTINUOUS MOTION` 暂停后消失。新增关键更正：历史两张 frozen 原 PNG 的白字区域乃至整个渲染区域 RGBA 完全相同，不能继续把它们解释为“白字消失”的证据。独立 clean 构建纠正了产物来源问题；真实 Legacy trace 证明本轮暂停窗口内 item 保留、Draw 调用及 batch Dispose 持续完成。当前候选仍不可发布。**

父基线 `51154f0052f86428282550e2ca8910dccc228a03`。完整读取候选 `c15db2d1f52254cd2b49cb7e2bdd8937f750f587` 和独立审查 `3957d96b238e93fd665a5a4697ae7cfcd2d4fd00` 的 README，特别遵循审查第 4、7 节。两份历史文档提交已合并到本分支，内容未改；其“最终无插桩”声明仍由审查报告纠正，不因本轮新 clean 产物而恢复成立。没有将候选合入正式依赖。

## 1. 执行范围和来源

- 工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9d663a29`，分支 `t3/engine-49-trace-diagnostics`；开始 HEAD 为父基线且 clean。
- AGENTS、docs/agents、systematic-debugging、code-testing、run-tests、winui-code-review、winui-ui-testing、winui-dev-workflow、verification-before-completion 已读。遵守源 `src/.editorconfig`；[上游 MIT](UPSTREAM-LICENSE.txt) 原样保留。不启动子代理；本轮为诊断脚本，不新增单元测试框架或全套测试。
- 只读复用候选工作区归档 `artifacts/engine49/source/upstream.zip`，重新计算 SHA256 为 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`。精确上游 commit 为 `4d0f8691f08f1e143294402477e72306a1b38b6f`。
- 分别解压到自己 ignored `artifacts/engine49-trace/{original,candidate,trace}/danmaku-kernel-<commit>`。original 未改；candidate 仅应用历史 scheduler 补丁；trace 在同一 scheduler 候选上仅改 Legacy `DanmakuRender.cs` 并增加 `LegacyTrace49.cs`。Core render、GameLoopThread、资源管理器没有改动。没有应用 `rejected-render-experiment.patch`。
- 每组使用自己的 `NUGET_PACKAGES`、`NUGET_HTTP_CACHE_PATH`、`DOTNET_CLI_HOME` 和源树 bin/obj。未借用原缓存或原 DLL，未修改他人产物。未 shell 创建 worktree、安装工具、push/publish 或写 PR/issue；GitHub #49 仅 gh 只读。

## 2. 先完成 clean 对照，再建立明确插桩的 trace 构建

SDK `10.0.403`，WinApp `0.7.1`，Release JIT x64。新解压目录执行 `dotnet build ... -c Release -p:Platform=x64 --no-incremental`。original/candidate 的 Core、Legacy 四次构建均 exit0、0 warnings、0 errors。日志在 [evidence](evidence)。fresh 源树是 clean 的依据，`--no-incremental` 不是旧目录清理的替代。

PEReader 扫描所有 type 的 field metadata，original/candidate 均无 `_test*` 或本轮 trace 字段：[原源元数据](metadata-original.json)、[候选元数据](metadata-candidate.json)。包含完整 DLL 路径、SHA256、MVID；不是只在源码搜字符串。

| clean 对照 DLL | SHA256 |
|---|---|
| original Core | `9cbf889d39dc89e9b0d1cf6c684c43236e948941f677b135a4676418f6e62180` |
| original Legacy | `572b236ac109d81e33a8c8ee125c0ac943e61a8eb2f7b4e4b385998ae950278a` |
| candidate Core | `043e7076aab40d0aa09927b7e00432d3d32cb90885b19f65168c22a31a24dd35` |
| candidate Legacy | `e06cf825e7bef76aa798dd85d3ece5fcc7eb0e91abdb65d29e7f814d3a2509b1` |

这两个对照只验证来源与构建，没有对它们做本轮 UI 行为验收。它们不等于 NuGet 发布包的字节重现，也不证明 scheduler 候选安全。

随后单独构建 trace 宿主。首次编译因新 trace 文件的冗余 `using System.Threading` 报 IDE0005；删除该 using 后构建 exit0、0 warnings/errors。原失败日志保留。没有以构建错误为理由改引擎逻辑。

**实际运行 Legacy DLL**：`0182ad4dc51afc0e3f93c1913b5be4f39ec18e23c9770c4fd2f4af59f21bf14d`。PE 明确存在 `_trace49Instance`、`_trace49NextInstance`、`_trace49Generation`、`_trace49Sequence`、`_trace49Owner`，无 `_testDequeued/_testIdle`。见 [trace 元数据](metadata-trace.json)。**它是插桩诊断 DLL，绝不称为无插桩或发布候选。**

本地归档包版本统一为 **`0.0.1-preview6-trace49-9d66`**；Share/Core/Legacy pack 均成功。包内 DLL 与实际 layout DLL 字节相同，运行时导出也对 `typeof(DanmakuFrostMaster).Assembly.Location` 重算 hash。宿主仍使用 ProjectReference；没有把包从 feed 重新 restore 到另一个消费者，不能声称包消费验收通过。

[source-hashes.json](evidence/source-hashes.json) 记录每个非 bin/obj 源文件 hash 和排序 JSON manifest hash；[artifact-hashes.json](evidence/artifact-hashes.json) 记录程序集、包、截图路径/大小/hash及包内 DLL 比较。复放准备脚本已执行并逐文件比对精确相同。最初复放发现 git checkout 导致补丁混合换行/BOM 差异；脚本仅规范补丁生成的 Core 为 CRLF/no BOM、Legacy 为 CRLF/BOM，使其匹配**已经构建的源字节**，没有重写已运行产物。三个准备目录保留在 ignored artifacts。

## 3. Trace 的证据语义

[instrument.py](instrument.py) 仅增加观察点；[LegacyTrace49.cs](LegacyTrace49.cs) 每线程一个 32768 槽单写者环。不可变记录经 Volatile 引用发布，跨线程全局 sequence 用 Interlocked；后台 collector 读取并写文件。UI 的 ExportTrace 异步发起 collector，不等动画线程、不加状态屏障，不使用引擎 logger 文件写入。宿主原 FirstChanceException 的并发 AppendAllText 钩子已从副本移除，生产适配器保留 NullLogger。

记录包括 managed/native thread ID、Stopwatch ticks/频率、sequence、render instance、诊断 generation、mode、item ID/text、X/width、FirstRenderTime、初始化标记、动画 TotalTime/推导 age、paused/stopped 采样、RenderTarget managed identity、remove reason、HRESULT。generation 只在 Clear/Close 观察入口推进，**不是 scheduler 的代次，不参与生产判断**。target 是 RuntimeHelpers.GetHashCode 的 managed identity，不冒充 COM 指针或 GPU resource ID。

- Pause enter / flag write / exit，Start，Update enter/exit，UpdateItem，Draw enter/exit，Add/Remove，Clear/Close，CreateResources/device loss 和 catch HRESULT 均有记录。
- `DrawQueued` 在原 `spriteBatch.Draw` 返回后记录；`BatchDisposed` 在原 using 块退出后记录，关联同一线程、frame TotalTime 和 layer。它证明 CPU API 返回及 batch Dispose 完成，**不证明 GPU completion / compositor present**；不手动 Dispose DrawingSession。
- `ageMs` 是当前动画 TotalTime 减 FirstRenderTime 的诊断值；暂停时 Draw 的动画时间继续增长，不代表 Update 已修改 item。本轮 paused 窗口没有 UpdateItem。恢复时年龄跳变的兼容问题没有试修。
- 异常照原逻辑处理；包装 catch 只记录并 rethrow。未增加等待、播放切换、时钟修正或新的 render 所有权。
- 环不是全局原子快照；并发 snapshot 可能短暂遗漏未发布记录，所以分析只用卸载后完整导出，强制检查 sequence 从 1 连续到末尾、每环写入小于容量。两个 run 均完整、无 overwrite。分配和 Interlocked 会扰动时序，不能用未复现排除窄竞态。

没有进行运行后的 trace 增强或第四轮试修，没有制造屏障竞争。当前数据未证明需要 GPU 诊断，因此没有安装/探索 GPU 工具或未知 API。

## 4. 真实宿主与有界执行

独立离线身份 `BiliCopilot.Acceptance49Trace9D66_y1mf40g4r9srg`，本机获准的独占桌面。复用现有 harness-49 + candidate harness.patch，编译真实生产 VideoDanmakuPanel/XAML、VM、parser、session 与既有适配器；没有登录、网络视频、正式 app 引用/配置或 kernel 修改。

先逐张查看历史 Win2D frozen 两张原图。最初也将图片展示解读为白字差异；后续原 PNG 像素核验直接否定了这一解读，详见下一节。核对生产 panel：实时 VM 字符串经 SettingsToolkit 默认 `DanmakuLocation.Scroll=1`，转为 `DanmakuMode.Rolling=1`。本轮 runtime Add 记录再次证明 `CONTINUOUS MOTION` 是 Rolling，不能以 Top 的 3s 寿命解释它。

1. **PID38392，runID `cacb386bda334518810908c9c8a35a55`**：先单 rolling，再单 Top。均先在播放时截图看见文本，再 Pause；暂停两图间安排 3.5s 观察窗，非完成 fence。rolling 初始在右侧部分可见，并非完全进入屏幕后暂停。之后先采无 Sample 的简化原流程。全部 11 张截图逐张查看。
2. 为避免简化流程缩短采样时间，再用相同 DLL、**新 PID22196，runID `27335651d73042728cb4c4f1aa25dffe`**，执行原 `visual.ps1` 的 Win2D `Exact→SeekForward→Pause→Sample→Motion→Sample×2→FreezeMotion→Sample×2` 子序列。暂停进度达到 **22s**，与历史失败样本一致。5 张截图逐张查看。没有复跑原流程前面的 DirectX、双向切换失败矩阵；这仍是条件限制。

两次脚本各 exit0，仅代表采集完成，不标记修复 PASS。采样结束通过生产 panel UnloadQueued 路径观察 Close 后再导出。累计 events 文件按 runID/PID过滤；第二个目录中复制来的旧 trace-2/3/4 **不作为第二 run 证据**，其有效文件明确为 trace-1.json。

## 5. Item 级结果与没有闭合的历史分支

### 历史截图证据更正

对历史 `Win2D-frozen-1.png` / `Win2D-frozen-2.png` 用 System.Drawing 只读解码，白字所在区域 `x890..1309,y390..444` 均有 **4482** 个 RGB>150 的白色像素，RGBA SHA256 均为 `4afe935975b7eeb9207f099d343b4bb5d7ed99db3d51e92a48dce6dd8453e2c8`。整个渲染区 `x20..1359,y320..799` 的 RGBA SHA256 也相同：`67237563bbb14751d483ba4b455e71a3bde1fc2391cbd54785fe139fc97ac9c8`。两个区域所有 alpha 均为255，不是透明度差异。两文件各自 hash 与原候选工作区的文件匹配，未更改历史图片。

因此应撤回候选 README 和审查第4节中“这两张图确认白字消失”的证据解释；本轮最初同样的视觉解读也在这里明确纠正。文件内容的逐像素证据优先于不一致的展示解读。**不能据此确定图片查看链路哪个环节出了问题，也不能证明采样之间或其他运行从未发生消失。**不修改旧报告原文，不扩展到未经复核的其他截图结论。

可复核命令：`./docs/performance-acceptance/engine-49-trace/historical-pixels.ps1`，结果见 [historical-pixels.json](evidence/historical-pixels.json)。首次临时命令误将 PathInfo 传给 Bitmap 构造，属于诊断脚本错误；最终脚本用 `.Path` 且 `ErrorActionPreference=Stop`，上列数值来自成功重跑，不采用失败输出。

### 本轮动态记录

| 场景 | 观测和原始 sequence |
|---|---|
| 单 Rolling，item1 | PauseExit 590 到 ClearEnter 2565，约5.467s；329次 DrawQueued/batch完成，X固定1086.4017，target48319561；无 UpdateItem/Remove；直到下一用例 Clear 才 Remove2566 |
| 单 Top，item2 | PauseExit2979 到 ClearEnter4948，约5.450s；328次 DrawQueued/batch完成，X固定565.85547，target45833318；无 UpdateItem/Remove；下一用例 Clear 才 Remove4949 |
| 简化原流程白字，item6 | PauseExit7280 到 CloseEnter8388，约2.038s；123次 DrawQueued，X固定1028.8021，target14448300；卸载 Remove8391 |
| 包含原 Sample 的白字，item3 | Add1160（mode Rolling）；PauseExit2539 到 CloseEnter3448，约2.144s；130次 DrawQueued，X固定877.60315，target39852431；最后 Draw3445→BatchDisposed3446；卸载才 Remove3451，reason=clear-layer |
| 同一复放的 EXACT TOP 20 | Remove2017，paused=false，reason=duration，age3016.6546ms；发生在最后 FreezeMotion 之前。不是暂停白字被删除 |

两 run 分别 8394、3453 条连续记录；无 Exception/DeviceLost/Unhandled trace。原始选择记录见 [run-records](evidence/run-records.json)、[replay-original-records](evidence/replay-original-records.json)，分析脚本 [analyze.py](analyze.py) 可从完整 ignored trace 重算计数和窗口。

**三分支判别：**本轮暂停窗口没有“列表已删”；有持续 item 级 DrawQueued 与对应 BatchDisposed，因而也不是“仍在但未提交到 batch”。本轮没有白字消失，历史那两张图也不支持消失，故没有证据建立“已提交未显示”分支。所报告症状的实际发生条件仍未确立，不能把怀疑升级为 GPU/driver/compositor 根因。

图像复核没有只凭文本描述：本轮16张均逐张 view_image，并对原 PNG 只读比较。单 rolling/Top、简化 frozen 两张各自文件 SHA256 相同；原 Sample 复放两图状态序号不同，整个文件 hash 不同，但渲染区 `x20..1359,y320..799` 的 RGB SHA256 完全相同，均含4528个白色阈值像素。见 [pixels.json](evidence/pixels.json) 与 [pixel-check.ps1](pixel-check.ps1)。不编辑图片，也不以像素一致外推长时正确性。

逐图对应：`run/single-rolling-before` 右侧部分可见；`single-rolling-paused-1/2` 同一位置；`single-top-before/paused-1/paused-2` 居中保留；`run/original-exact-pause-after-seek` 有目标Top/右侧rolling；`original-motion-1/2` 向左运动；`original-frozen-1/2` 保持。`replay-original/Win2D-exact-pause-after-seek` 有目标；`Win2D-motion-1/2` 黄/白rolling向左，Top在播放阶段过期；`Win2D-frozen-1/2` 黄字与白字均保留。右侧裁切是 rolling 进入过程，不签署完整 UI 布局验收。

截图、捕获 UTC 区间、runID、PID、DLL hash、PNG hash 都在 [run-summary](evidence/run-summary.json) 和 [replay-original-summary](evidence/replay-original-summary.json)。这是同 run/action 窗口关联；没有 compositor frame ID，不宣称每张截图精确匹配某一 GPU frame。

## 6. 限制、复放和交付

**原候选仍不可发布。**本轮不改变 paused-seek 目标选择/初始化、Close完成语义、active-set重建、列表别名/Stop/Clear/Restart兼容或 render ownership 结论。这些仍需后续具体设计。当前诊断仅在确定的单项/离线/短时条件中未复现；未覆盖历史全套操作前史、未插桩宿主动态对照、极端密度、真实视频时钟、多生产者、设备丢失或 crash dump。没有强迫竞态或高频重复运行来“找到绿”。

Native AOT 已知工具链 blocked，未重试。WinUIAnalyzer 未安装/运行；源自带 analyzers 保持启用且构建通过。正式应用没有改动，未重跑403全套；原26 scheduler屏障测试也未重跑，未引用成 fresh 结果。

复放准备（必须新目录；不要覆盖本轮证据）：

```powershell
$archive = 'C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-c9d5219a/artifacts/engine49/source/upstream.zip'
./docs/performance-acceptance/engine-49-trace/prepare.ps1 -Archive $archive -Destination artifacts/engine49-new-run
./docs/performance-acceptance/engine-49-trace/build-controls.ps1 -ArtifactRoot artifacts/engine49-new-run
./docs/performance-acceptance/engine-49-trace/metadata.ps1 -Root artifacts/engine49-new-run/original
./docs/performance-acceptance/engine-49-trace/metadata.ps1 -Root artifacts/engine49-new-run/candidate
./docs/performance-acceptance/engine-49-trace/build-trace.ps1 -ArtifactRoot artifacts/engine49-new-run
```

准备脚本不下载归档；归档不在时需取得同 SHA256 文件。UI脚本固定到本轮目录/身份作为实际运行记录，复放前须改成新输出目录和新独立身份，并使用新 owned PID；不可复用报告里的 PID。启动方式是 project-mode `winapp run <Harness.csproj> -c Release --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory <new-layout>`，启动前设置对应隔离缓存。先 metadata 再 launch；保持版本明确为 trace。包/程序集/完整环 JSON 保留 ignored，不提交大二进制；可按脚本与源 hash 复放，不承诺不同路径上的程序集字节完全相同。

最终 [cleanup.json](cleanup.json)：owned PID38392/22196 均通过 Close 退出，remainingOwnedProcesses=0；核对精确 InstallLocation 后移除独立包，remainingPackages=0；最后同 workflow `engine49-trace-9d66` yield exit0、released=true，之后无桌面操作。PID9608、Dev/生产包、凭据未操作。正式 src/externals 相对父基线无 diff，kernel gitlink不变。

提交仅本目录诊断报告、脚本、小原始证据和元数据；历史文档仅通过 merge 保留且 diff为零。提交前同步最新本地 feat/performance-45；最终 SHA/clean 在交付消息报告，不自嵌提交哈希。
