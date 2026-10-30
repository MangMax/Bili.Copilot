# RC3 player timer fix 本地集成审查

2026-10-31。精确候选 `885012ab65474ddf0121d889130b583dad9bdfc8` 已按授权
以 `--no-ff` 合入 `feat/performance-45`，merge **`be7105b003470782c74fbc8aaac0ea6a23a44a60`**。
第一父 `f9271158c315ef7b1101050cb10560abdacc722a`，第二父即固定候选。
初始父工作树 clean；合并无冲突，生产源码/config/test 树与候选完全一致。
本报告及证据脚本是合并后文档提交，不再修改生产源码。

结论范围：**11 RC3 为当前主候选；x64 Release JIT 离线退出有限通过**。
这是候选的两次历史 clean fixture 退出证据，加上本轮 fresh 构建/测试/文件门禁，
不是新生产应用运行验收。**AOT BLOCKED、#49 pause FAIL、认证最新组合未验、
历史 net9 crash 未归因、总体性能 HOLD**；不宣称全面稳定或性能收益。

## 独立 Standards / Spec / WinUI 审查

读取 AGENTS.md、docs/agents 和四项指定技能，无子代理。正式差异仅 global.json 的
精确 RC SDK/allowPrerelease、UI/Visor/Visor.Models/tests 的 net11 TFM，以及 panel：
constructor 一次注册本地 Loaded/Unloaded；managed flag 幂等启停；卸载先清 flag、Stop，
再执行既有 CleanupInteractionState；tick 在任何 ViewModel/GetValue 前检查 flag。
没有修改共享 base、清理方法体、窗口/播放器关闭顺序、XAML 或引入 COM catch。
已有 LatestPatch、2.4.1 依赖与历史修复保留。未发现此次范围内实质阻断。
普通编译分析器按现有配置运行；未安装或运行独立 WinUIAnalyzer，不能等同其完整审查。

从原 frozen DLL 重新运行 metadata helper，校验模块/PDB hash 和 PE/PDB GUID，
确认真实 Richasy.WinUIKernel.Share base 首次 Unloaded 注销 Loaded/Unloaded，然后调用
override；ViewModel getter 经过 GetValue。本地生命周期所有权避免第二次 remount
不再启动。无需修改通用 base；guard 不使用 native IsLoaded 属性。

原 dump SHA256 本轮重新计算为
`eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d`。
核对既有 LEO/DAC/PDB/IL 因果链：`COMException 8000FFFF` →
`OnTempControlTimerTick` 的首个 ViewModel/GetValue → ABI callback 报错。
Application.Start 是报告时当前栈，不是已证明原始抛出点。本轮没有重新扫描堆或读取凭据，
也没有从该 dump 推断所属 panel/window、Unloaded 已完成或所有 net9 crash 同因。

## 候选证据复核与保留的失败

在 **原 t3-e497cf43 cwd** 运行候选 `verify.py`，exit=0，候选工作树仍 clean。
该脚本实际会写 evidence.json（虽其 docstring 写 read-only）；本次重算字节未改变 tracked 内容。
没有在父目录伪造 artifact 路径或覆写原二进制。核对三组各 901 文件/363 DLL、
134 package SHA512 图、source 差异、coreclr hash、生产 Debug/Release 冻结集及历史487 TRX。
另独立核对 final1/final2 layout 各363个 DLL 与 final frozen fixture 的双向集合及哈希完全相同，且无 recipe。
最终 fixture panel 与生产 raw bytes 相同；其余仅既有离线 App/GlobalDependencies 和 FontCatalog probe。
本轮 SDK archive SHA512 与既有固定值一致，使用绝对 SDK 路径及进程局部环境。

