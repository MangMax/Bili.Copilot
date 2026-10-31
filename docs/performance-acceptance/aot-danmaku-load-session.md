# DanmakuLoadSession 严格接口生成

2026-10-31，输入父提交 `acca8df07108777c8547a46d6b6b909ff9650cdd`，kernel gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`。生产改动仅 `DanmakuLoadSession` 顶层声明增加一个 `partial`。这是应用数据加载 session，与独立 Danmaku CoreRX 引擎资源改造无关；未改调度、渲染、kernel、正式依赖、warning level 或 NoWarn。完整 AOT **HOLD**，#49/CoreRX 独立推进。

## 接口与实际使用路径

依据 [payload 台账](aot-payload-and-compatibility.md#严格诊断编译阻塞与可达性分开) 和 [HTTP handler 集成台账](integration-aot-http-handler.md)，区分严格生成阻塞和运行故障。

`DanmakuLoadSession : IDisposable` 对应 CsWinRT 的 `Windows.Foundation.IClosable` 映射。唯一生产构造点为 `PlayerViewModel.Danmaku.cs`，实例仅保存于私有 `_danmakuLoadSession` 字段。调用方传入 IDanmakuService、dispatch 和 publish 委托，直接调用 LoadAsync/Cancel/Dispose。DispatcherQueue.TryEnqueue 接收包装 Action 的回调，session 是闭包间接捕获对象；没有发现 session 本体进入 DataContext、Tag、Content、ItemsSource、WinRT 方法参数或 DI/WinRT 注册。持有 UI 回调并不证明 session 本体跨 ABI。本片满足 level 2 严格接口生成要求，不声称修复了已复现的生产事故。

顶层类型没有需要补 partial 的包含类型；私有 `Session` 只保存 CancellationTokenSource/Completed，不实现 IDisposable，不作额外改动。生成属性将 DanmakuLoadSession 关联到详情类型，GetExposedInterfaces 使用 `IDisposableMethods.IID` 和 `AbiToProjectionVftablePtr`。

## 保留的行为与测试边界

去掉新增的一个 partial 后，整份源码与输入父提交一致。锁、先失效再取消、SemaphoreSlim(6,6)、取消等待不释放未取得的许可、迟到结果抑制、非取消失败部分结果、CTS 的 finally 释放均不变。PlayerViewModel 在 InitializeCoreAsync/换 P/reload 取消旧请求，在 CloseAsync/DisposeAsync 入口 Dispose session；这些消费端未改。

既有测试直接链接真实 Danmaku 源码，以受控 IDanmakuService 作为传输边界。未新增镜像测试。修改前后 session 各 **Passed: 7, Failed: 0, Skipped: 0**，fresh 全套前后各 **Passed: 535, Failed: 0, Skipped: 0**。TRX 验证逐结果 Passed、executionId 唯一，前后名称集合相同。

| 保持要求 | 真实 session 回归 |
| --- | --- |
| 换 P 标识、取消旧请求、late 结果与排队发布隔离 | Switch_LateOldResult_OnlyCurrentSessionPublishes |
| 6 个 active + 排队分片，Cancel/Dispose 后停止调度发布 | CancelOrClose_SixActiveAndQueuedSegments_StopsSchedulingAndPublishing（false/true） |
| Close 后排队完成/迟到失败不发布，拒绝新加载；重复 Dispose | Close_QueuedCompletionAndLateFailure_PublishesNothingAndRejectsNewLoads |
| 同媒体连续 reload 仅最后结果/计数发布 | Reload_ContinuousSameMediaLoads_OnlyLastResultAndCountPublish |
| 普通失败释放许可、最大并发6、保留其余19份结果 | Load_FailedSegment_ReleasesPermitAndPublishesRemainingSegments |
| 空媒体不请求、完成为空计数 | Load_EmptyMedia_CompletesWithEmptyCountAndNoRequests |

这些是无 UI 的真实 session 行为证据，不等于原生窗口关闭或真实 AOT session 验收；消费端调用顺序另由源码审查支持。

## 证据与复放

strict 红 **350 = 303 MVVMTK0045 + 46 MVVMTK0046 + 1 CsWinRT1028**，绿 **349 = 303 + 46**，两者均 exit 1。按路径、行列、编号、完整消息去重比对，唯一删除 DanmakuLoadSession 的1028，零新增，其余349条逐项相同。两侧另有同一条既有 protobuf unused-import warning。这里没有执行完整无抑制 ILC，不称整个 AOT 清零。

1543 份 strict 输入逐文件对比，唯一源码差异为目标 partial。生成源码与最终 DLL 的详情类型相互对应；Metadata 工具验证属性指向真实存在的类型、GetExposedInterfaces 有 IL、CodeView 与 portable PDB identity 匹配，并逐文档重新散列磁盘源码。源生成文件同样包含在 PDB/source 核验中。

应用 Debug/Release 新构建均 exit 0，各仅既有 protobuf warning；最终接口方法 IL 分别为52/47字节，两配置各2595份 PDB 文档全部存在且匹配。完整 verify.py 复算 exit 0。Python 首次重读超长生成路径失败，补 Windows extended-path 前缀后复算通过；这是工具路径处理修正，不是产品红灯。

[verify.py](evidence-danmaku-aot/verify.py) 复算完整诊断集合、strict 输入源码、测试 TRX、生成接口及最终 PE/PDB/source 闭包。小证据见 [verification.json](evidence-danmaku-aot/verification.json)，大产物保持 ignored，位置/长度/SHA256 见 [large-artifacts.json](evidence-danmaku-aot/large-artifacts.json)。Metadata 工具只读 PE/PDB，不加载应用 initializer，不激活应用；未新增动态接口 probe，不声称执行 COM QueryInterface 或 native 接口调用。

strict 两侧从没有 bin/obj 的 export 构建，green 显式复制本地目标源码，避免 HEAD 导出遗漏修复。[replay-strict.ps1](evidence-danmaku-aot/replay-strict.ps1) 要求新输出路径以保护红灯。复用既有 audit-build 脚本，移除副本既有 IL/MVVMTK 抑制，保持 level 2、分析器和 warnings-as-errors；仅副本 WinUICookies 项目局部启用 AllowUnsafeBlocks 以越过既有1030。没有全局 unsafe 参数，不声称正式 kernel 1030 已解决。

```powershell
pwsh -NoProfile -File docs/performance-acceptance/evidence-danmaku-aot/replay-strict.ps1 -OutputRoot "$pwd/artifacts/danmaku-aot-replay"
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter 'FullyQualifiedName~DanmakuLoadSessionTests' --logger 'trx;LogFileName=session-after.trx' --results-directory artifacts/danmaku-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=all.trx' --results-directory artifacts/danmaku-aot/tests
# Debug/Release 分别执行；正式构建保留项目既有 NoWarn，不替代 strict 审计。
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/danmaku-aot/generated-debug" -bl:artifacts/danmaku-aot/debug.binlog
dotnet run --project docs/performance-acceptance/evidence-danmaku-aot/Metadata/Metadata.csproj -c Release -- src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.pdb artifacts/danmaku-aot/debug-pdb.json
python docs/performance-acceptance/evidence-danmaku-aot/verify.py
```

## 环境与审查

初始本地 kernel HEAD 为 `f02c33f639addd5d421700c43fa66382beafe323`，索引0项、源码未展开，root 显示 kernel 修改。保存初始 status/HEAD、working/index binary patch，确认 gitdir 属于本 worktree 后，只读 fetch 父 kernel 精确对象，恢复本地到固定 gitlink，结束 clean。媒体资产只复制现有 handler checkout 的 libmpv/ffmpeg 到本 checkout；未下载。准备阶段不存在路径的读取错误不计入产品红灯。

WinUI review/dev 静态审查：没有 UI/XAML、公开 API、异步顺序或资源所有权变化。未新增独立 WinUIAnalyzer 包，其检查未运行。没有 desktop、UI、注册、部署、登录、安装、UAC、进程终止或受保护身份修改；无子代理、push、PR、issue 或父工作区写入。

交付前 `git merge --ff-only feat/performance-45` 返回 Already up to date，父最新仍为 `acca8df0`。native/真实 AOT session 仍待实机，完整 AOT HOLD 不变。
