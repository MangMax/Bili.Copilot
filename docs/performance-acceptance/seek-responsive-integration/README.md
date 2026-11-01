# Seek worker 与面板补丁集成

2026-11-01，父区 `t3-1d19cc80` / `feat/performance-45`。起点
`0d3a22f61575550e23f935028ec5f6376cac22ce`，kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b`，初态均 clean。

核验候选 `d6d54b6104a3dae961d06bf2aa112c6baa002af2` 和 review
`b88f5333e6fe3cefca9b6bc875fda6ed51209cfe` 均直接基于父起点后，按固定 SHA
分别 `--no-ff` 合并，无冲突。合并提交为 `ede4a38a2b68c71694d64e724ad14ee7438195b0`
和 `4d75356ec63ca907b296b1e729d2d8fc2b83e4ad`；接入版本提交
`dbb614bd1e90bd1cfce601cfeeeff86c6a868d54`。

生产仅改变已审 `VideoDanmakuPanel.xaml.cs` 和中央三包版本。面板逐字节等于候选；
首页两项修复、列表优化、播放器 seek/speed、生命周期支持代码及 kernel/gitlink 均保留。
没有其他分支生产变更。WinUI 集成自审确认隐藏时退休、隐藏期间保存最新目标、显示时
VM 请求重绘、重绘队列二次门禁和既有卸载清理链保持一致；未发现新增集成阻断。
未新增或运行专用 WinUIAnalyzer，既有 NoWarn 和分析设置未修改。

## 包与恢复闭包

Core/Legacy/Share 精确使用 `0.0.1-seekd1fc.20261101.2`，从已审 candidate-feed
复制 nupkg 原字节至 `packages/danmaku`，未重新打包、覆盖同版或删除历史包。
仓库 `nuget.config` 原有相对 feed 与三个精确 ID 映射直接适用，无需修改。
[feed README](../../../packages/danmaku/README.md) 和 verify 已更新。
Legacy/Share 源码 `15434adfee3c80032ca12ab4840bc36f54858f65`；Core
`e094d330a69dbedeba9b0260b6bb499391d643c4`。
[来源 README](../../seek-responsive-fix/README.md)、packages.json、bundle、patch、
snupkg 与 source-snapshot 均保留，供离线恢复与审计。

Fresh closure.py 验证原 manifest/六包哈希、3 组 DLL/PDB CodeView 身份、所有 PDB
文档哈希；37 份生产文档再与固定 bundle HEAD 核对，两个基线 patch 逐字节一致。
只编译元数据读取工具，没有重编引擎。未宣称重建可复现二进制哈希。

## Fresh 验证

实际命令、UTC 时间、源码和输出 SHA、TRX、PDB 核对及 assets/deps 保存在
[evidence.zip](evidence.zip)。本地大产物/binlog 在 `artifacts/seek-responsive-integration/`。
使用已安装的固定 RC3 SDK。依次运行本目录 `verify.py Restore`、`Tests`、`Debug`、
`Release`、`RuntimeGate`、`DebugRuntimeGate`，再运行 `packages.py`、`audit.py`。
离线复核归档：`python -X utf8 docs/performance-acceptance/seek-responsive-integration/verify-evidence.py`。

Restore 使用全新 `artifacts/seek-responsive-integration/packages`，没有从旧目录复制包；
assets 唯一包目录及三包 `.nupkg.metadata` 来源均核对。Restore 启动时版本修改尚未提交，
其 HEAD 仍为 review merge，记录的实际源码哈希已与接入后的字节逐一核对；
后续测试/构建 HEAD 均为 `dbb614bd`。

全套 Release：**Passed: 577, Failed: 0, Skipped: 0**，实际重新执行，TRX 名称集合
与历史 577 项相等。测试沿用已有 test assets，应用使用本轮隔离恢复；两者不混称。
headless 测试并不执行 GPU/WinUI 弹幕 worker，也不代表完整 UI 或认证验收。

Debug/Release x64 JIT 全新输出目录 Rebuild 均 exit 0，各 1 个既有 protobuf
unused-import 警告、0 错误，分别耗时 3:53.98 / 3:24.51。关闭签名/打包，PublishAot=false。
两配置 ValidateRuntimeDependencies 均 exit 0；输出 libmpv、ffmpeg、BBDown 与本区
输入 SHA256 一致，resources.pri 非空。三包 nupkg SHA256/SHA512、assets、deps 与
Debug/Release 输出 DLL 均逐项相符；没有把磁盘输出验证称作运行时加载证明。

测试 DLL/PDB 身份匹配，168 份文档中 128 份可读取源码全匹配；应用两配置各 2566 份
文档中 1297 份可读取源码全匹配，包含面板变更。不可读取文档不宣称逐字节核对。
全部记录的生产源码及输出 hash 在构建后重新核对；diffcheck 通过，归档 verifier 通过。
最终 root/kernel clean、kernel HEAD/gitlink 不变，最终报告提交 SHA 在交付消息提供。

## 历史与后续边界

[固定独立 review](../../seek-responsive-review-d6d54b61/review.md) 原样保留。
原 13 项红绿探针、review 扩展 18 项、44 项相关回归及 native 15 项均为历史证据，
本轮未重跑，未启动 native 桌面、发布 AOT 或操作用户包。

故障终态继续向调用方抛出含原始原因的异常；面板没有新增局部 catch，可能重复记录拒绝日志，
需要关闭后替换实例，不自动重试/复用故障资源。XAML UnhandledException handler 不代表
所有线程、ABI/native fault 的保证。本轮未额外扩修此策略。

历史 Legacy 密集 12000 条 native ready 仍约 2355 ms；UI 提交、媒体确认到进度通知、
ready 与光学呈现是不同终点。Core 原短密集样本无提速，不声称全场景收益。
历史长时间轴为弹幕数据，实际媒体 60 秒；单项原生准备与 Close 延迟限制仍在。

最终 AOT 修复包及有界实机由父任务后续安排；当前 JIT/资源文件门禁不等于实际加载、
端到端性能或 AOT 通过。没有子代理、push、PR、issue、部署、用户包或凭据操作。
