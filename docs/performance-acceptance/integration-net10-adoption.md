# .NET 10 有限兼容采用集成

本轮按既有授权将独立审查提交 `cec615167b60352b4a63a732709c5107af65d8c1`
以 `--no-ff` 合入 `feat/performance-45`。合并提交为
`5e628048815afc1602d882bcf7e9aa4cbde74b21`，第一父为
`eff8d24c87620cf2b6025a11b8f8ddda27b1a116`，第二父为指定审查提交。
合并无冲突，合并树与审查提交相同。没有新增生产修复或扩大架构。

结论仅为**有限兼容采用**。性能 **HOLD**，同源 net9 对照的原生 crash 根因未解，
不代表 net10 已修复，也不是全面稳定性 PASS。**#49 pause FAIL、NativeAOT 因 MSVC
工具链缺失 BLOCKED、最新认证首页未验**。RC11 仍实验，历史 RC production output
已被覆盖，不能用当前目录倒推原 bytes。五份历史 launch JSON 哈希小修仍由父后续处理，
本轮没有修改旧 archive 或声称关闭。能力审计只支持“未建立足够适用性”，不支持
“所有包不存在 ≤M 适配机会”；preview-only 依赖也未因稳定 net10 而变成 GA。

## 精确闭包与 kernel 恢复

完整候选 `79ffd4f1b5ef6898b18426b9159f9342c3483450`、稳定依赖 `60abad78`、
独立依赖审查 `cce01fd6`、性能调查 `0ac4a89f`、CPU 调查 `42836305`、PR27 `f19f030`
及 PR43 `2f1ae8f` 均为合并祖先。net11 候选 `3367dd98a1285feb7e912eab22454be4397dbe32`
不是祖先。生产 src/tests/global.json/gitlink 与指定 review 完全一致；没有 cherry-pick 拆包。

父及 nested 初始均 clean，kernel 初始精确为 `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`。
先验证 committed `dependency-upgrade/kernel.bundle`，再 fetch 到专用本地分支
`local/net10-integration-5e628048`，最后 detached checkout 到
`0ae5e4e000a41a07f6696746706df11c06e8835b`。bundle prerequisite
`6501985d0f30c55c28b3f78c6e045c2536e1d389`、取消 `2ca79bd`、pageSize `f02c33f`
祖先检查通过。首次 fetch 使用 HEAD 被拒绝（bundle 仅有具名引用），随后使用
`refs/heads/integration/dependency-upgrade-d48` 成功；没有 reset、删除分支或对象。
**仅证明 local-only 可恢复，不证明 remote 可达**；后续分享仍需交付 bundle/prerequisite
或另行发布专用 kernel 分支，本轮没有远端写入。

## Fresh gates 与封存

执行脚本：[validate.py](net10-integration/validate.py)，按 Debug → Release → Visor →
RuntimeGate → Tests → DebugClean 串行执行。每次成功命令结束立即冻结，下一次构建只在清单完成后开始。
每步完整 argv、cwd、开始/完成/冻结 UTC、SDK `--info`、源码树及 kernel SHA 见各自
`provenance.json`；原始命令日志和小证据在 [归档清单](net10-integration/archive-ledger.json)。
没有关闭规则、测试断言或添加 warning suppression。测试使用仓库 xUnit/VSTest 模式。

本轮实际结果（2026-10-30 至 31 日）：

| 门禁 | 新执行结果 |
| --- | --- |
| 全套 Release / TRX | Passed: 487, Failed: 0, Skipped: 0；net10.0 |
| Debug 原始 / DebugClean 独立全量重建 | 均 1 warning / 0 errors；只有 DebugClean 作为最终 Debug 交接 |
| Release x64 JIT 全量重建 | 1 warning / 0 errors |
| Visor Release x64 JIT | 0 warnings / 0 errors |
| ValidateRuntimeDependencies | exit 0 |
| 冻结目录 / 闭包 / 四个 resolved graph | 全量 hash 与双向集合 PASS；174 份小证据原字节 gzip |

