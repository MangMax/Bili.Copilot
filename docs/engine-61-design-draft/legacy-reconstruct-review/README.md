# 固定候选独立 review

审查候选 `e30c8240533d52e30e0216dc34c2c11209d6b90f`，父 `29e266884b9459b4d5a0f62a4d76482950cefdb1`；引擎 bundle tip `d7984643f4792133e7664c55e01df6e95e9cbc21`。本区 fast-forward 候选，结束合本地 `feat/performance-45`（仍为上述父，Already up to date）。没有子代理、生产修改、push、PR 或 issue 操作。

**结论：没有发现可实证的阻断缺陷，可以采用该隔离 Legacy 四模式目标时间重建切片。不能据此切换正式依赖或宣布双引擎应用包完成。**

## Standards

无阻断项。差异只有三个隔离源码覆盖文件和证据；无新增 XAML、公开接口、同步 UI 等待 API 或诊断钩子。当前实现复用已有锁序、文本测量、密度和槽位方法，未引入全历史或 owner 架构。按照本次明确要求，implement-spec/code-review 的代理编排没有执行，两个审查维度由本 review 独立完成。采用 WinUI-code-review、run-tests、verification；新增四个小反例采用 code-testing 的 focused 路径。测试 overlay 和 code-testing-extensions 未提供，使用现有自有 console runner；没有运行 WinUIAnalyzer，也不声称 analyzer/AOT 通过。

成本说明：Seek 扫描从列表开头到目标的全部前缀，并对每个出生点推进当前存活项，还为符合过滤条件的历史出生项做文本测量、位图创建及释放。不是 O(N) 的轻量索引搜索：时间约为 O(N + Σ当前存活数 + 文本/native 成本)，零速且不限制密度时可退化至 O(N²)。操作持有 list/playback 锁，UI 发起 Seek 时也可能承担这段耗时。本轮未做实机性能测量，没有足以阻断本切片的新严重性能回退证据；保留 perf 未签绿，不能宣称耗时有界。无需为此重新设计历史存储或全面调度架构。

## Spec

无阻断项，采用范围严格为真实 Legacy 的 Rolling / ReverseRolling / Top / Bottom。

| Requirement | Evidence |
|---|---|
| 年龄为 target−StartMs，不改出生时间 | 出生顺序逐段推进，累计 ReconstructedAgeMs；首次 Update 用 TotalTime−age 建立基点。StartMs 只读。原 `CallerStartMsUnchangedAndFutureExcluded` 历史结果核验；当前 23 项集合/API 回归保持容器/条目别名。原 Render 修改 TextColor alpha 的行为仍存在，不能扩大成“调用者对象完全不变”的承诺。 |
| 暂停墙钟排除、Resume 剩余寿命 | Update 在 paused 且已设基点时累加 ElapsedTime 到 FirstRenderTime；滚动 elapsed 为0。Resume 不清年龄/坐标。历史 `PausedAgeAndFrameStable`、`ResumeExpiresWithoutAgeReset` 和三个模式 Resume 剩余寿命核验；本轮没有冒充新的 native 时间证明。 |
| 同目标、连续 seek、future、0ms | 每次 Stop/Clear 后从头重建，出生条件 <=target，最后推进至 target；目标0不会被无请求哨兵吞掉；not-ready 保留最新目标。历史 `SameTargetOverflowDeterministic`、`ContinuousSeekLatest`、`BeforeReadyLatestSeekRestoresOld`、`SeekZeroIncludesZero` 核验；重跑 queue/close 边界命令。 |
| 当前配置、无固定回看窗口 | 原 Render 当前字号测量，Advance 使用当前速度；四种 enabled 开关保持过滤。`ZeroSpeedLongAgeAndNegativeSpeed` 本轮直接验证 uint.MaxValue 年龄未溢出/未按窗口清除、负速方向保持。零负速不等于屏内可见。 |
| 密度与槽位、精确边界 | 出生前推进并释放，仍调用原密度分支/GetY；只有重建溢出采用确定性 ordinal。新增 `TopExactBoundaryThenOneMillisecondReleasesSlot`、`RollingStrictExitAndReverseInclusiveExit`、`EntranceReleaseDoesNotReleaseNewOwnerAtOldExpiry` 全通过，后者验证旧滚动项退出不释放新 owner 的槽位。 |
| 普通播放 | 两参数 Render 委托 reconstructing=false；正常 worker 仍走原入口、age默认0、随机溢出保持。普通递增时间未新增重建，既有大跳/回退仍进入 Seek。原 Set/Get/Add/Clear 和 IsRealtime 普通路径不改。 |

输入依旧要求按 StartMs 排序；不承诺外部并发乱改列表、配置 setter 自动重播、所有 logger/native 重入、device reset、跨独立图层互不遮挡。

## 本轮执行与历史核验

