# Legacy 目标时间重建：隔离源码、真实包与应用验收

基于父 `29e266884b9459b4d5a0f62a4d76482950cefdb1`，结束再次合入本地 `feat/performance-45`（仍为该提交，Already up to date）。本片实现目标前仍活跃弹幕的年龄、位置及剩余寿命重建。**交父方安排一次独立 review；本实现者没有启动子代理，也不自行签署独立通过。** 正式依赖、应用源码、父工作区及 kernel gitlink 未改，无 push/PR/issue 写入。

最终完整运行：主原生 **18/0**，扩展 **21/0**，旧暂停回归 **12/0**，early-close **5/0**，集合/API **23/0**；均 Skipped:0、runner exit0。另35项像素/应用/包/退出验证及459份封存文件离线audit通过。不是 #49/#61 全生命周期结案。

最终源码提交在独立 bundle 中为 `d7984643f4792133e7664c55e01df6e95e9cbc21`。恢复顺序：校验上游 archive 的45份文件 → `legacy-ready/source` 两文件 → `legacy-early-close/source/DanmakuRender.cs` → 本区三个覆盖文件。完整原字节源为 [candidate-source.zip](candidate-source.zip)，历史为 [candidate.bundle](candidate.bundle)，审查差异为 [candidate.patch](candidate.patch)，来源和逐文件 hash 为 [source.json](source.json)。bundle 使用上游 `.gitattributes` 的文本换行归一化，ZIP/hash 保留实际打包输入字节；bundle verify、补丁反向 check 均通过。

## 实现与真实支持范围

- Legacy **Rolling、ReverseRolling、Top、Bottom** 有实际 Update/Draw 分支；Advanced/Subtitle 只有部分准备字段、没有 Draw 分支，Unknown 被过滤。本片只为四个实际绘制模式重建，未声称高级/字幕模式通过，也未把 Core 的静态模式语义搬来。
- Seek 停止旧画面后，按既有排序列表的出生顺序推进重建时间。在下一个出生前推进现存条目、释放过期/已离开入口的槽位，复用原文本测量、密度过滤及分槽，最后推进至目标。只使用调用者当前列表，不保存播放历史；没有固定回看窗口，没有 owner 框架。
- 年龄为目标减 StartMs，不改调用者 StartMs。滚动使用当前字号测得的宽度和当前速度，退出条件保留 Legacy 原值：Rolling `X < -Width`，Reverse `X >= CanvasWidth`。Top/Bottom 默认3000ms或正自定义寿命，严格 `age > duration` 删除。首次 Canvas Update 从目标年龄建立时间基点，暂停排除墙钟，Resume 继续剩余寿命。
- 普通播放走原两参数 Render 和原更新分支，重建年龄默认为0。只有重建时、槽位不足但允许溢出，使用出生序号确定 fallback Y；普通播放原随机 fallback 不变。
- explicit Seek 在初始 ready 前保留目标；ready 后原 worker 处理，最新目标覆盖旧目标。出队以 `hasUpdate` 区分无请求和0ms目标。原暂停意图、list→playback 锁序、logger 锁外 Stop/Close 回调和 early-close 精确移除保留。Set/Get/Add/Clear 算法及别名契约未改。

输入仍须遵守原 SetDanmakuList 的按 StartMs 排序合同。调用者并发替换/乱改列表、device-reset、所有任意 logger/native 回调交错不在本次证明范围。重建全列表是有成本的，未作 perf/AOT 或耗时上界承诺；跨图层仍沿用原独立槽位，不保证高密度混合模式互不遮挡。配置 setter 本身没有新增自动重放；后续 Seek/应用 redraw 采用当前配置。

## 原生红绿与要求覆盖

正常构造真实 CanvasAnimatedControl，公开 master API 驱动；反射只读 renderer/条目，未替代 native 构造，未加生产测试钩子。同一份18项 Program.cs 原字节相同：基线（含 ready/early-close）**8 PASS / 10 FAIL** → 最终包 **18 PASS / 0 FAIL**。扩展第一版 **17/1**，唯一红项为溢出随机位置；同18项最终 **18/0**，再加三个模式 Resume 寿命断言后 **21/0**。

