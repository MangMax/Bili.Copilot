# 应用与冻结三包的实际集成

父起点 `f3f5fc22068ac0a9f6702dccb29cb009e471a879`，固定独立 review tip
`e23d3f40744ba83dd2a81eee2cf908d068ea18da`，以 `--no-ff` 合入，merge commit
`cdd33625f88c64bca70a0dc10d9a2efc448710b8`。随后同一轮接入仓库本地 feed
与三个中央版本，未在旧 Danmaku preview6 组合上运行应用。

## 生产供应

- [packages/danmaku](../../../packages/danmaku/README.md) 提交原始三 nupkg，合计约332 KiB。
  Core `0.0.1-pxdpi.9dc8c6c9.clean`；Legacy/Share `0.0.1-reconstruct0b4f.final`。
  逐包 SHA256、来源提交、snupkg/PDB/源码归档和恢复命令均在 feed README。
- `nuget.config` 使用相对目录 `packages/danmaku`；三个 exact ID 映射到该源，
  其余包仍使用原有五源及原版本。没有修改其他源 URL、kernel、mpv 或扩大 NoWarn。
  普通 checkout 自带供包，不需要代理全局缓存，不需要生成新同版本包或发布外部包。
- 首次全新隔离恢复遇到 kernel 的 NU1507（多源、无映射、TreatWarningsAsErrors），
  停止本轮 owned restore 后补精确映射并重试。首次日志保留，不冒称首次成功。
  映射后的 restore exit0，0警告/0错误，日志与 `.nupkg.metadata` 均证明从
  当前仓库 feed 安装三包到 `artifacts/danmaku-integration/nuget-packages`。
  其他既有依赖从原源/HTTP缓存恢复；这不是全项目断网构建的承诺。
- `restore.py` 已实际验证并恢复归档全闭包。未重编引擎或重打包。
  `packages/danmaku/verify.py` 可重复检查原始包字节、中央版本与相对映射。

## 验证

**整体接入验证通过。** 使用固定 SDK `11.0.100-rc.3.26522.148`，直接在当前父
checkout 构建和测试，不使用其他 worktree。Fresh 结果：

| 检查 | 本轮结果 |
| --- | --- |
| 完整 BiliCopilot.UI.Tests，xUnit2/VSTest | Passed: 572, Failed: 0, Skipped: 0；exit0 |
| review-only 三个边界测试，单独过滤 | Passed: 3, Failed: 0, Skipped: 0；exit0；不混入572 |
| Debug x64 JIT 全量 Rebuild | exit0，1警告/0错误，174.25秒 |
| Release x64 JIT 全量 Rebuild | exit0，1警告/0错误，163.85秒 |
| Debug/Release `ValidateRuntimeDependencies` | 均 exit0；只检查依赖，不启动应用 |
| 新隔离 NuGet 目录 restore | exit0，0警告/0错误；实际读取仓库 feed |
| app assets/deps 与两种输出配置三DLL | 唯一固定版本，原包→隔离解包→Debug/Release 输出哈希完全相等 |

两次构建唯一警告均是 kernel 原有 `google/protobuf/empty.proto` unused import；
测试编译也保留该警告，没有以修改 NoWarn 取得通过。完整测试链接真实应用逻辑，
使用 headless WinUI adapters，不称为完整 native UI 自动化。
三个单独边界为 `MiddleWriteFinishingLastCannotLeaveMediaAt19`、
`SynchronousNativeCallbackCanEnqueueLatestWithoutDeadlock`、
`ThrowingSynchronousSeekSubscriberStillReleasesQueue`。通过 ignored `review.targets`
条件性引入原 review 文件，不修改主测试源码；没有重新做历史受控红或 native 矩阵。

核心命令（从仓库根执行；完整实际参数数组及 exit 见证据）：

```powershell
dotnet restore src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --packages artifacts/danmaku-integration/nuget-packages --configfile nuget.config -p:Platform=x64 -p:PublishAot=false -p:NuGetAudit=false -v:normal
# 下列命令分别以 Debug 和 Release 执行：
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -t:Rebuild -c Debug -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:SolutionDir="$PWD/src/"
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Release -p:Platform=x64 -p:PublishAot=false -p:SolutionDir="$PWD/src/"
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:NuGetAudit=false --logger 'trx;LogFileName=full.trx' --results-directory artifacts/danmaku-integration/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -p:CustomAfterMicrosoftCommonTargets="$PWD/artifacts/danmaku-integration/review.targets" --filter 'FullyQualifiedName~SeekQueueReviewTests' --logger 'trx;LogFileName=review.trx' --results-directory artifacts/danmaku-integration/tests
```

