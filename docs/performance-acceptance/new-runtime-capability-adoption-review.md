# .NET 10 能力采用独立审查

2026-10-30。结论：**可以合入完整净候选作为有限的稳定运行时兼容升级；不是性能验收 PASS，也不是发布或认证验收。没有发现必须阻断这次兼容合入的生产代码缺陷。** net9 原生崩溃保持未解决；整体性能收益、#49、AOT、认证首页及旧证据修复仍未完成。不要把“本次没有找到值得实施的新 API”写成“不存在 ≤M 的适配”。

基线 `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`，候选 `79ffd4f1b5ef6898b18426b9159f9342c3483450`，本地审查分支 `t3/net10-adoption-review`。本审查只在自己的 worktree 快进候选；未修改父分支、来源 checkout、生产代码或原报告。未使用子代理，未 build/test/UI 重跑、部署、认证、push、PR 或 issue 操作。

已读 AGENTS/docs/agents、code-review（Standards/Spec 两轴自行审查）、winui-code-review、receiving-code-review、verification-before-completion、src/.editorconfig，以及 new-runtime-capability-adoption、dependency-upgrade-review、dependency-performance-diagnosis、dependency-cpu-profile 和新协议/数据/脚本。没有运行 .NET 测试，以下 487 是原始 TRX 的重新解析，不是本审查新测试执行。没有重新执行 WinUIAnalyzer；历史 0.7.1 试用的 436 项仍未解决。

## Standards：按严重度的发现

### P2，非兼容合入阻断：最终生产 rebuild 缺少当时生成的二进制 hash 封存

定位：`net10-adaptation/final-validation.ps1` 最后的 graph 保存段，以及 `new-runtime-capability-adoption.md` 的 Final validation and handoff。

脚本保存 build 日志、TRX 和四个 assets 原始文件，但没有给最终 Debug/Release/Visor 生产 DLL/runtimeconfig 生成当时的 hash 清单。原有 `verification.json` 的 3,223 个 DLL 是九次**离线 fixture 部署**的计数，不是最终生产 rebuild 的 hash 认证。两种产物不能相互替代。

本审查已只读计算当前生产输出的 DLL、runtimeconfig、存在的 coreclr SHA256，记录在 `net10-adaptation/review-verification.json` 的 `currentProductionSnapshots`。当前 Debug/Release runtimeconfig 均为自包含 10.0.14，coreclr 均为 `1BC65B8D45474B19ADF3CA85CE881087AFF599FFC72B2C1A323E878227757B65`。这是审查时快照，不能倒推等于日志结束瞬间的 bytes。生产 DLL 当前 Debug 为 `BF5485EF808A10D8EBD92B5E78E0229AF249AAECD8D18F961FCD7EA833654723`，Release 为 `63C85350453663E9E501C5DB4C92B696B9B948D442FEC4A5772448D5D2BFF04A`。

最小动作：后续认证/发布构建完成后，在下一个构建覆盖共享输出前冻结完整产物、manifest、resolved graph 和 raw-byte hashes；记录实际启动的包/进程/runtime。兼容源码合入不必为这项重跑构建。本次审查补充的快照不得冒称原始构建时 provenance。

### 历史 P2 / P3，仍归父处理

`dependency-upgrade-review.md` 指出的旧 RC 生产输出被覆盖（P2）、五份 launch JSON newline/hash 不一致（P3）仍未被本候选修复。五份是 baseline-controls-launch、baseline-launch-2、baseline-launch、controls-diagnostic-launch、controls-fixed-launch。新 gzip ledger 校验通过不修复旧 ledger；原始 hash 不应被静默替换。RC 仍仅有历史编译/测试及前驱 native fixture 的证据，不能认证当前共享目录中的“旧 RC 最终二进制”。

其余 Standards：未发现新增 blanket NoWarn、pragma、IL/CsWinRT 屏蔽或删除已有 analyzer。`CsWinRTGenerateProjection=false` 是消费包自带 projection 的选择，并非禁用 Roslyn generator/AOT analyzer；仍不构成 AOT 证据。日志 guard 保留状态修改，snapshot 工厂仍复制列表；移除空条件发生在 null/empty 提前返回之后。LCS 保留重复键、tie 方向和引用语义；非空中段 jagged 分配的历史测量已补齐，额外行对象不应描述为普遍省内存。无新 XAML/lifecycle 改动需要为了评审重跑桌面。