| Requirement | Evidence |
|---|---|
| 目标前 Top/Bottom 可见 | `PreTargetTopVisible`、`PreTargetBottomVisible`；源码红→绿，原像素均有 TARGET |
| 不改 StartMs，未来项不提前 | `CallerStartMsUnchangedAndFutureExcluded`；应用 `AppFutureGlyphOnlyAtBirth` |
| 暂停稳定与 Top 剩余寿命 | `PausedAgeAndFrameStable`、`ResumeRetainsRemainingLifetime`、`ResumeExpiresWithoutAgeReset` |
| 双向位置、同目标重绘 | `RollingRestoresPosition`、`ReverseRollingRestoresPosition`、`RollingSameTargetRedraw`、`ReverseRollingSameTargetRedraw` |
| 当前速度、字号、无有限回看 | `RollingSpeed0/2.5/10/-5`、`ReverseRollingSpeed0/2.5/10/-5`、`CurrentFontRebuildsWidth`、`RollingZeroSpeedNoLookback`、`ReverseRollingZeroSpeedNoLookback`；零/负速是状态/坐标验证，不冒称屏内可见 |
| 四模式到期 | `ExpiredItemsExcluded`、`CustomDurationExpires`、`ExactDurationStillVisible`、`BoundaryExpiresOnResume`、`RollingOldExpired`、`ReverseRollingOldExpired`、`RollingResumeExpiresRemaining`、`ReverseRollingResumeExpiresRemaining`、`BottomResumeExpiresRemaining` |
| 目标0、目标等时、过滤、槽位释放 | `SeekZeroIncludesZero`、`ExactTargetIncluded`、`DisabledTopFiltered`、`ExpiredSlotReleasedBeforeBirth` |
| 稳定溢出、不同文本、连续 latest | `SameTargetOverflowDeterministic`、`DifferentTextReplacesOld`、`ContinuousSeekLatest` |
| 未 ready 的连续显式 seek | `BeforeReadyLatestSeekRestoresOld`：ready 后仅最新21000，保留19000旧项年龄，仍暂停 |
| 原暂停/队列/Close | 原 `Regression.cs` 12/12，包括 `DequeuedSeekThenPause/Resume/Close`、`PausedRepeated_RemainsPaused`、`CloseThenResume_RemainsClosed` |
| early-close 精确移除与兄弟保留 | 原 `EarlyClose.cs` 5/5：`ImmediateDetach`、`RepeatedEarlyClose`、`EarlyCloseAfterPump`、`NormalLoaded`、`NormalCloseAfterPump` |
| 集合别名、API、包 PDB | 原 C1a 23/23；冻结 public-api 逐字节相同，Legacy 7份/Share 9份 PDB 文档校验通过（Core作为原集合对照，不是Core native验收） |
| logger 重入、异常、队列 | 原边界七命令 `reentry/closereentry/throwstop/throwclose/queue/close/stop` exit0；其中 `DequeuedThenStop` 仍保留 stopped=false 旧限制，不冒签已修 |

## 真实包及完整应用

最终唯一版本 **`0.0.1-reconstruct0b4f.final`**（Legacy+未改源码Share）实际由所有最终宿主和完整应用 PackageReference 消费。本地 red/clean 是分开的因果实验版本，未覆盖旧包，没有发布。

Legacy DLL SHA256 `2cc03ebaf8557cbf0abb96344276af717ba7a756541e0223a29a353313adfabf`，MVID `8d1cd8ca-75b9-42fb-b36a-dd25a267a4b8`。nupkg、NuGet cache、native layout、应用 build/layout2 逐字节相同；native Loaded 日志与 owned 应用 Modules 路径/hash均匹配，详见 [closure.json](closure.json)。共用现有 NuGet cache，不宣称干净机器 restore。