- **本轮执行** `python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/audit.py`：459/459 文件 SHA256、ZIP 原字节及原像素复算通过，保留 red 8/10、extended 17/1。
- **本轮执行** `python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct/compat.py`：`Passed: 23, Failed: 0, Skipped: 0`；公开 API 与冻结基线逐字节一致。7 条 boundary 命令 reentry/closereentry/throwstop/throwclose/queue/close/stop 全 exit0。stop 的 stoppedAfterSeek=false 是已记载旧限制，未声称修复。
- **本轮执行** `python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct-review/run-independent.py`：`Passed: 4, Failed: 0, Skipped: 0`。直接加载封存 final DLL，用无 native 对象的托管 seam 调用真实 AdvanceReconstruction/槽位方法，不复制算法、不证明 native 生命周期。
- **本轮重算历史产物** `python -X utf8 -B docs/engine-61-design-draft/legacy-reconstruct-review/verify.py`：35/35 应用/像素/包/退出断言成立。这是重新计算原始记录，不是重新运行应用35项。
- **历史执行、本轮核验** 主 native 18、扩展21、暂停回归12、early-close5均0失败；未重跑整套 native 桌面。历史23项已另外在本轮重跑。无生产变更且源码/最终包/像素/退出闭包齐全，没有新增 native 启动的必要。
- 独立夹具初版正常构造 DanmakuItem 触发 Colors WinRT 激活失败 `REGDB_E_CLASSNOTREG`，exit 3762504530；日志保留。改成同现有托管夹具的未初始化模型后四例通过，未修改生产代码。verify 初次路径重定位有重复 artifacts 前缀，修正脚本后35项通过，未改原记录。

## 包、源码和图像闭包

bundle verify、独立 clone 后 candidate.patch 反向 check 均通过。三个覆盖文件与 source ZIP 字节一致，ZIP 全文件 hash 通过。最终实际执行 DLL SHA256 `2cc03ebaf8557cbf0abb96344276af717ba7a756541e0223a29a353313adfabf`，MVID `8d1cd8ca-75b9-42fb-b36a-dd25a267a4b8`，版本 `0.0.1-reconstruct0b4f.final`；本轮兼容夹具重新验证 Legacy 7份、Share 9份 PDB 文档源 hash 和 PE/PDB 配对。Core是从ZIP本区编译的集合对照，不是新 Core native 验收。

重新读取最终 nupkg，复算 closure 所列 NuGet cache/native layout/应用 build/layout2 DLL hash，与封存 native Loaded 和应用 Modules 路径/hash相符。仅只读来源区文件，未写来源。使用已有 NuGet cache，不是干净机器 restore。

21张 native、18张应用原图的固定区域重新解码；重新执行空白和不同文本负例以及 future 独立行比较。查看原尺寸 native-contact/app-contact 局部图：TOP/TOPSTABLE 一致，BLANK为黑，OTHER与TARGET不同；三轮 PRIOR19 一致，卸载负例空白；21秒 FUTURE21 与返回20秒的负例在独立行断言成立。没有用大图“看起来消失”推翻原像素。原图没有改写。

历史最终进程 retained exit0、nonforced、退出≥31秒 WER精确范围无匹配、卸载/yield记录复核通过；应用 PID8200 路径位于来源 owned layout2，WER 31.019秒。这里只验证已封存记录，不声称本轮重新观测31秒窗口。没有启动、操作或清理其他桌面实例，没有访问凭据、dump或外部内存。

## 下一步实际双引擎应用组合缺项

本候选完整应用仍是 **Core正式preview6 + Legacy final + Share final**，因此还缺父方选定的已审 Core候选与本Legacy候选在同一完整应用中的真实包组合。下一步应固定该组合的 Core/Legacy/Share版本、二进制与PDB/source/加载来源，做组合特有的双引擎切换/暂停seek/卸载重挂/关闭验收及owned退出记录。无需再泛泛重开本片四模式年龄、已有全矩阵或已纠正的视觉误判。

历史 AV/c000027b/UNKNOWN、device-reset、AOT/perf 和 Core完整native矩阵仍保留原限制；本报告不把它们签绿，也不据此要求无关架构扩展。正式依赖与 gitlink `0ae5e4e` 未改。初始 kernel `f02c33f` 的 HEAD/status/staged/worktree二进制diff摘要前后相同，474项异常记录保持原样。

审查交付只包含本目录和小证据；详细执行日志位于本区 `artifacts/reconstruct-final`，原始提取证据位于 `artifacts/review-e30`。`verify.py` 需要先将候选 evidence.zip 解包到该目录；独立 runner 需要候选源码ZIP解到 reconstruct-final/source、最终feed解到 reconstruct-final/feed，并先执行 compat.py。所有这些路径均在本区，不能把脚本用于覆盖来源工作区。