## Spec：按严重度的风险与证据边界

### P1 未解运行风险；不是已定位的 net10 合入 blocker

定位：`net10-adaptation/evidence/r3-net9/{windows-errors.json,Report.wer,failure.json,provenance.json,hash-gate.json,cleanup.json}.gz`。

这是本轮新发生的真实原生失败，不能归为“仅旧历史 red”：2026-10-30 22:47:28.756 +08 的 Application Error 1000 指向 PID `0x7B60 = 31584`、确切 owned layout 和包 `Mang.BiliCopilot.Runtime.N854.r3-net9`，模块 CoreMessagingXP.dll 12.6.4.0，异常 `0xc000027b`，偏移 `0xadf5e`。22:47:35.357 的 WER 1001/Report.wer 指向相同报告 ID/应用路径，记录 combase.dll 10.0.26100.9711、`80131509`、偏移 `0xc4063`。两个模块记录不能当作两次独立崩溃，也不能靠 HRESULT 命名推导抛异常的具体应用调用。

该进程 Player15、Main4 已通过，但尚未完成采样/20 cycles/显式退出。observer 的 `RuntimeError: Target exited` 是发现目标退出，不是 crash cause；没有完整 counter 文件，不能从失败运行补造性能值。读取原始事件可见离线图像 `ms-appx` HTTP scheme 错误日志和业务边界调用，但没有 stowed/native stack 将其连接到致命事件；不得把普通记录错误认作原因或忽略所有错误。未发现候选通过新 catch/Handled 吞异常。

本审查把失败 layout 的全部 357 个 DLL 重新与 player9common freeze 和原 provenance 比较，相等；r1/r3/r5 和两个 v3 net9 都匹配同一 freeze。模块记录对应 net9 9.0.22。旧 recipe 混装解释在这里没有证据。22:47:39 的 cleanup 记录目标退出、注册清除及实际 Python PID4508/launcher18456 清理；初始两个运行的 launcher/shim 另有真实 interpreter 退出补充记录。

为什么允许有限合入：失败的是 net9 对照闭包，未出现稳定复现、没有定位到两闭包共享的错误调用；同二进制后续 net9 完成，net10-common 的两次 v3 和最终 net10/2.4.1 的独立原生场景也完成。因此无法据此断言 net10 有必现回归或要求任意生产修复。但 AppSDK/WinUI/业务路径共享，风险不能判定为 net9 专属，更不能因 net10 没崩就关闭。**性能/稳定性“无回归认证”仍 HOLD；源码兼容采用可继续。** 后续认证若同类事件复现，暂停该候选发布，保存 exact PID/path/hash/时间、日志和能取得的 stowed exception dump/stack，再确定最小修复；不得改 observer 或吞异常冒充修复。

### P2，非合入阻断：能力审计支持“未建立适用性”，不支持全称“没有 ≤M 机会”

独立扫描 source checkout 的 app/tests/kernel 非 Samples 项目及中央 PackageReference，85 条 metadata 记录对实际引用无遗漏（Metadata workaround 的历史解释行仍在）。58 个 unused central 和三个 sample-only 项不能算作应用运行时升级。H.NotifyIcon 最终已选 2.4.1，关闭旧 net9/2.3.2 排除项；preview-only Richasy 依赖仍是 preview，net10 稳定不使它们变成 GA。

审计有实际代码位置、old/selected 版本、nuspec repository commit、XML delta、验收边界；关键 native/MVVM/JSON/protobuf/logging 族有官方变更依据，超过单改 TFM。局限是一些 utility/connector/analyzer 行仍只给类别理由或 XML/member 未变化：XML 可缺失、含 internal 和其他 TFM，nuspec commit 本身也不是行为变更审查。因此足够支撑当前“不强行为用新 API 改业务”的选择，不足以证明全部包不存在可做的小适配。后续 provider/Markdown/protocol 验收属于具体业务任务，不应笼统推给用户选择 API。

核对已知问题后，没有建立被遗漏、可直接解决当前问题的 ≤M 新 API：Win2D pause 修复早于原 1.3.2；WinUIEx tray 修复不能移植给应用实际使用的 H.NotifyIcon；OrderedDictionary 索引 overload 不保留 LCS 重复键语义；JSON strictness 改协议接受面；PipeReader 需要所有权/取消验证；Serilog SelfMetrics 是诊断能力，不是已识别 sink 热点的修复；GC 参数没有相应 managed GC 瓶颈证据。MVVM 局部迁移可以小做，但并不因此成为当前故障的修复，也不能把整个迁移成本一概用来证明任何局部试点都 >M。