三个 app rebuild 及测试依赖编译均保留 `pgc/gateway/vega/v1.proto(10,1)` 的
`Import google/protobuf/empty.proto is unused`。没有把此警告隐去或声称全部零警告。
测试命令为 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release
--logger "trx;LogFileName=integration-net10.trx" --results-directory <冻结根>/Tests/trx`；
其实际绝对路径 argv 见 Tests/provenance。未指定过滤器，也未复用旧 TRX。

冻结根目录（ignored）：

`artifacts/net10-integration/5e628048815afc1602d882bcf7e9aa4cbde74b21/`

Debug、DebugClean、Release、Visor、RuntimeGate、Tests 分别独立保存。`files.json` 覆盖目录全部文件的
相对路径、长度及 raw-byte SHA256（不自包含其自身 hash）；清单 hash 由
[summary.json](net10-integration/summary.json) 记录。resolved assets 保留原始 bytes，
并核对 app/Visor/Visor.Models/tests 的实际 targets 和 library 集合与审查版本一致。
生产源码 raw hashes 在 `net10-integration/source-hashes.json.gz`，没有读取凭据文件。

**初次 Debug 不作为认证输入**：虽然编译通过，最终 recipe 集合核验发现共享输出留下七个额外 DLL：
Configuration.Abstractions、Diagnostics.Abstractions、FileProviders.Abstractions、Hosting.Abstractions、
net9 mscordaccore、嵌套 WebView2Loader 和 WinRT.Host.Shim。原封存未修改。
随后 DebugClean 使用全新独立 `-p:BuildOutDir=…/DebugClean/build-output/` 全量重建，
继续保留所有规则；这属于构建输出隔离修正，不是生产代码缺陷修复。最终交接选 **DebugClean/activation**
或 **Release/activation**，要求 DLL 集合与当前 recipe 精确一致。Tests 封存同时保留历史 net9
子目录，仅 `net10.0` DLL、runtimeconfig 和本轮 TRX 是 fresh 测试证据。

最终两套 activation 各 666 文件，无 recipe、无 recipe 之外的 DLL；原/新 Debug 主 DLL hash
一致，额外重建解决的是输出残留。关键 SHA256：

| 项 | SHA256 |
| --- | --- |
| DebugClean/files.json（1,875 项，含独立 build-output） | `a088e381d5df80200c2d845e4b9bfc17311f42830af19038b0d831eb7509e69f` |
| Release/files.json（1,279 项） | `875e4f2e954ac3c61b5f3d2ac764060ae8177f1b11ab8aa30125b9bd8d47736b` |
| DebugClean BiliCopilot.UI.dll | `7ced45643cb2cbe5a533792a1aac3a1ce01008e271f035cd31bcf66d52b93a95` |
| Release BiliCopilot.UI.dll | `7bdedb93504e76e813663307e84769e378b0ebc266ee3163460573087ec4ea7f` |
| 两套 coreclr.dll | `1bc65b8d45474b19adf3ca85ce881087aff599ffc72b2c1a323e878227757b65` |

DebugClean/Release 的 `outputs/BiliCopilot.UI` 是原始输出，包含原 recipe。独立 `activation`
副本补齐 recipe 指定的缺失 content 后才移除 recipe；现有文件从不被 recipe 覆盖，所有
补入来源、hash 和移除项记录在 `activation-preparation.json`。两个 manifest 身份保持原样
（Debug Dev、Release production），不能将它们误称已更新的 Caa6a7be 认证身份。
父任务后续同身份更新应从冻结副本制作新的 owned 工作副本，保留这里的原始 bytes 与清单，
记录 identity 变换及实际部署/加载 hash；不得让 recipe 重新注入共享输出。

Visor Release JIT 独立输出同样封存；它不会事后注入或改写已冻结的 UI 包。
runtime gate 只证明 libmpv/ffmpeg/BBDown 文件存在，不证明媒体功能实际执行。
app runtimeconfig 为自包含 net10 / 10.0.14；测试 LatestPatch 限于 net10，SDK 精确 10.0.403。
本轮没有 app loaded-module trace，不把 runtimeconfig 声称为实际启动证据。

独立 [audit.py](net10-integration/audit.py) 检查每个冻结文件 hash、双向文件集合、
activation 内容完整性、原始文件未覆盖、manifest/TFM/runtime、TRX 和祖先。
所有归档采用 gzip 原始字节，不对 JSON 做 newline 规范化。

## 保护边界与后续

没有部署、启动应用、占用桌面、读取凭据、操作 PID9608/Dev、修改 Caa6a7be 注册或
`t3-1630ecee/artifacts/full-app-duration-fix/layout`、新旧 layout/login 或其他 worktree。
没有 push、PR、issue 或默认分支操作，没有子代理。没有重跑历史 native/UI 性能场景；
`net10-adaptation/verify.py --tracked` 本轮通过仅证明历史归档完整，不能替代 fresh UI。

后续认证更新与最新首页/刷新/撤销/pageSize、播放器切换取消和托盘退出验收仍由父执行。
同类 native crash 若复现，应保存 exact PID/path/hash/时间及可取得的 stowed/native 因果证据，
保持发布 HOLD；不能靠新 observer、吞异常或单次绿色关闭风险。本轮只准备可审查产物。