本轮命令沿用离线验收的 `NuGetAudit=false`，不构成依赖漏洞审计通过。独立三项重跑前需从
本目录证据 ZIP 恢复 `review.targets` 到上述路径；其中相对链接指向仓库已提交的 review-only 源文件。

| DLL | Debug/Release 与原包相同的 SHA256 |
| --- | --- |
| Danmaku.Core.dll | `b15225ecec93b08943ecdbf889c74bebe8873e7851b43cc3c1feb98fc84f97fd` |
| Danmaku.Legacy.dll | `2cc03ebaf8557cbf0abb96344276af717ba7a756541e0223a29a353313adfabf` |
| Danmaku.Share.dll | `0541962accff2ca6823213b5e52449a744f0378dbf61492c82a04a45abfe8329` |

[evidence.zip](evidence.zip) 封存原始成功/失败日志、命令 exit、两份 TRX、实际 app
`project.assets.json`、两种输出的 deps/runtimeconfig、包来源元数据、输出哈希及配置快照。
[evidence-manifest.json](evidence-manifest.json) 提供逐文件 SHA256。
运行 `python -X utf8 -B docs/seek-latest-media/integration/verify.py` 可核验封存记录。
原应用和独立 review 的两个 `verify.py` 也在本轮通过；它们的历史测试数量不计为本轮 fresh 测试。

WinUI 集成自审：生产应用和测试源码与固定 review tip 一致，11文件接线与后续
Playback 队列均保留；speed partial 与父逐字一致，Overlay 只含已审 seek 接线，
卸载/重挂代码未被覆盖。异步队列成功释放节点、关闭/换媒体 drain、旧 sender/closed
防护与订阅/退订配对保持。没有新增 XAML、同步 UI 等待或扩大警告抑制。
此次没有另跑专用 WinUIAnalyzer，也不冒充新一次独立 review。

## 文档更正与边界

原 README 顶部已加醒目更正并指向独立 review，保留旧叙述为历史误判。
Legacy runtime1、runtime3 first-activation/stable 及导出图全部同 SHA256
`120e87529b9e580a016482bef778b708cbdc61b8d2d721c0eaeda7389f98950a`，
原像素均为20秒；不支持“首帧缺时间、等待0.8秒才恢复”的旧解释。
未修改原图/归档，未为纠错重跑UI，runtime2不额外授予资格。

历史 native 矩阵与已审真实宿主证据继续引用原报告，不重复冒领本轮桌面通过。
本轮 runtime gate 是构建目标 `ValidateRuntimeDependencies`，不是运行播放器。
没有部署、注册身份、操作桌面或更新用户 AotDev；AotDev 仍是旧 preview6 组合。
已有本地 mpv AOT 兼容候选未正式采用。AOT/perf、历史 AV/c000027b/device-reset
以及跨媒体迟到通知、队列外历史直接 seek 入口的限制仍保留。

## resize 排查起点

从本次最终集成提交继续，不再以 preview6 依赖组合排查。实际 JIT 输出位于
`src/_build/x64/{Debug,Release}/BiliCopilot.UI/bin`，未部署到任何用户身份。

已只读确认以下入口，尚无本轮 resize 性能测量或根因结论：

1. `Controls/VideoPartition/VideoPartitionMainBody.xaml` 使用 `AppRepeater` 与
   `App.xaml` 的 `VideoLayout`（UniformGridLayout，Uniform stretch）。先对比短列表与
   累积长列表的 realized 元素数、measure/arrange 次数及可视范围，确认虚拟化边界。
2. `Controls/RecommendationCoverImage.cs` 的 `SizeChanged` 和 `XamlRoot.Changed`
   调用 `Reload`；每个不同物理像素宽高都会取消旧请求、重新打开缓存并解码。
   native 解码阶段按既有 AV 修复要求等待完成，再拒绝过期结果。需测量尺寸事件、
   在途解码数及 UI 创建 CanvasImageSource 的成本，不能重新引入提前释放流的 AV。
3. `RecommendationScrollAnchor.Restore` 在列表 diff 后调用 `UpdateLayout` 并遍历
   realized 卡片；先区分纯 resize 与刷新/diff 重叠，避免把刷新锚定成本误归给 resize。

按用户优先级，下一项即推荐列表累积后 resize 卡顿及同构页面；本轮不混入未经测量的列表优化。
未 push/PR/issue/发布包，未创建其他 worktree，kernel 保持 `0ae5e4e` clean。