本次独立在线复核的主源：[.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) 为 active/LTS、SDK10.0.403/runtime10.0.14；[net10 libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries) 用于核对新增库能力边界；[net11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime) 仍列 preview runtime-native async 和新硬件基线。候选记录的 RC3 为 `11.0.100-rc.3.26522.148` / runtime `11.0.0-rc.3.26522.148`，不能把更新中的文档全部 API 归到该 build。metadata name scan 不等于 overload 编译、生命周期或生产验收。用户“考虑 .NET11”不等于必须采用 RC；选择有完整稳定兼容证据的 net10 有依据。

## 独立离线复算与闭包核验

执行本候选 `verify.py --tracked`，再对来源保留目录只读执行 `verify.py --live --tracked`，均通过。另增加独立 `review-verify.py`，不调用原 summarize：复算 raw counters、检验失败运行/完整运行 DLL、所有 freeze 文件、normalized C#、assets package hashes、20 cycles/font/显式退出以及当前生产 hash。结果保存在 `net10-adaptation/review-verification.json`。这是新核验结果，原报告与数据未改。

三个 freeze 分别 895/897/897 文件、357/359/359 DLL；每组 630 个 normalized C# 文件相等（仅 fixture identity 和 CRLF 规范化）。所有文件当前 hash 与 freeze 清单吻合，三个目录均无 appxrecipe。freeze 仅补缺失 recipe 内容、不覆盖已有 DLL；每个 deployed DLL 集合双向匹配。后加 BuildOutDir hardening 只有 property evaluation 证据，不能声称这些 freeze 是用该 hardening 重建的。

九次完整应用运行计数：四个 net9 ×357 + 五个 net10 ×359 = **3,223**；不包含失败 r3-net9 的另357，也不包含 Controls 的258。Controls7、每个完整应用运行的 Player15/Main4、20 same-HWND cycles、首次 font 从0加载、显式退出/zero unhandled 均有原数据；不等于生产视频画质、物理 DPI/device-loss、长时间 leak 或认证首页验收。最终 2.4.1 的 RealTrayEnableDisableRecreate 等原生检查属于单独 `final-net10`，不是拿 common 2.3.2 结果代替。

四次 v3 按 net9/net10/net9/net10 启动，runtimeconfig 和实际 loaded coreclr hashes 对应 9.0.22/10.0.14，共用 tray2.3.2；最终单独使用2.4.1。13 个 net9-only graph 项含 Metadata/Immutable/JSON/Pipelines 等，不能称仅替换 JIT。最终 tray 组三个包（H.NotifyIcon.WinUI、H.NotifyIcon、H.GeneratedIcons.System.Drawing）从2.3.2到2.4.1，source 相同。生产 app/Visor/Visor.Models/tests 为net10；UI.Models/resource generator 保留netstandard2.0，kernel保留net8/netstandard兼容图。最新 runtime 层之后无生产 C# 算法修改，但相对父已有经历史审查的 LCS/日志/snapshot 改动；“无算法修改”只能限于新增 runtime 阶段。

v3 `GetProcessTimes`、`GetProcessMemoryInfo`、`GetProcessHandleCount` 加 Toolhelp `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS=2,0)`，遍历 Process32FirstW/NextW 取目标 cntThreads；不是一个纯目标专属查询。[Microsoft 结构定义](https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/ns-tlhelp32-processentry32w) 与字段、指针宽度和 dwSize 初始化一致。UI fixture 在边界写 marker、await63秒，不执行每秒 Process.Refresh/Threads/GetProcessInfos。这里是源码证明移除已知调用路径，没有新 native trace 能证明整个进程所有库永不调用该 API。

v3 每次采样 wall median 10.398–10.976 ms，最大15.940 ms；仍有系统快照、调度、文件轮询和进程扰动，wall耗时不是 CPU overhead，不能从 target CPU 中相减。v2 全线程快照较重；最终2.4.1使用v2，median51.331/max90.406 ms，因此只作单独兼容验证，不并入 v3 性能对比。

CPU单位为累计 kernel+user 毫秒差 / monotonic 秒差；完整0→60，前半0→30，后半30→60。不是整机百分比；61读数形成60相关区间，每个进程才是实验单位。独立复算与原 summary 全精度相同。下表每格为完整/前半/后半 ms/s，所有区间保留，没有择低半段：

