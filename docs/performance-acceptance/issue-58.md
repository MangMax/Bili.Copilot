# #58 — HTTP 重复解析与复制：受控重放调查

## 决策与边界

建议一个 **S（≤M）后续实现切片**：仅在 kernel 的 protobuf `ParseAsync` 中用已验证存在的 `MessageParser<T>.ParseFrom(byte[], offset, length)` 替代 `[.. bytes.Skip(5)]`，保持短响应的公开解析行为。实验候选只存在于本报告实验工程的 `obj/candidate`，由脚本从固定源码生成；本次不修改生产文件、子模块源码或 gitlink。JSON 合并校验/业务解析、跨层缓存缓冲暂不建议实施：尚无兼容性与收益证据。下述数据是合成输入的局部重放，不是整机、真实网络或 WinUI 性能收益。

工单：[58](https://github.com/MangMax/Bili.Copilot/issues/58)、[45](https://github.com/MangMax/Bili.Copilot/issues/45)、[48](https://github.com/MangMax/Bili.Copilot/issues/48)。#58 已通过 `gh issue edit 58 --repo MangMax/Bili.Copilot --add-assignee '@me'` 认领，保持 OPEN。未 push、修改 PR、关闭工单、修改父集成分支或派生代理。后续票仅以本报告中的规格草案交付，未创建外部工单。

## 基线与可复现材料

- 独立工作区：`C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-897a0a69`；分支 `t3/issue-58-performance-spike`。
- 初始基线 `47f7253`，先合并本地 `feat/performance-45` 至 `5b713af`，正式重放前再合并至 **`ae16b95e8b6ac7066371a7b48474873203b64a16`**。正式运行的实际测量 SHA 也写入各 `environment.json`；它是集成输入 SHA，实验源码随本报告提交，不声称已测量尚不存在的最终报告提交。
- kernel 始终为 **`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`**。开始时子模块完整、干净，因此未执行指针修复、重新初始化或任何备份/覆盖。读取了仓库 AGENTS 与三份 docs/agents 规则，按规则检查领域文档。
- 完成前已合并最新本地集成 **`20ccfa953eb2dc24551d1112eee986772d7ed991`**；与实际测量 SHA 的差异为 #53/#54/#55 的客户端及文档改动，未改变测量链接的 kernel 源码/gitlink/依赖。最终报告提交基于此集成，测量 SHA 仍如实记录为 ae16b95，不改写原始记录。
- 实验入口：[run.ps1](issue-58-replay/run.ps1)、[Replay.cs](issue-58-replay/Replay.cs)、[工程](issue-58-replay/Replay.csproj)、[契约测试](issue-58-replay/Tests/ReplayTests.cs)。[prepare.ps1](issue-58-replay/prepare.ps1) 检查 kernel SHA 和源码工作区，再只生成临时候选副本。
- 原始记录：[正式 run-2](issue-58-replay/evidence/run-2/measurements.csv)、[正式 run-3](issue-58-replay/evidence/run-3/measurements.csv)、各目录 `environment.json` 与 `.bin` fixture；[源码 SHA256](issue-58-replay/evidence/source-hashes.json)。合成 JSON/弹幕从确定性生成器产生，不含账号、Cookie、token、认证头或抓包数据。
- `run-1` 是**排除的探索运行**：它与还原/构建重叠，且早于正式 SHA 记录与最终迭代数。保留原始文件供审计，不进入结果汇总。另一次契约测试构建遭正在运行的 Replay.exe 文件锁而失败；待该进程结束后重新构建、通过，再开始正式采样。初版已取消令牌测试错误地期待 HTTP handler 零调用，依据实际公开行为修正为一次调用；不是生产缺陷修复。

## 真实调用路径及所有权

### JSON

`BiliHttpClient.SendAsync` → `HttpClient.SendAsync(request, cancellationToken)`（默认 ResponseContentRead）→ `ThrowIfResponseInvalidAsync` → `EnsureSuccessStatusCode` → `ReadAsStringAsync` → source-generated `BiliResponse` 反序列化 → `Code == 0` → 调用者再 `BiliHttpClient.ParseAsync(response, JsonTypeInfo<T>)` → 再次 `ReadAsStringAsync` 和业务类型反序列化。

源码：`externals/bili-kernel/src/BiliKernel.Core/Http/BiliHttpClient.cs:78,121`；`BiliHttpClient.Extension.cs:25`；`Content/BiliResponse.cs`。实际业务调用例：`Services/Services.Search/Core/SearchClient.cs:39–40,49–50`。错误包解析仍需遍历/跳过业务 JSON，不能因为只取 code/message 就认为没有扫描成本。

同一次响应的内容由 HttpContent 缓冲；实测 handler 发送一次、SerializeToStreamAsync 一次。重复字符串读取在所测 .NET 8.0.33 返回不同字符串实例，数组读取返回内容相同但独立的数组；不是重复网络下载。此结论由 `RepeatedReads_UseOneBodyBuffer_ButReturnDistinctArraysAndStrings` 验证，不把方法名称当作网络证据。

### gRPC / octet-stream

相同 HTTP 入口；验证器先 `ReadAsByteArrayAsync`，只检查长度 `<5`，并未解析 protobuf 或检查压缩标志、帧声明长度、grpc-status/trailers。业务 `ParseAsync` 再读 byte[]，用 `[.. bytes.Skip(5)]` **复制剩余 payload**，然后解析 protobuf；不是复制五字节头本身，更不是第二次下载。实验使用真实 `bilibili/community/service/dm/v1.proto` 的 `DmSegMobileReply` 与 `DanmakuElem`，不以 StringValue 代替业务消息。

候选只去掉最后的 payload 数组复制，仍保留验证层和解析层各自 ReadAsByteArrayAsync 的数组分配。`ParseFrom(bytes, Math.Min(5, bytes.Length), Math.Max(0, bytes.Length - 5))` 维护直接调用 ParseAsync 时不足五字节返回空消息的旧行为；经 SendAsync 的不足五字节输入仍被拒绝。未改变协议验证缺口，未加入缓存、池化或新 API。

### 资源与缓冲生命周期

- 构造函数接收 handler 并由 HttpClient 拥有，Dispose client 会 Dispose handler。
- SendAsync 成功后返回 response，所有权在调用方；ParseAsync 不释放 response。实验边界使用 `using request/response`，衡量新响应输入到已物化业务结果及释放的完整循环。
- 校验失败抛出时 response 不返回也不 Dispose；错误测试实际观察到内容未释放，测试随后手动清理。已检查的 SearchClient、DanmakuClient 调用点也未见 response 的确定性 Dispose。此为**所有权风险**，本票没有证明长期泄漏或连接池耗尽，不将其并入去复制候选。
- HttpContent 缓冲归 content 生命周期；ReadAsByteArrayAsync 返回的独立数组不会被下一次读取覆盖。offset 候选在局部数组仍存活时同步解析，返回已物化消息，不向外发布 span/数组切片。
- 不建议现在跨验证/解析层保存额外缓存：须先明确错误路径释放、并发解析、重复解析、可变数组可见性及 Dispose 后使用契约。共享缓冲不能随意 Return 到 ArrayPool，不能以全局字典延长 response 生命周期。

## 依赖与实际可用接口

| 范围 | 核验事实 |
|---|---|
| kernel 源码 | Core 目标 netstandard2.0/net8.0；子模块中央版本 Google.Protobuf 3.29.3、Grpc.Tools 2.69.0、System.Text.Json 9.0.1、RichasyKernel.Abstractions 0.0.1 |
| 实验离线还原 | Google.Protobuf 3.29.3（lib/net5.0）、System.Text.Json 9.0.1（net8.0）、Grpc.Tools 2.69.0；源码链接无替身 Verify/KernelException，异常类型来自真实包 |
| 重载 | 3.29.3 包 XML 在 net5.0 与 netstandard2.0 均列出 `MessageParser<T>.ParseFrom(byte[], int, int)`；候选实际编译、执行并与原始结果比较；同包也有 ReadOnlySpan，但未把它当成本票候选或验证结论 |
| app 中央版本不能单独作准 | app props 声明 Google.Protobuf 3.27.2，而读取既有集成工作区 app `obj/project.assets.json` 得到实际解析 Google.Protobuf 3.29.3、System.Text.Json 10.0.0-rc.1.25451.107、RichasyKernel.Abstractions 0.0.1 |
| app 证据限制 | 上项是 `t3-1d19cc80` 中既有 assets 的只读观察，不是本票新还原/发布的 WinUI 应用；不据它宣称当前 app 构建通过。本票未启动或安装 app |
| 执行环境 | SDK 10.0.403；正式重放 net8.0 / Microsoft.NETCore.App 8.0.33；Windows 10.0.26300 x64；AMD Ryzen 7 7840HS，8 核/16 逻辑处理器；workstation GC；禁用 tiered compilation。实际程序集 informational version 见 environment.json |
| #48 原测试 | 原工程 net9.0 + RollForward=LatestMajor，本机无 net9 runtime，按配置运行于已装 .NET 10；这与 net8 重放是两种验证环境，不能混称生产 WinUI |

## 测量方法

每个 fixture/候选复用一个 client，handler 每次产生新 response/content；没有套接字、DNS、认证或真实 API。输入 fixture 在计时外生成；对象物化、请求创建、内容缓冲、两层校验/解析、响应释放在计时内。基线链接真实源文件，候选由同源临时复制，仅类型重命名及一处 offset 表达式变化。

四个 fixture：JSON 20/5000 项（1131/283931 B），gRPC 20/5000 条（821/214710 B）；完整内容、SHA256 与生成器均提交。JSON 是含 code/message/ttl/data 的合成列表，不能代表所有真实 JSON 模型。gRPC 头为未压缩标志与大端 payload 长度。

每种 fixture 先各预热 200 次，随后七轮交替 AB/BA；小 fixture 每轮 10000 次，大 fixture 500 次。每批前显式 GC，不计入该批计时；批内 GC 计入 CPU/墙钟，未强行关闭 GC。两次独立进程正式运行。JSON 的“offset”版本代码完全相同，是负对照，不能将波动解释为 JSON 优化。

CPU 用当前进程 TotalProcessorTime 差值（含进程内 GC，受 OS 计时量化影响），墙钟用 Stopwatch，分配用 GC.GetTotalAllocatedBytes(true) 差值（进程范围含运行时后台分配，不是存活堆/峰值/RSS）。每批记录发送数和业务计数 checksum；任何不符直接失败。统计使用各批除以迭代次数，再按两次正式运行合并的 14 批取中位数和范围。没有置信区间、真实工作负载分布或桌面隔离承诺；其他任务/系统活动可能扰动 CPU 和墙钟。

## 实测结果

[汇总 CSV](issue-58-replay/evidence/summary.csv) 由 [summarize.ps1](issue-58-replay/summarize.ps1) 从两次正式原始记录生成。下表各格为单次 HTTP 输入→结果循环；CPU 中位数与范围单位 µs，分配单位 B，非峰值内存。

| Fixture / 版本 | CPU 中位数 [最小, 最大] | 墙钟中位数 µs | 分配中位数 B/op |
|---|---:|---:|---:|
| JSON 20 / baseline | 17.969 [12.500, 21.875] | 19.016 | 10,928.4 |
| JSON 20 / 不变对照 | 17.188 [14.062, 20.312] | 18.368 | 10,928.3 |
| JSON 5000 / baseline | 3718.750 [3312.500, 5031.250] | 3659.395 | 2,075,672.5 |
| JSON 5000 / 不变对照 | 3640.625 [3375.000, 6125.000] | 3593.619 | 2,075,660.9 |
| gRPC 20 / baseline | 10.938 [7.812, 12.500] | 11.010 | 10,032.2 |
| gRPC 20 / offset | 7.812 [4.688, 9.375] | 7.700 | 9,312.2 |
| gRPC 5000 / baseline | 2000.000 [1687.500, 2156.250] | 1931.596 | 1,992,608.4 |
| gRPC 5000 / offset | 1218.750 [1062.500, 1468.750] | 1105.826 | 1,778,019.3 |

大 gRPC 夹具每操作少分配约 **214,589 B**（约 10.8%，payload 为 214,705 B），CPU 中位数差 **781.25 µs**。小 gRPC 只少约 720 B；offset 解析自身也有对象开销，所以净节省不必精确等于数组长度。该证据足以支持一个小范围后续实现试验，不证明某视频播放或界面响应提升相同比例。CPU 的范围不是统计置信区间；小输入的进程 CPU 计时量化明显，优先依赖分配证据。

JSON 两组实现相同，分配近似相同但 CPU 可波动；它是噪声对照，不是优化方案。尚不能从本结果量化“移除错误包扫描”在保持错误优先级后的净收益，更不能把 gRPC 结果外推到 JSON。

图形：[可独立打开的比较图](issue-58-replay/evidence/comparison.html)。

异常路径另在 `8530be8ee93db839bd3ccff32f11564744a190c7` 上追加一次独立进程采样，未重写成功路径测量 SHA。每例预热 100 次，7 批 × 500 次，所有批的发送数/异常数均为 500；[原始 CSV](issue-58-replay/evidence/errors.csv)、[版本](issue-58-replay/evidence/errors-environment.json)、[汇总](issue-58-replay/evidence/errors-summary.csv)。没有候选对比。六例 CPU 中位数均为 31.25 µs/op，受进程 CPU 计时粒度限制，**不能解释为六例等价开销**。

| 异常 fixture | 分配中位数 B/op |
|---|---:|
| JSON code=-101 | 4520.8 |
| 空 JSON | 4032.8 |
| 四字节 gRPC | 4096.8 |
| HTTP 503 | 4488.8 |
| handler 内触发调用者取消 | 4280.8 |
| 非调用者 OCE/超时 | 3856.8 |

此异常边界包含测试侧每次创建 CTS 与捕获/检查异常，也包含 handler 暴露的失败内容手动 Dispose（避免实验累计未释放内容）；这不是原生产调用者可访问的清理路径，因此只作受控异常循环成本，不作生产异常性能预测。未测真实 30 秒超时等待。

## 契约与 #48 验证

独立实验工程是 SDK-style xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.14.1 + VSTest，非 MTP。TRX：[contracts](issue-58-replay/evidence/contracts.trx)、[#48 HTTP/retry](issue-58-replay/evidence/issue-48.trx)、[#48 session](issue-58-replay/evidence/issue-48-session.trx)。通过 22 + 8 + 7 项，零失败/跳过。实验测试是固定 BiliHttpClient 类边界的 focused 工作，没有构建跨仓测试框架。

| Requirement | Evidence |
|---|---|
| HTTP 输入到 JSON/gRPC 业务结果、只发送/读取一次 | `Success_ReturnsBusinessResult_OneSendAndOneBodySerialization`（两种协议、两实现） |
| 非零错误码、消息及原始错误内容保留 | `Json_ErrorCode_PreservesMessageAndRawBody`（-101/-400/1） |
| 空白 JSON、损坏 JSON、null | `Json_Empty_ThrowsKernelEmptyResponse`；`Json_InvalidOrNull_WrappedValidationFailure` |
| 缺少 code 默认成功、业务解析异常不由 SendAsync 包装 | `Json_MissingCode_DefaultsToSuccess_AndBusinessShapeFailureIsUnwrapped` |
| HTTP 状态优先于 JSON 校验 | `HttpStatusFailure_IsWrappedBeforeJsonValidation` |
| gRPC 0/4/5 字节、直接短输入解析兼容 | `Grpc_LengthBoundary_PreservesEmptyMessageAndRejection`；`Grpc_DirectParseShortBody_OffsetPreservesSkipSemantics` |
| 候选保持实际字段、损坏 protobuf 异常 | `Grpc_OffsetCandidate_PreservesFieldsAndMalformedPayloadException` |
| 已取消/发送中取消不包装、非调用者取消仍包装 | `CallerCancellation_IsNotWrapped_AndNoExtraSend`；`TransportCancellationWithoutCallerCancellation_IsWrapped` |
| SendAsync 后取消不抢占 ParseAsync、client/response 所有权 | `CancellationAfterSend_DoesNotCancelParse_AndCallerOwnsResponse` |
| 重读为缓冲复制而非重复网络 | `RepeatedReads_UseOneBodyBuffer_ButReturnDistinctArraysAndStrings` |
| #48 服务层取消不重试/不发新请求、普通失败三次 | 原有 `Segment_CanceledDuringSend_DoesNotRetry`、`Segment_AlreadyCanceled_DoesNotSend`、`Segment_OrdinaryFailure_RetriesThreeTimes` |
| #48 六并发、关闭/切换、晚到结果与发布 | 原有 `CancelOrClose_SixActiveAndQueuedSegments_StopsSchedulingAndPublishing`、`Switch_LateOldResult_OnlyCurrentSessionPublishes`、`Close_QueuedCompletionAndLateFailure_PublishesNothingAndRejectsNewLoads`、`Reload_ContinuousSameMediaLoads_OnlyLastResultAndCountPublish` |

取消层次须区分：HTTP 包装器仅在捕获异常且调用者 token 已取消时透传 OCE；注入 handler 时预取消仍可调用 handler 一次。#48 的服务层循环前检查保证不安排请求；普通错误仍重试三次。成功 SendAsync 后 ParseAsync 不接收 token，已缓冲的解析可继续；`DanmakuLoadSession` 的解析后 token 检查、代次验证与发布队列内检查阻止旧结果上屏。offset 候选未改变这些行为。真实 transport 忽略取消、取消恰在复制/解析期间的耗时分布未做性能测量。

## WinUI 影响与验收边界审核

应用 `winui-code-review` 检查调用链、资源释放、异步与 MVVM 边界；应用 `winui-ui-testing` 审查用户可见验收范围。候选没有 UI/XAML/调度/线程模型修改，也不新增同步阻塞。Warning：上述 response 释放不明确；Note：ParseAsync 不可中途取消，依赖 #48 会话隔离；Note：gRPC 当前验证仅长度，协议增强需独立行为规格。没有为无关 UI 全仓规则扩展本票。

**未执行 WinUI build、analyzer、部署、启动、桌面输入、UIA 或截图。桌面由 #57 独占。没有 WinUI 实机验收通过结论。** 后续实现至少应在获准桌面窗口验证视频/番剧换分 P、连续 seek/切换、关闭播放器无旧弹幕/错误 toast，及正常错误提示一致；当前受控测试不能代替这些操作。未运行 analyzer，未声称 WinUI API 兼容性审核通过；本候选只涉及已编译核实的 Protobuf 重载。

## ≤M 后续票草案

**标题**：[Perf] kernel protobuf 响应解析避免 payload 去头复制。**scope:S**，仅 `BiliHttpClient.ParseAsync<T>(HttpResponseMessage, MessageParser<T>)`；独立 kernel 改动/发布后再由客户端单独消费 gitlink，不能绕过固定已发布基线直接夹带。

验收：使用已锁定 Protobuf 包的 offset/length 重载；保持直接短响应空消息与 SendAsync 短响应异常、非取消错误类型和消息、#48 取消/重试/代次行为；保持解析器配置和响应所有权。复用本票合成夹具与契约测试，验证成功字段、未知字段/空 protobuf/坏数据；补充真实脱敏大分片在目标客户端 runtime 下的 Release 重放，确认节省约一个 payload 数组且无结果差异。保留测量输入 SHA/版本与原始数据。排除 JSON 合并、缓存/池化、响应 dispose 修复、压缩/trailer 协议修复及认证策略变更。

JSON：本票只测基线和无变化对照，没有构造安全单解析候选，也没有逐阶段 CPU profiler 归因；**证据不足，不建议现在修改**。不得把两次扫描直接换算为 50% 提速。更广的跨层 buffer 复用同样暂不建议。

## 复现命令

在具备本机同版本现有依赖缓存的仓库根目录，以 PowerShell 执行；脚本仅用本地 NuGet cache 源，不下载/安装工具、不访问真实 API：

```powershell
./docs/performance-acceptance/issue-58-replay/run.ps1

# #48 既有测试（没有桌面操作）
dotnet restore tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~DanmakuCancellationTests' --logger 'trx;LogFileName=issue-48.trx' --results-directory docs/performance-acceptance/issue-58-replay/evidence
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~DanmakuLoadSessionTests' --logger 'trx;LogFileName=issue-48-session.trx' --results-directory docs/performance-acceptance/issue-58-replay/evidence
```

缺少缓存或目标 runtime 时应停止并报告缺项，不安装环境掩盖调查边界。原始 tests build 有子模块 proto 未使用 import 警告；实验候选有与原源对应的 nullable 警告（CS8603/CS8602），未通过修改生产源码或禁用警告隐藏。

本调查在 2026-10-30 约 02:04–02:25（Asia/Shanghai）时间箱内完成，远低于半工作日上限；未消费桌面。最终以 git diff 范围、子模块 clean、TRX 计数与实际测量 SHA 核验完成状态。