完整应用来自固定 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`；903份生产 C#/XAML 中901份按去BOM/换行后相同，只有既定 App.xaml.cs / GlobalDependencies.cs 离线启动注入。额外 OfflineAcceptance 提供合成账号、本地媒体、19秒 PRIOR19 与21秒 FUTURE21，未读取真实凭据。身份 `Bili.Reconstruct0b4f`，Root/layout 都在本区。Core仍正式preview6。

最终 runtime2 完整三轮 x:Load 卸载/重挂，目标20秒、始终暂停；每轮新master、4张固定时点图，PRIOR19均一致。三张卸载负例区域全黑。真实 ForwardButton 到21秒才出现 FUTURE21；BackwardButton 回20秒又只有 PRIOR19。`AppPausedRemountStateRound1/2/3`、`AppPausedRemountVisibleRound1/2/3`、`AppUnloadBlankRound1/2/3`、`AppDistinctMasters`、`AppActualButton_forward1/backward1`、`AppCloseDisposes` 均通过。

使用固定几何原像素：native 1282×891，Top/滚动 `(450,290,380,65)`，Bottom `(450,765,380,65)`；应用1680×1110，PRIOR19 `(710,18,260,40)`，未来字样另核对 `(710,58,260,25)`。System.Drawing 和独立 Python PNG 解码一致。21张 native 与18张最终应用图均以未缩放局部检查，[native-contact.png](native-contact.png) 左/中/右为red/clean/final，[app-contact.png](app-contact.png) 按原文件名排序。TOP/TOPSTABLE原像素相同，BLANK和OTHER负例不能通过TARGET等值检查。未采用 rows-only 或大图视觉猜测。

所有最终原生和应用进程 retained exit0、非forced；退出后 ≥31秒精确PID/path的Application Error/WER无匹配。owned包已卸载、进程退出、desktop yield；不触碰Caa6a7be/Dev/其他实例。仅普通源码/UI/事件日志，没有dump、外部内存或退出逆向。

## 保留失败与测试边界

- 首个生成脚本因Python默认GBK读取中文源码失败，随后产生一个未启动的 `green` 基线构建；该产物不作绿色证据。后用明确UTF-8及实际补丁完成候选。
- 原18项基线10红、第一版溢出1红全部封存。初次旧托管 Boundary 使用未初始化对象、layers=null，在新 replay 路径抛出 NullReferenceException，进程返回3221225477；保留原异常和返回值。修正夹具为空层数组后七项通过，未据此修改正常构造生产代码，也不将该托管夹具当native生命周期证明。
- 完整应用首次启动夹具先于 identity.json读取、随后无标题过渡窗口触发 KeyError；保留命令。第一次完整矩阵三轮成功、前跳成功，后退按钮自动隐藏导致 element_not_found，正常关闭；未把此轮当完整通过。runtime2在后退前调用原 ShowControls，完整一次完成，截图不筛选或重写旧记录。
- 初版应用局部高95像素包含媒体彩条，负例仍可区分但不全黑；随后将文字区域固定为40像素，保留两份报告，不修改PNG。图片呈现工具曾将相同TOPSTABLE像素显示为空白，最终以独立解码和未缩放拼图核验；没有重修已纠正的旧visibility问题。
- 历史AV/c000027b/UNKNOWN、device-reset、Core完整矩阵、AOT/perf仍未签绿。未安装/运行WinUIAnalyzer；本次构建、定向native/应用验收不替代那些范围。

## 复放与 review 入口

使用指定 implement-spec、tdd、code-testing、run-tests、systematic-debugging、WinUI review/UI testing、verification-before-completion。用户已授权公开引擎及真实应用测试 seam；未重新索取许可。run-tests overlay/测试扩展目录未提供，使用原自有JSON/WinApp runner，不误用dotnet test。WinUI自审：无新增XAML/公开API/同步UI等待；重建共用现有锁和native方法，正常入口不取新锁。重复的短模式分支是对现有更新语义的局部复用，没有扩大为策略/owner体系。

```powershell
# 在新的工作区/空 artifacts 路径重放；prepare 不启动桌面。
python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/prepare.py red --red
python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/prepare.py final
& artifacts/reconstruct-final/run.ps1 -RunName native
python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/prepare-host.py lifetimes Extended.cs final
& artifacts/reconstruct-lifetimes/run.ps1 -RunName native
# Regression.cs / EarlyClose.cs 同样 prepare-host，再执行各自 run.ps1。
python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/compat.py
python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/audit.py
```

实际执行日志、所有源码红绿结果、最终包、UI脚本/原图、退出/WER/cleanup在 [evidence.zip](evidence.zip)，[evidence-manifest.json](evidence-manifest.json)逐文件校验。`audit.py`离线验证封存结果与像素，不启动桌面。完整应用复放用 `prepare-app.py`，将本区 launch-retain.ps1/ui-app-screen.py 复制到 `artifacts/reconstruct-app`，以 `-Run runtime2 -Layout layout2` 启动；matrix后close，保留exit/WER，再按精确identity卸载/yield。应用媒体与kernel的只读来源依脚本固定路径；不是可携带的通用安装包。

本目录禁止Git换行转换以保持记录hash。原夹具、像素脚本及patch上下文保留实跑字节（含既有尾空白）；`diff --check`对此有提示，没有为消除这些提示重写原证据。

初始kernel异常 HEAD/status/staged/worktree二进制diff备份在 ignored `artifacts/legacy-reconstruct/kernel-backup`；没有untracked文件。结束逐字节比较不变，[kernel-unchanged.json](kernel-unchanged.json)记录hash，gitlink仍`0ae5e4e`。仅本docs目录进入提交。请父方以本候选补丁、源码ZIP、最终同源包和封存红绿证据交一次独立审查，重点核对重建边界、原契约保持及真实包消费。
