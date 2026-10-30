# #48 已登录分片错误：只读归因与剩余验收

2026-10-30。**1,953 个失败块的直接原因已确定为 kernel 响应校验拒绝空响应，不能归为正常取消。上游为什么返回空响应仍未定。#48 未完成验收。** 本轮没有启动应用或占用桌面，没有修改生产代码、依赖、kernel、包注册、layout 或登录状态，没有调用认证接口、安装工具、push、PR/issue 写操作或子代理。

## 基线和证据边界

- 从父基线 `c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0` 快进合入仅文档 `459d40d57731a8fba0af13cd9a39f56ceb0d701e`，包含独立 app 交接。已读 full-app-authenticated-acceptance.md 全文及安全摘要、full-app-acceptance-handoff.md、issue-48.md、integration-48.md、status.md、AGENTS 和 docs/agents。
- 应用 systematic-debugging、winui-code-review、winui-ui-testing、verification-before-completion；本次只有静态审查和安全日志分析，未运行 UI、WinUIAnalyzer、构建、15 项历史测试或完整套件。
- `Get-AppxPackage -Name Mang.BiliCopilot.PerfAcceptance.Caa6a7be` 核实唯一 family 为 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`，注册目录仍为 `t3-caa6a7be/artifacts/full-app-acceptance/layout`。唯一业务日志输入为此 family 的 `LocalState/Logger/log-2026-10-30.txt`。没有枚举或读取其他 LocalState 文件，也不检查 PID9608 或 Dev/生产包。
- [分析脚本](analyze-48-live-errors.ps1) 使用 FileAccess.Read / FileShare.ReadWrite，仅内存分块；时间窗是本地 **[12:45:00,13:08:00)**，即包含整个 13:07 分钟。按 Serilog 时间头至下一个时间头划分块，非按任意异常关键字拼接。GlobalDependencies.cs:196–209 核实应用使用默认文件 sink。无 PDB 源行号的日志不伪造行号。
- [白名单汇总](diagnosis-48-live-errors-summary.json) 保存固定类别、允许的异常类型链、已核实无参数的方法名、时间计数、明确 SocketException 数值错误码、首尾块 SHA256 及有序块摘要。哈希基于保留换行的解码块文本重新编码 UTF-8，不冒充原文件 bytes hash。每组所有块 hash 以 LF 串联再 hash，便于同源重算；不保存完整 log 或任意 Message。

## 同块证据

| 类别 | 块数 | 外层 → 内层序列化类型 | 数值码 |
| --- | ---: | --- | --- |
| 分片失败 | 1953 | RichasyKernel.KernelException；无序列化 InnerException | 无可提取码 |
| 搜索建议失败 | 6 | TaskCanceledException → TaskCanceledException → IOException → SocketException | SocketException 995 |
| 其他错误（正文不保留） | 9 | TaskCanceledException | 无 |

分片组起止为 **12:49:34.546–13:01:30.303**。每一个块都在内存精确匹配源码固定的空响应错误类别；所有分片块均具有 `ThrowIfResponseInvalidAsync → SendAsync → GetSegmentDanmakusWithGrpcAsync` 栈路径，且检查到 DanmakuLoadSession 帧。没有将用户/服务器任意错误文本回传。

6 个 IOException/SocketException 都位于搜索建议的 6 个块内；9 个其他块集中于 12:59:04.262–12:59:04.302。它们**不是分片块的 InnerException**。这足以排除“看到整个窗口的网络取消类型，所以所有分片失败都是取消”的归因；不能进一步证明不同业务没有共享网络环境因素。先前摘要的 TaskCanceledException 15 次不是完整内层类型出现次数，本次 6 个搜索块各有两层该类型，不矛盾于按块统计但不可混用。

## 实际源码如何产生这些日志

以下位置均为当前源码定位，不是日志自带的源行号。kernel 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，主仓 src/externals 与部署基线无差异。

1. 请求中提及的 `HttpClientExtensions` 在此 kernel 的受检路径不存在；实际实现是 `BiliHttpClient.cs` 与 `BiliHttpClient.Extension.cs` 的 partial class，日志也匹配 `Richasy.BiliKernel.Http.BiliHttpClient.ThrowIfResponseInvalidAsync`，而非 HttpClientExtensions。
2. `externals/bili-kernel/src/BiliKernel.Core/Http/BiliHttpClient.Extension.cs:25`：先 `EnsureSuccessStatusCode`，再按响应类型检查。二进制/gRPC 路径少于 **5 bytes**（:38–40）或文本路径为空/空白（:47–49）抛出同一个固定 KernelException。当前日志没有分支、字节长度、响应类型或确切 HTTP 状态码，因此不能区分这两个空响应分支；但按源码可确定它已经越过 HTTP 非成功状态检查。**不能写成具体 HTTP 200**。
3. `BiliHttpClient.cs:78` 的 SendAsync 对 KernelException 原样传播；对调用方取消且 token 已取消的 OperationCanceledException 原样传播；HttpRequestException 和其他异常保留 inner 包成 KernelException。超时配置为30秒（:40），未取消 caller token 的超时会包装，不能当作 caller cancelled。
4. `DanmakuClient.cs:108–154` 的 gRPC 分片请求在每次尝试前、catch 中均检查传入 token；非取消异常三次尝试后抛出最后一次异常，没有退避。Web 路径 :56–104 同理。**1,953 是最终失败发布块，不是 HTTP 请求数**。按代码这些调用已走完失败重试；前两次是什么失败、是否成功发到网络以及精确发送总数均不在日志中，不能简单认定 5,859 次空响应或网络发送。
5. `BiliHttpClient.cs:140` 附近 protobuf ParseAsync 位于 SendAsync 返回之后，从响应 bytes 跳过5字节再解析。当前最终异常来自校验器，不是 protobuf/decode 异常。5字节恰好空帧不会被 `<5` 条件拒绝；不能把所有“无弹幕”都等同本错误。业务 JSON 失败在 Extension.cs:55 有另一错误分支；显式 HTTP 429/non-success 会进入另一异常路径。当前块不支持业务错误码或限流结论；服务以成功状态空体响应的上游原因仍可能多种，未排除隐式服务策略。
6. `DanmakuLoadSession.cs:62–121` 为每次 load 自建 CTS、每会话6个并发槽。这个 token 传给 `IDanmakuService.GetSegmentDanmakusWithGrpcAsync`，再直接传 DanmakuClient 和 HttpClient。不是 RelayCommand 的自动取消 token，没有 CancelAfter。Cancel 先使 `_current=null` 再取消 CTS；替换、重新初始化、重载、Dispose 触发它。取消异常被过滤，晚到发布需在 dispatcher 回调内取得锁并确认当前会话。
7. `PlayerViewModel.Danmaku.cs:76` 的日志来自上述有效会话的 Error 发布。旧会话取消后发布会丢弃；不能把此处已发布的空响应异常解释成“正常取消被记录”。也不能由此证明所有其他真实在途请求都完成取消。非取消分片失败被保留为部分结果，最终计数是成功 items 数量，不是远端完整性校验。

只读比较旧 kernel `6501985d0f30c55c28b3f78c6e045c2536e1d389`：两文件差异只有 handler 构造 seam、caller-cancel catch 和四处重试取消检查。空响应判断、protobuf 跳过5字节、30秒超时、普通失败三次重试均未在 #48 修改。未切换 checkout 或回滚 app。故已证实的空响应行为不是新增加的错误类别，但本日志不能独立判定整体是否有 #48 时序回归。

## 上游候选：当前 P 使用了稿件时长（尚非本次实测根因）

静态高置信发现：`PlayerViewModel.Danmaku.cs:42–44` 取当前 `_part.Identifier.Id` 为 cid，却用 `_view.Information.Duration` 计算 `ceil(duration/360)` 个分片（:63）；不是 `_part.Duration`。kernel `VideoAdapter.cs:150–160,411–423` 把稿件 Arc.Duration 放入 Information，`:259–270` 单独把 page duration 放入 VideoPart；Web adapter 也分别保存稿件和 part duration。PGC 当前路径使用 `_episode.Duration`。

因此稿件时长显著大于当前 P 时长时，客户端会计划超过当前 P 时长的分片。这是**确定的源码取值差异**，对多 P 课程的大量空响应是值得优先检查的候选。旧 app `90f3577` 的同文件已经使用 Information.Duration，非 #48 新引入。日志没有 cid、segment index、planned count、两种 duration 的安全关联；本次不从时间接近或反复出现的315块推导每次播放因果，不声称已证明所有1,953块都是越界分片，也不能断言 PGC 没有错误。

最低安全补证（父级另行安排，不在本轮执行）：在独立离线真实 VM fixture 中给稿件时长7200秒、当前 P 时长120秒，记录受控服务收到的计划分片数与索引，验证实际生产命令是否请求20段而当前 P只需1段；随后使用与已知合法首段/越界段相符的合成空体响应检验错误分类。这证明算法风险，不证明真实服务器原因。若仍需 live 因果，仅采非身份的临时 session ordinal、segment index/attempt、稿件/part时长、planned count、caller-cancel布尔、响应字节长度、固定内容类型类别、显式数值状态/错误码及最终分类；不采 URI、正文、认证字段或媒体/账户标识。须经父级决定采样方式，不能擅加生产遥测或认证网络捕获。

## 重载入口和可达调用图

已对全部 src/tests 中 ReloadDanmaku/ReloadCurrentSource/Reload*Async、播放器 XAML/菜单与键盘路径检索，核对以下调用：

- `ReloadDanmakuAsync` 在 PlayerViewModel.Danmaku.cs:100 声明 RelayCommand（AllowConcurrentExecutions=true），有播放初始化和窗口存活 guard。**没有生产 XAML 或代码调用 ReloadDanmakuCommand 的入口**。它是可供离线真实 VM 调用的生成命令，不是用户已可点击的按钮。
- `ReloadCurrentSourceCommand` 唯一命中是 `PlayerOverlay.xaml:209–214` 注释内错误页按钮；没有对应当前 PlayerViewModel 方法/命令实现。不能描述成“已有命令，仅按钮隐藏”，更不能直接纳入真实命令级验收。
- `PlayerViewModel.Methods.cs:748–895` 的按键处理提供空格、方向、Shift+方向换上/下集、速度、全屏、小窗、章节、统计和Esc；没有重载。错误页只展示 ErrorMessage。搜索建议/字体/二维码等 Reload 命令与弹幕重载无关。
- 普通换 P：`VideoPartsSection.xaml.cs:58` SelectionChanged guard（必须是不同 P）→ VideoPlayerPartSectionDetailViewModel.SelectPartCommand → VideoConnectorViewModel.Methods.cs:80 的回调设置 PreferPart → Parent.InitializeCommand。重复选中当前 P被 guard拒绝，不能冒充重载。
- PGC：PgcEpisodesSection 的 PlayCommand 绑定及 EpisodeItemViewModel → PgcPlayerEpisodeSectionDetailViewModel.PlayEpisode → PgcConnectorViewModel.ChangeEpisode → NewMediaRequest → PlayerViewModel.OnConnectorNewMediaRequest（:700）→ InitializeAsync。ChangeEpisode 拒绝同一 episode。播放器 PlayNext/PlayPrevious 和 Shift+方向也是真实切源路径，不是重载。
- 初始化：InitializeAsync 首先递增播放代次并 Cancel；OnPlayerPropertyChanged 捕获代次，完成媒体准备的 awaits 后再次验证代次/窗口，再发 InitializeDanmakuCommand（Methods.cs:633–663）。关闭：PlayerWindow.Close → AppWindow.Destroy → PlayerViewModel.OnWindowDestroying（:487）→ DisposeAsync，首先 `_isDanmakuClosed=true` 和会话 Dispose（PlayerViewModel.cs:28–29）。集成窗口还可能关闭主窗口。

结论：**真实 UI 连续同源重载不可触发**；`ReloadDanmakuCommand` 的真实 VM 命令级重载可在离线受控环境补证；不存在的 ReloadCurrentSourceCommand 不能要求运行。不可为验收新增产品按钮，也不可向已登录生产进程反射注入。

## 剩余最小验收方案（全部待执行，不是结果）

1. **真实公开 UI 接线**：父级安排桌面后复用保留包和登录状态，重新按注册 EXE 发现 owned PID/HWND。只补普通换P、PGC换集、关闭这三类路径；观察可交互控件、当前标题/画面/最终计数和关闭/再开。历史已登录交接已经覆盖有限终态，不必重复搜索/窗口模式等所有已绿场景。遇到禁用按钮或旧 selector 先重定位，不用 UIA 绕过不可交互状态。加载提示只证明媒体状态，不代表弹幕 HTTP 在途。
2. **真实生产 VM + 实际 Dispatcher 的离线屏障**：另建明确独立身份的验收宿主（不改当前包、不借登录数据），编译原 PlayerViewModel、真实 connectors/selection commands、Danmaku VM；记录源码和实际 DLL hash。通过既有服务注册 seam替换 IDanmakuService、媒体发现/播放服务和必要环境依赖；禁止复制一个简化 PlayerViewModel 或反射改私有状态。若现有依赖不能无改动构造真实 VM，应先报告具体构造阻碍，不把 coordinator fixture冒充真实 VM。
3. **四个必要交错**：普通P A→B、PGC A→B、同源 ReloadDanmakuCommand 连续调用、Close/Dispose。用 TaskCompletionSource 屏障等到真实服务入口已开始才触发替换/关闭；服务故意忽略取消并在替换后逆序返回旧结果/旧错误；在真实 dispatcher drain 后核对只有当前 cid对应的合成内容进入 Danmaku.Initialize、CountText/Loading/日志归属正确，关闭后无发布/新请求。验证每个旧服务 token 已取消和排队段未开始。Close 要分别保留真实窗口事件接线与直接 Dispose 命令层证据，不能混称同一保真度。
4. **连接既有 HTTP 证据**：既有15项只覆盖7会话+8传输用例；csproj source-link Danmaku/*.cs，未实例化 PlayerViewModel。离线 VM至少一条路径让真实 DanmakuService/BiliHttpClient 经受控 handler到达 SendAsync屏障，再从真实 VM触发取消，核对 handler观测取消与无后续尝试；仅替换 IDanmakuService 的 token观察仍不能证明同一路实际 transport接线。现有普通失败测试只是 handler抛 HttpRequestException；新增空体/正确空帧以及前述时长差的最小边界用例即可，不重跑所有已绿矩阵来掩盖连接缺口。
5. **完整性边界**：真实网络计数仍需成功段数、失败段数、有效段上界的独立核验才能称完整。离线合成数据可准确验证 count==items.Count 及迟到隔离，但不能推出真实网络全量成功，也不能从正常进程退出推出网络取消已经完成。

优先下一步：父级先复核本汇总和实际校验器源码，再安排**无认证的真实 VM时长差/空响应屏障用例**；确认失败来源前保持真实验收 PARTIAL，不洗为取消、不直接修生产。

## 交付检查

只新增本报告、白名单 JSON 和只读分析脚本。字段来源：包 family来自精确注册查询；时间/异常类型/995来自指定日志块；固定空响应类别/方法名来自已核对源码；hash为脚本计算；其他数字如30秒/三次/六并发是源码常量；没有从公开信息推断私人账户字段。

已实际运行脚本并核对1953/6/9分组，重新运行结果统一 CRLF/LF 后逐字一致（首次直接比较因换行规范不同失败，随后核对各组 hash 并规范换行复算通过）；PowerShell语法解析、git diff --check、针对认证字段/媒体地址参数/账户键的敏感模式扫描通过。扫描不能替代白名单审查，交付无原始日志、任意远端 Message、认证请求或私密图片。主仓生产路径与 c9ac082 无差异、kernel HEAD不变且clean。最终同步 `feat/performance-45`（仍为 c9ac082），提交SHA和clean状态由交付消息给出。
