# 图片 HTTP handler 的严格接口生成

2026-10-31，输入父提交 `7aa55712bd1ffef840df9ace9b1c0a1300c1af5c`。生产差异仅 `InternalHttpExtensions` 和其私有嵌套 `CustomHttpClientHandler` 各增加一个 `partial`。未改 DanmakuLoadSession、依赖、kernel gitlink、网络行为或项目配置。完整 AOT **HOLD**，#49/OX-R1 独立进行。

## 传参与生成要求

按 [payload strict 审查台账](aot-payload-and-compatibility.md#严格诊断编译阻塞与可达性分开) 追踪实际调用：handler 唯一构造位置是 `InternalHttpExtensions.GetImageClient`，传给 `new System.Net.Http.HttpClient(handler)` 后只由静态 `ImageClient` 间接持有。BasicCoverImage、BlurCoverImage 的托管 `GetCustomHttpClient` override 返回此 HttpClient；RecommendationCoverImage 调用其 GetByteArrayAsync。返回值是 HttpClient，不是 handler 本体。仓库没有 handler 的 DI/WinRT 注册，也没有把它放入 DataContext、Tag、Content、ItemsSource 或传给 WinRT 方法的路径。

继承链 `CustomHttpClientHandler → HttpClientHandler → HttpMessageHandler → IDisposable` 对应 CsWinRT 的 `Windows.Foundation.IClosable` 映射。严格 level 2 要求类型及包含类型均 partial，才能向嵌套类型附加生成属性。此修复满足严格生成要求；不将持有 WinUI 控件的图片调用路径推定为 handler 本体跨 ABI，也不声称已观察到运行故障。第三方图片控件的全部内部路径及真实 AOT 运行未在本片验收。

`SendAsync` 仍将 HTTP 版本设为 2.0、策略设为 RequestVersionOrLower，原 cancellationToken 原样传给 base.SendAsync 并 ConfigureAwait(false)。未增 catch、超时或重试。HttpClient 默认拥有 handler，静态客户端的持有方式及继承的 Dispose 实现不变；仓库未见新增或原有 ImageClient.Dispose 调用。kernel 的调用方取消重抛、非调用方 timeout 包装和弹幕重试均未改。

## 红绿与最终产物

[verify.py](evidence-handler-aot/verify.py) 检查完整错误集合、逐文件源码、TRX、PDB 文档与源码散列；结果见 [verification.json](evidence-handler-aot/verification.json)，大文件位置和 SHA256 见 [large-artifacts.json](evidence-handler-aot/large-artifacts.json)。两个 strict export 从无 bin/obj 的源码开始，使用正式依赖；green 显式复制未提交的目标源码，避免 HEAD 导出遗漏修复。

strict 红 **351 = 303 MVVMTK0045 + 46 MVVMTK0046 + 2 CsWinRT1028**，绿 **350 = 303 + 46 + 1**；两者仍 exit 1。按路径、行列、编号、完整消息去重，唯一删除 handler 的1028；零新增，其余逐项保留，DanmakuLoadSession 的1028仍在。完整集合压缩提交，未用总数相同代替逐项比较。

隔离审计移除副本原有 IL/MVVMTK0045/0046 NoWarn，保留 warnings-as-errors、分析器和 CsWinRTAotWarningLevel=2。与旧审计不同，本次仅在两份 export 的 `Resolvers.WinUICookies.csproj` 加局部 AllowUnsafeBlocks，以越过既有 kernel Dictionary 生成的1030；没有全局 unsafe 参数，没有修改正式 kernel，也不声称1030已修复。两副本该项目改动完全一致，生成/分析器配置未降级。

应用 Debug/Release 均新构建 exit 0，各仅一条既有 protobuf unused-import warning。最终 DLL 包含 handler 的 WinRTExposedType 属性及有 IL 的 GetExposedInterfaces（Debug 52字节、Release 47字节）。只读 PE 验证属性指向真实存在的详情类型；CodeView/portable PDB identity 对应，各2594份 PDB 文档均存在且与磁盘源码 hash 相符。生成源码明确映射 IDisposableMethods.IID/AbiToProjectionVftablePtr。普通构建保留正式配置中的既有 NoWarn，因此其成功与 strict 审计是两种证据，不能相互替代。两份 strict 输入共1543份源码逐一比对，完整 verify.py 复算 exit 0。

Models 直接链接**完整、未抽取或改写**的 InternalHttpExtensions.cs，保留真实外层及所有成员。红模型产生1028，同一 probe 因缺 WinRTExposedType 属性失败；绿 Debug/Release 检查 IClosable IID `30d5a829-7fa4-4026-83bb-d75bae4ea99e` 及非零 vtable 通过。Models 的 DLL/PDB/source 同样核验；WinRT.Runtime 与对应完整应用输出逐字节相同。probe 只读取类型属性并调用生成详情，不构造外层静态客户端或 handler、不联网、不加载应用 initializer；不等同于 COM QueryInterface、native AOT 或 UI 验收。

## 真实逻辑回归与边界

fresh 基线及修后全套各 **Passed: 535, Failed: 0, Skipped: 0**；修改前后 HTTP/取消筛选各 **Passed: 32, Failed: 0, Skipped: 0**。TRX 逐结果及前后名称集合一致。没有新增镜像测试。

| 保持要求 | 既有真实逻辑用例 |
| --- | --- |
| 调用方取消传播 | HttpSend_CallerCanceled_PreservesCancellation |
| timeout 保持包装 | HttpSend_TimeoutWithoutCallerCancellation_KeepsWrappedFailure |
| 取消后不重试、不发送 | Segment_CanceledDuringSend_DoesNotRetry、Segment_AlreadyCanceled_DoesNotSend（web/grpc） |
| 普通失败仍三次尝试 | Segment_OrdinaryFailure_RetriesThreeTimes（web/grpc） |

这些用例使用真实 BiliHttpClient/DanmakuService；32 项还包括 MarkdownImageLoaderTests。它们不直接覆盖私有图片 handler 的线上 HTTP 或 Dispose；handler 行为不变由去掉两个 partial 后整份源码与父提交一致支持，不能用535测试替代其 AOT 运行证据。

WinUI review/dev 静态审查未发现本片 UI/XAML、公开成员、异步顺序或资源所有权变化。没有新增 WinUIAnalyzer 包，该独立分析器未运行。无 desktop、登录、UI、部署、注册、安装、UAC、进程终止或现有包修改。无子代理、push、PR、issue 或父工作区写入。

## 环境与复放

初始 kernel HEAD 为 `f02c33f639addd5d421700c43fa66382beafe323`，索引0项，源码未展开；保存 initial-root/initial-kernel、working/index binary patch 后，仅本工作区从父 kernel 只读 fetch 精确对象并恢复至 gitlink `0ae5e4e000a41a07f6696746706df11c06e8835b`，结束 clean。媒体资产复制自现有 `t3-4b0da1fe`，没有下载、替换受保护布局或触碰 PID。准备阶段两处路径查询失败与 probe 初稿缺括号的编译失败留有日志；修正 probe 后重新取得真实产品红灯，没有将工具错误算为红灯。

复放使用新 artifacts 路径，不覆盖原始 red。[replay-strict.ps1](evidence-handler-aot/replay-strict.ps1) 用现有 export-source.ps1 建两个 export，红目标源码取上述父提交，绿复制当前目标源码；仅副本 WinUICookies.csproj 加局部 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`，再调用 audit-build.ps1，**不传** AllowUnsafeForAudit。其余命令如下（两种配置分别执行）：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter "FullyQualifiedName~DanmakuCancellationTests|FullyQualifiedName~MarkdownImageLoaderTests" --logger "trx;LogFileName=http-after.trx" --results-directory artifacts/handler-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=all.trx" --results-directory artifacts/handler-aot/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/handler-aot/generated-debug" -bl:artifacts/handler-aot/debug.binlog
dotnet build docs/performance-acceptance/evidence-handler-aot/Models/Models.csproj -c Debug "-p:HandlerSource=$pwd/src/Desktop/BiliCopilot.UI/Extensions/InternalHttpExtensions.cs" "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/handler-aot/models-generated-debug"
dotnet run --project docs/performance-acceptance/evidence-handler-aot/Probe/Probe.csproj -c Release -- "$pwd/docs/performance-acceptance/evidence-handler-aot/Models/bin/Debug/net11.0-windows10.0.22621.0"
dotnet run --project docs/performance-acceptance/evidence-handler-aot/Metadata/Metadata.csproj -c Release -- src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.pdb artifacts/handler-aot/debug-pdb.json
python docs/performance-acceptance/evidence-handler-aot/verify.py
```

交付前两次 `git merge --ff-only feat/performance-45` 均 Already up to date，父最新仍为 `7aa55712`。完整 AOT 实机运行仍待验，不推进完整采用 gate，不关闭 #49，不替代 OX-R1。
