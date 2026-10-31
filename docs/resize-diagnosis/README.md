# 累积推荐列表 resize：本机因果验证与最小修复

起点为 `3f607587cdda285f0331068427ec8ad433f9b0e3`。生产仅修改
`Controls/RecommendationCoverImage.cs`，三引擎、seek 队列、依赖版本、列表条数、
增量刷新和撤销算法均未修改。

真实离线宿主复现了推荐页的 resize 阻塞。`SizeChanged` / `XamlRoot.Changed`
原来按每一个不同物理尺寸启动 WIC 解码；原生解码不能提前取消，旧请求仍在执行时
新请求已经开始，随后集中回到 UI 线程创建 Win2D surface。AppRepeater 的包内 XAML
设置 `VerticalCacheLength="20"`，本轨迹只有 4–6 张可见卡片，却保留了 76 张 realized
卡片参与该流程。从 200 累积到 500 条时 realized 仍为 76，不支持“本次整个长列表
虚拟化完全失效”的解释。纯 resize 的列表更新次数为零，也没有执行刷新锚点恢复。

修复将尺寸请求合并到 200 ms 稳定窗口，并将原生解码至 surface 提交的全局并发限制为 2。
布局继续响应，现有 surface 随控件缩放；源变化立即处理，最终仍按当前 DPI 和实际尺寸
解码。等待槽位可以取消；已进入 WIC 的 `AsTask()` **没有传 cancellation token**，
流在原生操作完成前保持有效，之后才拒绝旧结果。卸载停止尺寸 timer 并取消请求。

只有防抖的版本曾失败，不能省略并发上限。失败记录 `green1` 中 80/200 条的心跳最大
间隔仍为 789/715 ms。单变量固定 600×338 请求尺寸的诊断版则没有新增解码，心跳最大
间隔为 130/103/130 ms；它只用于定位，没有进入生产补丁。

## 同源红绿实测

`red-final` 与 `green-final` 使用同一真实 PopularMainBody、IncrementVerticalScrollControl、
AppRepeater、UniformGridLayout、VideoCardControl 模板和封面代码。
推荐通过生产 `RefreshVideoCommand`（每次连续三批）累积，热门通过生产加载命令累积。
只替换发现服务与卡片数据适配器，禁止网络，图片为固定本地 1600×900 PNG。
不是直接创建空白卡片。目标 50/200/500；推荐首档因真实加载及刷新批次达到 **80**，
没有截断至 50。每档 resize 前后条目数完全相同。

| 推荐条目数 | realized | UI 心跳最大间隔，红→绿 ms | resize 调用 p95，红→绿 ms | WIC decoder 创建，红→绿 | 原生流水线峰值，红→绿 |
| --- | --- | --- | --- | --- | --- |
| 80 | 76→76 | 522.1→123.6 | 244.7→74.5 | 2576→304 | 109→2 |
| 200 | 76→76 | 424.6→123.1 | 178.7→65.6 | 2577→257 | 111→2 |
| 500 | 76→76 | 421.2→124.1 | 144.5→57.2 | 2833→250 | 110→2 |

对应心跳最大阻塞降低约 70–76%，decoder 创建减少约 88–91%。整个宿主在轨迹期间的
托管累计分配约 2.15–2.24 GB→146–154 MB；这是累计分配，不是存活内存，也不是
原生/GPU 内存。封面 measure/arrange 次数没有降低：例如 500 条 measure 2750→4977、
arrange 2857→5442，说明响应改善不是通过停止布局取得的。原始计数、每次 resize 耗时、
全部心跳间隔及后台进程样本见证据。

热门页实测 50/200/500 条，realized 为 50/50/50，心跳最大间隔 75.5/71.2/64.3 ms，
resize p95 35.6/35.4/35.1 ms，均通过。它使用 BasicCoverImage；分区/搜索也复用
AppRepeater/VideoLayout，但使用各自的 BasicCoverImage 模板。此次没有证明这些页面
存在相同解码问题，也没有改动它们或宣称全部页面已修复。

尺寸轨迹为后台线程对 owned HWND 调用原生 SetWindowPos：120 步，宽度从 1400 到
1580 再返回 1403 物理像素，高 900，调用返回后间隔 16 ms，DPI 固定 1.5。
UI 使用独立 16 ms dispatcher 心跳；不是恒定墙钟频率注入，也不是帧率测试。
采样段后额外观察 800 ms；500 条绿样本此时仍有 1 个在途请求，不声称全部封面
在 800 ms 内完成。其后功能检查确认最终物理像素尺寸及重挂加载。
进程 CPU/内存/线程采集每 500 ms 在外部 PowerShell 进行，没有在 UI 线程查进程。
最终红绿采样期间没有并行构建。其他用户进程未暂停，包括仍运行的 AotDev PID 19868，
所以优先采用受控事件计数与重复红绿证据，不依据一次 CPU 数字声称全局收益。

## 验证与复跑

