# Final runtime baseline — bounded ABBA

2026-10-31：四轮同源 net10 / net11RC / net11RC / net10 完成，**4/4
PASS_BOUNDED、732 个新 v3 样本**，未补跑。没有生产源码、依赖声明或父分支改动。
本次仅完成离线 Player/Main 后的 warmed Settings 基线；**perf HOLD 保留**。
可见/恢复 CPU 区间重叠，背景活动及 observer 尖峰无法排除，不能声称总体提升、
显著性、纯 JIT 因果或内存泄漏。#49 FAIL、AOT BLOCKED、历史 net9 crash 未归因、
#56 预算/任意大重排限制不变。RC 继续是主候选，net10 仅为对照臂。

[重放与验证说明](runtime-final-baseline/README.md) ·
[原始字节账本](runtime-final-baseline/ledger.json) ·
[完整分析](runtime-final-baseline/raw/analysis.json.gz) ·
[全部逐样本 CSV](runtime-final-baseline/raw/samples.csv.gz) ·
[逐轮 CPU 图](runtime-final-baseline/results.html)。

## 预先固定的协议

Source parent: `76af64717dc7bd0e95e1d34c8622d5e8cad48805`; production
`334d47c78e04bebe836f6a8191b16859e6343843`; kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b` (local-only).
This is an offline full-source fixture comparison, not a change to the RC candidate.

Protocol recorded before builds or runs on 2026-10-31. Exactly four planned runs:
**A1 net10, B1 net11RC, B2 net11RC, A2 net10**. No second sequence and no
direction-dependent retries. Fresh identity/data per process, warm OS cache not
controlled; no cold-start measurement. No forced GC or cache clearing.

Each arm receives a complete source archive of the same parent and kernel,
same offline host substitutions, final production timer/exit/foreground fixes,
and NotifyIcon 2.4.1. Only pinned SDK/TFM and required framework assets differ.
SDKs 10.0.403/runtime 10.0.14 and 11.0.100-rc.3.26522.148 are reused locally.
No install, default SDK change, production optimization, or API migration.
Build serially and freeze immediately; retain source, package/assets, DLL and
loaded-module hashes. Compare the complete package graph, including selected
TFM assets. This cannot isolate pure JIT effects.

Replay uses existing Player15, Main4, settings visible/hidden/restored and 20
hide/restore cycles, followed by font-first-display and explicit exit with a
live player. Existing list/undo coverage remains separate: adding that path to
this measurement would change workload and is deliberately out of scope.
Observer is the existing external-os-v3 process-snapshot sampler: 61 readings
at monotonic one-second deadlines per state, after five-second warmup and
two-second settling. Atomic markers verify PID, HWND, DPI and visibility;
fixed main-window size/position is identical in both arms and archived.
No Process counter sampling on the target dispatcher. Old 183-row runs remain
historical and are never pooled with these fresh final-source runs.

Report CPU milliseconds divided by actual first-to-last wall seconds, plus
private bytes, working set, handles and threads as proxies. Report every run
and state, medians and observed min/max ranges, raw counters and observer wall
overhead. Record background process CPU changes without stopping other tasks.
No significance, population improvement, native-allocation or leak inference.

Gate every run on Player15/Main4/cycles/font, exact PID/path/creation time,
retained handle exit code zero, Application1000/linked or unresolved WER, and
last query both started and finished >=30 seconds after observed exit. Any
native failure is retained, excluded from performance, and stops that arm;
no repair or replacement run. Failed readiness, graph/source inequivalence,
unresolved interference or missing evidence prevents comparative claims.
Do not run a dependent B2/A2 after either arm has failed: the comparison is
then incomplete and remains HOLD.

Only new task-owned offline identities may be registered. Never operate on
Caa6a7be/layout-v3, Dev, PID9608, login data or old outputs. Screenshots target
only owned windows; inspect every retained image. Cleanup records owned app,
observer and registration absence and exact UI yield result. No unrelated 487
suite, push, PR or issue update. #49 FAIL, AOT BLOCKED, historical net9 crash,
#56 budget and arbitrary-reorder limits remain unchanged.

## 实际来源、构建与框架资产差异

生产 `src`/global.json 与 `334d47c` 相同；父 tip 仍为 `76af647`。两臂分别归档
1545 个完整源码输入、1029 个 fixture 输入；同一套最终 timer/退出/托盘前台修复。
UI host 仅沿用既有离线服务、入口、实例键和字体枚举计数替换，加上相同的主窗几何
及外部采样边界。不是完整认证启动链。没有复用旧 net10 可执行产物。

源码两臂差异仅 global.json 和 UI、Visor、Visor.Models 三个项目的 TFM；fixture
差异仅 global.json 与 UI 项目的 TFM/本臂绝对引用路径。其余 fixture 输入逐字节相同。
对生产源码的验证允许旧 preparation 对顶层文本新增末尾换行；原始字节 hash 仍保留，
不将换行规范化后的相等称为原始字节相等。

| Arm | SDK / runtime | NuGet packages | Frozen files / DLLs | Build |
|---|---|---:|---:|---|
| net10 | 10.0.403 / 10.0.14 | 138 | 897 / 359 | exit0，1548 warnings，0 errors |
| net11RC | 11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148 | 134 | 901 / 363 | exit0，1548 warnings，0 errors |

精确版本 SDK 从本机现有安装读取；没有安装或全局默认变更。串行 build 后立即 freeze，
另一个臂才开始构建。完整图及 SHA256 见
[preflight](runtime-final-baseline/raw/preflight.json.gz)，原始 project.assets.json
另有逐项目 raw-byte gzip，不只保存摘要。两个 package graph hash 分别为
`ae7a63ab35a007102f944ff7ec441e603f91637614c86c31c064db027eb83288`、
`ae16916e5b12892fa2dfcbe4b61e0df73cab8362cc5340b23cd6c920218f3abb`。

**不能把两臂都写成 134 包。** net10 额外四包均为 10.0.0：
Microsoft.Extensions.DependencyInjection.Abstractions、Logging.Abstractions、
Options、Primitives。RC 的 `project.frameworks.*.packagesToPrune` 明确包含这四项，
上界为 11.0.0-rc.3.26522.148；对应依赖边被裁除，由框架资产参与解析。
共同的 134 包 library 元数据/hash 完全相等。两个 target（普通、win-x64）共
28 项选择/依赖边差异完整归档，包括这四个增减；并未额外回退应用包。
这是一组 **SDK + TFM + 必要框架资产** 的比较，不能归因为纯 JIT。

每个进程实际加载的 coreclr 与本臂 freeze 一致：

- net10：`1bc65b8d45474b19adf3ca85ce881087aff599ffc72b2c1a323e878227757b65`。
- RC3：`46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`。

NotifyIcon 2.4.1 实际 DLL 两臂相同，SHA256 为
`5f9befc63a787908a95bc216ddf28f97e97ca06cfdced829edd70e706f6ac1d1`。
UI.dll 则分别为 `06f9e8213506f3bdcc7ef664de9ac5af37fbb503b1a49475734b40af049a6536`
和 `9b8af587cf4a2142aa8f66cad7e03ed2d862d2e69edcd941102feb615d1beef5`。
逐轮模块快照及路径/hash 全部保留；这是快照，不是全生命周期模块跟踪。
两臂 1798 个冻结文件全量复核、四轮 1444 个部署 DLL 双向集合/hash、602 个包内加载
模块 hash 复核通过，见 [verification](runtime-final-baseline/verification.json)。

## 逐轮、逐状态结果

每行 61 次读取、约 60 秒首尾间隔。CPU 是 kernel+user 总时间除以实际 monotonic
wall time；1000 ms/s 等于一个逻辑核。括号是前/后半程，方括号是本状态观察 min/max，
不是置信区间。时间边界、逐秒 CPU 区间、首尾值全部在 analysis/CSV 中。

| Run | State | CPU ms/s (first / last half) | Private MiB median [min,max] | Working set MiB median [min,max] | Handles median [min,max] | Threads median [min,max] |
|---|---|---|---|---|---|---|
| a1-net10 | visible | 42.974 (29.170 / 56.778) | 435.91 [435.60,439.15] | 544.31 [544.00,547.23] | 4492 [4483,4528] | 251 [248,262] |
| a1-net10 | hidden | 0.521 (1.042 / 0.000) | 435.71 [435.00,436.22] | 543.80 [543.17,544.19] | 4471 [4465,4491] | 245 [242,251] |
| a1-net10 | restored | 36.980 (22.914 / 51.049) | 435.50 [435.25,435.78] | 545.91 [545.67,546.12] | 4444 [4441,4449] | 242 [242,245] |
| b1-net11 | visible | 49.482 (63.028 / 35.938) | 436.11 [435.96,437.00] | 543.68 [543.14,544.29] | 4431 [4426,4467] | 253 [250,263] |
| b1-net11 | hidden | 3.385 (4.166 / 2.604) | 431.58 [430.91,432.19] | 539.82 [539.30,540.45] | 4408 [4402,4432] | 246 [243,253] |
| b1-net11 | restored | 36.198 (43.754 / 28.644) | 431.53 [431.35,431.77] | 543.70 [543.52,543.93] | 4389 [4387,4394] | 243 [243,246] |
| b2-net11 | visible | 48.958 (50.516 / 47.400) | 475.36 [475.17,479.68] | 541.50 [541.32,545.42] | 4559 [4555,4602] | 252 [251,268] |
| b2-net11 | hidden | 1.562 (2.604 / 0.521) | 475.42 [474.48,475.77] | 541.22 [540.45,541.50] | 4554 [4522,4558] | 251 [243,254] |
| b2-net11 | restored | 42.710 (32.807 / 52.616) | 475.19 [475.01,475.33] | 540.38 [540.21,540.49] | 4512 [4511,4516] | 243 [243,246] |
| a2-net10 | visible | 50.001 (44.794 / 55.208) | 436.88 [436.64,440.06] | 537.04 [536.80,539.94] | 4467 [4463,4506] | 252 [251,264] |
| a2-net10 | hidden | 2.604 (3.125 / 2.084) | 436.90 [436.02,437.39] | 536.28 [535.49,536.64] | 4468 [4441,4473] | 251 [244,254] |
| a2-net10 | restored | 33.334 (31.247 / 35.421) | 436.50 [436.28,436.89] | 535.21 [535.04,535.58] | 4434 [4430,4439] | 246 [244,247] |

每臂 n=2 的 CPU run 范围：visible net10 42.974–50.001 / RC 48.958–49.482；
hidden 0.521–2.604 / 1.562–3.385；restored 33.334–36.980 / 36.198–42.710。
各状态范围重叠，半程变化方向不一致。RC 第二轮 private bytes 约 475 MiB，第一轮
约 432–436 MiB；它是进程代理指标，不能推出 native allocation、GC 原因或泄漏。
未指定性能阈值，也没有用小样本显著性检验制造结论。

Observer 单次墙钟开销 median / max（ms）按 ABBA 为 **13.226 / 19.379、
14.563 / 21.170、12.830 / 18.413、11.130 / 211.482**。最后一个尖峰位于
a2 visible 的 sample 2；原样保留，没有剔除或补跑。这不是 observer 的 CPU 消耗，
不能从目标 CPU 中减去。当前进程快照扫描仍有成本，不宣称“无扰动”。

每轮前后背景快照确认有其他持续活动：GameViewer 累计约 30.7–34.5 CPU 秒、Taskmgr
约 24.0–30.4 秒，T3 Code 约 20.7–60.5 秒；这些是整轮 surviving-process 窗口，
不是精确三个测量窗口的 on-CPU 归因，也遗漏中途退出进程。没有停止其他任务。
因此背景调度、机器状态和 observer 尖峰无法排除；本轮**不能收窄为已证明的 runtime
性能收益或回归**。只完成上述 workload 的可复算基线，认证播放/列表刷新/冷启动等
性能边界仍未测。没有把旧 v1/v2/v3 数据、旧 183 行或纯列表算法结果混入本表。

## 行为、退出、视觉与清理

| Run | PID | Player/Main | Retained exit | lastQuery after observed exit | Verdict |
|---|---:|---|---:|---:|---|
| a1-net10 | 27784 | 15/15 + 4/4 | 0 | 30.156797 s | PASS_BOUNDED |
| b1-net11 | 17804 | 15/15 + 4/4 | 0 | 30.462298 s | PASS_BOUNDED |
| b2-net11 | 39612 | 15/15 + 4/4 | 0 | 30.486416 s | PASS_BOUNDED |
| a2-net10 | 3980 | 15/15 + 4/4 | 0 | 30.452029 s | PASS_BOUNDED |

四轮 exact PID/path/creation/start/package 相符、句柄保留、无强制结束；最后 query
开始时间也都 >=30 秒。safe Application1000/WER 集合为空、managedUnhandled=0。
未来精确故障仍可撤销有界结论。原有 exit gate 的十项历史/反例守卫本次通过；没有
重跑 487 或 .NET tests，没有新增生产优化、API 迁移或 WinUIAnalyzer 通过宣称。

行为证据包括 `ModesCloseWithoutLateAccess`、`VideoSwitchLateSuccessAndError`、
`PgcSwitchLateSuccessAndError`、`DirectDisposeInFlight`、`WindowDestroyInFlight`、
`VmCancellationReachesHttpHandler`、三项 0/360/361 分段边界和
`RealTrayEnableDisableRecreate`。Main 四项为 `NativeMainCloseHidesToTray`、
`RestoreSameMainHwnd`、`DisableTrayRestoresHiddenMain`、
`ClosingPlayerPreservesIntentionalResidency`。每轮 `idle-cycles` 记录 20 次同 HWND，
字体结构化检查 before=0、count=108、selected/expected 相同。完整命名结果保留逐轮 raw。

48 张 PNG 均逐张检查，仅 owned 窗口，无桌面全屏或认证区域。40 张播放器/控制器截图
保留离线白色视频、右缘弹幕裁切的已知限制；四张设置页一致。**b2 的字体截图没有浮层**，
其结构化字体检查通过不能代替这一视觉缺口；其余三张字体图可见设置浮层。
不补拍、不称所有字体首次显示都获视觉验证。详见
[视觉记录](runtime-final-baseline/raw/visual-review.json.gz)。

准备失败全部保留：net10 freeze 后 JSON 默认 GBK 解码失败（改显式 UTF-8，仅继续
未构建的 RC 臂）；runner 生成的非唯一替换守卫拒绝；预检对 134 包的过严假设被真实
138 包图拒绝；原字节生产比较被 preparation 额外末尾换行拒绝。更正的是准备/证据
解释，没有修改冻结二进制。后处理截图枚举曾误计 packaged PNG，被计数守卫拒绝，
随后只枚举 player/main。无 native 失败被删除或当作有效性能数据，也无方向性补跑。

最终 owned 应用、四个注册、四组 observer launcher/实际解释器均不存在。
最后显式 yield 返回 **released:false**（无剩余 lease），如实保存；未操作已登录
Caa6a7be/layout-v3、Dev、PID9608。kernel 为 `0ae5e4e` clean、仅 local-only；
初始不完整 kernel 索引已备份后从现有本地 bundle 恢复，没有远端操作。
父 `feat/performance-45` 仍为 `76af647`，无 push/PR/issue。

Standards 审查：本次仅 docs/evidence/replay，未引入生产变更；raw gzip 防止换行
改写，完整 freeze/部署/模块 hash 验证通过。Spec 审查：四轮上限、同源/差异、退出
gate、保护边界满足；列表新测量按预先声明不引入，视觉缺口与背景干扰保持明确。
400 项可移植证据通过 byte/hash 验证；352 个解压文本的签名媒体查询/凭据值模式扫描
无命中（仅此扫描范围，不宣称能排除所有可能秘密）。提交前修正 replay 一处行尾空格；
raw/replay-source 保留实际执行版本原字节，签入脚本仅有该空白整理。复算方法及范围见 README。