Stop-only 精确三项失败：LoadedTimerRuns2、LoadedTimerTicks2、LateTickDoesNotReadClosedVm；
重新运行 assert-timer 返回2，final2 返回0。首次误将目录传给该脚本的两份 PermissionError
日志也保留于 review-evidence，修正路径后的结果另存，未覆盖失败。
最终行为检查使用真实 timer/mount 与过期音量状态；late tick 是显式边界注入，
不能声称自然观察到了 Stop 后 OS 投递。诊断 read counter 只在 diagnostic fixture，
其绿例不替代最终无 panel instrumentation 的两次 clean run。

final1 PID40300 和 final2 PID10064 retained handle exit=0；独立重算 exact
PID/path/creation-time AppError 及 linked/unresolved WER 门禁均 PASS_BOUNDED，退出后
观察分别 30.501905s / 30.521655s。未来精确错误证据仍可撤销结论。
stop1 PID31716 与 controls1 PID40740 unknown exit/BLOCKED，startup deadline 和
system-key runner 失败原样保留，后续成功不覆盖。原始 PID38208 exit C000027B、
AppError6525/WER6526 仍为 red。既有 accepted=true 不是本轮验收依据。
本轮退出门禁10项单元测试通过；没有新 UI、部署、注册、性能复跑或桌面操作。

## 合并后 fresh 门禁与冻结

全套 Release TRX 已逐条解析：**Passed: 487, Failed: 0, Skipped: 0**。
DebugClean / Release JIT 全量重建均 **1 existing protobuf warning / 0 errors**；
ValidateRuntimeDependencies **exit=0**。冻结 Tests 347、DebugClean 1890、Release 1887、RuntimeGate 26 文件。
全量 hash/双向集合、recipe/DLL 闭包、两套 RC3 runtime hash、candidate package graph 审计均 PASS；
122 份小证据已按原 bytes gzip 归档，见 [summary.json](integration-net11-player-timer-fix/summary.json)。
DebugClean activation 671文件，Release activation 670文件。无 recipe 外额外 DLL，coreclr 与候选固定 RC3 hash 相同。

执行器 [validate.py](integration-net11-player-timer-fix/validate.py) 使用
`t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe`，精确
`11.0.100-rc.3.26522.148`。Tests → DebugClean → Release → RuntimeGate 串行，
每步完成立即冻结；两个 app Rebuild 都使用各自新 BuildOutDir。命令、开始/完成/冻结 UTC、
源码树、kernel、runtimeconfig 和 SDK info 存于每步 provenance/log。
Tests 封存保留既有其他 TFM 子目录，仅 net11.0 DLL/runtimeconfig 和本轮 TRX 是 fresh 证据。
Release 显式 `PublishAot=false`。runtime gate 仅检查媒体依赖文件存在，不代表媒体功能运行。
已有 ignored libmpv/ffmpeg 均存在，本轮不复制或下载新资源。

冻结根：`artifacts/integration-net11-player-timer-fix/be7105b003470782c74fbc8aaac0ea6a23a44a60/`。
原始 output recipe 保留；独立 activation 副本按 recipe 补齐缺失 content，移除 recipe，
不覆盖已有 payload、不更改 manifest 身份、不注册/启动。冻结后 hash/双向文件集合、
DLL/recipe 闭包及 candidate resolved package graph 由 [audit.py](integration-net11-player-timer-fix/audit.py) 核验。
大文件保持 ignored；小证据 raw-byte gzip 随文档提交，包含失败和精确 hash。

## Kernel 与保护边界

父 nested kernel HEAD 精确 `0ae5e4e000a41a07f6696746706df11c06e8835b`，clean。
`git bundle verify` 确认 committed dependency-upgrade/kernel.bundle 可在本地 prerequisites
满足时恢复该对象；取消/pageSize 祖先继续保留。**local-only，不声称 remote 可达**。
没有 push、PR、issue、新子代理、登录/Dev/PID9608 或 Caa6a7be 操作。
所有历史产物和 red 证据保留；本轮冻结输出只供后续另行授权的认证组合验收使用。