| v3进程 | visible | hidden | restored |
|---|---:|---:|---:|
| r1 net9 | 32.812 / 33.332 / 32.292 | 1.302 / 1.042 / 1.563 | 47.396 / 57.814 / 36.978 |
| r2 net10 | 51.043 / 73.954 / 28.128 | 1.302 / 0.521 / 2.083 | 50.002 / 64.584 / 35.419 |
| r3 net9 | 53.905 / 63.543 / 44.267 | 4.427 / 5.729 / 3.125 | 62.762 / 78.125 / 47.398 |
| r4 net10 | 47.914 / 36.979 / 58.847 | 2.864 / 4.688 / 1.042 | 43.488 / 33.332 / 53.643 |

显著段内漂移仍在，两个net10 private-memory medians分别在net9两侧。不能推出整体性能win、内存减少或统计显著回归。v1/v2/v3不可相减；没有原始包组在v3下的基线，故旧 restored-CPU疑问未因改协议而关闭。没有用户性能预算；兼容升级也无需先制造一个性能收益。

## 构建、测试和恢复边界

原始 Debug/Release rebuild 日志各0 errors、1 existing warning，具体为 `pgc/gateway/vega/v1.proto(10,1)` 的 `google/protobuf/empty.proto` unused import；不能写零警告。Visor Release JIT为0 warnings/errors，ValidateRuntimeDependencies日志及driver成功边界支持 x64 libmpv/ffmpeg/BBDown 检查；不代表执行外部媒体功能。

最终 TRX counters total/executed/passed=487，failed/skipped=0。测试runtimeconfig TFMnet10、LatestPatch；结合机器可用稳定runtime10.0.14和测试成功，支持稳定net10执行，不能把 `dotnet --info` Host列的11 RC当作测试实际runtime。原档没有testhost loaded-module trace，实际patch依配置/安装集合推定，未伪称取得过模块证据。app自包含运行则有loaded coreclr直接证据。根global.json精确SDK10.0.403/rollForward disable/allowPrerelease false；没有改变机器默认SDK。

本审查初始kernel staged deletions/unstaged state已分别保存为ignored `artifacts/net10-adoption-review/kernel-{staged,unstaged}.patch`。在自己的子模块执行 bundle verify/fetch 后恢复候选gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`；bundle prerequisite `6501985d0f30c55c28b3f78c6e045c2536e1d389` 满足，取消`2ca79bd`、pageSize`f02c33f`祖先检查均exit0。这只证明local bundle可恢复，不证明remote可达；父后续分享候选必须保持bundle/prerequisite的交付说明。原11个owned identity/process清理档完整，本审查没有运行当前桌面/包状态操作，不能将旧cleanup说成本次实时包审计。

## 最小必须动作与后续认证验收

1. 合入时保留完整候选闭包（包括kernel bundle、net10、2.4.1、Metadata移除、LatestPatch及必要分析器修复），不要把本结论外推到任意拆包组合。若只挑提交，先核对resolved graph和相关边界，不能继承整套通过记录。
2. 父交付说明明确“有限兼容采用”，列出本轮net9 crash未解、性能未认证；#49 pause FAIL、AOT因MSVC缺失blocked、最新认证首页未验、RC实验状态不可删除。这不是新增用户审批步骤，也不要求为了求全机械重跑UI/fullsuite。
3. 五份旧JSON按原bytes gzip或并列original/normalized hash修复；旧RC产物标记overwritten/historical。它们是既有父跟进，不因本次新证据自动关闭。
4. 后续在获授权的认证验收会话里，使用冻结的确切net10/2.4.1生产包，按既有保护策略处理登录/最新首页，验收推荐加载/刷新/撤销/pageSize、播放器切换取消和tray显式退出；检查对应owned PID的错误日志/WER并保留产物/runtime provenance。无需为了本审查获取、复制或更换账户。只重跑改变或未覆盖的真实路径；若出现同类native crash则发布HOLD并取因果证据。

Standards：无新增生产规则阻断；1项新P2产物溯源改进，历史P2/P3仍开放。Spec：1项P1未解原生风险、1项P2能力审计结论边界；均不支持把当前候选称作性能/全面稳定性PASS，但不构成已证实的净net10兼容源码合入阻断。