| Requirement | Evidence |
| --- | --- |
| 原问题可重放且能判红 | `verify.py red-final` 实际 exit 1；三档最大心跳均超过 200 ms |
| 修复真实 resize 阻塞 | `verify.py green-final`、`verify.py hot-final` 实际 PASS；不减少列表或 realized 数 |
| 精确 undo 序列和视口 | native `UndoExactReferenceSequence`、`UndoExactViewportAnchor` PASS |
| 最终 DPI 清晰度 | native `FinalPhysicalPixelDecode` PASS，检查真实 CanvasImageSource.SizeInPixels |
| 迟到尺寸结果、卸载、重挂 | native `SourceClearedDuringResizeRejectsOldResult`、`UnloadCancelsPendingResize`、`RemountLoadsCurrentSize` PASS |
| 既有逻辑回归 | targeted：Passed 98, Failed 0, Skipped 0；full：Passed 572, Failed 0, Skipped 0；98 是 572 的子集，不相加；原始 TRX 已封存 |
| 生产应用构建 | Release x64 JIT 全量 Rebuild exit 0，143.74 秒，1 个既有 protobuf unused import 警告、0 错误；未抑制新增警告 |

测试使用仓库固定 .NET SDK 11.0.100-rc.3.26522.148 / xUnit2 / VSTest。
200 ms 是本次“严重阻塞”回归门槛，不是 60 fps 承诺；仍有约 100 ms 的推荐布局/调度间隔。
没有实测跨显示器 DPI 切换、AOT 发布或用户登录态；DPI 变化路径保留并按物理尺寸合并。
没有新增/升级 WinUIAnalyzer，不能声称它检查通过。原像素封面局部截图位于证据中的
`red-final/cover-crop-1x.png`、`green-final/cover-crop-1x.png`，已经人工视觉检查无空白封面。

```powershell
# 新目录准备，不能覆盖已经注册的 owned layout。
./docs/resize-diagnosis/prepare.ps1
# --baseline 固定读取 3f607587 的封面；去掉它即本工作区候选。
python docs/resize-diagnosis/instrument.py artifacts/resize-diagnosis/host --refresh --baseline
dotnet build artifacts/resize-diagnosis/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
./docs/resize-diagnosis/run.ps1 -Stage new-red
# 新 runner 会在保留结果、退出观察及清理后以失败结束红样本。
python docs/resize-diagnosis/instrument.py artifacts/resize-diagnosis/host --refresh
dotnet build artifacts/resize-diagnosis/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
./docs/resize-diagnosis/run.ps1 -Stage new-green
./docs/resize-diagnosis/run.ps1 -Stage new-hot -Action HotResizeChecks
python docs/resize-diagnosis/verify.py artifacts/resize-diagnosis/new-green
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:NuGetAudit=false --filter 'FullyQualifiedName~CoverDecodeSizeTests|FullyQualifiedName~Recommend|FullyQualifiedName~PopularPageLoadingTests|FullyQualifiedName~IncrementVerticalScrollControlTests' --logger 'trx;LogFileName=targeted.trx' --results-directory artifacts/resize-diagnosis/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -p:NuGetAudit=false --logger 'trx;LogFileName=full.trx' --results-directory artifacts/resize-diagnosis/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -t:Rebuild -c Release -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:NuGetAudit=false "-p:SolutionDir=$PWD/src/"
```

宿主唯一身份 `BiliCopilot.Resize5a5e.Owned`，所有布局位于本工作区
`artifacts/resize-diagnosis/<stage>/layout`；每次核对精确 PID/exe 路径。
最终红/绿/热门 PID 分别 53472/44672/22564，关闭后继续观察至少 32 秒，均退出、
精确 owned 路径的 Application 1000/1001 WER 查询为空；随后注销 owned 身份并 yield。
早期 runner 从已退出 Process 对象取 Path 得到 null，该缺陷保留在旧记录；最终记录
改为启动时保存路径字符串。没有把旧记录冒充完整退出证明。

kernel 初始 f02c33f6 / 474 个暂存删除先保存 HEAD、状态和二进制补丁，无 untracked。
仅从本地只读来源 fetch 指定 0ae5e4e 并恢复本区子模块；父/来源未写入。
首次完整应用 Rebuild 在运行时资产门失败：本区缺少未入库的 libmpv。之后仅从父区
已有 Assets 复制并逐字节核验 x64 libmpv、ffmpeg、BBDown（SHA256 见 runtime-assets.json），
不替换其版本，不读取安装包或运行实例。首次失败日志 build-app.log 原样保留。
初始编译错误、两次启动准备失败、无效的 Python 准备命令、只防抖的性能红样本
均保留或明确记录；没有通过删失败日志获得全绿。保护的 AotDev、正式、Caa6a7be、Dev
实例、凭据和布局均未操作。没有 dump、外部内存读取、反向工程、push、PR、issue 或发布。

证据打包使用 `archive.py`，`evidence-manifest.json` 为原始文件字节 SHA256。
该实现代理自审后交父方安排一次独立 review，不把自审称为独立审查。
自审检查了 timer 停止/重挂、源变化立即处理、相同物理尺寸通知不会不断延后、
semaphore 等待取消不释放未获得的槽位、获得后所有路径 finally 释放、WIC 流生存期、
旧 request/IsLoaded 拒绝、device recovery 原路径保持。生产改动无 XAML、同步 UI 等待、
新依赖或日志采集。最终 owned 进程/注册均为空，保护的 AotDev PID 19868 仍存活，
kernel 0ae5e4e clean，见 final-safety.json。
